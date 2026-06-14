using System.CommandLine;
using Nong.Cli.Commands;

var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
var root = new RootCommand("nong-pptx — PowerPoint operations");
root.AddGlobalOption(jsonOpt);
root.AddCommand(PptxCommands.Create(jsonOpt));

await root.InvokeAsync(NormalizeArgs(args, "pptx"));

static string[] NormalizeArgs(string[] args, string group)
{
    if (args.Length > 0 && string.Equals(args[0], group, StringComparison.OrdinalIgnoreCase))
        return args;

    return new[] { group }.Concat(args).ToArray();
}
