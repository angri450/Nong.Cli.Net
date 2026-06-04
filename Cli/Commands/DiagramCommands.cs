using System.CommandLine;
using System.Text.Json;
using DiagramCore;
using DiagramCore.Models;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>Diagram command group: flowchart, network, tree.</summary>
public static class DiagramCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("diagram", "Scientific diagrams");

        cmd.AddCommand(CreateFlowchart(jsonOpt));
        cmd.AddCommand(CreateNetwork(jsonOpt));
        cmd.AddCommand(CreateTree(jsonOpt));

        return cmd;
    }

    static Command CreateFlowchart(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to flowchart spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("flowchart", "Flowchart from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError("diagram flowchart", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var jsonText = File.ReadAllText(spec);
                    DiagramBuilder.FromDsl(jsonText, output);
                });

                if (json)
                {
                    var oj = JsonOutput.Ok("diagram flowchart", $"Saved: {output}");
                    oj.Artifacts["png"] = Path.GetFullPath(output);
                    oj.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"OK: {Path.GetFullPath(output)}");
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("diagram flowchart", ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }

        }, specArg, outOpt, jsonOpt);

        return cmd;
    }

    static Command CreateNetwork(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to network graph spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("network", "Network graph from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError("diagram network", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var graph = JsonSerializer.Deserialize<Graph>(File.ReadAllText(spec),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    DiagramBuilder.NetworkGraph(graph!, output);
                });

                if (json)
                {
                    var oj = JsonOutput.Ok("diagram network", $"Saved: {output}");
                    oj.Artifacts["png"] = Path.GetFullPath(output);
                    oj.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"OK: {Path.GetFullPath(output)}");
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("diagram network", ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }

        }, specArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== diagram tree =====

    static Command CreateTree(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to Newick (.nwk/.txt) or JSON spec");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("tree", "Phylogenetic tree from Newick or JSON") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError("diagram tree", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);

                var elapsed = CliHelpers.Time(() =>
                {
                    var ext = Path.GetExtension(spec).ToLowerInvariant();

                    if (ext == ".json")
                    {
                        // Parse JSON: {"newick":"...", "title":"..."}
                        var jsonText = File.ReadAllText(spec);
                        DiagramBuilder.FromDsl(jsonText, output);
                    }
                    else
                    {
                        // Read as raw Newick string
                        var newick = File.ReadAllText(spec).Trim();
                        var tree = NewickTree.Parse(newick);
                        DiagramBuilder.PhylogeneticTree(tree, output);
                    }
                });

                var aerr = CliHelpers.CheckArtifact(output, "PNG");
                if (aerr != null) { CliHelpers.WriteError("diagram tree", aerr, json); return; }

                if (json)
                {
                    var oj = JsonOutput.Ok("diagram tree", $"Saved: {output}");
                    oj.Artifacts["png"] = Path.GetFullPath(output);
                    oj.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"OK: {Path.GetFullPath(output)}");
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("diagram tree", ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }

        }, specArg, outOpt, jsonOpt);

        return cmd;
    }
}
