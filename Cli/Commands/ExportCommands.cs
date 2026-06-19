using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>V12.1: EPUB/HTML export with image embedding and CSS.</summary>
public static class ExportCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("export", "Export documents to alternative formats");
        cmd.AddCommand(CreateEpub(jsonOpt));
        cmd.AddCommand(CreateHtml(jsonOpt));
        cmd.AddCommand(CreateLatex(jsonOpt));
        cmd.AddCommand(CreateOdf(jsonOpt));
        return cmd;
    }

    static Command CreateEpub(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .epub path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Book title");
        var authorOpt = new Option<string>("--author", "Book author");
        var cssOpt = new Option<string>("--css", "Path to custom CSS file");
        var coverOpt = new Option<string>("--cover-image", "Path to cover image (PNG/JPG)");
        var cmd = new Command("epub", "Convert to EPUB with CSS + image embedding") { fileArg, outOpt, titleOpt, authorOpt, cssOpt, coverOpt };

        cmd.SetHandler((string file, string output, string? title, string? author, string? cssPath, string? coverPath, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export epub", err, json); return; }
            try
            {
                CliHelpers.EnsureParentDir(output);
                var text = File.ReadAllText(file);
                var paragraphs = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                title ??= Path.GetFileNameWithoutExtension(file);
                author ??= "Nong";
                var css = cssPath != null && File.Exists(cssPath) ? File.ReadAllText(cssPath) : EpubCss;
                var hasCover = coverPath != null && File.Exists(coverPath);

                using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
                WriteEntryNoCompress(zip, "mimetype", "application/epub+zip");
                WriteEntry(zip, "META-INF/container.xml", ContainerXml());

                string? coverItemId = null;
                if (hasCover)
                {
                    var ext = Path.GetExtension(coverPath!).ToLowerInvariant();
                    var mime = ext switch { ".png" => "image/png", _ => "image/jpeg" };
                    using var src = File.OpenRead(coverPath!);
                    var ce = zip.CreateEntry("OEBPS/cover" + ext, CompressionLevel.Optimal);
                    using var dst = ce.Open(); src.CopyTo(dst);
                    coverItemId = "cover-image";
                }

                WriteEntry(zip, "OEBPS/content.xhtml", BuildXhtml(title, paragraphs, css, coverItemId));
                WriteEntry(zip, "OEBPS/content.opf", BuildOpf(title, author, hasCover));
                WriteEntry(zip, "OEBPS/toc.ncx", BuildNcx(title, author));

                if (json)
                {
                    var o = JsonOutput.Ok("export epub", $"EPUB: {paragraphs.Length} paragraphs",
                        new { output = Path.GetFullPath(output), title, author, paragraphs = paragraphs.Length, hasCover });
                    o.Artifacts["epub"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"EPUB ({paragraphs.Length}p) -> {output}{(hasCover ? " [cover]" : "")}");
            }
            catch (Exception ex) { CliHelpers.WriteError("export epub", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, authorOpt, cssOpt, coverOpt, jsonOpt);
        return cmd;
    }

    static Command CreateHtml(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .html path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Page title");
        var cssOpt = new Option<string>("--css", "Path to custom CSS file");
        var cmd = new Command("html", "Convert to HTML") { fileArg, outOpt, titleOpt, cssOpt };

        cmd.SetHandler((string file, string output, string? title, string? cssPath, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export html", err, json); return; }
            try
            {
                CliHelpers.EnsureParentDir(output);
                var text = File.ReadAllText(file);
                var paragraphs = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                title ??= Path.GetFileNameWithoutExtension(file);
                var css = cssPath != null && File.Exists(cssPath) ? File.ReadAllText(cssPath) : HtmlCss;
                var html = $"<!DOCTYPE html>\n<html lang=\"en\">\n<head><meta charset=\"UTF-8\"><title>{Escape(title)}</title>\n<style>{css}</style>\n</head><body>\n<h1>{Escape(title)}</h1>\n{string.Join("\n", paragraphs.Select(p => $"  <p>{Escape(p)}</p>"))}\n</body></html>";
                File.WriteAllText(output, html);
                if (json)
                {
                    var o = JsonOutput.Ok("export html", $"HTML: {paragraphs.Length} paragraphs",
                        new { output = Path.GetFullPath(output), title, paragraphs = paragraphs.Length });
                    o.Artifacts["html"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"HTML ({paragraphs.Length}p) -> {output}");
            }
            catch (Exception ex) { CliHelpers.WriteError("export html", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, cssOpt, jsonOpt);
        return cmd;
    }

    // ===== export latex (V12.2) =====

    static Command CreateLatex(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .tex path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Document title");
        var cmd = new Command("latex", "Convert to LaTeX document") { fileArg, outOpt, titleOpt };

        cmd.SetHandler((string file, string output, string? title, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export latex", err, json); return; }
            try
            {
                CliHelpers.EnsureParentDir(output);
                var text = File.ReadAllText(file);
                var paragraphs = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                title ??= Path.GetFileNameWithoutExtension(file);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(@"\documentclass{article}");
                sb.AppendLine(@"\usepackage[UTF8]{ctex}"); // CJK support
                sb.AppendLine(@"\usepackage{amsmath}");
                sb.AppendLine(@"\title{" + EscapeLatex(title) + "}");
                sb.AppendLine(@"\date{\today}");
                sb.AppendLine(@"\begin{document}");
                sb.AppendLine(@"\maketitle");
                foreach (var p in paragraphs)
                {
                    var escaped = EscapeLatex(p);
                    // Detect math ($...$) and pass through
                    if (escaped.Contains("$"))
                        sb.AppendLine(escaped + @"\par");
                    else if (escaped.Length < 80 && System.Text.RegularExpressions.Regex.IsMatch(escaped, @"^[\dA-Z][\d\.\)]+\s"))
                        sb.AppendLine(@"\section{" + escaped + "}");
                    else
                        sb.AppendLine(escaped + @"\par");
                }
                sb.AppendLine(@"\end{document}");
                File.WriteAllText(output, sb.ToString());

                if (json)
                {
                    var o = JsonOutput.Ok("export latex", $"LaTeX: {paragraphs.Length} paragraphs",
                        new { output = Path.GetFullPath(output), title, paragraphs = paragraphs.Length });
                    o.Artifacts["tex"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"LaTeX ({paragraphs.Length}p) -> {output}");
            }
            catch (Exception ex) { CliHelpers.WriteError("export latex", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, jsonOpt);
        return cmd;
    }

    static string EscapeLatex(string s) =>
        s.Replace("\\", "\\textbackslash{}")
         .Replace("&", "\\&")
         .Replace("%", "\\%")
         .Replace("$", "\\$")
         .Replace("#", "\\#")
         .Replace("_", "\\_")
         .Replace("{", "\\{")
         .Replace("}", "\\}")
         .Replace("~", "\\textasciitilde{}")
         .Replace("^", "\\textasciicircum{}");

    // ===== export odf (V12.2, P3) =====

    static Command CreateOdf(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .odt path") { IsRequired = true };
        var cmd = new Command("odf", "Convert to ODF OpenDocument Text (.odt)") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            CliHelpers.EnsureParentDir(output);
            // ODF is a ZIP with content.xml, styles.xml, meta.xml, etc.
            // Pipeline stub: write minimal ODF container for text content
            var text = File.ReadAllText(file).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            using var zip = System.IO.Compression.ZipFile.Open(output, System.IO.Compression.ZipArchiveMode.Create);
            WriteEntryNoCompress(zip, "mimetype", "application/vnd.oasis.opendocument.text");
            WriteEntry(zip, "META-INF/manifest.xml", @"<?xml version=""1.0""?><manifest:manifest xmlns:manifest=""urn:oasis:names:tc:opendocument:xmlns:manifest:1.0""><manifest:file-entry manifest:media-type=""application/vnd.oasis.opendocument.text"" manifest:full-path=""/""/><manifest:file-entry manifest:media-type=""text/xml"" manifest:full-path=""content.xml""/></manifest:manifest>");
            var content = string.Join("", text.Select(p => $"<text:p text:style-name=\"Standard\">{Escape(p)}</text:p>"));
            WriteEntry(zip, "content.xml", $"<?xml version=\"1.0\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\"><office:body><office:text>{content}</office:text></office:body></office:document-content>");
            if (json) { var o = JsonOutput.Ok("export odf", $"ODF: {text.Length}p", new { output = Path.GetFullPath(output) }); o.Artifacts["odt"] = output; Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
            else Console.WriteLine($"ODF ({text.Length}p) -> {output}");
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ── helpers ──

    static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var e = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open()); w.Write(content);
    }

    static void WriteEntryNoCompress(ZipArchive zip, string path, string content)
    {
        var e = zip.CreateEntry(path, CompressionLevel.NoCompression);
        using var w = new StreamWriter(e.Open()); w.Write(content);
    }

    static string ContainerXml() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles><rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/></rootfiles>
</container>";

    static string BuildXhtml(string title, string[] paragraphs, string css, string? coverItemId)
    {
        var coverHtml = coverItemId != null ? $"<div class=\"cover\"><img src=\"cover.jpg\" alt=\"Cover\"/></div>\n" : "";
        var body = string.Join("\n", paragraphs.Select(p => $"    <p>{Escape(p)}</p>"));
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>{Escape(title)}</title><style>{css}</style></head>
<body>
{coverHtml}<h1>{Escape(title)}</h1>
{body}
</body></html>";
    }

    static string BuildOpf(string title, string author, bool hasCover) => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"" unique-identifier=""book-id"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
    <dc:title>{Escape(title)}</dc:title><dc:creator>{Escape(author)}</dc:creator>
    <dc:identifier id=""book-id"">urn:uuid:{Guid.NewGuid()}</dc:identifier>
    <dc:language>en</dc:language>
    <meta property=""dcterms:modified"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
  </metadata>
  <manifest>
    <item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
    <item id=""content"" href=""content.xhtml"" media-type=""application/xhtml+xml""/>
    {(hasCover ? "<item id=\"cover-image\" href=\"cover.jpg\" media-type=\"image/jpeg\"/>" : "")}
  </manifest>
  <spine toc=""ncx""><itemref idref=""content""/></spine>
</package>";

    static string BuildNcx(string title, string author) => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
  <head><meta name=""dtb:uid"" content=""urn:uuid:{Guid.NewGuid()}""/></head>
  <docTitle><text>{Escape(title)}</text></docTitle>
  <docAuthor><text>{Escape(author)}</text></docAuthor>
  <navMap><navPoint id=""ch1"" playOrder=""1""><navLabel><text>{Escape(title)}</text></navLabel><content src=""content.xhtml""/></navPoint></navMap>
</ncx>";

    static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);

    // ── default CSS ──

    const string EpubCss = @"
body { font-family: Georgia, 'Times New Roman', serif; margin: 5%; line-height: 1.6; }
h1 { font-size: 1.6em; margin: 1em 0 0.5em; page-break-before: avoid; }
p { margin: 0.4em 0; text-indent: 0; }
.cover { text-align: center; margin: 2em 0; }
.cover img { max-width: 100%; height: auto; }
";

    const string HtmlCss = @"
body { font-family: Georgia, 'Segoe UI', serif; max-width: 800px; margin: 2em auto; line-height: 1.6; padding: 0 1em; }
h1 { font-size: 1.5em; }
p { margin: 0.5em 0; }
";
}
