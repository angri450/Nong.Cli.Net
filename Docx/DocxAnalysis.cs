using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Word CLI analysis helpers: stats, fonts, styles, validate, extract, dissect, merge.
/// </summary>
public static class DocxAnalysis
{
    // ===== stats =====

    public sealed record DocxStatsResult(
        int Paragraphs, int Tables, int Images, int Footnotes, int Endnotes,
        int Characters, int WordsApprox, int Sections
    );

    public static DocxStatsResult GetStats(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;

        int paragraphs = 0, tables = 0, sections = 0, characters = 0;
        int footnotes = 0, endnotes = 0;

        if (body != null)
        {
            paragraphs = body.Elements<Paragraph>().Count();
            tables = body.Elements<Table>().Count();
            sections = body.Descendants<SectionProperties>().Count();

            foreach (var para in body.Elements<Paragraph>())
                characters += para.InnerText.Length;
            foreach (var tbl in body.Elements<Table>())
                characters += tbl.InnerText.Length;
        }

        int images = doc.MainDocumentPart?.ImageParts?.Count() ?? 0;

        var fnPart = doc.MainDocumentPart?.FootnotesPart;
        if (fnPart?.Footnotes != null)
        {
            foreach (var fn in fnPart.Footnotes.Elements<Footnote>())
            {
                footnotes++;
                characters += fn.InnerText.Length;
            }
        }

        var enPart = doc.MainDocumentPart?.EndnotesPart;
        if (enPart?.Endnotes != null)
        {
            foreach (var en in enPart.Endnotes.Elements<Endnote>())
            {
                endnotes++;
                characters += en.InnerText.Length;
            }
        }

        int wordsApprox = characters / 2;

        return new DocxStatsResult(paragraphs, tables, images, footnotes, endnotes,
            characters, wordsApprox, sections);
    }

    // ===== fonts =====

    public sealed record FontEntry(string Name, int Count, string Source);

    public sealed record DocxFontsResult(
        List<FontEntry> Fonts,
        List<FontEntry> EastAsiaFonts,
        List<FontEntry> AsciiFonts,
        List<string> Warnings
    );

    public static DocxFontsResult GetFonts(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;

        var fontCounts = new Dictionary<string, (int count, string source)>();
        var eastAsiaCounts = new Dictionary<string, int>();
        var asciiCounts = new Dictionary<string, int>();
        var warnings = new List<string>();

        // Collect from runs
        if (body != null)
        {
            foreach (var run in body.Descendants<Run>())
            {
                var rf = run.RunProperties?.RunFonts;
                if (rf == null) continue;

                AddFontCount(fontCounts, rf.Ascii?.Value, "run");
                AddFontCount(fontCounts, rf.HighAnsi?.Value, "run");
                AddFontCount(fontCounts, rf.EastAsia?.Value, "run");
                AddFontCount(fontCounts, rf.ComplexScript?.Value, "run");

                AddSingleFont(eastAsiaCounts, rf.EastAsia?.Value);
                AddSingleFont(asciiCounts, rf.Ascii?.Value);
            }
        }

        // Collect from styles
        var sp = doc.MainDocumentPart?.StyleDefinitionsPart;
        if (sp?.Styles != null)
        {
            foreach (var style in sp.Styles.Elements<Style>())
            {
                var rf = style.StyleRunProperties?.RunFonts;
                if (rf == null) continue;

                AddFontCount(fontCounts, rf.Ascii?.Value, "style");
                AddFontCount(fontCounts, rf.HighAnsi?.Value, "style");
                AddFontCount(fontCounts, rf.EastAsia?.Value, "style");
                AddFontCount(fontCounts, rf.ComplexScript?.Value, "style");
            }
        }

        var fonts = fontCounts
            .OrderByDescending(kv => kv.Value.count)
            .Select(kv => new FontEntry(kv.Key, kv.Value.count, kv.Value.source))
            .ToList();

        var eastAsia = eastAsiaCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new FontEntry(kv.Key, kv.Value, "eastAsia"))
            .ToList();

        var ascii = asciiCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new FontEntry(kv.Key, kv.Value, "ascii"))
            .ToList();

        return new DocxFontsResult(fonts, eastAsia, ascii, warnings);
    }

    private static void AddFontCount(Dictionary<string, (int count, string source)> dict, string? name, string source)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (dict.TryGetValue(name, out var existing))
            dict[name] = (existing.count + 1, source);
        else
            dict[name] = (1, source);
    }

    private static void AddSingleFont(Dictionary<string, int> dict, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (dict.ContainsKey(name))
            dict[name]++;
        else
            dict[name] = 1;
    }

    // ===== styles =====

    public sealed record StyleEntry(string Id, string Name, string Type, string? BasedOn, bool IsDefault);

    public sealed record DocxStylesResult(List<StyleEntry> Styles, int Count);

    public static DocxStylesResult GetStyles(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var sp = doc.MainDocumentPart?.StyleDefinitionsPart;

        var styles = new List<StyleEntry>();

        if (sp?.Styles != null)
        {
            foreach (var style in sp.Styles.Elements<Style>())
            {
                styles.Add(new StyleEntry(
                    style.StyleId?.Value ?? "",
                    style.StyleName?.Val?.Value ?? "",
                    style.Type?.InnerText ?? "paragraph",
                    style.BasedOn?.Val?.Value,
                    style.Default?.Value ?? false
                ));
            }
        }

        return new DocxStylesResult(styles, styles.Count);
    }

    // ===== validate =====

    public sealed record DocxValidationResult(bool Valid, List<string> Errors, List<string> Warnings);

    public static DocxValidationResult Validate(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var validator = new OpenXmlValidator();
        var results = validator.Validate(doc).ToList();

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var r in results)
        {
            var desc = r.Description ?? "";
            // Use ErrorType enum, not string matching on Description
            if (r.ErrorType == ValidationErrorType.Schema ||
                r.ErrorType == ValidationErrorType.Semantic ||
                r.ErrorType == ValidationErrorType.Package)
                errors.Add(desc);
            else
                warnings.Add(desc);
        }

        return new DocxValidationResult(errors.Count == 0, errors, warnings);
    }

    // ===== extract =====

    public sealed record DocxExtractResult(string Dir, List<string> Images);

    public static DocxExtractResult ExtractImages(string docxPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var images = new List<string>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
            return new DocxExtractResult(Path.GetFullPath(outputDir), images);

        int i = 0;
        foreach (var imagePart in mainPart.ImageParts)
        {
            string ext = ContentTypeToExtension(imagePart.ContentType);
            string fileName = $"image_{i + 1}{ext}";
            string filePath = Path.Combine(outputDir, fileName);

            using var stream = imagePart.GetStream();
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);

            images.Add(filePath);
            i++;
        }

        return new DocxExtractResult(Path.GetFullPath(outputDir), images);
    }

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tiff",
        "image/x-wmf" => ".wmf",
        "image/x-emf" => ".emf",
        "image/svg+xml" => ".svg",
        _ => ".bin"
    };

    // ===== dissect =====

    public sealed record TableStructure(int RowCount, int ColCount);

    public sealed record NumberingInfo(int AbstractNums, int Instances);

    public sealed record SectionInfo(int Count, List<string> PageSizes);

    public sealed record DocxDissectResult(
        DocxStatsResult Stats,
        DocxFontsResult Fonts,
        DocxStylesResult Styles,
        List<TableStructure> Tables,
        NumberingInfo Numbering,
        SectionInfo Sections,
        List<string> Warnings
    );

    public static DocxDissectResult Dissect(string docxPath)
    {
        var warnings = new List<string>();

        // Stats
        var stats = GetStats(docxPath);

        // Fonts
        var fonts = GetFonts(docxPath);

        // Styles
        var styles = GetStyles(docxPath);

        // Tables: count and basic structure
        var tableStructures = new List<TableStructure>();
        using (var doc = WordprocessingDocument.Open(docxPath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var table in body.Elements<Table>())
                {
                    int rows = table.Elements<TableRow>().Count();
                    int cols = 0;
                    var firstRow = table.Elements<TableRow>().FirstOrDefault();
                    if (firstRow != null)
                        cols = firstRow.Elements<TableCell>().Count();
                    tableStructures.Add(new TableStructure(rows, cols));
                }
            }

            // Numbering
            var numPart = doc.MainDocumentPart?.NumberingDefinitionsPart;
            int abstractNums = 0, instances = 0;
            if (numPart?.Numbering != null)
            {
                abstractNums = numPart.Numbering.Elements<AbstractNum>().Count();
                instances = numPart.Numbering.Elements<NumberingInstance>().Count();
            }

            // Sections
            var pageSizes = new List<string>();
            if (body != null)
            {
                foreach (var sectPr in body.Descendants<SectionProperties>())
                {
                    var pgSz = sectPr.GetFirstChild<PageSize>();
                    if (pgSz != null)
                        pageSizes.Add($"{pgSz.Width?.Value}x{pgSz.Height?.Value}");
                    else
                        pageSizes.Add("default");
                }
            }

            return new DocxDissectResult(
                stats,
                fonts,
                styles,
                tableStructures,
                new NumberingInfo(abstractNums, instances),
                new SectionInfo(stats.Sections, pageSizes),
                warnings
            );
        }
    }

    // ===== merge =====

    /// <summary>
    /// Merge multiple docx files by appending body content with deep merge.
    /// Uses <see cref="WordEditOperations.MergeDocuments"/> internally which handles
    /// images, styles, numbering, and relationships via
    /// <see cref="AdvancedFeatures.AppendDocument"/>.
    ///
    /// Known limitations:
    /// - Headers and footers from source documents are not merged.
    /// - Numbering definitions may conflict between documents.
    /// - Style naming conflicts are resolved by keeping the first-encountered style.
    /// </summary>
    public static WordEditOperations.MergeResult MergeDocx(string[] inputFiles, string outputPath)
    {
        return WordEditOperations.MergeDocuments(inputFiles, outputPath);
    }
}
