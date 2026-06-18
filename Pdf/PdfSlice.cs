using System.Text;
using System.Text.Json;
using PandocCore;

namespace PdfCore;

public interface IPdfOcrRecognizer
{
    PdfOcrRecognizeResult Recognize(string imagePath, int pageNumber);
}

public sealed record PdfOcrRecognizeResult
{
    public int Page { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Engine { get; set; } = "";
    public string ModelId { get; set; } = "";
    public List<PdfOcrRecognizedBlock> Blocks { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfOcrRecognizedBlock
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public double? Confidence { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public bool ConfidenceValid { get; set; } = true;
    public bool GeometryValid { get; set; } = true;
    public string? NumericIssue { get; set; }
}

public static class PdfSlice
{
    public static PdfSliceResult Dissect(
        string pdfPath,
        string outputDir,
        PdfSliceOptions options,
        IPdfOcrRecognizer? ocrRecognizer = null)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        ValidateOptions(options);
        PrepareOutputDirectory(outputDir);

        // ── Preflight: Poppler pdfinfo ──
        var check = PdfPopplerInspector.Check(pdfPath);
        var effectiveMode = ResolveMode(options.Mode, check);
        var warnings = new List<string>(check.Warnings);

        if (effectiveMode == "text" && check.TextCharCount == 0)
        {
            throw new PdfProcessingException(
                PdfErrorKind.ValidationFailed,
                "Text mode requested, but this PDF has no useful text layer. Use --mode ocr with local OCR runtime installed.");
        }

        PdfDocumentModel model;

        if (effectiveMode == "ocr")
        {
            model = BuildOcrModel(pdfPath, outputDir, check, options, ocrRecognizer, warnings);
        }
        else
        {
            // ── Poppler pdftotext is the sole text extraction engine ──
            model = PdfPopplerExtractor.ExtractTextModel(pdfPath, check);
            model.Warnings.AddRange(warnings.Where(w => !model.Warnings.Contains(w)));

            if (effectiveMode == "hybrid")
            {
                model.Warnings.Add("Hybrid mode currently preserves native PDF text and embedded image evidence; image-region OCR will be expanded in the next layout pass.");
            }
        }

        // ── Image extraction: Poppler pdfimages ──
        // Bug 1: skip image extraction for text PDFs with zero embedded images,
        // and for non-ocr modes where the PDF is text-only.
        if (effectiveMode != "ocr" && check.ImageCount > 0)
        {
            try
            {
                var assets = PdfPopplerImageExtractor.Extract(pdfPath, Path.Combine(outputDir, "assets"));
                model.Assets = assets.Items;
                model.Warnings.AddRange(assets.Warnings);
            }
            catch (Exception ex)
            {
                // Bug 1: image extraction failure should not block the whole pipeline.
                // Downgrade to warning and continue with text-only blocks.
                model.Warnings.Add($"Image extraction skipped: {ex.Message}");
            }
        }
        else if (effectiveMode != "ocr" && check.ImageCount == 0)
        {
            model.Warnings.Add("No embedded images found; image extraction skipped.");
        }

        AddImageBlocks(model, model.Assets);
        ReindexBlocks(model.Blocks);

        WriteSliceFiles(pdfPath, outputDir, model, check);

        return new PdfSliceResult
        {
            OutputDir = Path.GetFullPath(outputDir),
            ManifestPath = Path.GetFullPath(Path.Combine(outputDir, NongPandocArtifactNames.Manifest)),
            BlockCount = model.Blocks.Count,
            AssetCount = model.Assets.Count,
            PageCount = model.Pages.Count,
            Classification = check.Classification,
            Warnings = model.Warnings.Distinct().ToList(),
        };
    }

    static void ValidateOptions(PdfSliceOptions options)
    {
        var mode = options.Mode.ToLowerInvariant();
        if (mode is not ("auto" or "text" or "hybrid" or "ocr"))
        {
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Unsupported --mode. Supported: auto, text, hybrid, ocr.");
        }

        if (options.Dpi is < 72 or > 600)
        {
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "DPI must be between 72 and 600.");
        }
    }

    static string ResolveMode(string mode, PdfCheckResult check)
    {
        mode = mode.ToLowerInvariant();
        if (mode != "auto") return mode;
        return check.Classification switch
        {
            "scan" => "ocr",
            "hybrid" => "hybrid",
            _ => "text"
        };
    }

    static PdfDocumentModel BuildOcrModel(
        string pdfPath,
        string outputDir,
        PdfCheckResult check,
        PdfSliceOptions options,
        IPdfOcrRecognizer? ocrRecognizer,
        List<string> warnings)
    {
        if (ocrRecognizer == null)
        {
            throw new PdfProcessingException(
                PdfErrorKind.DependencyMissing,
                "PDF OCR mode requires local PP-OCRv6 runtime. Run 'nong ocr install-model pp-ocrv6-medium --json', then rerun pdf dissect --mode ocr. No Python is required.");
        }

        var assets = PdfPopplerImageExtractor.Extract(pdfPath, Path.Combine(outputDir, "assets"));
        var imagesByPage = assets.Items
            .GroupBy(a => a.Page)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pagesDir = Path.Combine(outputDir, "pages");
        var render = PdfPageRenderer.Render(pdfPath, pagesDir, options.Dpi);
        var ocrDir = Path.Combine(outputDir, "ocr");
        Directory.CreateDirectory(ocrDir);
        double scale = options.Dpi / 72.0;
        double expandRatio = 0.05;

        var model = new PdfDocumentModel
        {
            Source = new PdfSourceInfo
            {
                Path = Path.GetFileName(pdfPath),
                Sha256 = check.Sha256 ?? PdfUtilities.Sha256(pdfPath),
                PageCount = check.PageCount,
                Classification = check.Classification,
            },
            Warnings = warnings,
            Assets = assets.Items,
        };
        model.Warnings.AddRange(assets.Warnings);

        var ocrBlockIndex = 0;
        using var pagesWriter = new StreamWriter(Path.Combine(ocrDir, "pages.jsonl"), false, Encoding.UTF8);
        using var blocksWriter = new StreamWriter(Path.Combine(ocrDir, "blocks.jsonl"), false, Encoding.UTF8);

        foreach (var page in render.Pages)
        {
            var pageInfo = check.Pages.FirstOrDefault(p => p.Page == page.Page);
            double pdfW = pageInfo?.Width ?? 612;
            double pdfH = pageInfo?.Height ?? 792;
            double pngW = page.Width;
            double pngH = page.Height;

            model.Pages.Add(new PdfPageModel
            {
                Page = page.Page,
                Width = pdfW,
                Height = pdfH,
                Unit = "pt",
                TextCharCount = 0,
                ImageCount = imagesByPage.TryGetValue(page.Page, out var imgs) ? imgs.Count : 0,
            });

            var maskRects = new List<(double Left, double Top, double Right, double Bottom)>();
            if (imagesByPage.TryGetValue(page.Page, out var pageImages))
            {
                foreach (var asset in pageImages)
                {
                    if (asset.Bbox is not { Length: >= 4 }) continue;
                    double sLeft = asset.Bbox[0] * scale;
                    double sRight = asset.Bbox[2] * scale;
                    double sTop = (pdfH - asset.Bbox[3]) * scale;
                    double sBottom = (pdfH - asset.Bbox[1]) * scale;

                    double expW = (sRight - sLeft) * expandRatio;
                    double expH = (sBottom - sTop) * expandRatio;
                    maskRects.Add((
                        Math.Max(0, sLeft - expW),
                        Math.Max(0, sTop - expH),
                        Math.Min(pngW, sRight + expW),
                        Math.Min(pngH, sBottom + expH)));
                }
            }

            var pageImage = Path.Combine(pagesDir, page.Path);
            var ocr = ocrRecognizer.Recognize(pageImage, page.Page);
            foreach (var warning in ocr.Warnings)
                model.Warnings.Add(warning);

            pagesWriter.WriteLine(JsonSerializer.Serialize(ocr, PdfUtilities.JsonlOpts));

            int blockIndex = model.Blocks.Count;
            foreach (var ocrBlock in ocr.Blocks.Where(b => !string.IsNullOrWhiteSpace(b.Text)))
            {
                double oLeft, oTop, oRight, oBottom;
                if (ocrBlock.Bbox is { Length: >= 4 })
                {
                    oLeft = ocrBlock.Bbox[0];
                    oTop = ocrBlock.Bbox[1];
                    oRight = ocrBlock.Bbox[2];
                    oBottom = ocrBlock.Bbox[3];
                }
                else
                {
                    oLeft = oTop = oRight = oBottom = 0;
                }

                bool isMasked = false;
                foreach (var m in maskRects)
                {
                    if (!(oRight < m.Left || oLeft > m.Right || oBottom < m.Top || oTop > m.Bottom))
                    {
                        isMasked = true;
                        break;
                    }
                }
                if (isMasked) continue;

                ocrBlockIndex++;
                blockIndex++;
                var id = $"ocr{ocrBlockIndex:D4}";

                var pdfBbox = oLeft > 0 || oTop > 0 || oRight > 0 || oBottom > 0
                    ? new[]
                    {
                        Math.Round(oLeft * (pdfW / pngW), 3),
                        Math.Round(pdfH - (oBottom * (pdfH / pngH)), 3),
                        Math.Round(oRight * (pdfW / pngW), 3),
                        Math.Round(pdfH - (oTop * (pdfH / pngH)), 3),
                    }
                    : new[] { 0d, 0d, 0d, 0d };

                var block = new PdfContentBlock
                {
                    Id = id,
                    BlockId = id,
                    Index = blockIndex - 1,
                    Kind = "ocrText",
                    Page = page.Page,
                    Bbox = pdfBbox,
                    Source = "localOcr",
                    Text = ocrBlock.Text,
                    Confidence = ocrBlock.GeometryValid ? "medium" : "low",
                };
                if (!ocrBlock.ConfidenceValid)
                    block.Warnings.Add("OCR confidence was invalid and serialized as null.");
                if (!ocrBlock.GeometryValid)
                    block.Warnings.Add("OCR geometry was invalid or incomplete.");

                model.Blocks.Add(block);
                blocksWriter.WriteLine(JsonSerializer.Serialize(block, PdfUtilities.JsonlOpts));
            }
        }

        if (model.Blocks.Count == 0)
        {
            throw new PdfProcessingException(
                PdfErrorKind.ReadFailed,
                "Local OCR returned no text blocks after image-region masking. Try a higher --dpi value or use cloud OCR.");
        }

        return model;
    }

    static void AddImageBlocks(PdfDocumentModel model, List<PdfAssetEntry> assets)
    {
        var imageIndex = 0;
        foreach (var asset in assets)
        {
            imageIndex++;
            var id = asset.Id.Length > 0 ? asset.Id : $"img{imageIndex:D4}";
            model.Blocks.Add(new PdfContentBlock
            {
                Id = id,
                BlockId = id,
                Index = model.Blocks.Count,
                Kind = "image",
                Page = asset.Page,
                Bbox = asset.Bbox,
                Source = "pdfimages",
                Text = asset.Caption ?? asset.AltTextCandidate ?? "image",
                AssetId = asset.Id,
                AssetPath = "assets/" + asset.Path.Replace('\\', '/'),
                CaptionBlockId = asset.CaptionBlockId,
                OcrBlockIds = asset.OcrBlockIds,
                Confidence = "medium",
                Warnings = asset.Warnings,
            });
        }
    }

    static void ReindexBlocks(List<PdfContentBlock> blocks)
    {
        var ordered = blocks
            .OrderBy(b => b.Page)
            .ThenBy(b => b.Index)
            .ToList();

        blocks.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Index = i;
            ordered[i].BlockId = string.IsNullOrWhiteSpace(ordered[i].BlockId) ? ordered[i].Id : ordered[i].BlockId;
            blocks.Add(ordered[i]);
        }
    }

    static void WriteSliceFiles(string pdfPath, string outputDir, PdfDocumentModel model, PdfCheckResult check)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "diagnostics"));
            Directory.CreateDirectory(Path.Combine(outputDir, "ocr"));

            var source = Path.GetFileName(pdfPath);
            var metrics = ComputeMetrics(model);
            var warnings = model.Warnings.Distinct().ToList();
            var manifest = new NongPandocSliceManifest
            {
                Source = new NongPandocSourceInfo
                {
                    Path = source,
                    Format = "pdf",
                    Sha256 = model.Source.Sha256,
                    PageCount = model.Source.PageCount,
                },
                CreatedAt = DateTime.UtcNow,
                Metrics = ToPandocMetrics(metrics),
                Warnings = warnings,
            };
            var structure = BuildStructure(source, model);
            var format = BuildFormat(source, model);
            var diagnostics = new
            {
                schemaVersion = "nongpdf/diagnostics/v1",
                source,
                warnings,
                files = new[]
                {
                    Path.Combine("diagnostics", "check.json"),
                    Path.Combine("diagnostics", "reading-order.json"),
                    Path.Combine("diagnostics", "warnings.json"),
                },
            };
            var assetManifest = new PdfAssetManifest { Source = source, Items = model.Assets };

            NongPandocSlicePackageWriter.Write(
                new NongPandocSliceWritePayload
                {
                    OutputDirectory = outputDir,
                    Manifest = manifest,
                    Document = model,
                    ContentJsonlItems = model.Blocks.OrderBy(b => b.Index).Cast<object>().ToList(),
                    NongMarkText = PdfNongMarkTextWriter.Write(model),
                    Structure = structure,
                    Format = format,
                    Diagnostics = diagnostics,
                    AssetsManifest = assetManifest,
                    TextPreview = PdfTextPreviewWriter.Write(model),
                },
                new NongPandocSliceWriteOptions
                {
                    JsonOptions = PdfUtilities.JsonOpts,
                    JsonlOptions = PdfUtilities.JsonlOpts,
                    RequiredArtifacts = NongPandocSlicePackageWriter.DefaultRequiredArtifacts
                        .Concat(new[] { NongPandocArtifactNames.TextPreview })
                        .ToArray(),
                });

            PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "check.json"), check);
            PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "reading-order.json"), PdfReadingOrder.BuildDiagnostics(model));
            PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "warnings.json"), warnings);
        }
        catch (NongPandocSliceWriteException ex)
        {
            throw new PdfProcessingException(PdfErrorKind.WriteFailed, ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            throw new PdfProcessingException(PdfErrorKind.WriteFailed, $"Failed to write PDF slice package: {ex.Message}", ex);
        }
    }

    static PdfSliceMetrics ComputeMetrics(PdfDocumentModel model) => new()
    {
        Pages = model.Pages.Count,
        Blocks = model.Blocks.Count,
        Paragraphs = model.Blocks.Count(b => b.Kind == "paragraph"),
        Headings = model.Blocks.Count(b => b.Kind == "heading"),
        Images = model.Blocks.Count(b => b.Kind == "image"),
        OcrTextBlocks = model.Blocks.Count(b => b.Kind == "ocrText"),
        Tables = model.Blocks.Count(b => b.Kind == "table"),
        Warnings = model.Warnings.Distinct().Count(),
    };

    static NongPandocMetrics ToPandocMetrics(PdfSliceMetrics metrics) => new()
    {
        Blocks = metrics.Blocks,
        Paragraphs = metrics.Paragraphs,
        Headings = metrics.Headings,
        Tables = metrics.Tables,
        Figures = 0,
        Images = metrics.Images,
        References = 0,
        Warnings = metrics.Warnings,
    };

    static PdfStructure BuildStructure(string source, PdfDocumentModel model)
    {
        var structure = new PdfStructure { Source = source };
        foreach (var block in model.Blocks.OrderBy(b => b.Index))
        {
            structure.BlockIndex[block.BlockId] = new PdfBlockIndexEntry
            {
                Kind = block.Kind,
                Order = block.Index,
                Page = block.Page,
                TextPreview = PdfUtilities.Preview(block.Text),
                Bbox = block.Bbox,
                Source = block.Source,
                Provenance = new NongPandocBlockProvenance
                {
                    Format = "pdf",
                    Source = block.Source,
                    Page = block.Page,
                    Position = block.Index,
                    Bbox = block.Bbox.Length > 0 ? block.Bbox : null,
                    AssetId = block.AssetId,
                    Confidence = block.Confidence ?? "high",
                    Notes = block.Warnings.Count > 0 ? block.Warnings : null,
                },
            };

            var page = structure.Pages.FirstOrDefault(p => p.Page == block.Page);
            if (page == null)
            {
                page = new PdfPageStructure { Page = block.Page };
                structure.Pages.Add(page);
            }
            page.BlockIds.Add(block.BlockId);

            if (block.Kind == "heading")
            {
                structure.Outline.Add(new PdfOutlineItem
                {
                    Id = block.BlockId,
                    Text = block.Text ?? "",
                    Page = block.Page,
                    Level = 1,
                });
            }
        }

        if (model.Source.Classification is "hybrid" or "scan")
            structure.Issues.Add("Layout/reading-order confidence is limited for image-heavy PDFs.");
        return structure;
    }

    static PdfFormatDocument BuildFormat(string source, PdfDocumentModel model)
    {
        var format = new PdfFormatDocument
        {
            Source = source,
            Warnings = model.Warnings.Distinct().ToList(),
        };

        foreach (var page in model.Pages)
        {
            var fonts = model.Blocks
                .Where(b => b.Page == page.Page)
                .SelectMany(b => b.Runs)
                .Select(r => r.Format?.Font)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct()
                .OrderBy(f => f)
                .Cast<string>()
                .ToList();
            format.Pages.Add(new PdfPageFormat
            {
                Page = page.Page,
                Width = page.Width,
                Height = page.Height,
                Unit = page.Unit,
                Fonts = fonts,
            });
            format.Fonts.AddRange(fonts);
        }

        format.Fonts = format.Fonts.Distinct().OrderBy(f => f).ToList();
        format.VisualEvidence = new NongPandocVisualEvidence
        {
            Format = "pdf",
            Source = source,
            Fonts = format.Fonts,
            Tables = model.Blocks
                .Where(b => b.Kind == "table")
                .Select(b => $"{b.BlockId}:page={b.Page};bbox={string.Join(",", b.Bbox)}")
                .ToList(),
            Layout = model.Pages
                .Select(p => $"page{p.Page}:{p.Width}x{p.Height}{p.Unit};readingOrder={p.ReadingOrderMethod}")
                .ToList(),
            Assets = model.Assets
                .Select(a => $"{a.Id}:page={a.Page};bbox={string.Join(",", a.Bbox)};method={a.ExtractionMethod}")
                .ToList(),
            Warnings = format.Warnings,
        };
        return format;
    }

    static void PrepareOutputDirectory(string outputDir)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Output directory is required.");

        var full = Path.GetFullPath(outputDir);
        if (File.Exists(full))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, $"Output path is a file, not a directory: {outputDir}");

        Directory.CreateDirectory(full);
    }
}
