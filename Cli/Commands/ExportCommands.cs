using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>V12.1: New format export commands with real container generation.</summary>
public static class ExportCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("export", "Export documents to alternative formats");
        cmd.AddCommand(CreateEpub(jsonOpt));
        cmd.AddCommand(CreateHtml(jsonOpt));
        return cmd;
    }

    static Command CreateEpub(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .epub path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Book title") { IsRequired = false };
        var authorOpt = new Option<string>("--author", "Book author") { IsRequired = false };
        var cmd = new Command("epub", "Convert document to EPUB container") { fileArg, outOpt, titleOpt, authorOpt };

        cmd.SetHandler((string file, string output, string? title, string? author, bool json) =>
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

                // Build EPUB (ZIP with required structure)
                using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
                // mimetype (first entry, uncompressed)
                var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var w = new StreamWriter(mimeEntry.Open())) w.Write("application/epub+zip");

                // META-INF/container.xml
                var containerXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>";
                WriteEntry(zip, "META-INF/container.xml", containerXml);

                // Build chapter XHTML
                var chapterPath = "OEBPS/content.xhtml";
                var chapterHtml = BuildXhtml(title, paragraphs);
                WriteEntry(zip, chapterPath, chapterHtml);

                // OEBPS/content.opf
                var opfXml = BuildOpf(title, author, chapterPath);
                WriteEntry(zip, "OEBPS/content.opf", opfXml);

                // OEBPS/toc.ncx
                var ncxXml = BuildNcx(title, author);
                WriteEntry(zip, "OEBPS/toc.ncx", ncxXml);

                if (json)
                {
                    var o = JsonOutput.Ok("export epub", $"EPUB generated: {paragraphs.Length} paragraphs",
                        new { output = Path.GetFullPath(output), title, author, paragraphs = paragraphs.Length });
                    o.Artifacts["epub"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"EPUB ({paragraphs.Length} paragraphs) -> {output}");
            }
            catch (Exception ex) { CliHelpers.WriteError("export epub", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, authorOpt, jsonOpt);
        return cmd;
    }

    static Command CreateHtml(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx or .txt file");
        var outOpt = new Option<string>("-o", "Output .html path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Page title");
        var cmd = new Command("html", "Convert document to HTML") { fileArg, outOpt, titleOpt };

        cmd.SetHandler((string file, string output, string? title, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("export html", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var text = File.ReadAllText(file);
                var paragraphs = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                title ??= Path.GetFileNameWithoutExtension(file);

                var html = BuildHtml(title, paragraphs);
                File.WriteAllText(output, html);

                if (json)
                {
                    var o = JsonOutput.Ok("export html", $"HTML generated: {paragraphs.Length} paragraphs",
                        new { output = Path.GetFullPath(output), title, paragraphs = paragraphs.Length });
                    o.Artifacts["html"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"HTML ({paragraphs.Length} paragraphs) -> {output}");
            }
            catch (Exception ex) { CliHelpers.WriteError("export html", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, jsonOpt);
        return cmd;
    }

    static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open());
        w.Write(content);
    }

    static string BuildXhtml(string title, string[] paragraphs)
    {
        var body = string.Join("\n", paragraphs.Select(p => $"    <p>{EscapeXml(p)}</p>"));
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>{EscapeXml(title)}</title>
<style>body {{ font-family: Georgia, serif; margin: 2em; line-height: 1.6; }} p {{ margin: 0.5em 0; }} h1 {{ font-size: 1.4em; }}</style>
</head><body>
<h1>{EscapeXml(title)}</h1>
{body}
</body></html>";
    }

    static string BuildOpf(string title, string author, string chapterPath)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"" unique-identifier=""book-id"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
    <dc:title>{EscapeXml(title)}</dc:title>
    <dc:creator>{EscapeXml(author)}</dc:creator>
    <dc:identifier id=""book-id"">urn:uuid:{Guid.NewGuid()}</dc:identifier>
    <dc:language>en</dc:language>
    <meta property=""dcterms:modified"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
  </metadata>
  <manifest>
    <item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
    <item id=""content"" href=""content.xhtml"" media-type=""application/xhtml+xml""/>
  </manifest>
  <spine toc=""ncx"">
    <itemref idref=""content""/>
  </spine>
</package>";
    }

    static string BuildNcx(string title, string author)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
  <head><meta name=""dtb:uid"" content=""urn:uuid:{Guid.NewGuid()}""/></head>
  <docTitle><text>{EscapeXml(title)}</text></docTitle>
  <docAuthor><text>{EscapeXml(author)}</text></docAuthor>
  <navMap><navPoint id=""ch1"" playOrder=""1""><navLabel><text>{EscapeXml(title)}</text></navLabel><content src=""content.xhtml""/></navPoint></navMap>
</ncx>";
    }

    static string BuildHtml(string title, string[] paragraphs)
    {
        var body = string.Join("\n", paragraphs.Select(p => $"  <p>{EscapeXml(p)}</p>"));
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><title>{EscapeXml(title)}</title>
<style>body {{ font-family: Georgia, serif; max-width: 800px; margin: 2em auto; line-height: 1.6; }} p {{ margin: 0.5em 0; }}</style>
</head><body>
<h1>{EscapeXml(title)}</h1>
{body}
</body></html>";
    }

    static string EscapeXml(string s) => System.Net.WebUtility.HtmlEncode(s);
}
