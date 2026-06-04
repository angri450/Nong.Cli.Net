using System.CommandLine;
using System.Text.Json;
using Bioicons;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>Icons command group: list, search (phase 8).</summary>
public static class IconsCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("icons", "Bioicons operations");

        cmd.AddCommand(CreateList(jsonOpt));
        cmd.AddCommand(CreateSearch(jsonOpt));

        return cmd;
    }

    static Command CreateList(Option<bool> jsonOpt)
    {
        var cmd = new Command("list", "List all Bioicons");
        cmd.SetHandler((bool json) =>
        {
            var cats = IconProvider.GetCategories();
            var all = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var cat in cats) all[cat] = IconProvider.GetIcons(cat);

            if (json)
            {
                var oj = JsonOutput.Ok("icons list", $"{cats.Count} categories, {all.Values.Sum(v => v.Count)} icons",
                    new { categories = all });
                Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (var (cat, icons) in all)
                {
                    Console.WriteLine($"{cat} ({icons.Count}): {string.Join(", ", icons)}");
                }
            }

        }, jsonOpt);
        return cmd;
    }

    static Command CreateSearch(Option<bool> jsonOpt)
    {
        var queryArg = new Argument<string>("query", "Search keyword");
        var cmd = new Command("search", "Search Bioicons") { queryArg };
        cmd.SetHandler((string query, bool json) =>
        {
            var cats = IconProvider.GetCategories();
            var results = new List<string>();
            foreach (var cat in cats)
            {
                foreach (var icon in IconProvider.GetIcons(cat))
                {
                    if (icon.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        cat.Contains(query, StringComparison.OrdinalIgnoreCase))
                        results.Add($"{cat}/{icon}");
                }
            }

            if (json)
            {
                var oj = JsonOutput.Ok("icons search", $"{results.Count} results for '{query}'",
                    new { query, results });
                Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (var r in results) Console.WriteLine(r);
            }

        }, queryArg, jsonOpt);
        return cmd;
    }
}
