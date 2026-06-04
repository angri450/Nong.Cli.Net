using System.CommandLine;
using System.Text.Json;
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
                            texts = s.Texts
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
}
