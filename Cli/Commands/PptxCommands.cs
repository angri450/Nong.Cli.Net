using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using Angri450.Nong.Data;
using Nong.Cli.Common;
using PptxCore;

namespace Nong.Cli.Commands;

/// <summary>Pptx command group: read, slides.</summary>
public static class PptxCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("pptx", "PowerPoint operations");
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreateSlides(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateCreatePptx(jsonOpt));
        cmd.AddCommand(CreateDbImport(jsonOpt));
        cmd.AddCommand(CreateDbList(jsonOpt));
        cmd.AddCommand(CreateDbBlocks(jsonOpt));
        cmd.AddCommand(CreateDbImages(jsonOpt));
        return cmd;
    }

    // ===== pptx read =====

    static Command CreateRead(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var cmd = new Command("read", "Extract slide text") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx read", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PptxReader.Read(file));

                if (json)
                {
                    var data = new
                    {
                        text = result.Text,
                        slides = result.Slides.Select(s => new
                        {
                            index = s.Index,
                            title = s.Title,
                            texts = s.Texts,
                            background = s.Background,
                            runs = s.Runs
                        }).ToList()
                    };
                    var metrics = new Dictionary<string, object>
                    {
                        ["slides"] = result.Slides.Count,
                        ["textBlocks"] = result.Slides.Sum(s => s.Texts.Count),
                        ["characters"] = result.Text.Length
                    };
                    var output = JsonOutput.Ok("pptx read", $"Extracted {result.Slides.Count} slides, {metrics["textBlocks"]} text blocks", data);
                    foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.Write(result.Text);
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx read", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== pptx slides =====

    static Command CreateSlides(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var cmd = new Command("slides", "List slide structure") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx slides", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PptxReader.Slides(file));

                if (json)
                {
                    var data = new
                    {
                        slides = result.Slides.Select(s => new
                        {
                            index = s.Index,
                            shapeCount = s.ShapeCount,
                            textCount = s.TextCount,
                            pictureCount = s.PictureCount,
                            tableCount = s.TableCount,
                            chartCount = s.ChartCount,
                            title = s.Title
                        }).ToList()
                    };
                    var metrics = new Dictionary<string, object>
                    {
                        ["slides"] = result.Slides.Count,
                        ["totalShapes"] = result.Slides.Sum(s => s.ShapeCount),
                        ["totalPictures"] = result.Slides.Sum(s => s.PictureCount),
                        ["totalTables"] = result.Slides.Sum(s => s.TableCount),
                        ["totalCharts"] = result.Slides.Sum(s => s.ChartCount)
                    };
                    var output = JsonOutput.Ok("pptx slides", $"Analyzed {result.Slides.Count} slides", data);
                    foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var s in result.Slides)
                    {
                        Console.WriteLine($"Slide {s.Index}: {s.ShapeCount} shapes, {s.TextCount} text, {s.PictureCount} pics, {s.TableCount} tables, {s.ChartCount} charts");
                        if (!string.IsNullOrEmpty(s.Title))
                            Console.WriteLine($"  Title: {s.Title}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx slides", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== pptx dissect =====

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for NongPandoc slice") { IsRequired = true };
        var ingestOpt = new Option<bool>("--ingest", () => false, "Auto-import dissect output into NongDb for semantic search");
        var cmd = new Command("dissect", "Slice pptx into a NongPandoc package") { fileArg, outOpt, ingestOpt };

        cmd.SetHandler((string file, string output, bool ingest, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx dissect", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(output, ".keep"));
                var (result, elapsed) = CliHelpers.Time(() => PptxSlice.Slice(file, output));
                if (json)
                {
                    var o = JsonOutput.Ok("pptx dissect",
                        $"Sliced: {result.SlideCount} slides, {result.BlockCount} blocks",
                        new { outputDir = result.OutputDir, slideCount = result.SlideCount, blockCount = result.BlockCount, warnings = result.Warnings });
                    o.Artifacts["dir"] = Path.GetFullPath(output);
                    o.Metrics["slides"] = result.SlideCount;
                    o.Metrics["blocks"] = result.BlockCount;
                    o.Metrics["warnings"] = result.Warnings.Count;
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Sliced to {Path.GetFullPath(output)}: {result.SlideCount} slides, {result.BlockCount} blocks");
                    foreach (var warning in result.Warnings)
                        Console.Error.WriteLine($"[WARN] {warning}");
                }
                if (ingest)
                {
                    try
                    {
                        using var ctx = new IngestionContext();
                        var ir = ctx.IngestSlice(file, output, "pptx", "dissect");
                        if (!json) Console.Error.WriteLine($"[ingest] {ir.Blocks} blocks imported to nong.db");
                    }
                    catch (Exception ex) { if (!json) Console.Error.WriteLine($"[ingest] warning: {ex.Message}"); }
                }
            }
            catch (FileNotFoundException ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.FileNotFound with { Message = ex.Message }, json);
            }
            catch (InvalidDataException ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, ingestOpt, jsonOpt);

        return cmd;
    }

    // ===== pptx create =====

    static Command CreateCreatePptx(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output .pptx path") { IsRequired = true };
        var cmd = new Command("create", "Create pptx from JSON spec") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("pptx create", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<PptxCreateSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Slides == null || spec.Slides.Count == 0)
                {
                    CliHelpers.WriteError("pptx create",
                        ErrorCodes.ValidationFailed with { Message = "slides array must be non-empty." }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                var (slideCount, elapsed) = CliHelpers.Time<int>(() =>
                {
                    var theme = ResolveTheme(spec.Theme);
                    var builder = SlideBuilder.Create().Theme(theme);
                    foreach (var s in spec.Slides)
                        ApplySlide(builder, s, theme);
                    builder.Save(output);
                    return spec.Slides.Count;
                });

                var aerr = CliHelpers.CheckArtifact(output, "PPTX");
                if (aerr != null) { CliHelpers.WriteError("pptx create", aerr, json); return; }

                if (json)
                {
                    var o = JsonOutput.Ok("pptx create",
                        $"Created PPTX with {slideCount} slides", new { slides = slideCount });
                    o.Artifacts["pptx"] = Path.GetFullPath(output);
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Created: {Path.GetFullPath(output)} ({slideCount} slides)"); }
            }
            catch (JsonException jex) { CliHelpers.WriteError("pptx create", ErrorCodes.ValidationFailed with { Message = $"Invalid JSON: {jex.Message}" }, json); }
            catch (Exception ex) { CliHelpers.WriteError("pptx create", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static ThemePreset ResolveTheme(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return ThemePreset.Default;
        return ThemePreset.ByName(themeName) ?? ThemePreset.BuildFromJson(themeName);
    }

    static void ApplySlide(PresentationBuilder builder, PptxSlideSpec s, ThemePreset theme)
    {
        // Priority: Chart > Table > Picture > Items > Title-only
        if (s.Chart != null)
        {
            ApplyChartSlide(builder, s, theme);
            return;
        }
        if (s.Table != null && s.Table.Length > 0)
        {
            ApplyTableSlide(builder, s, theme);
            return;
        }
        if (s.Picture != null)
        {
            ApplyPictureSlide(builder, s, theme);
            return;
        }

        // Fallback to old spec: kind-based routing or items-based
        var layout = s.Layout ?? (s.Kind switch
        {
            "title" => "SingleFocus",
            "content" => "SingleFocus",
            "two-column" => "TwoColumns",
            "three-column" => "ThreeColumn",
            _ => null
        });

        if (layout != null)
        {
            ApplyLayoutSlide(builder, s, theme, layout);
            return;
        }

        // Default: title + items as bullet content
        builder.AddTitleSlide(tsb =>
        {
            if (!string.IsNullOrEmpty(s.Title)) tsb.Title(s.Title);
            if (!string.IsNullOrEmpty(s.Subtitle)) tsb.Subtitle(s.Subtitle);
            if (!string.IsNullOrEmpty(s.Author)) tsb.Author(s.Author);
        });
    }

    static void ApplyChartSlide(PresentationBuilder builder, PptxSlideSpec s, ThemePreset theme)
    {
        var chart = s.Chart!;
        if (chart.Kind == "pie" && chart.Data != null)
        {
            builder.AddChartSlide(cb =>
            {
                if (!string.IsNullOrEmpty(s.Title)) cb.Title(s.Title);
                if (!string.IsNullOrEmpty(chart.Title)) cb.ChartTitle(chart.Title);
                cb.PieChart(chart.Data);
            });
        }
        else if (chart.Data != null)
        {
            builder.AddChartSlide(cb =>
            {
                if (!string.IsNullOrEmpty(s.Title)) cb.Title(s.Title);
                if (!string.IsNullOrEmpty(chart.Title)) cb.ChartTitle(chart.Title);
                cb.BarChart(chart.Data, chart.SeriesName ?? "");
            });
        }
    }

    static void ApplyTableSlide(PresentationBuilder builder, PptxSlideSpec s, ThemePreset theme)
    {
        builder.AddTableSlide(tsb =>
        {
            if (!string.IsNullOrEmpty(s.Title)) tsb.Title(s.Title);
            tsb.Data(s.Table!);
        });
    }

    static void ApplyPictureSlide(PresentationBuilder builder, PptxSlideSpec s, ThemePreset theme)
    {
        // AddPictureSlide not yet implemented in PresentationBuilder (V6-2 Task 5)
        // Fallback: create a placeholder slide with picture path as text
        var pic = s.Picture!;
        builder.AddSlide()
            .TextBox(s.Title ?? "Picture", LayoutSystem.Margin_X, LayoutSystem.Content.TitleY,
                LayoutSystem.ContentWidth, LayoutSystem.Content.TitleHeight, fontSize: 32, bold: true)
            .TextBox($"Picture: {pic.Path}", LayoutSystem.Margin_X, LayoutSystem.Content.BodyY,
                LayoutSystem.ContentWidth, 40, fontSize: 16)
            .EndSlide();
    }

    static void ApplyLayoutSlide(PresentationBuilder builder, PptxSlideSpec s, ThemePreset theme, string layout)
    {
        switch (layout)
        {
            case "HeroTop":
                builder.AddSlide()
                    .HeroTop(s.Title ?? "", s.Subtitle)
                    .EndSlide();
                break;
            case "SingleFocus":
                builder.AddSlide()
                    .SingleFocus(s.Title ?? "", s.Subtitle)
                    .EndSlide();
                break;
            case "TwoColumns":
                {
                    var items = s.Items ?? Array.Empty<string>();
                    builder.AddSlide()
                        .TwoColumns(s.Title ?? "",
                            items.Length > 0 ? items[0] : "",
                            items.Length > 1 ? items[1] : "")
                        .EndSlide();
                }
                break;
            case "ThreeColumn":
                {
                    var items = s.Items ?? Array.Empty<string>();
                    builder.AddSlide()
                        .ThreeColumn(
                            items.Length > 0 ? items[0] : "", items.Length > 1 ? items[1] : "",
                            items.Length > 2 ? items[2] : "", items.Length > 3 ? items[3] : "",
                            items.Length > 4 ? items[4] : "", items.Length > 5 ? items[5] : "")
                        .EndSlide();
                }
                break;
            case "BigNumber":
                {
                    var items = s.Items ?? Array.Empty<string>();
                    builder.AddSlide()
                        .BigNumber(items.Length > 0 ? items[0] : "", s.Title ?? "", s.Subtitle)
                        .EndSlide();
                }
                break;
            case "Symmetric":
                {
                    var items = s.Items ?? Array.Empty<string>();
                    builder.AddSlide()
                        .Symmetric(s.Title ?? "", items.Length > 0 ? items[0] : "",
                            s.Subtitle ?? "", items.Length > 1 ? items[1] : "")
                        .EndSlide();
                }
                break;
            case "Cards":
                {
                    var cards = (s.Items ?? Array.Empty<string>())
                        .Select((text, i) => (cardTitle: $"Card {i + 1}", cardBody: text))
                        .ToArray();
                    builder.AddSlide()
                        .Cards(s.Title ?? "", cards)
                        .EndSlide();
                }
                break;
            default:
                // For kind-based fallback: title + content layout
                if (!string.IsNullOrEmpty(s.Title))
                {
                    builder.AddSlide().TextBox(s.Title, LayoutSystem.Margin_X, LayoutSystem.Content.TitleY,
                        LayoutSystem.ContentWidth, LayoutSystem.Content.TitleHeight, fontSize: 32, bold: true);
                }
                if (s.Items != null)
                {
                    int y = LayoutSystem.Content.BodyY;
                    foreach (var item in s.Items)
                    {
                        builder.AddSlide().TextBox(item, LayoutSystem.Margin_X + 20, y,
                            LayoutSystem.ContentWidth - 20, 30, fontSize: 16);
                        y += 40;
                    }
                }
                break;
        }
    }

    /// <summary>Validate .pptx extension.</summary>
    static ErrorEntry? ValidatePptxFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path))
            return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".pptx")
            return ErrorCodes.UnsupportedFormat with { Message = $"Expected .pptx file, got: {ext}" };
        return null;
    }

    // ════════════════════════════════════════════════════════════
    // pptx db — unified ingestion via IngestionContext
    // ════════════════════════════════════════════════════════════

    static Command CreateDbImport(Option<bool> jsonOpt)
    {
        var sliceArg = new Argument<string>("slice-dir", "Directory from pptx dissect");
        var pptxArg = new Argument<string>("pptx", "Original .pptx file");
        var cmd = new Command("db-import", "Import pptx dissect output into NongDb (unified ingestion)") { sliceArg, pptxArg };
        cmd.SetHandler((string dir, string pptx, bool json) =>
        {
            if (!Directory.Exists(dir)) { CliHelpers.WriteError("pptx db-import", ErrorCodes.FileNotFound with { Message = $"Directory not found: {dir}" }, json); return; }
            if (!File.Exists(pptx)) { CliHelpers.WriteError("pptx db-import", ErrorCodes.FileNotFound with { Message = $"File not found: {pptx}" }, json); return; }

            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var result = ctx.IngestSlice(pptx, dir, "pptx", "db-import");

            var shaShort = result.Sha256[..12];
            var dbPath = Path.Combine(Angri450.Nong.NongWorkplace.Cache, "nong.db");

            var o = JsonOutput.Ok("pptx db-import", $"Imported: {result.Blocks} blocks, {result.Images} images", new
            {
                documentId = result.DocumentId, result.FileName, result.Format, sha = shaShort,
                result.Blocks, result.Images,
                result.HasFormat,
                dbFile = dbPath,
                runId = result.RunId
            });
            o.Metrics["blocks"] = result.Blocks; o.Metrics["images"] = result.Images;
            if (json) Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            else Console.WriteLine($"Imported {result.FileName}: {result.Blocks} blocks, {result.Images} images -> nong.db");
        }, sliceArg, pptxArg, jsonOpt);
        return cmd;
    }

    static Command CreateDbList(Option<bool> jsonOpt)
    {
        var cmd = new Command("db-list", "List documents in NongDb");
        cmd.SetHandler((bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var docs = ctx.QueryDocuments();
            var o = JsonOutput.Ok("pptx db-list", $"{docs.Count} documents", new
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

            var o = JsonOutput.Ok("pptx db-blocks", $"{blocks.Count} blocks", new
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

            var o = JsonOutput.Ok("pptx db-images", $"{images.Count} images", new
            {
                count = images.Count,
                items = images.Select(i => new { id = i.Id.ToString(), i.FileName, i.MimeType, i.Width, i.Height, i.Usage, dataSize = i.Data?.Length ?? 0 })
            });
            o.Metrics["images"] = images.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, idArg, jsonOpt);
        return cmd;
    }
}

internal class PptxCreateSpec
{
    public string? Theme { get; set; }
    public List<PptxSlideSpec> Slides { get; set; } = new();
}

internal class PptxSlideSpec
{
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public List<string>? Items { get; set; }
    // New optional fields for V6
    public string? Layout { get; set; }
    public string[][]? Table { get; set; }
    public PptxChartSpec? Chart { get; set; }
    public PptxPictureSpec? Picture { get; set; }
    public string? Notes { get; set; }
}

internal sealed class PptxChartSpec
{
    public string Kind { get; set; } = "bar";
    public Dictionary<string, double>? Data { get; set; }
    public Dictionary<double, double>? PointData { get; set; }
    public string? SeriesName { get; set; }
    public string? Title { get; set; }
}

internal sealed class PptxPictureSpec
{
    public string Path { get; set; } = "";
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? W { get; set; }
    public int? H { get; set; }
    public string? Caption { get; set; }
}
