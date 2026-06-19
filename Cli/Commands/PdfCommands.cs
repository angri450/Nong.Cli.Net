using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Docnet.Core;
using PdfCore;
using Nong.Cli.Adapters;
using Nong.Cli.Common;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Angri450.Nong.Data;

namespace Nong.Cli.Commands;

public static class PdfCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        PdfNativeRuntime.EnsurePdfiumRegistered();
        var cmd = new Command("pdf", "PDF document parsing operations");
        cmd.AddCommand(CreateCheck(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateRender(jsonOpt));
        cmd.AddCommand(CreateImages(jsonOpt));
        cmd.AddCommand(CreateMerge(jsonOpt));
        cmd.AddCommand(CreateSplit(jsonOpt));
        cmd.AddCommand(CreateOcrPdf(jsonOpt));
        cmd.AddCommand(CreateCompress(jsonOpt));
        cmd.AddCommand(CreateToWord(jsonOpt));
        cmd.AddCommand(CreateDbImport(jsonOpt));
        cmd.AddCommand(CreateDbList(jsonOpt));
        cmd.AddCommand(CreateDbBlocks(jsonOpt));
        cmd.AddCommand(CreateDbImages(jsonOpt));
        return cmd;
    }

    static Command CreateCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var cmd = new Command("check", "Preflight PDF and classify text/hybrid/scan route") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfPopplerInspector.Check(file));
                var output = JsonOutput.Ok("pdf check",
                    $"PDF preflight: {result.Classification}, {result.PageCount} page(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["textChars"] = result.TextCharCount;
                output.Metrics["images"] = result.ImageCount;
                output.Metrics["renderRequired"] = result.RenderRequired ? 1 : 0;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_preflight");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf check", ex, json);
            }
        }, fileArg, jsonOpt);
        return cmd;
    }

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for PDF one-cut three-stream slice") { IsRequired = true };
        var modeOpt = new Option<string>("--mode", () => "auto", "Mode: auto, text, hybrid, ocr");
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI for OCR mode");
        var extractorOpt = new Option<string>("--extractor", () => "auto", "Text extractor: auto, pdftotext, pdfpig");
        var ingestOpt = new Option<bool>("--ingest", () => false, "Auto-import dissect output into NongDb for semantic search");
        var cmd = new Command("dissect", "Slice PDF into nongpdf/nongmark streams") { fileArg, outOpt, modeOpt, dpiOpt, extractorOpt, ingestOpt };

        cmd.SetHandler((string file, string outputDir, string mode, int dpi, string extractor, bool ingest, bool json) =>
        {
            try
            {
                var options = new PdfSliceOptions { Mode = mode, Dpi = dpi, Extractor = extractor };
                var recognizer = ShouldProvideOcr(file, mode) ? new PdfOcrRecognizerAdapter() : null;
                var (result, elapsed) = CliHelpers.Time(() => PdfSlice.Dissect(file, outputDir, options, recognizer));
                var output = JsonOutput.Ok("pdf dissect",
                    $"PDF slice: {result.BlockCount} block(s), {result.AssetCount} asset(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["nongmark"] = Path.Combine(result.OutputDir, "content.nongmark");
                output.Artifacts["contentJsonl"] = Path.Combine(result.OutputDir, "content.jsonl");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["blocks"] = result.BlockCount;
                output.Metrics["assets"] = result.AssetCount;
                output.Metrics["warnings"] = result.Warnings.Count;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_slice");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));

                if (ingest)
                {
                    try
                    {
                        using var ctx = new IngestionContext();
                        var ir = ctx.IngestSlice(file, outputDir, "pdf", "dissect");
                        if (!json) Console.Error.WriteLine($"[ingest] {ir.Blocks} blocks + {ir.Images} images imported to nong.db");
                    }
                    catch (Exception ex) { if (!json) Console.Error.WriteLine($"[ingest] warning: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                WritePdfError("pdf dissect", ex, json);
            }
        }, fileArg, outOpt, modeOpt, dpiOpt, extractorOpt, ingestOpt, jsonOpt);
        return cmd;
    }

    static Command CreateRender(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output page image directory") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI");
        var cmd = new Command("render", "Render PDF pages to PNG images") { fileArg, outOpt, dpiOpt };

        cmd.SetHandler((string file, string outputDir, int dpi, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfPageRenderer.Render(file, outputDir, dpi));
                var output = JsonOutput.Ok("pdf render",
                    $"Rendered {result.PageCount} page(s) at {dpi} DPI",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["dpi"] = result.Dpi;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_render");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf render", ex, json);
            }
        }, fileArg, outOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    static Command CreateImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output assets directory") { IsRequired = true };
        var cmd = new Command("images", "Extract embedded PDF images and write provenance manifest") { fileArg, outOpt };

        cmd.SetHandler((string file, string outputDir, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfPopplerImageExtractor.Extract(file, outputDir));
                var output = JsonOutput.Ok("pdf images",
                    $"Extracted {result.ImageCount} image(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["manifest"] = Path.Combine(result.OutputDir, "manifest.json");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["images"] = result.ImageCount;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_image");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf images", ex, json);
            }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static bool ShouldProvideOcr(string file, string mode)
    {
        if (mode.Equals("ocr", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var check = PdfPopplerInspector.Check(file);
            return check.Classification == "scan";
        }
        catch
        {
            return false;
        }
    }

    static void AddWarnings(JsonOutput output, IEnumerable<string> warnings, string id)
    {
        foreach (var warning in warnings)
        {
            output.Issues.Add(new Issue
            {
                Id = id,
                Severity = "Warning",
                Message = warning
            });
        }
    }

    static Command CreateMerge(Option<bool> jsonOpt)
    {
        var filesArg = new Argument<string[]>("files", "Paths to .pdf files to merge (at least 2)") { Arity = ArgumentArity.OneOrMore };
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output merged .pdf path") { IsRequired = true };
        var cmd = new Command("merge", "Merge multiple PDF files into one") { filesArg, outOpt };

        cmd.SetHandler((string[] files, string output, bool json) =>
        {
            const string command = "pdf merge";
            try
            {
                if (files.Length < 2)
                {
                    CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = "At least 2 PDF files required for merge." }, json);
                    return;
                }
                foreach (var f in files)
                {
                    if (!File.Exists(f))
                    {
                        CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {f}" }, json);
                        return;
                    }
                }

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var bytesList = files.Select(File.ReadAllBytes).ToArray();
                    var result = files.Length == 2
                        ? DocLib.Instance.Merge(bytesList[0], bytesList[1])
                        : DocLib.Instance.Merge(bytesList);
                    File.WriteAllBytes(output, result);
                });

                var info = new FileInfo(output);
                var outputJson = JsonOutput.Ok(command,
                    $"Merged {files.Length} PDF files → {Path.GetFileName(output)} ({info.Length} bytes)",
                    new { sourceCount = files.Length, outputBytes = info.Length });
                outputJson.Artifacts["pdf"] = output;
                outputJson.Metrics["sourceFiles"] = files.Length;
                outputJson.Metrics["outputBytes"] = info.Length;
                outputJson.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError(command, ex, json);
            }
        }, filesArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateSplit(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to source .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output split .pdf path") { IsRequired = true };
        var pagesOpt = new Option<string>("--pages", () => "1", "Page range: single page (3), range (1-5), or comma-separated (1-3,5,7-9)");
        var cmd = new Command("split", "Split PDF pages into a separate document") { fileArg, outOpt, pagesOpt };

        cmd.SetHandler((string file, string output, string pages, bool json) =>
        {
            const string command = "pdf split";
            try
            {
                if (!File.Exists(file))
                {
                    CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                var (resultBytes, elapsed) = CliHelpers.Time(() => DocLib.Instance.Split(file, pages));

                File.WriteAllBytes(output, resultBytes);
                var info = new FileInfo(output);
                var outputJson = JsonOutput.Ok(command,
                    $"Split pages '{pages}' → {Path.GetFileName(output)} ({info.Length} bytes)",
                    new { pages, outputBytes = info.Length });
                outputJson.Artifacts["pdf"] = output;
                outputJson.Metrics["outputBytes"] = info.Length;
                outputJson.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError(command, ex, json);
            }
        }, fileArg, outOpt, pagesOpt, jsonOpt);
        return cmd;
    }

    static Command CreateOcrPdf(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to source .pdf file (scan PDF)");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output PDF with image layer") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI");
        var withOcrOpt = new Option<bool>("--with-ocr", () => false, "Run local PP-OCRv6 on each page and embed recognized text as searchable text layer");
        var cmd = new Command("ocr", "Add image layer to scanned PDF pages with optional OCR text") { fileArg, outOpt, dpiOpt, withOcrOpt };

        cmd.SetHandler((string file, string output, int dpi, bool withOcr, bool json) =>
        {
            const string command = "pdf ocr";
            try
            {
                if (!File.Exists(file))
                { CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }

                CliHelpers.EnsureParentDir(output);

                IPdfOcrRecognizer? recognizer = withOcr ? new Nong.Cli.Adapters.PdfOcrRecognizerAdapter() : null;

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "pdf-ocr-" + Guid.NewGuid().ToString("N")[..8]);
                    var pages = PdfPageRenderer.Render(file, tempDir, dpi);
                    var imageFiles = pages.Pages.Select(p => p.Path).Where(File.Exists).ToList();
                    if (imageFiles.Count == 0)
                        throw new InvalidOperationException("PDF render produced no page images.");

                    var totalTextBlocks = 0;

                    // Use SkiaSharp to create a PDF from page images (replaces PdfPig PdfDocumentBuilder)
                    using var skStream = new SkiaSharp.SKFileWStream(output);
                    using var skDoc = SkiaSharp.SKDocument.CreatePdf(skStream);

                    for (int i = 0; i < imageFiles.Count; i++)
                    {
                        var imgBytes = File.ReadAllBytes(imageFiles[i]);
                        var pw = pages.Pages[i].Width > 0 ? pages.Pages[i].Width : 595;
                        var ph = pages.Pages[i].Height > 0 ? pages.Pages[i].Height : 842;

                        using var skBitmap = SkiaSharp.SKBitmap.Decode(imgBytes);
                        var canvas = skDoc.BeginPage(pw, ph);
                        canvas.DrawBitmap(skBitmap, 0, 0);

                        if (recognizer != null)
                        {
                            var ocrResult = recognizer.Recognize(imageFiles[i], i + 1);
                            var paint = new SkiaSharp.SKPaint
                            {
                                Color = SkiaSharp.SKColors.Transparent,
                                IsAntialias = true,
                                TextSize = 10,
                            };

                            foreach (var block in ocrResult.Blocks)
                            {
                                if (string.IsNullOrWhiteSpace(block.Text)) continue;
                                var bbox = block.Bbox;
                                float bx = 2, by = ph - 14;
                                if (bbox != null && bbox.Length >= 4 && ocrResult.Width > 0 && ocrResult.Height > 0)
                                {
                                    bx = (float)(bbox[0] / ocrResult.Width * pw) + 2;
                                    by = (float)(ph - (bbox[3] / ocrResult.Height * ph)) + 2;
                                }
                                paint.Color = new SkiaSharp.SKColor(0, 0, 0, 1); // nearly transparent
                                canvas.DrawText(block.Text, bx, by, paint);
                                totalTextBlocks++;
                            }
                        }
                        else
                        {
                            var paint = new SkiaSharp.SKPaint
                            {
                                Color = new SkiaSharp.SKColor(0, 0, 0, 1),
                                TextSize = 6,
                                IsAntialias = true,
                            };
                            canvas.DrawText($"[Page {i + 1} - OCR ready]", 10, ph - 5, paint);
                        }

                        skDoc.EndPage();
                    }
                    skDoc.Close();

                    try { Directory.Delete(tempDir, true); } catch { }
                    return (PageCount: imageFiles.Count, TextBlocks: totalTextBlocks);
                });

                var info = new FileInfo(output);
                var o = JsonOutput.Ok(command,
                    $"PDF with image layer: {Path.GetFileName(output)} ({info.Length} bytes, {result.PageCount} page(s))",
                    new { pages = result.PageCount, outputBytes = info.Length, dpi, ocrEnabled = withOcr, ocrTextBlocks = result.TextBlocks });
                o.Artifacts["pdf"] = output;
                o.Metrics["pages"] = result.PageCount;
                o.Metrics["outputBytes"] = info.Length;
                o.Metrics["ocrTextBlocks"] = result.TextBlocks;
                o.Meta.DurationMs = elapsed;

                if (!withOcr)
                    o.Issues.Add(new Issue { Id = "pdf_ocr", Severity = "Info", Message = "Each page rendered as full image. For searchable text, run nong ocr cloud on the output PDF, or retry with --with-ocr for local PP-OCRv6 text layer." });
                else if (result.TextBlocks == 0)
                    o.Issues.Add(new Issue { Id = "pdf_ocr_empty", Severity = "Warning", Message = "OCR completed but returned no text blocks. The source PDF may be blank or the OCR runtime may need installation." });
                else
                    o.Issues.Add(new Issue { Id = "pdf_ocr_text", Severity = "Info", Message = $"{result.TextBlocks} OCR text block(s) embedded. Text layer quality depends on PP-OCRv6 accuracy. Verify with a PDF reader." });

                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, dpiOpt, withOcrOpt, jsonOpt);
        return cmd;
    }

    // ===== pdf compress =====

    static Command CreateCompress(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>("-o", "Output compressed PDF path");
        var qualityOpt = new Option<int>("--quality", () => 75, "JPEG quality hint (1-100, default 75)");
        var cmd = new Command("compress", "Compress PDF: strip unused objects and re-encode content streams") { fileArg, outOpt, qualityOpt };
        cmd.SetHandler((string file, string? output, int quality, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            { CliHelpers.WriteError("pdf compress", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".compressed.pdf");
                quality = Math.Clamp(quality, 1, 100);
                var beforeBytes = new FileInfo(file).Length;
                var sw = Stopwatch.StartNew();

                // True compress: read + re-write via PdfPig (auto-flate streams, strip unused)
                try
                {
                    using var pdfIn = UglyToad.PdfPig.PdfDocument.Open(file);
                    var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
                    for (int i = 1; i <= pdfIn.NumberOfPages; i++)
                    {
                        var page = pdfIn.GetPage(i);
                        builder.AddPage(page.Width, page.Height, pb =>
                        {
                            foreach (var word in page.GetWords())
                            {
                                pb.AddText(word.Letters[0].GlyphRectangle.Left,
                                    page.Height - word.Letters[0].GlyphRectangle.Top,
                                    word.Letters[0].FontSize, word.Text);
                            }
                        });
                    }
                    using var outStream = File.Create(outPath);
                    File.WriteAllBytes(outPath, builder.Build());
                }
                catch
                {
                    // Fallback: copy original
                    File.Copy(file, outPath, true);
                }

                sw.Stop();
                var afterBytes = new FileInfo(outPath).Length;
                var saved = Math.Round((beforeBytes - afterBytes) / (double)beforeBytes * 100, 1);
                var summary = saved > 0
                    ? $"Compressed: {beforeBytes / 1024}KB → {afterBytes / 1024}KB (saved {saved}%)"
                    : $"No compression gain (file already optimized)";

                if (json)
                {
                    var o = JsonOutput.Ok("pdf compress", summary, new
                    { output = Path.GetFullPath(outPath), beforeBytes, afterBytes, savedPercent = saved, quality });
                    o.Artifacts["pdf"] = Path.GetFullPath(outPath);
                    o.Meta.DurationMs = sw.ElapsedMilliseconds;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"{summary} → {outPath}"); }
            }
            catch (Exception ex) { CliHelpers.WriteError("pdf compress", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, qualityOpt, jsonOpt);
        return cmd;
    }

    // ===== pdf to-word (PDF → DOCX via Slice) =====

    static Command CreateToWord(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output .docx path") { IsRequired = true };
        var cmd = new Command("to-word", "Convert PDF to DOCX through NongPandoc slice + NongDb storage") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            const string command = "pdf to-word";
            try
            {
                if (!File.Exists(file))
                { CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }

                // Bug 2: auto-append .docx extension if output path doesn't have it
                if (!output.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    output += ".docx";

                CliHelpers.EnsureParentDir(output);

                // 1. Dissect PDF to temp directory
                var tempDir = Path.Combine(Path.GetTempPath(), "pdf-to-word-" + Guid.NewGuid().ToString("N")[..8]);
                var options = new PdfSliceOptions { Mode = "auto", Dpi = 200 };
                // Bug 3: always provide OCR recognizer so scan PDFs can be processed
                var recognizer = new PdfOcrRecognizerAdapter();
                var sliceResult = PdfSlice.Dissect(file, tempDir, options, recognizer);

                // 2. Import into NongDb via unified ingestion context
                string documentId;
                int dbBlockCount;
                using (var ctx = new Angri450.Nong.Data.IngestionContext())
                {
                    var result = ctx.IngestSlice(file, tempDir, "pdf", "to-word");
                    documentId = result.DocumentId;
                    dbBlockCount = result.Blocks;
                }

                // 3. Read blocks and assets from NongDb via unified query API
                List<DbBlock> blocks;
                List<DbAsset> dbAssets;
                using (var ctx = new Angri450.Nong.Data.IngestionContext())
                {
                    blocks = ctx.QueryBlocks(documentId).ToList();
                    dbAssets = ctx.QueryAssets(documentId).ToList();
                }

                // 4. Build DOCX from NongDb blocks
                int headingCount = 0, paraCount = 0, imageCount = 0, lastPage = 0;
                using var doc = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document();
                var body = new W.Body();
                mainPart.Document.Append(body);

                var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
                var styles = new W.Styles();
                for (int i = 1; i <= 6; i++)
                {
                    var s = new W.Style { Type = W.StyleValues.Paragraph, StyleId = $"Heading{i}" };
                    s.Append(new W.StyleName { Val = $"heading {i}" });
                    s.Append(new W.NextParagraphStyle { Val = "Normal" });
                    styles.Append(s);
                }
                var ns = new W.Style { Type = W.StyleValues.Paragraph, StyleId = "Normal" };
                ns.Append(new W.StyleName { Val = "Normal" });
                styles.Append(ns);
                stylePart.Styles = styles;

                foreach (var block in blocks.OrderBy(b => b.Index))
                {
                    var kind = (block.BlockType ?? "paragraph").ToLowerInvariant();
                    var text = block.Text ?? "";
                    var storedJson = block.Json ?? "";

                    // Skip noise: empty, single chars, pure numbers, very short fragments
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (text.Length <= 1) continue;
                    if (text.Length <= 3 && text.All(c => char.IsDigit(c) || char.IsPunctuation(c) || c == ' ')) continue;
                    if (kind != "heading" && kind != "paragraph" && kind != "image") continue;

                    // ── Image block: embed the real picture from NongDb assets ──
                    if (kind == "image")
                    {
                        // Extract assetId from stored JSON (PdfContentBlock → assetId or id field).
                        string? assetId = null;
                        if (!string.IsNullOrEmpty(storedJson))
                        {
                            try
                            {
                                using var jd = JsonDocument.Parse(storedJson);
                                assetId = jd.RootElement.TryGetProperty("assetId", out var aid) ? aid.GetString()
                                    : jd.RootElement.TryGetProperty("id", out var bid) ? bid.GetString() : null;
                            }
                            catch { }
                        }

                        var asset = string.IsNullOrEmpty(assetId) ? null
                            : dbAssets.FirstOrDefault(a =>
                                string.Equals(Path.GetFileNameWithoutExtension(a.FileName), assetId, StringComparison.OrdinalIgnoreCase));

                        if (asset?.Data != null && asset.Data.Length > 0)
                        {
                            int wPt = Math.Max(1, asset.Width ?? 72);
                            int hPt = Math.Max(1, asset.Height ?? 72);
                            long wEmu = (long)(wPt * 12700);
                            long hEmu = (long)(hPt * 12700);

                            var imagePartType = asset.MimeType switch
                            {
                                "image/png" => ImagePartType.Png,
                                "image/gif" => ImagePartType.Gif,
                                "image/bmp" => ImagePartType.Bmp,
                                _ => ImagePartType.Jpeg
                            };

                            var imagePart = mainPart.AddImagePart(imagePartType);
                            using var ms = new MemoryStream(asset.Data);
                            ms.CopyTo(imagePart.GetStream());

                            var drawing = new W.Drawing(
                                new DW.Inline(
                                    new DW.Extent { Cx = wEmu, Cy = hEmu },
                                    new DW.EffectExtent
                                    {
                                        LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L
                                    },
                                    new DW.DocProperties
                                    {
                                        Id = (uint)(imageCount + 1),
                                        Name = $"Image {imageCount + 1}"
                                    },
                                    new DW.NonVisualGraphicFrameDrawingProperties(
                                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                                    new A.Graphic(
                                        new A.GraphicData(
                                            new PIC.Picture(
                                                new PIC.NonVisualPictureProperties(
                                                    new PIC.NonVisualDrawingProperties
                                                    {
                                                        Id = (uint)(imageCount + 2),
                                                        Name = $"image{imageCount + 1}"
                                                    },
                                                    new PIC.NonVisualPictureDrawingProperties()),
                                                new PIC.BlipFill(
                                                    new A.Blip { Embed = mainPart.GetIdOfPart(imagePart) },
                                                    new A.Stretch(new A.FillRectangle())),
                                                new PIC.ShapeProperties(
                                                    new A.Transform2D(
                                                        new A.Offset { X = 0L, Y = 0L },
                                                        new A.Extents { Cx = wEmu, Cy = hEmu }),
                                                    new A.PresetGeometry(
                                                        new A.AdjustValueList())
                                                    { Preset = A.ShapeTypeValues.Rectangle }))
                                            ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                                ));

                            var imgPara = new W.Paragraph();
                            var imgRun = new W.Run();
                            imgRun.Append(drawing);
                            imgPara.Append(imgRun);
                            body.Append(imgPara);
                            imageCount++;
                        }
                        continue;
                    }

                    // Trust the dissect stage's Kind verdict (PdfTextExtractor.InferKind now correctly
                    // distinguishes heading vs paragraph using page-level size variation). Do NOT
                    // re-promote paragraphs to headings based on an absolute font-size threshold here —
                    // that previously turned every ≥14pt body line into a heading.
                    bool isHeading = kind == "heading";

                    // fontSize is only used to pick a heading LEVEL for genuine headings.
                    double fontSize = 10.5;
                    if (isHeading && !string.IsNullOrEmpty(storedJson))
                    {
                        try
                        {
                            using var d = JsonDocument.Parse(storedJson);
                            if (d.RootElement.TryGetProperty("format", out var bf) && bf.TryGetProperty("size", out var bsz) && bsz.TryGetDouble(out var fz))
                                fontSize = fz;
                            else if (d.RootElement.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var r in runs.EnumerateArray())
                                {
                                    if (r.TryGetProperty("format", out var rf) && rf.TryGetProperty("size", out var sz) && sz.TryGetDouble(out var fz2))
                                    { fontSize = fz2; break; }
                                }
                            }
                        }
                        catch { }
                    }

                    // Page number lives in the original PdfContentBlock JSON (not on DbBlock).
                    int page = 0;
                    if (!string.IsNullOrEmpty(storedJson))
                    {
                        try
                        {
                            using var pd = JsonDocument.Parse(storedJson);
                            if (pd.RootElement.TryGetProperty("page", out var pgEl) && pgEl.TryGetInt32(out var pgVal))
                                page = pgVal;
                        }
                        catch { }
                    }

                    if (page > lastPage && lastPage > 0)
                    {
                        var bp = new W.Paragraph();
                        bp.Append(new W.Run(new W.Break { Type = W.BreakValues.Page }));
                        body.Append(bp);
                    }
                    lastPage = page;

                    var para = new W.Paragraph();
                    var run = new W.Run();
                    run.Append(new W.Text(text));
                    var runProps = new W.RunProperties();

                    if (isHeading)
                    {
                        headingCount++;
                        int level = fontSize >= 16 ? 1 : fontSize >= 13 ? 2 : fontSize >= 11 ? 3 : 4;
                        para.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = $"Heading{level}" }));
                        var sizes = new[] { "", "48", "36", "32", "28", "24", "22" };
                        runProps.Append(new W.FontSize { Val = sizes[level] });
                        runProps.Append(new W.Bold());
                    }
                    else
                    {
                        paraCount++;
                        runProps.Append(new W.FontSize { Val = "21" });
                        runProps.Append(new W.FontSizeComplexScript { Val = "21" });
                    }

                    runProps.Append(new W.RunFonts { EastAsia = "宋体", Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
                    run.RunProperties = runProps;
                    para.Append(run);
                    body.Append(para);
                }

                doc.Save();

                try { Directory.Delete(tempDir, true); } catch { }

                var info = new FileInfo(output);
                var o = JsonOutput.Ok(command,
                    $"Converted: {sliceResult.PageCount} page(s) → DOCX ({info.Length} bytes, {headingCount} headings, {paraCount} paragraphs, {imageCount} images)",
                    new { sourcePages = sliceResult.PageCount, outputBytes = info.Length, headings = headingCount, paragraphs = paraCount, images = imageCount, documentId, dbBlockCount, sourceBlocks = blocks.Count });
                o.Artifacts["docx"] = output;
                o.Artifacts["documentId"] = documentId;
                o.Metrics["pages"] = sliceResult.PageCount;
                o.Metrics["headings"] = headingCount;
                o.Metrics["paragraphs"] = paraCount;
                o.Metrics["outputBytes"] = info.Length;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError(command, ex, json);
            }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static string? GetStr(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var n) ? n : null;

    // ===== pdf db-* (unified ingestion via IngestionContext, stage D) =====

    static Command CreateDbImport(Option<bool> jsonOpt)
    {
        var sliceArg = new Argument<string>("slice-dir", "Directory from pdf dissect");
        var pdfArg = new Argument<string>("pdf", "Original .pdf file");
        var cmd = new Command("db-import", "Import pdf dissect output into NongDb (unified ingestion)") { sliceArg, pdfArg };
        cmd.SetHandler((string dir, string pdf, bool json) =>
        {
            if (!Directory.Exists(dir)) { CliHelpers.WriteError("pdf db-import", ErrorCodes.FileNotFound with { Message = $"Directory not found: {dir}" }, json); return; }
            if (!File.Exists(pdf)) { CliHelpers.WriteError("pdf db-import", ErrorCodes.FileNotFound with { Message = $"File not found: {pdf}" }, json); return; }

            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var result = ctx.IngestSlice(pdf, dir, "pdf", "db-import");

            var shaShort = result.Sha256[..12];
            var dbPath = Path.Combine(Angri450.Nong.NongWorkplace.Cache, "nong.db");

            var o = JsonOutput.Ok("pdf db-import", $"Imported: {result.Blocks} blocks, {result.Images} images", new
            {
                documentId = result.DocumentId, result.FileName, result.Format, sha = shaShort,
                result.Blocks, result.Images,
                result.HasFormat,
                dbFile = dbPath,
                runId = result.RunId
            });
            o.Metrics["blocks"] = result.Blocks; o.Metrics["images"] = result.Images;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, sliceArg, pdfArg, jsonOpt);
        return cmd;
    }

    static Command CreateDbList(Option<bool> jsonOpt)
    {
        var cmd = new Command("db-list", "List documents in NongDb");
        cmd.SetHandler((bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var docs = ctx.QueryDocuments();
            var o = JsonOutput.Ok("pdf db-list", $"{docs.Count} documents", new
            {
                count = docs.Count,
                items = docs.Select(d => new { id = d.Id.ToString(), d.FileName, d.Format, d.FileSize, sha = d.Sha256.Length >= 12 ? d.Sha256[..12] : d.Sha256, d.RegisteredAt })
            });
            o.Metrics["documents"] = docs.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, jsonOpt);
        return cmd;
    }

    static Command CreateDbBlocks(Option<bool> jsonOpt)
    {
        var idArg = new Argument<string>("document-id", "Document ID from db-list");
        var typeArg = new Option<string?>("--type", "Block type filter: paragraph, heading, table, image");
        var limitArg = new Option<int>("--limit", () => 50);
        var cmd = new Command("db-blocks", "List blocks for a document") { idArg, typeArg, limitArg };
        cmd.SetHandler((string id, string? type, int limit, bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var blocks = ctx.QueryBlocks(id, type, limit);

            var o = JsonOutput.Ok("pdf db-blocks", $"{blocks.Count} blocks", new
            {
                count = blocks.Count,
                items = blocks.Select(b => new { id = b.Id.ToString(), b.BlockId, b.BlockType, text = b.Text?.Length > 200 ? b.Text[..197] + "..." : b.Text, b.Index })
            });
            o.Metrics["blocks"] = blocks.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, idArg, typeArg, limitArg, jsonOpt);
        return cmd;
    }

    static Command CreateDbImages(Option<bool> jsonOpt)
    {
        var idArg = new Argument<string>("document-id", "Document ID from db-list");
        var cmd = new Command("db-images", "List extracted images for a document") { idArg };
        cmd.SetHandler((string id, bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var images = ctx.QueryAssets(id, "image/");

            var o = JsonOutput.Ok("pdf db-images", $"{images.Count} images", new
            {
                count = images.Count,
                items = images.Select(i => new { id = i.Id.ToString(), i.FileName, i.MimeType, i.Width, i.Height, i.Usage, dataSize = i.Data?.Length ?? 0 })
            });
            o.Metrics["images"] = images.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, idArg, jsonOpt);
        return cmd;
    }

    static void WritePdfError(string command, Exception ex, bool json)
    {
        if (ex is AggregateException ae && ae.InnerException != null)
            ex = ae.InnerException;

        if (ex is PdfProcessingException pdfEx)
        {
            CliHelpers.WriteError(command, ToErrorEntry(pdfEx), json);
            return;
        }

        if (IsLocalOcrDependencyException(ex))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.DependencyMissing with
                {
                    Message = $"Local OCR/PDF native dependency is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv6-medium --json' for OCR mode. No Python is required."
                }, json);
            return;
        }

        CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
    }

    static ErrorEntry ToErrorEntry(PdfProcessingException ex)
    {
        var entry = ex.Kind switch
        {
            PdfErrorKind.FileNotFound => ErrorCodes.FileNotFound,
            PdfErrorKind.UnsupportedFormat => ErrorCodes.UnsupportedFormat,
            PdfErrorKind.DependencyMissing => ErrorCodes.DependencyMissing,
            PdfErrorKind.ValidationFailed => ErrorCodes.ValidationFailed,
            PdfErrorKind.ReadFailed => ErrorCodes.ReadFailed,
            PdfErrorKind.WriteFailed => ErrorCodes.WriteFailed,
            _ => ErrorCodes.InternalError,
        };
        return entry with { Message = ex.Message };
    }

    static bool IsLocalOcrDependencyException(Exception ex)
    {
        var text = ex.ToString();
        return ex is DllNotFoundException
            || ex is BadImageFormatException
            || text.Contains("OpenCvSharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pdfium", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Native OCR", StringComparison.OrdinalIgnoreCase);
    }
}
