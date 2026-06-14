using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using DiagramCore;
using DiagramCore.Models;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class DiagramRenderWorkerCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("__render-worker", "Internal diagram render worker") { IsHidden = true };

        var kindArg = new Argument<string>("kind");
        var actionArg = new Argument<string>("action");
        var fileOpt = new Option<string>("--file", () => "", "Input file");
        var outputOpt = new Option<string>("--output", () => "", "Output file");

        cmd.AddArgument(kindArg);
        cmd.AddArgument(actionArg);
        cmd.AddOption(fileOpt);
        cmd.AddOption(outputOpt);

        cmd.SetHandler((InvocationContext context) =>
        {
            var kind = context.ParseResult.GetValueForArgument(kindArg);
            var action = context.ParseResult.GetValueForArgument(actionArg);
            var file = context.ParseResult.GetValueForOption(fileOpt) ?? "";
            var output = context.ParseResult.GetValueForOption(outputOpt) ?? "";
            var json = context.ParseResult.GetValueForOption(jsonOpt);

            if (!string.Equals(kind, "diagram", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError("__render-worker",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown diagram worker kind: {kind}" },
                    json);
                return;
            }

            switch (action)
            {
                case "flowchart":
                    RenderFlowchart(file, output, json);
                    break;
                case "network":
                    RenderNetwork(file, output, json);
                    break;
                case "tree":
                    RenderTree(file, output, json);
                    break;
                default:
                    CliHelpers.WriteError("__render-worker",
                        ErrorCodes.ValidationFailed with { Message = $"Unknown diagram worker action: {action}" },
                        json);
                    break;
            }
        });

        return cmd;
    }

    static bool ValidateWorkerInput(string command, string file, string output, bool json)
    {
        var err = CliHelpers.ValidateTextFile(file);
        if (err != null)
        {
            CliHelpers.WriteError(command, err, json);
            return false;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.MissingArgument with { Message = "Output path is required." },
                json);
            return false;
        }

        CliHelpers.EnsureParentDir(output);
        return true;
    }

    static void RenderFlowchart(string file, string output, bool json)
    {
        const string command = "diagram flowchart";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() => DiagramBuilder.FromDsl(File.ReadAllText(file), output));
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderNetwork(string file, string output, bool json)
    {
        const string command = "diagram network";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() =>
            {
                var graph = JsonSerializer.Deserialize<Graph>(File.ReadAllText(file),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (graph == null)
                    throw new InvalidOperationException("Network graph spec cannot be empty.");
                DiagramBuilder.NetworkGraph(graph, output);
            });
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (JsonException ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {ex.Message}" },
                json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderTree(string file, string output, bool json)
    {
        const string command = "diagram tree";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() =>
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".json")
                {
                    DiagramBuilder.FromDsl(File.ReadAllText(file), output);
                }
                else
                {
                    var tree = NewickTree.Parse(File.ReadAllText(file).Trim());
                    DiagramBuilder.PhylogeneticTree(tree, output);
                }
            });
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void WritePngSuccess(string command, string summary, object? data, string output, long elapsed, bool json)
    {
        var error = CliHelpers.CheckArtifact(output, "PNG");
        if (error != null)
        {
            CliHelpers.WriteError(command, error, json);
            return;
        }

        if (json)
        {
            var result = JsonOutput.Ok(command, summary, data);
            result.Artifacts["png"] = Path.GetFullPath(output);
            result.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(result, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine(summary);
        }
    }
}
