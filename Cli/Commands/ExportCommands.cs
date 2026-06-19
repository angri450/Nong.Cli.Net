using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class ExportCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("export", "Export documents to alternative formats");
        cmd.AddCommand(CreateEpub(jsonOpt));
        cmd.AddCommand(CreateHtml(jsonOpt));
        cmd.AddCommand(CreateLatex(jsonOpt));
        return cmd;
    }

    // ══════ EPUB ══════

    static Command CreateEpub(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .epub path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Book title");
        var authorOpt = new Option<string>("--author", () => "Nong", "Book author");
        var cmd = new Command("epub", "Convert DOCX to EPUB") { fileArg, outOpt, titleOpt, authorOpt };

        cmd.SetHandler((string file, string output, string title, string author, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export epub", err, json); return; }
            try
            {
                CliHelpers.EnsureParentDir(output);
                var chapters = ExtractDocxContent(file);
                if (chapters.Count == 0)
                { CliHelpers.WriteError("export epub", ErrorCodes.ValidationFailed with { Message = "No content." }, json); return; }
                if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileNameWithoutExtension(file);
                BuildEpub(output, title, author, chapters);
                var info = new FileInfo(output);
                if (json)
                {
                    var o = JsonOutput.Ok("export epub", $"EPUB: {chapters.Count} chapter(s), {info.Length} bytes",
                        new { output = Path.GetFullPath(output), chapters = chapters.Count, bytes = info.Length });
                    o.Artifacts["epub"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"EPUB: {chapters.Count} chapter(s) -> {output} ({info.Length} bytes)");
            }
            catch (Exception ex) { CliHelpers.WriteError("export epub", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, authorOpt, jsonOpt);
        return cmd;
    }

    static void BuildEpub(string path, string title, string author, List<Chapter> chapters)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var w = new StreamWriter(mime.Open(), Encoding.ASCII)) w.Write("application/epub+zip");

        var container = "<?xml version=\"1.0\"?>\n<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n  <rootfiles>\n    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/>\n  </rootfiles>\n</container>";
        WriteEntry(zip, "META-INF/container.xml", container);

        var sbMf = new StringBuilder();
        var sbSp = new StringBuilder();
        var sbNav = new StringBuilder();
        int po = 1;

        for (int i = 0; i < chapters.Count; i++)
        {
            var ch = chapters[i];
            var cf = "chapter" + (i + 1) + ".xhtml";
            var ci = "chapter" + (i + 1);
            WriteEntry(zip, "OEBPS/" + cf, BuildChapterXhtml(ch));
            sbMf.Append("    <item id=\"" + ci + "\" href=\"" + cf + "\" media-type=\"application/xhtml+xml\"/>\n");
            sbSp.Append("    <itemref idref=\"" + ci + "\"/>\n");
            sbNav.Append("    <navPoint id=\"nav" + (i + 1) + "\" playOrder=\"" + po + "\">\n      <navLabel><text>" + EscapeXml(ch.Title) + "</text></navLabel>\n      <content src=\"" + cf + "\"/>\n    </navPoint>\n");
            po++;
        }

        var opf = "<?xml version=\"1.0\"?>\n<package version=\"2.0\" unique-identifier=\"bookid\" xmlns=\"http://www.idpf.org/2007/opf\">\n  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n    <dc:title>" + EscapeXml(title) + "</dc:title>\n    <dc:creator>" + EscapeXml(author) + "</dc:creator>\n    <dc:language>zh-CN</dc:language>\n    <dc:identifier id=\"bookid\">urn:uuid:" + Guid.NewGuid() + "</dc:identifier>\n  </metadata>\n  <manifest>\n    <item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>\n" + sbMf + "  </manifest>\n  <spine toc=\"ncx\">\n" + sbSp + "  </spine>\n</package>";
        WriteEntry(zip, "OEBPS/content.opf", opf);

        var ncx = "<?xml version=\"1.0\"?>\n<ncx version=\"2005-1\" xmlns=\"http://www.daisy.org/z3986/2005/ncx/\">\n  <head><meta name=\"dtb:uid\" content=\"urn:uuid:" + Guid.NewGuid() + "\"/></head>\n  <docTitle><text>" + EscapeXml(title) + "</text></docTitle>\n  <navMap>\n" + sbNav + "  </navMap>\n</ncx>";
        WriteEntry(zip, "OEBPS/toc.ncx", ncx);
    }

    static string BuildChapterXhtml(Chapter ch)
    {
        var body = "  <h2>" + EscapeXml(ch.Title) + "</h2>\n";
        foreach (var p in ch.Paragraphs)
        {
            var cls = p.IsHeading ? "heading" : "para";
            body += "  <p class=\"" + cls + "\">" + EscapeXml(p.Text) + "</p>\n";
        }
        return "<?xml version=\"1.0\"?>\n<!DOCTYPE html>\n<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"zh-CN\">\n<head>\n  <title>" + EscapeXml(ch.Title) + "</title>\n  <style>\n    body { font-family: \"Microsoft YaHei\", \"SimSun\", serif; margin: 1em 2em; line-height: 1.6; }\n    h2 { font-size: 1.4em; margin-top: 1.2em; }\n    .heading { font-weight: bold; font-size: 1.1em; margin-top: 0.8em; }\n    .para { text-indent: 2em; margin: 0.4em 0; }\n  </style>\n</head>\n<body>\n" + body + "</body>\n</html>";
    }

    // ══════ HTML ══════

    static Command CreateHtml(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .html path") { IsRequired = true };
        var cmd = new Command("html", "Convert DOCX to HTML") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            CliHelpers.EnsureParentDir(output);
            try
            {
                var chapters = ExtractDocxContent(file);
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
                sb.AppendLine("<style>body{font-family:\"Microsoft YaHei\",SimSun,serif;max-width:800px;margin:auto;line-height:1.6}h2{font-size:1.3em;margin-top:1.5em}.heading{font-weight:bold;font-size:1.1em}.para{text-indent:2em;margin:.4em 0}</style>");
                sb.AppendLine("<title>" + EscapeXml(Path.GetFileNameWithoutExtension(file)) + "</title></head><body>");
                foreach (var ch in chapters)
                {
                    sb.AppendLine("<h2>" + EscapeXml(ch.Title) + "</h2>");
                    foreach (var p in ch.Paragraphs)
                        sb.AppendLine("<p class=\"" + (p.IsHeading ? "heading" : "para") + "\">" + EscapeXml(p.Text) + "</p>");
                }
                sb.AppendLine("</body></html>");
                File.WriteAllText(output, sb.ToString(), Encoding.UTF8);
                if (json)
                {
                    var o = JsonOutput.Ok("export html", "HTML exported", new { output = Path.GetFullPath(output) });
                    o.Artifacts["html"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine("HTML -> " + output);
            }
            catch (Exception ex) { CliHelpers.WriteError("export html", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ══════ LaTeX ══════

    static Command CreateLatex(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .tex path") { IsRequired = true };
        var cmd = new Command("latex", "Convert DOCX to LaTeX") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            CliHelpers.EnsureParentDir(output);
            try
            {
                var chapters = ExtractDocxContent(file);
                var sb = new StringBuilder();
                sb.AppendLine("\\documentclass[12pt,a4paper]{article}");
                sb.AppendLine("\\usepackage[UTF8]{ctex}");
                sb.AppendLine("\\usepackage{geometry}\\geometry{margin=2.5cm}");
                sb.AppendLine("\\begin{document}");
                sb.AppendLine("\\title{" + EscapeLatex(Path.GetFileNameWithoutExtension(file)) + "}");
                sb.AppendLine("\\maketitle");
                foreach (var ch in chapters)
                {
                    sb.AppendLine("\\section{" + EscapeLatex(ch.Title) + "}");
                    foreach (var p in ch.Paragraphs)
                        sb.AppendLine(EscapeLatex(p.Text) + "\\\\");
                }
                sb.AppendLine("\\end{document}");
                File.WriteAllText(output, sb.ToString(), Encoding.UTF8);
                if (json)
                {
                    var o = JsonOutput.Ok("export latex", "LaTeX exported", new { output = Path.GetFullPath(output) });
                    o.Artifacts["tex"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine("LaTeX -> " + output);
            }
            catch (Exception ex) { CliHelpers.WriteError("export latex", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ══════ shared ══════

    sealed record Chapter(string Title, List<Para> Paragraphs);
    sealed record Para(string Text, bool IsHeading);

    static List<Chapter> ExtractDocxContent(string file)
    {
        var chapters = new List<Chapter>();
        using var doc = WordprocessingDocument.Open(file, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return chapters;
        var current = new Chapter("Chapter 1", new List<Para>());
        var first = true;
        foreach (var para in body.Elements<Paragraph>())
        {
            var styleId = para.Elements<ParagraphProperties>().FirstOrDefault()?.Elements<ParagraphStyleId>().FirstOrDefault()?.Val?.Value ?? "";
            var isHeading = styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || styleId.StartsWith("heading", StringComparison.OrdinalIgnoreCase) || styleId.StartsWith("Title", StringComparison.OrdinalIgnoreCase);
            var text = para.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (isHeading && (styleId.Contains("1") || styleId.Equals("Title", StringComparison.OrdinalIgnoreCase)))
            {
                if (!first || current.Paragraphs.Count > 0) { chapters.Add(current); current = new Chapter(text, new List<Para>()); }
                else current = current with { Title = text };
            }
            current.Paragraphs.Add(new Para(text, isHeading));
            first = false;
        }
        if (current.Paragraphs.Count > 0) chapters.Add(current);
        return chapters;
    }

    static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
        w.Write(content);
    }

    static string EscapeXml(string s) => System.Net.WebUtility.HtmlEncode(s);
    static string EscapeLatex(string s) => s.Replace("\\", "\\textbackslash ").Replace("&", "\\&").Replace("%", "\\%").Replace("#", "\\#").Replace("_", "\\_").Replace("{", "\\{").Replace("}", "\\}").Replace("^", "\\^{}").Replace("~", "\\~{}");
}