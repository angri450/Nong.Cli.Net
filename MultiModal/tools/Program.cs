using System.CommandLine;
using Nong.Cli.Commands;

var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
var root = new RootCommand("nong-ocr — OCR operations");
root.AddGlobalOption(jsonOpt);
root.AddCommand(OcrCommands.Create(jsonOpt));

await root.InvokeAsync(NormalizeArgs(args, "ocr"));

static string[] NormalizeArgs(string[] args, string group)
{
    if (args.Length > 0 && string.Equals(args[0], group, StringComparison.OrdinalIgnoreCase))
        return args;

    return new[] { group }.Concat(args).ToArray();
}
