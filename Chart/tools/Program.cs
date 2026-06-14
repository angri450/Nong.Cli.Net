using System.CommandLine;
using Nong.Cli.Commands;

var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
var root = new RootCommand("nong-chart — statistical charts and analysis");
root.AddGlobalOption(jsonOpt);
root.AddCommand(ChartCommands.Create(jsonOpt));
root.AddCommand(ChartRenderWorkerCommands.Create(jsonOpt));

await root.InvokeAsync(NormalizeArgs(args, "chart"));

static string[] NormalizeArgs(string[] args, string group)
{
    if (args.Length > 0 && args[0].StartsWith("__", StringComparison.Ordinal))
        return args;

    if (args.Length > 0 && string.Equals(args[0], group, StringComparison.OrdinalIgnoreCase))
        return args;

    return new[] { group }.Concat(args).ToArray();
}
