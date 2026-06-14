using System.CommandLine;
using Nong.Cli.Commands;

var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
var root = new RootCommand("nong-diagram — scientific diagrams");
root.AddGlobalOption(jsonOpt);
root.AddCommand(DiagramCommands.Create(jsonOpt));
root.AddCommand(DiagramRenderWorkerCommands.Create(jsonOpt));

await root.InvokeAsync(NormalizeArgs(args, "diagram"));

static string[] NormalizeArgs(string[] args, string group)
{
    if (args.Length > 0 && args[0].StartsWith("__", StringComparison.Ordinal))
        return args;

    if (args.Length > 0 && string.Equals(args[0], group, StringComparison.OrdinalIgnoreCase))
        return args;

    return new[] { group }.Concat(args).ToArray();
}
