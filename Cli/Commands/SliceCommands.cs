using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;
using PandocCore;

namespace Nong.Cli.Commands;

public static class SliceCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("slice", "NongPandoc slice package operations");
        cmd.AddCommand(CreateInspect(jsonOpt));
        cmd.AddCommand(CreateBlocks(jsonOpt));
        cmd.AddCommand(CreateBlock(jsonOpt));
        cmd.AddCommand(CreateAssets(jsonOpt));
        return cmd;
    }

    static Command CreateInspect(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("slice-dir", "Path to a NongPandoc slice directory");
        var strictOpt = new Option<bool>("--strict", () => false, "Validate block-level provenance evidence contract");
        var cmd = new Command("inspect", "Inspect a NongPandoc slice package contract") { dirArg };
        cmd.AddOption(strictOpt);

        cmd.SetHandler((string sliceDir, bool strict, bool json) =>
        {
            if (!ValidateSliceDir("slice inspect", sliceDir, json))
                return;

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => NongPandocSlicePackageReader.Read(sliceDir, new NongPandocSliceReadOptions
                {
                    StrictEvidence = strict,
                }));
                var summary = result.Summary;
                if (json)
                {
                    var output = JsonOutput.Ok("slice inspect",
                        $"Slice package: {summary.SchemaVersion}, {summary.Metrics.Blocks} block(s)",
                        summary);
                    output.Artifacts["dir"] = result.Directory;
                    output.Artifacts["manifest"] = result.Artifacts[NongPandocArtifactNames.Manifest];
                    output.Metrics["blocks"] = summary.Metrics.Blocks;
                    output.Metrics["paragraphs"] = summary.Metrics.Paragraphs;
                    output.Metrics["headings"] = summary.Metrics.Headings;
                    output.Metrics["tables"] = summary.Metrics.Tables;
                    output.Metrics["figures"] = summary.Metrics.Figures;
                    output.Metrics["images"] = summary.Metrics.Images;
                    output.Metrics["references"] = summary.Metrics.References;
                    output.Metrics["warnings"] = summary.Metrics.Warnings;
                    output.Metrics["evidenceCheckedBlocks"] = summary.Evidence.CheckedBlocks;
                    output.Meta.DurationMs = elapsed;
                    foreach (var warning in summary.Warnings)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "slice_warning",
                            Severity = "warning",
                            Message = warning,
                        });
                    }
                    foreach (var warning in summary.Evidence.Warnings)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "slice_evidence_warning",
                            Severity = "warning",
                            Message = warning,
                        });
                    }

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Slice: {result.Directory}");
                    Console.WriteLine($"Schema: {summary.SchemaVersion}");
                    Console.WriteLine($"Source: {summary.Source.Format} {summary.Source.Path}");
                    Console.WriteLine($"Evidence: {(summary.Evidence.Valid ? "valid" : "invalid")} ({summary.Evidence.CheckedBlocks} block(s))");
                    Console.WriteLine("AI read order:");
                    foreach (var item in summary.AiReadOrder)
                        Console.WriteLine($"- {item}");
                }
            }
            catch (NongPandocSliceReadException ex)
            {
                CliHelpers.WriteError("slice inspect",
                    ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError("slice inspect",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid slice JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("slice inspect",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, strictOpt, jsonOpt);

        return cmd;
    }

    static Command CreateBlocks(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("slice-dir", "Path to a NongPandoc slice directory");
        var cmd = new Command("blocks", "List content blocks from a NongPandoc slice") { dirArg };

        cmd.SetHandler((string sliceDir, bool json) =>
        {
            if (!ValidateSliceDir("slice blocks", sliceDir, json))
                return;

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => NongPandocSlicePackageReader.Read(sliceDir));
                var blocks = NongPandocSliceQuery.Blocks(result);
                if (json)
                {
                    var output = JsonOutput.Ok("slice blocks", $"{blocks.Count} block(s)", blocks);
                    output.Metrics["blocks"] = blocks.Count;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var block in blocks)
                        Console.WriteLine(block?.ToJsonString());
                }
            }
            catch (NongPandocSliceReadException ex)
            {
                CliHelpers.WriteError("slice blocks",
                    ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError("slice blocks",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid slice JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("slice blocks",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, jsonOpt);

        return cmd;
    }

    static Command CreateBlock(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("slice-dir", "Path to a NongPandoc slice directory");
        var idArg = new Argument<string>("block-id", "Block ID to read, such as p0001");
        var cmd = new Command("block", "Read one block with content, structure, format, diagnostics, and asset evidence") { dirArg, idArg };

        cmd.SetHandler((string sliceDir, string blockId, bool json) =>
        {
            if (!ValidateSliceDir("slice block", sliceDir, json))
                return;
            if (string.IsNullOrWhiteSpace(blockId))
            {
                CliHelpers.WriteError("slice block",
                    ErrorCodes.MissingArgument with { Message = "Block ID is required." }, json);
                return;
            }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => NongPandocSlicePackageReader.Read(sliceDir));
                var view = NongPandocSliceQuery.Block(result, blockId);
                if (view.Content == null && view.Structure == null)
                {
                    CliHelpers.WriteError("slice block",
                        ErrorCodes.ReadFailed with { Message = $"Block not found: {blockId}" }, json);
                    return;
                }

                if (json)
                {
                    var output = JsonOutput.Ok("slice block", $"Block {blockId}", view);
                    output.Metrics["assets"] = view.Assets.Count;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(view, CliHelpers.JsonOpts));
                }
            }
            catch (NongPandocSliceReadException ex)
            {
                CliHelpers.WriteError("slice block",
                    ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError("slice block",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid slice JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("slice block",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, idArg, jsonOpt);

        return cmd;
    }

    static Command CreateAssets(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("slice-dir", "Path to a NongPandoc slice directory");
        var cmd = new Command("assets", "List assets from a NongPandoc slice") { dirArg };

        cmd.SetHandler((string sliceDir, bool json) =>
        {
            if (!ValidateSliceDir("slice assets", sliceDir, json))
                return;

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => NongPandocSlicePackageReader.Read(sliceDir));
                var assets = NongPandocSliceQuery.Assets(result);
                if (json)
                {
                    var output = JsonOutput.Ok("slice assets", $"{assets.Count} asset(s)", assets);
                    output.Metrics["assets"] = assets.Count;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var asset in assets)
                        Console.WriteLine(asset?.ToJsonString());
                }
            }
            catch (NongPandocSliceReadException ex)
            {
                CliHelpers.WriteError("slice assets",
                    ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError("slice assets",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid slice JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("slice assets",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, jsonOpt);

        return cmd;
    }

    static bool ValidateSliceDir(string command, string sliceDir, bool json)
    {
        if (string.IsNullOrWhiteSpace(sliceDir))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.MissingArgument with { Message = "Slice directory is required." }, json);
            return false;
        }

        if (!Directory.Exists(sliceDir))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.FileNotFound with { Message = $"Slice directory not found: {sliceDir}" }, json);
            return false;
        }

        if (!File.Exists(Path.Combine(sliceDir, NongPandocArtifactNames.Manifest)))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ReadFailed with { Message = $"Slice manifest was not found: {Path.Combine(sliceDir, NongPandocArtifactNames.Manifest)}" }, json);
            return false;
        }

        return true;
    }
}
