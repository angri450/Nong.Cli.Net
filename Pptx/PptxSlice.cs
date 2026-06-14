using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PandocCore;
using D = DocumentFormat.OpenXml.Drawing;

namespace PptxCore;

public static class PptxSlice
{
    public static PptxSliceResult Slice(string pptxPath, string outputDir)
    {
        if (string.IsNullOrWhiteSpace(pptxPath))
            throw new ArgumentException("PPTX path is required.", nameof(pptxPath));
        if (!File.Exists(pptxPath))
            throw new FileNotFoundException($"File not found: {pptxPath}", pptxPath);
        var ext = Path.GetExtension(pptxPath).ToLowerInvariant();
        if (ext != ".pptx")
            throw new InvalidDataException($"Expected .pptx file, got: {ext}");

        var source = Path.GetFileName(pptxPath);
        var read = PptxReader.Read(pptxPath);
        var slides = PptxReader.Slides(pptxPath);
        var evidence = ExtractSlideEvidence(pptxPath);
        var blocks = new List<PptxContentBlock>();
        var warnings = new List<string>();
        var blockIndex = 0;

        foreach (var slide in read.Slides)
        {
            if (!string.IsNullOrWhiteSpace(slide.Title))
            {
                blocks.Add(new PptxContentBlock
                {
                    Id = $"h{++blockIndex:D4}",
                    BlockId = $"h{blockIndex:D4}",
                    Index = blockIndex - 1,
                    Kind = "heading",
                    Slide = slide.Index,
                    Text = slide.Title,
                });
            }

            foreach (var text in slide.Texts.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                if (text == slide.Title)
                    continue;

                blocks.Add(new PptxContentBlock
                {
                    Id = $"p{++blockIndex:D4}",
                    BlockId = $"p{blockIndex:D4}",
                    Index = blockIndex - 1,
                    Kind = "paragraph",
                    Slide = slide.Index,
                    Text = text,
                });
            }
        }

        if (read.Slides.Count == 0)
            warnings.Add("Presentation has no readable slides.");

        var document = new PptxSliceDocument
        {
            Source = new PptxSourceInfo
            {
                Path = source,
                Sha256 = Sha256(pptxPath),
                SlideCount = read.Slides.Count,
            },
            Slides = read.Slides.Select(s => new PptxSlideSlice
            {
                Index = s.Index,
                Title = s.Title,
                Texts = s.Texts,
                ShapeCount = slides.Slides.FirstOrDefault(x => x.Index == s.Index)?.ShapeCount ?? 0,
                TextCount = slides.Slides.FirstOrDefault(x => x.Index == s.Index)?.TextCount ?? s.Texts.Count,
                PictureCount = slides.Slides.FirstOrDefault(x => x.Index == s.Index)?.PictureCount ?? 0,
                TableCount = slides.Slides.FirstOrDefault(x => x.Index == s.Index)?.TableCount ?? 0,
                ChartCount = slides.Slides.FirstOrDefault(x => x.Index == s.Index)?.ChartCount ?? 0,
                Shapes = evidence.TryGetValue(s.Index, out var slideEvidence) ? slideEvidence.Shapes : new List<PptxShapeEvidence>(),
                Assets = evidence.TryGetValue(s.Index, out slideEvidence) ? slideEvidence.Assets : new List<PptxAssetItem>(),
                Notes = evidence.TryGetValue(s.Index, out slideEvidence) ? slideEvidence.Notes : "",
            }).ToList(),
            Blocks = blocks,
            Warnings = warnings,
        };
        var metrics = new NongPandocMetrics
        {
            Blocks = blocks.Count,
            Paragraphs = blocks.Count(b => b.Kind == "paragraph"),
            Headings = blocks.Count(b => b.Kind == "heading"),
            Tables = document.Slides.Sum(s => s.TableCount),
            Figures = document.Slides.Sum(s => s.PictureCount + s.ChartCount),
            Images = document.Slides.Sum(s => s.PictureCount),
            References = 0,
            Warnings = warnings.Count,
        };
        var manifest = new NongPandocSliceManifest
        {
            Source = new NongPandocSourceInfo
            {
                Path = source,
                Format = "pptx",
                Sha256 = document.Source.Sha256,
                SlideCount = read.Slides.Count,
            },
            CreatedAt = DateTime.UtcNow,
            Metrics = metrics,
            Warnings = warnings,
        };

        var writeResult = NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
        {
            OutputDirectory = outputDir,
            Manifest = manifest,
            Document = document,
            ContentJsonlItems = blocks.Cast<object>().ToList(),
            NongMarkText = BuildNongMark(document),
            Structure = BuildStructure(document),
            Format = BuildFormat(document),
            Diagnostics = new PptxSliceDiagnostics { Source = source, Warnings = warnings },
            AssetsManifest = BuildAssetsManifest(document),
            TextPreview = read.Text + (read.Text.EndsWith("\n", StringComparison.Ordinal) ? "" : "\n"),
        }, new NongPandocSliceWriteOptions
        {
            RequiredArtifacts = NongPandocSlicePackageWriter.DefaultRequiredArtifacts
                .Concat(new[] { NongPandocArtifactNames.TextPreview })
                .ToArray(),
        });

        return new PptxSliceResult
        {
            OutputDir = writeResult.OutputDirectory,
            ManifestPath = writeResult.ManifestPath,
            BlockCount = blocks.Count,
            SlideCount = read.Slides.Count,
            Warnings = warnings,
        };
    }

    private static PptxSliceStructure BuildStructure(PptxSliceDocument document)
    {
        var structure = new PptxSliceStructure { Source = document.Source.Path };
        foreach (var slide in document.Slides)
        {
            structure.Slides.Add(new PptxSlideRef
            {
                Index = slide.Index,
                Title = slide.Title,
                ShapeCount = slide.ShapeCount,
                TextCount = slide.TextCount,
                PictureCount = slide.PictureCount,
                TableCount = slide.TableCount,
                ChartCount = slide.ChartCount,
                Shapes = slide.Shapes,
                Assets = slide.Assets,
                NotesPreview = Preview(slide.Notes),
            });
        }

        foreach (var block in document.Blocks)
        {
            var shape = FindShapeForBlock(document, block);
            structure.BlockIndex[block.BlockId] = new PptxBlockIndexEntry
            {
                Kind = block.Kind,
                Order = block.Index,
                Slide = block.Slide,
                Layout = shape?.Layout,
                TextPreview = Preview(block.Text),
                Provenance = new NongPandocBlockProvenance
                {
                    Format = "pptx",
                    Source = shape?.Kind ?? block.Kind,
                    Slide = block.Slide,
                    Position = block.Index,
                    Layout = ToPandocLayout(shape?.Layout),
                    AssetId = shape?.AssetId,
                    RelationshipId = shape?.RelationshipId,
                    Confidence = shape == null ? "medium" : "high",
                    Notes = shape == null ? new List<string> { "No matching OpenXML shape evidence found for this text block." } : null,
                },
            };
        }

        return structure;
    }

    private static PptxShapeEvidence? FindShapeForBlock(PptxSliceDocument document, PptxContentBlock block) =>
        document.Slides.FirstOrDefault(s => s.Index == block.Slide)
            ?.Shapes.FirstOrDefault(s => string.Equals(s.TextPreview, Preview(block.Text), StringComparison.Ordinal));

    private static NongPandocLayoutEvidence? ToPandocLayout(PptxLayoutBox? layout)
    {
        if (layout == null)
            return null;

        return new NongPandocLayoutEvidence
        {
            X = layout.X,
            Y = layout.Y,
            Width = layout.Cx,
            Height = layout.Cy,
            Unit = "emu",
        };
    }

    private static PptxSliceFormat BuildFormat(PptxSliceDocument document)
    {
        var format = new PptxSliceFormat
        {
            Source = document.Source.Path,
            Slides = document.Slides.Select(s => new PptxSlideFormat
            {
                Index = s.Index,
                ShapeCount = s.ShapeCount,
                TextCount = s.TextCount,
                PictureCount = s.PictureCount,
                TableCount = s.TableCount,
                ChartCount = s.ChartCount,
                Shapes = s.Shapes.Select(shape => new PptxShapeFormat
                {
                    Id = shape.Id,
                    Name = shape.Name,
                    Kind = shape.Kind,
                    Layout = shape.Layout,
                }).ToList(),
            }).ToList(),
        };

        format.VisualEvidence = new NongPandocVisualEvidence
        {
            Format = "pptx",
            Source = document.Source.Path,
            Tables = document.Slides
                .SelectMany(s => s.Shapes.Where(shape => shape.Kind == "table").Select(shape => $"slide{s.Index}:{shape.Id}:{shape.Name}"))
                .ToList(),
            Layout = document.Slides
                .SelectMany(s => s.Shapes.Select(shape => $"slide{s.Index}:{shape.Kind}:{shape.Id}:x={shape.Layout?.X};y={shape.Layout?.Y};cx={shape.Layout?.Cx};cy={shape.Layout?.Cy}"))
                .ToList(),
            Assets = document.Slides
                .SelectMany(s => s.Assets.Select(a => $"slide{s.Index}:{a.Kind}:{a.Id}:rel={a.RelationshipId}"))
                .ToList(),
            Warnings = document.Warnings,
        };

        return format;
    }

    private static PptxAssetManifest BuildAssetsManifest(PptxSliceDocument document)
    {
        var manifest = new PptxAssetManifest { Source = document.Source.Path };
        foreach (var slide in document.Slides)
            manifest.Items.AddRange(slide.Assets);
        return manifest;
    }

    private static string BuildNongMark(PptxSliceDocument document)
    {
        var sb = new StringBuilder();
        foreach (var slide in document.Slides)
        {
            sb.AppendLine($"::: slide {{#slide-{slide.Index:D3} number={slide.Index} shapes={slide.ShapeCount} pictures={slide.PictureCount} tables={slide.TableCount} charts={slide.ChartCount}}}");
            if (!string.IsNullOrWhiteSpace(slide.Notes))
                sb.AppendLine($"<!-- notes-preview: {EscapeText(Preview(slide.Notes, 160))} -->");
            if (!string.IsNullOrWhiteSpace(slide.Title))
            {
                sb.AppendLine($"# {EscapeText(slide.Title)} {{#slide-{slide.Index:D3}-title kind=heading slide={slide.Index}}}");
                sb.AppendLine();
            }

            foreach (var text in slide.Texts.Where(t => !string.IsNullOrWhiteSpace(t) && t != slide.Title))
            {
                sb.AppendLine($"::: paragraph {{kind=paragraph slide={slide.Index}}}");
                sb.AppendLine(EscapeText(text));
                sb.AppendLine(":::");
                sb.AppendLine();
            }

            sb.AppendLine(":::");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static Dictionary<int, PptxSlideEvidence> ExtractSlideEvidence(string pptxPath)
    {
        using var doc = PresentationDocument.Open(pptxPath, false);
        var presPart = doc.PresentationPart;
        var result = new Dictionary<int, PptxSlideEvidence>();
        var slideIdList = presPart?.Presentation.SlideIdList;
        if (presPart == null || slideIdList == null)
            return result;

        var index = 0;
        foreach (var slideId in slideIdList.ChildElements.OfType<SlideId>())
        {
            index++;
            if (slideId.RelationshipId == null)
                continue;

            if (presPart.GetPartById(slideId.RelationshipId!) is not SlidePart slidePart)
                continue;

            var evidence = new PptxSlideEvidence();
            var tree = slidePart.Slide.CommonSlideData?.ShapeTree;
            if (tree != null)
            {
                foreach (var child in tree.ChildElements)
                    CollectShapeEvidence(child, index, slidePart, evidence, null);
            }

            evidence.Notes = string.Join("\n", slidePart.NotesSlidePart?.NotesSlide?.Descendants<D.Text>()
                .Select(t => t.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t)) ?? Enumerable.Empty<string>());
            result[index] = evidence;
        }

        return result;
    }

    private static void CollectShapeEvidence(
        OpenXmlElement element,
        int slide,
        SlidePart slidePart,
        PptxSlideEvidence evidence,
        string? groupName)
    {
        switch (element)
        {
            case Shape shape:
                var shapeProps = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
                var placeholder = shape.NonVisualShapeProperties
                    ?.ApplicationNonVisualDrawingProperties
                    ?.PlaceholderShape?.Type?.Value.ToString();
                var text = string.Join(" ", shape.Descendants<D.Text>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                evidence.Shapes.Add(new PptxShapeEvidence
                {
                    Id = shapeProps?.Id?.Value.ToString() ?? "",
                    Name = shapeProps?.Name?.Value ?? "",
                    Kind = string.IsNullOrWhiteSpace(placeholder) ? "shape" : "placeholder",
                    Placeholder = placeholder,
                    Slide = slide,
                    Group = groupName,
                    TextPreview = Preview(text),
                    Layout = LayoutFrom(element),
                });
                break;

            case Picture picture:
                var pictureProps = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
                var blip = picture.BlipFill?.Blip;
                var relationshipId = blip?.Embed?.Value ?? blip?.Link?.Value;
                var assetId = $"slide-{slide:D3}-asset-{evidence.Assets.Count + 1:D3}";
                var contentType = TryGetPart(slidePart, relationshipId)?.ContentType ?? "";
                evidence.Shapes.Add(new PptxShapeEvidence
                {
                    Id = pictureProps?.Id?.Value.ToString() ?? "",
                    Name = pictureProps?.Name?.Value ?? "",
                    Kind = "picture",
                    Slide = slide,
                    Group = groupName,
                    RelationshipId = relationshipId,
                    AssetId = assetId,
                    Layout = LayoutFrom(element),
                });
                evidence.Assets.Add(new PptxAssetItem
                {
                    Id = assetId,
                    Kind = "picture",
                    Slide = slide,
                    ShapeId = pictureProps?.Id?.Value.ToString() ?? "",
                    Name = pictureProps?.Name?.Value ?? "",
                    RelationshipId = relationshipId,
                    ContentType = contentType,
                });
                break;

            case GraphicFrame graphicFrame:
                var frameProps = graphicFrame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties;
                var uri = graphicFrame.Graphic?.GraphicData?.Uri?.Value ?? "";
                var frameKind = uri.Contains("/chart", StringComparison.OrdinalIgnoreCase)
                    ? "chart"
                    : uri.Contains("/table", StringComparison.OrdinalIgnoreCase)
                        ? "table"
                        : "graphicFrame";
                var frameRelationshipId = RelationshipIdFromGraphicFrame(graphicFrame);
                var frameAssetId = !string.IsNullOrWhiteSpace(frameRelationshipId)
                    ? $"slide-{slide:D3}-asset-{evidence.Assets.Count + 1:D3}"
                    : null;
                evidence.Shapes.Add(new PptxShapeEvidence
                {
                    Id = frameProps?.Id?.Value.ToString() ?? "",
                    Name = frameProps?.Name?.Value ?? "",
                    Kind = frameKind,
                    Slide = slide,
                    Group = groupName,
                    RelationshipId = frameRelationshipId,
                    AssetId = frameAssetId,
                    Layout = LayoutFrom(element),
                });
                if (frameAssetId != null)
                {
                    var part = TryGetPart(slidePart, frameRelationshipId);
                    evidence.Assets.Add(new PptxAssetItem
                    {
                        Id = frameAssetId,
                        Kind = frameKind,
                        Slide = slide,
                        ShapeId = frameProps?.Id?.Value.ToString() ?? "",
                        Name = frameProps?.Name?.Value ?? "",
                        RelationshipId = frameRelationshipId,
                        ContentType = part?.ContentType ?? "",
                    });
                }
                break;

            case GroupShape groupShape:
                var groupProps = groupShape.NonVisualGroupShapeProperties?.NonVisualDrawingProperties;
                var currentGroup = groupProps?.Name?.Value ?? groupName;
                evidence.Shapes.Add(new PptxShapeEvidence
                {
                    Id = groupProps?.Id?.Value.ToString() ?? "",
                    Name = currentGroup ?? "",
                    Kind = "group",
                    Slide = slide,
                    Group = groupName,
                    Layout = LayoutFrom(element),
                });
                foreach (var child in groupShape.ChildElements)
                    CollectShapeEvidence(child, slide, slidePart, evidence, currentGroup);
                break;

            case ConnectionShape connectionShape:
                var connectionProps = connectionShape.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties;
                evidence.Shapes.Add(new PptxShapeEvidence
                {
                    Id = connectionProps?.Id?.Value.ToString() ?? "",
                    Name = connectionProps?.Name?.Value ?? "",
                    Kind = "connectionShape",
                    Slide = slide,
                    Group = groupName,
                    Layout = LayoutFrom(element),
                });
                break;
        }
    }

    private static PptxLayoutBox? LayoutFrom(OpenXmlElement element)
    {
        var offset = element.Descendants<D.Offset>().FirstOrDefault();
        var extents = element.Descendants<D.Extents>().FirstOrDefault();
        if (offset == null && extents == null)
            return null;

        return new PptxLayoutBox
        {
            X = offset?.X?.Value,
            Y = offset?.Y?.Value,
            Cx = extents?.Cx?.Value,
            Cy = extents?.Cy?.Value,
        };
    }

    private static OpenXmlPart? TryGetPart(SlidePart slidePart, string? relationshipId)
    {
        if (string.IsNullOrWhiteSpace(relationshipId))
            return null;

        try
        {
            return slidePart.GetPartById(relationshipId);
        }
        catch
        {
            return null;
        }
    }

    private static string? RelationshipIdFromGraphicFrame(GraphicFrame graphicFrame)
    {
        const string relationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return graphicFrame.Descendants()
            .Select(e => e.GetAttribute("id", relationshipsNs).Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private static string Preview(string? value, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= max ? text : text[..max] + "...";
    }

    private static string EscapeText(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();
}

public sealed record PptxSliceResult
{
    public string OutputDir { get; init; } = "";
    public string ManifestPath { get; init; } = "";
    public int BlockCount { get; init; }
    public int SlideCount { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public sealed record PptxSliceDocument
{
    public string SchemaVersion { get; init; } = "nongpptx/v1";
    public PptxSourceInfo Source { get; init; } = new();
    public List<PptxSlideSlice> Slides { get; init; } = new();
    public List<PptxContentBlock> Blocks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed record PptxSourceInfo
{
    public string Path { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public int SlideCount { get; init; }
}

public sealed record PptxSlideSlice
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public List<string> Texts { get; init; } = new();
    public int ShapeCount { get; init; }
    public int TextCount { get; init; }
    public int PictureCount { get; init; }
    public int TableCount { get; init; }
    public int ChartCount { get; init; }
    public List<PptxShapeEvidence> Shapes { get; init; } = new();
    public List<PptxAssetItem> Assets { get; init; } = new();
    public string Notes { get; init; } = "";
}

public sealed record PptxContentBlock
{
    public string Id { get; init; } = "";
    public string BlockId { get; init; } = "";
    public int Index { get; init; }
    public string Kind { get; init; } = "";
    public int Slide { get; init; }
    public string Text { get; init; } = "";
}

public sealed record PptxSliceStructure
{
    public string SchemaVersion { get; init; } = "nongpptx/structure/v1";
    public string Source { get; init; } = "";
    public List<PptxSlideRef> Slides { get; init; } = new();
    public Dictionary<string, PptxBlockIndexEntry> BlockIndex { get; init; } = new();
}

public sealed record PptxSlideRef
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public int ShapeCount { get; init; }
    public int TextCount { get; init; }
    public int PictureCount { get; init; }
    public int TableCount { get; init; }
    public int ChartCount { get; init; }
    public List<PptxShapeEvidence> Shapes { get; init; } = new();
    public List<PptxAssetItem> Assets { get; init; } = new();
    public string NotesPreview { get; init; } = "";
}

public sealed record PptxBlockIndexEntry
{
    public string Kind { get; init; } = "";
    public int Order { get; init; }
    public int Slide { get; init; }
    public PptxLayoutBox? Layout { get; init; }
    public string TextPreview { get; init; } = "";
    public NongPandocBlockProvenance? Provenance { get; init; }
}

public sealed record PptxSliceFormat
{
    public string SchemaVersion { get; init; } = "nongpptx/format/v1";
    public string Source { get; init; } = "";
    public List<PptxSlideFormat> Slides { get; init; } = new();
    public NongPandocVisualEvidence VisualEvidence { get; set; } = new();
}

public sealed record PptxSlideFormat
{
    public int Index { get; init; }
    public int ShapeCount { get; init; }
    public int TextCount { get; init; }
    public int PictureCount { get; init; }
    public int TableCount { get; init; }
    public int ChartCount { get; init; }
    public List<PptxShapeFormat> Shapes { get; init; } = new();
}

public sealed record PptxSliceDiagnostics
{
    public string SchemaVersion { get; init; } = "nongpptx/diagnostics/v1";
    public string Source { get; init; } = "";
    public List<string> Warnings { get; init; } = new();
}

public sealed record PptxAssetManifest
{
    public string SchemaVersion { get; init; } = "nongpptx/assets/v1";
    public string Source { get; init; } = "";
    public List<PptxAssetItem> Items { get; init; } = new();
}

public sealed record PptxSlideEvidence
{
    public List<PptxShapeEvidence> Shapes { get; init; } = new();
    public List<PptxAssetItem> Assets { get; init; } = new();
    public string Notes { get; set; } = "";
}

public sealed record PptxShapeEvidence
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public int Slide { get; init; }
    public string? Group { get; init; }
    public string? Placeholder { get; init; }
    public PptxLayoutBox? Layout { get; init; }
    public string TextPreview { get; init; } = "";
    public string? RelationshipId { get; init; }
    public string? AssetId { get; init; }
}

public sealed record PptxShapeFormat
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public PptxLayoutBox? Layout { get; init; }
}

public sealed record PptxLayoutBox
{
    public long? X { get; init; }
    public long? Y { get; init; }
    public long? Cx { get; init; }
    public long? Cy { get; init; }
}

public sealed record PptxAssetItem
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public int Slide { get; init; }
    public string ShapeId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? RelationshipId { get; init; }
    public string ContentType { get; init; } = "";
}
