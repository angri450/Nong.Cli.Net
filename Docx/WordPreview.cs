using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace DocxCore;

/// <summary>
/// 文档预览与诊断。打开 docx，输出结构化文本预览和诊断警告。
/// 集成 OpenXmlValidator 进行正式 OOXML 模式校验。
/// </summary>
public static class WordPreview
{
    /// <summary>预览和诊断结果。</summary>
    public sealed record PreviewResult(
        string Text,
        List<string> Warnings,
        List<string> Errors,
        List<string> Info,
        Stats Statistics
    );

    public sealed record Stats(
        int Paragraphs, int Tables, int Images, int Footnotes, int Endnotes,
        List<string> FontSizes, string? CjkFont,
        bool HasStyles, bool HasNumbering, bool HasHeaders, bool HasFooters,
        List<string> ReferencedUndefinedStyles,
        int OoxmlErrors, int OoxmlWarnings  // NEW: OpenXmlValidator results
    );

    /// <summary>打开 docx 文件，生成预览和诊断。</summary>
    public static PreviewResult Preview(string docxPath)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var info = new List<string>();

        if (!File.Exists(docxPath))
            return new PreviewResult("", new() { "File not found" }, new() { $"ERROR: {docxPath} does not exist" }, new(), new(0, 0, 0, 0, 0, new(), null, false, false, false, false, new(), 0, 0));

        using var zip = ZipFile.OpenRead(docxPath);
        var entries = zip.Entries.Select(e => e.FullName).ToHashSet();

        // 1. 基础结构
        foreach (var required in new[] { "[Content_Types].xml", "word/document.xml", "word/_rels/document.xml.rels" })
            if (!entries.Contains(required))
                errors.Add($"MISSING: {required}");

        bool hasStyles = entries.Any(e => e.Contains("word/styles.xml"));
        bool hasNumbering = entries.Any(e => e.Contains("word/numbering"));
        bool hasHeaders = entries.Any(e => e.Contains("word/header"));
        bool hasFooters = entries.Any(e => e.Contains("word/footer"));

        if (!hasStyles) warnings.Add("No styles.xml — using defaults");
        if (!hasNumbering) info.Add("No numbering.xml — no lists defined");
        if (!hasHeaders) info.Add("No headers");
        if (!hasFooters) info.Add("No footers");

        // 2. XML well-formedness
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var stream = entry.Open();
                using var reader = XmlReader.Create(stream);
                while (reader.Read()) { }
            }
            catch (Exception ex)
            {
                errors.Add($"XML parse error in {entry.FullName}: {ex.Message}");
            }
        }

        // 3. Content extraction
        var docEntry = zip.GetEntry("word/document.xml");
        string docText = "";
        if (docEntry != null)
        {
            using var stream = docEntry.Open();
            using var reader = new StreamReader(stream);
            docText = reader.ReadToEnd();
        }

        int pCount = Regex.Matches(docText, @"<w:p[ >]").Count;
        int tblCount = Regex.Matches(docText, @"<w:tbl[ >]").Count;
        int imgCount = Regex.Matches(docText, @"<w:drawing").Count;
        int fnCount = Regex.Matches(docText, @"<w:footnoteReference").Count;
        int enCount = Regex.Matches(docText, @"<w:endnoteReference").Count;

        // Extract plain text
        var plainText = Regex.Replace(docText, @"<[^>]+>", " ");
        plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

        // 4. Style references
        var referencedUndefined = new List<string>();
        if (hasStyles)
        {
            var styleEntry = zip.GetEntry("word/styles.xml");
            if (styleEntry != null)
            {
                using var stream = styleEntry.Open();
                using var reader = new StreamReader(stream);
                var styleText = reader.ReadToEnd();

                var defined = Regex.Matches(styleText, "w:styleId=\"([^\"]+)\"")
                    .Select(m => m.Groups[1].Value)
                    .Concat(Regex.Matches(styleText, "<w:name w:val=\"([^\"]+)\"")
                        .Select(m => m.Groups[1].Value))
                    .ToHashSet();
                var used = Regex.Matches(docText, "w:pStyle w:val=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
                referencedUndefined = used.Except(defined).ToList();
                foreach (var u in referencedUndefined)
                    errors.Add($"Style referenced but not defined: {u}");
            }
        }

        // 5. Three-line table check
        var borderMatches = Regex.Matches(docText, @"<w:tblBorders>(.+?)</w:tblBorders>", RegexOptions.Singleline);
        foreach (Match b in borderMatches)
        {
            var inner = b.Groups[1].Value;
            if (Regex.IsMatch(inner, "w:insideH[^/]*w:val=\"[^n]"))
                warnings.Add("Table has inside horizontal borders — should be none for three-line style");
            if (Regex.IsMatch(inner, "w:insideV[^/]*w:val=\"[^n]"))
                warnings.Add("Table has inside vertical borders — should be none for three-line style");
        }

        // 6. Font info
        string? cjkFont = null;
        var cjkMatch = Regex.Match(docText, "w:rFonts[^/]*w:eastAsia=\"([^\"]+)\"");
        if (cjkMatch.Success) cjkFont = cjkMatch.Groups[1].Value;

        var fontSizes = Regex.Matches(docText, "w:sz w:val=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        // 7. Content quality (lightweight)
        if (plainText.Length > 500)
        {
            if (plainText.Contains("填补空白") || plainText.Contains("重大理论价值") || plainText.Contains("首次提出"))
                warnings.Add("Content: inflated contribution language detected — replace with specific claims");
            if ((plainText.Contains("导致") || plainText.Contains("决定")) &&
                !Regex.IsMatch(plainText, "(DID|双重差分|PSM|工具变量|随机实验|固定效应)"))
                warnings.Add("Content: causal language without causal design indicators");
            if (tblCount == 0)
                warnings.Add("Content: no tables found — consider adding at minimum a variable table");
            if (imgCount == 0 && plainText.Length > 800)
                info.Add("Content: no embedded images found");
        }

        // 7b. OpenXmlValidator
        int ooxmlErrors = 0, ooxmlWarnings = 0;
        try
        {
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var validator = new OpenXmlValidator();
            var results = validator.Validate(doc).ToList();
            foreach (var r in results.Take(10))
            {
                var desc = r.Description ?? "";
                if (desc.Contains("Error", StringComparison.OrdinalIgnoreCase))
                { ooxmlErrors++; errors.Add($"OOXML: {desc}"); }
                else
                { ooxmlWarnings++; warnings.Add($"OOXML: {desc}"); }
            }
        }
        catch { /* Validator unavailable */ }

        zip.Dispose();

        return new PreviewResult(
            Text: plainText.Length > 3000 ? plainText[..3000] + "..." : plainText,
            Warnings: warnings,
            Errors: errors,
            Info: info,
            Statistics: new Stats(
                Paragraphs: pCount, Tables: tblCount, Images: imgCount,
                Footnotes: fnCount, Endnotes: enCount,
                FontSizes: fontSizes, CjkFont: cjkFont,
                HasStyles: hasStyles, HasNumbering: hasNumbering,
                HasHeaders: hasHeaders, HasFooters: hasFooters,
                ReferencedUndefinedStyles: referencedUndefined,
                OoxmlErrors: ooxmlErrors, OoxmlWarnings: ooxmlWarnings)
        );
    }
}
