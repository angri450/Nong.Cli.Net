using System.CommandLine;
using DiagramCore;
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
                NativeRenderWorkerHost.Run("diagram flowchart", json,
                    new[] { "diagram", "flowchart", "--file", spec, "--output", output });
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
                NativeRenderWorkerHost.Run("diagram network", json,
                    new[] { "diagram", "network", "--file", spec, "--output", output });
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
                NativeRenderWorkerHost.Run("diagram tree", json,
                    new[] { "diagram", "tree", "--file", spec, "--output", output });
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
