using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>V12: New format export commands (EPUB, HTML, LaTeX, ODF).</summary>
public static class ExportCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("export", "Export documents to alternative formats (EPUB, HTML, LaTeX, ODF)");
        cmd.AddCommand(CreateEpub(jsonOpt));
        cmd.AddCommand(CreateHtml(jsonOpt));
        return cmd;
    }

    static Command CreateEpub(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .nongmark file");
        var outOpt = new Option<string>("-o", "Output .epub path") { IsRequired = true };
        var cmd = new Command("epub", "Convert document to EPUB (V12)") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export epub", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);
                // V12 scaffold: launch EPUB export pipeline
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".docx")
                {
                    // DOCX → EPUB: extract content, build EPUB container
                    System.IO.File.Copy(file, output, true); // placeholder
                }
                if (json)
                {
                    var o = JsonOutput.Ok("export epub", "EPUB exported (placeholder)",
                        new { output = Path.GetFullPath(output) });
                    o.Artifacts["epub"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"EPUB placeholder → {output}"); }
            }
            catch (Exception ex) { CliHelpers.WriteError("export epub", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateHtml(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .nongmark file");
        var outOpt = new Option<string>("-o", "Output .html path") { IsRequired = true };
        var cmd = new Command("html", "Convert document to HTML (V12)") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            CliHelpers.EnsureParentDir(output);
            // V12 scaffold
            System.IO.File.Copy(file, output, true);
            if (json)
            {
                var o = JsonOutput.Ok("export html", "HTML exported (placeholder)",
                    new { output = Path.GetFullPath(output) });
                o.Artifacts["html"] = output;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            else { Console.WriteLine($"HTML placeholder → {output}"); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }
}
