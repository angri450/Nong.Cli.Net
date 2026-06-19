using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nong.Cli.Common;

namespace Nong.Cli;

public static class MarkdownCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("markdown", "Markdown utilities");
        cmd.AddCommand(CreateMdToNongmark(jsonOpt));
        cmd.AddCommand(CreateNongmarkToMd(jsonOpt));
        return cmd;
    }

    static Command CreateMdToNongmark(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .md file");
        var outOpt = new Option<string>("-o", "Output .nongmark path") { IsRequired = true };
        var cmd = new Command("to-nongmark", "Convert Markdown (GFM) to NongMark") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("md to-nongmark", err, json); return; }
            try
            {
                var md = File.ReadAllText(file);
                var nongmark = ConvertMarkdownToNongMark(md);

                CliHelpers.EnsureParentDir(output);
                File.WriteAllText(output, nongmark, new UTF8Encoding(false));

                if (json)
                {
                    var o = JsonOutput.Ok("md to-nongmark", $"Converted to NongMark: {nongmark.Length} chars",
                        new { output = Path.GetFullPath(output), chars = nongmark.Length });
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"Markdown → NongMark: {output} ({nongmark.Length} chars)");
            }
            catch (Exception ex) { CliHelpers.WriteError("md to-nongmark", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateNongmarkToMd(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .nongmark file");
        var outOpt = new Option<string>("-o", "Output .md path") { IsRequired = true };
        var cmd = new Command("to-md", "Convert NongMark to Markdown") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("md to-md", err, json); return; }
            try
            {
                var nmk = File.ReadAllText(file);
                var md = ConvertNongMarkToMarkdown(nmk);

                CliHelpers.EnsureParentDir(output);
                File.WriteAllText(output, md, new UTF8Encoding(false));

                if (json)
                {
                    var o = JsonOutput.Ok("md to-md", $"Converted to Markdown: {md.Length} chars",
                        new { output = Path.GetFullPath(output), chars = md.Length });
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"NongMark → Markdown: {output} ({md.Length} chars)");
            }
            catch (Exception ex) { CliHelpers.WriteError("md to-md", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ── GFM → NongMark converter ──

    static string ConvertMarkdownToNongMark(string md)
    {
        var sb = new StringBuilder();
        var lines = md.Replace("\r\n", "\n").Split('\n');
        bool inCodeBlock = false;
        bool inList = false;
        string? langTag = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Fenced code block
            if (line.StartsWith("```"))
            {
                if (inCodeBlock) { sb.AppendLine(":::"); inCodeBlock = false; langTag = null; }
                else { langTag = line[3..].Trim(); sb.AppendLine($"::: code{(langTag.Length > 0 ? " " + langTag : "")}"); inCodeBlock = true; }
                continue;
            }
            if (inCodeBlock) { sb.AppendLine(line); continue; }

            // Empty line — close list if open
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList) { sb.AppendLine(); inList = false; }
                sb.AppendLine();
                continue;
            }

            // ATX headings
            var hMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)");
            if (hMatch.Success)
            {
                if (inList) { inList = false; }
                sb.AppendLine($"{hMatch.Groups[1].Value} {hMatch.Groups[2].Value}");
                continue;
            }

            // Unordered list
            if (Regex.IsMatch(line, @"^[\-\*\+]\s"))
            {
                inList = true;
                sb.AppendLine(line);
                continue;
            }

            // Ordered list
            if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                inList = true;
                sb.AppendLine(line);
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                if (inList) { inList = false; }
                sb.AppendLine(line);
                continue;
            }

            // Horizontal rule
            if (Regex.IsMatch(line, @"^[\-\*_]{3,}\s*$"))
            {
                if (inList) { inList = false; }
                sb.AppendLine("---");
                continue;
            }

            // Normal paragraph
            if (inList) { inList = false; sb.AppendLine(); }
            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    // ── NongMark → Markdown converter (mostly pass-through) ──

    static string ConvertNongMarkToMarkdown(string nmk)
    {
        var sb = new StringBuilder();
        var lines = nmk.Replace("\r\n", "\n").Split('\n');
        bool inFenced = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("::: code"))
            {
                inFenced = true;
                var lang = line.Length > 8 ? line[9..].Trim() : "";
                sb.AppendLine($"```{lang}");
                continue;
            }
            if (inFenced && line.Trim() == ":::")
            {
                inFenced = false;
                sb.AppendLine("```");
                continue;
            }
            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd() + "\n";
    }
}
