using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;
using Nong.Genre;

namespace Nong.Cli.Commands;

/// <summary>Genre command group: list, show (phase 8).</summary>
public static class GenreCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("genre", "Format template library");

        cmd.AddCommand(CreateList(jsonOpt));
        cmd.AddCommand(CreateShow(jsonOpt));

        return cmd;
    }

    static Command CreateList(Option<bool> jsonOpt)
    {
        var cmd = new Command("list", "List format templates");
        cmd.SetHandler((bool json) =>
        {
            try
            {
                var names = GenreTemplate.List();
                if (json)
                {
                    var oj = JsonOutput.Ok("genre list", $"{names.Length} templates", new { templates = names });
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var n in names) Console.WriteLine(n);
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("genre list",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, jsonOpt);
        return cmd;
    }

    static Command CreateShow(Option<bool> jsonOpt)
    {
        var nameArg = new Argument<string>("name", "Template name");
        var cmd = new Command("show", "Show template content") { nameArg };
        cmd.SetHandler((string name, bool json) =>
        {
            try
            {
                var content = GenreTemplate.Load(name);
                if (json)
                {
                    var oj = JsonOutput.Ok("genre show", $"Template: {name}",
                        new { name, content = JsonSerializer.Deserialize<object>(content) });
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine(content);
            }
            catch (FileNotFoundException)
            {
                CliHelpers.WriteError("genre show",
                    ErrorCodes.FileNotFound with { Message = $"Template not found: {name}. Available: {string.Join(", ", GenreTemplate.List())}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("genre show",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, nameArg, jsonOpt);
        return cmd;
    }
}
