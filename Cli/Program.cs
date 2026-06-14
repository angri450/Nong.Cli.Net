using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using Nong.Cli.Commands;
using Nong.Cli.Common;

namespace Nong.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"nong v{CliVersion.Current}");
            return 0;
        }

        if (TryDispatchExternal(args, out var externalExitCode))
            return externalExitCode;

        var root = new RootCommand("nong — Nong.NET CLI toolkit for document generation and inspection.");

        // === Global options ===
        var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
        var verboseOpt = new Option<bool>("--verbose", () => false, "Verbose output");
        root.AddGlobalOption(jsonOpt);
        root.AddGlobalOption(verboseOpt);

        // === nong --version ===
        root.SetHandler(() =>
        {
            Console.WriteLine($"nong v{CliVersion.Current}");
        });

        // === nong commands --json / --format openai-tools ===
        var allOpt = new Option<bool>("--all", () => false, "Include stub commands");
        var formatOpt = new Option<string>("--format", () => "default", "Output format: default, json, openai-tools");
        var commandsCmd = new Command("commands", "List available commands") { allOpt, formatOpt };
        commandsCmd.SetHandler((bool json, bool all, string format) =>
        {
            var manifest = Manifest.All();
            var filtered = all ? manifest : manifest.Where(c => c.Status == "implemented").ToList();

            if (string.Equals(format, "openai-tools", StringComparison.OrdinalIgnoreCase))
            {
                var tools = filtered.Select(OpenAiToolSchema.FromCommand).ToList();
                Console.WriteLine(JsonSerializer.Serialize(tools, CliHelpers.JsonOpts));
                return;
            }

            if (json)
            {
                var output = JsonOutput.Ok("commands", $"{filtered.Count} commands available", filtered);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (var c in filtered)
                {
                    var aliasStr = c.Aliases.Length > 0 ? $" (alias: {string.Join(", ", c.Aliases)})" : "";
                    var stubTag = c.Status == "stub" ? " [stub]" : "";
                    Console.WriteLine($"{c.Name,-35} {c.Description}{aliasStr}{stubTag}");
                }
            }
        }, jsonOpt, allOpt, formatOpt);
        root.AddCommand(commandsCmd);

        // === Real command groups ===
        root.AddCommand(WordCommands.Create(jsonOpt));
        root.AddCommand(InspectCommands.Create(jsonOpt));
        root.AddCommand(ExcelCommands.Create(jsonOpt));
        root.AddCommand(GenreCommands.Create(jsonOpt));
        root.AddCommand(IconsCommands.Create(jsonOpt));
        root.AddCommand(SkillCommands.Create(jsonOpt));
        root.AddCommand(LitCommands.Create(jsonOpt));
        root.AddCommand(SliceCommands.Create(jsonOpt));
        root.AddCommand(ProgressCommands.Create(jsonOpt));
        // Heavy modules dispatched to external tools:
        root.AddCommand(CreateExternalGroup("chart"));
        root.AddCommand(CreateExternalGroup("diagram"));
        root.AddCommand(CreateExternalGroup("ocr"));
        root.AddCommand(CreateExternalGroup("pdf"));
        root.AddCommand(CreateExternalGroup("pptx"));

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();

        if (args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            var parseResult = builder.Parse(args);
            if (parseResult.Errors.Count > 0)
            {
                CliHelpers.WriteError(InferCommandName(args),
                    ErrorCodes.MissingArgument with
                    {
                        Message = string.Join(" ", parseResult.Errors.Select(e => e.Message))
                    },
                    json: true);
                return 1;
            }
        }

        var code = await builder.InvokeAsync(args);
        return Environment.ExitCode != 0 ? Environment.ExitCode : code;
    }

    static string InferCommandName(string[] args)
    {
        var tokens = args
            .Where(a => !string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (tokens.Length == 0) return "nong";

        if (tokens[0] == "commands") return "commands";
        if (tokens.Length == 1) return tokens[0];

        if (tokens[0] == "word" && tokens.Length >= 2)
        {
            if (tokens[1] == "add" && tokens.Length >= 3)
                return $"word add {tokens[2]}";
            return $"word {tokens[1]}";
        }

        return $"{tokens[0]} {tokens[1]}";
    }

    static Command CreateStubGroup(string name, string description, Option<bool> jsonOpt,
        params (string name, string desc)[] subs)
    {
        var cmd = new Command(name, description);
        foreach (var (n, d) in subs)
        {
            var sub = new Command(n, d);
            CliHelpers.SetNotImplemented(sub, d, jsonOpt);
            cmd.AddCommand(sub);
        }
        return cmd;
    }

    static bool TryDispatchExternal(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
            return false;

        var normalized = args[0].ToLowerInvariant();
        if (!ExternalTools.TryGetValue(normalized, out var tool))
            return false;

        exitCode = CliHelpers.RunTool(tool.ToolName, tool.PackageId, args.Skip(1).ToArray());
        return true;
    }

    static Command CreateExternalGroup(string name)
    {
        var tool = ExternalTools[name];

        var cmd = new Command(name, $"External: use {tool.ToolName} from {tool.PackageId} (auto-installs on first use)");
        cmd.SetHandler(() =>
        {
            Environment.ExitCode = CliHelpers.RunTool(tool.ToolName, tool.PackageId, Array.Empty<string>());
        });
        return cmd;
    }

    static readonly Dictionary<string, (string ToolName, string PackageId)> ExternalTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chart"] = ("nong-chart", ToolPackages.Chart),
        ["diagram"] = ("nong-diagram", ToolPackages.Diagram),
        ["ocr"] = ("nong-ocr", ToolPackages.Ocr),
        ["pdf"] = ("nong-pdf", ToolPackages.Pdf),
        ["pptx"] = ("nong-pptx", ToolPackages.Pptx),
    };
}
