using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.Json.Serialization;
using System.Threading;

namespace DocxCore;

/// <summary>
/// Word document add operations. Each command: copy input -> open output -> append -> save.
/// Never modifies the original input file.
/// </summary>
public static class WordAddOperations
{
    // ========================================================================
    // Known Limitations
    // ========================================================================

    /// <summary>
    /// Known limitations that callers should be aware of.
    /// --after blockId supports precise insertion for paragraphs and tables.
    /// Images and math equations use paragraph-level insertion.
    /// </summary>
    public static readonly List<string> KnownLimitations = new()
    {
        "--after blockId for images inserts after the containing paragraph.",
        "--after blockId for math equations inserts after the containing paragraph.",
    };

    // ========================================================================
    // Helpers — stable ID system
    // ========================================================================

    /// <summary>
    /// Format a public block ID with the same zero-padded shape used by
    /// WordSlice/WordBlockIdMap.
    /// </summary>
    private static string FormatBlockId(string prefix, int index) => $"{prefix}{index:D4}";

    /// <summary>
     /// Find the element identified by a blockId in the document body.
    /// blockId format: "p1" (1st paragraph), "t2" (2nd table),
    /// "img3" (3rd image), "m1" (1st math equation).
    /// Returns null if the element is not found.
    /// </summary>
    private static OpenXmlElement? FindElementByBlockId(Body body, string blockId)
    {
        if (string.IsNullOrEmpty(blockId)) return null;

        if (!TryParseBlockId(blockId, out var prefix, out int index))
            return null;

        return prefix switch
        {
            "p" => body.Elements<Paragraph>().Where(ParagraphProducesPlainBlock).Skip(index - 1).FirstOrDefault(),
            "h" => body.Elements<Paragraph>().Where(IsHeadingParagraph).Skip(index - 1).FirstOrDefault(),
            "t" => body.Elements<Table>().Skip(index - 1).FirstOrDefault(),
            "img" => FindBodyElementForNthDescendant<Drawing>(body, index),
            "m" => FindBodyElementForNthMath(body, index),
            "toc" => body.Elements<Paragraph>().Where(HasTocField).Skip(index - 1).FirstOrDefault(),
            "fld" => body.Elements<Paragraph>().Where(HasComplexField).Skip(index - 1).FirstOrDefault(),
            "link" => FindBodyElementForNthHyperlink(body, index, internalLink: false),
            "xref" => FindBodyElementForNthHyperlink(body, index, internalLink: true),
            "bm" => FindBodyElementForNthDescendant<BookmarkStart>(body, index),
            _ => null
        };
    }

    private static bool TryParseBlockId(string blockId, out string prefix, out int index)
    {
        prefix = "";
        index = 0;

        foreach (var candidate in new[] { "link", "xref", "img", "toc", "fld", "raw", "rev", "bm", "tr", "tc", "ce" })
        {
            if (blockId.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                prefix = candidate;
                return int.TryParse(blockId[candidate.Length..], out index) && index > 0;
            }
        }

        if (blockId.Length >= 2)
        {
            prefix = blockId[..1].ToLowerInvariant();
            return int.TryParse(blockId[1..], out index) && index > 0;
        }

        return false;
    }

    private static bool IsHeadingParagraph(Paragraph paragraph)
    {
        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var outline = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
        return (style != null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
            || outline != null;
    }

    private static bool ParagraphProducesPlainBlock(Paragraph paragraph)
    {
        if (IsHeadingParagraph(paragraph) || HasTocField(paragraph) || HasComplexField(paragraph))
            return false;

        if (paragraph.Descendants<Drawing>().Any()
            || paragraph.Elements<Hyperlink>().Any()
            || paragraph.Elements<BookmarkStart>().Any()
            || paragraph.Descendants().Any(IsOfficeMathElement)
            || paragraph.Descendants().Any(IsOfficeMathParagraphElement))
        {
            return paragraph.Elements<Run>().Any(r =>
                !r.Descendants<Drawing>().Any()
                && !r.Descendants().Any(IsOfficeMathElement)
                && !r.Descendants().Any(IsOfficeMathParagraphElement)
                && !string.IsNullOrWhiteSpace(r.InnerText));
        }

        return true;
    }

    private static bool HasTocField(Paragraph paragraph) =>
        paragraph.Descendants<FieldCode>().Any(fc =>
            fc.InnerText.Trim().StartsWith("TOC", StringComparison.OrdinalIgnoreCase));

    private static bool HasComplexField(Paragraph paragraph)
    {
        if (!paragraph.Descendants<FieldChar>().Any()) return false;

        foreach (var fc in paragraph.Descendants<FieldCode>())
        {
            var code = fc.InnerText.Trim().ToUpperInvariant();
            if (code.StartsWith("PAGE") || code.StartsWith("DATE") ||
                code.StartsWith("SEQ") || code.StartsWith("REF") ||
                code.StartsWith("NUMPAGES") || code.StartsWith("SECTION") ||
                code.StartsWith("SECTIONPAGES") || code.StartsWith("STYLEREF") ||
                code.StartsWith("DOCPROPERTY") || code.StartsWith("MERGEFIELD") ||
                code.StartsWith("HYPERLINK"))
                return true;
        }

        return false;
    }

    private static OpenXmlElement? FindBodyElementForNthDescendant<T>(Body body, int index)
        where T : OpenXmlElement
    {
        var target = body.Descendants<T>().Skip(index - 1).FirstOrDefault();
        return target == null ? null : FindBodyChild(body, target);
    }

    private static OpenXmlElement? FindBodyElementForNthMath(Body body, int index)
    {
        var target = body.Descendants()
            .Where(e => IsOfficeMathElement(e) || IsOfficeMathParagraphElement(e))
            .Skip(index - 1)
            .FirstOrDefault();
        return target == null ? null : FindBodyChild(body, target);
    }

    private static OpenXmlElement? FindBodyElementForNthHyperlink(Body body, int index, bool internalLink)
    {
        var target = body.Descendants<Hyperlink>()
            .Where(h => internalLink
                ? !string.IsNullOrEmpty(h.Anchor?.Value)
                : string.IsNullOrEmpty(h.Anchor?.Value))
            .Skip(index - 1)
            .FirstOrDefault();
        return target == null ? null : FindBodyChild(body, target);
    }

    private static OpenXmlElement? FindBodyChild(Body body, OpenXmlElement element)
    {
        var current = element;
        while (current.Parent != null && current.Parent != body)
            current = current.Parent;
        return current.Parent == body ? current : null;
    }

    private static string BlockIdForParagraph(Body body, Paragraph paragraph)
    {
        if (IsHeadingParagraph(paragraph))
            return FormatBlockId("h", Rank(body.Elements<Paragraph>().Where(IsHeadingParagraph), paragraph));

        return FormatBlockId("p", Rank(body.Elements<Paragraph>().Where(ParagraphProducesPlainBlock), paragraph));
    }

    private static string BlockIdForTable(Body body, Table table) =>
        FormatBlockId("t", Rank(body.Elements<Table>(), table));

    private static string BlockIdForToc(Body body, Paragraph paragraph) =>
        FormatBlockId("toc", Rank(body.Elements<Paragraph>().Where(HasTocField), paragraph));

    private static string BlockIdForImage(Body body, string imagePartId)
    {
        var drawing = body.Descendants<Drawing>()
            .FirstOrDefault(d => d.Descendants<DocumentFormat.OpenXml.Drawing.Blip>()
                .Any(b => b.Embed?.Value == imagePartId));
        return FormatBlockId("img", Rank(body.Descendants<Drawing>(), drawing));
    }

    private static string BlockIdForMath(Body body, OpenXmlElement mathElement) =>
        FormatBlockId("m", Rank(
            body.Descendants().Where(e => IsOfficeMathElement(e) || IsOfficeMathParagraphElement(e)),
            mathElement));

    private static bool IsOfficeMathElement(OpenXmlElement element) =>
        element.LocalName == "oMath";

    private static bool IsOfficeMathParagraphElement(OpenXmlElement element) =>
        element.LocalName == "oMathPara";

    private static string BlockIdForHyperlink(Body body, Hyperlink hyperlink, string prefix) =>
        FormatBlockId(prefix, Rank(
            body.Descendants<Hyperlink>().Where(h => prefix == "xref"
                ? !string.IsNullOrEmpty(h.Anchor?.Value)
                : string.IsNullOrEmpty(h.Anchor?.Value)),
            hyperlink));

    private static string BlockIdForBookmark(Body body, BookmarkStart bookmark) =>
        FormatBlockId("bm", Rank(body.Descendants<BookmarkStart>(), bookmark));

    private static int Rank<T>(IEnumerable<T> items, T? target)
        where T : OpenXmlElement
    {
        if (target == null) return 1;

        var index = 1;
        foreach (var item in items)
        {
            if (ReferenceEquals(item, target)) return index;
            index++;
        }

        return Math.Max(1, index);
    }

    /// <summary>
    /// Insert an element after the element identified by afterBlockId.
    /// If afterBlockId is null, appends to the end of the body.
    /// Throws ArgumentException if afterBlockId is specified but not found.
    /// </summary>
    private static void InsertAfterBlockId(Body body, string? afterBlockId, OpenXmlElement element)
    {
        if (string.IsNullOrEmpty(afterBlockId))
        {
            AppendBeforeSectPr(body, element);
            return;
        }

        var target = FindElementByBlockId(body, afterBlockId);
        if (target == null)
            throw new ArgumentException(
                $"BlockId '{afterBlockId}' not found in document body.", nameof(afterBlockId));

        body.InsertAfter(element, target);
    }

    private static void AppendBeforeSectPr(Body body, OpenXmlElement element)
    {
        var sectionProperties = body.Elements<SectionProperties>().LastOrDefault();
        if (sectionProperties != null)
        {
            body.InsertBefore(element, sectionProperties);
        }
        else
        {
            body.Append(element);
        }
    }

    private static List<OpenXmlElement> MoveAppendedElementsAfter(Body body, int originalChildCount, string? afterBlockId)
    {
        var appended = body.ChildElements.Skip(originalChildCount).Cast<OpenXmlElement>().ToList();
        MoveElementsToInsertionPoint(body, appended, afterBlockId);
        return appended;
    }

    private static void InsertElementsAfterBlockId(Body body, string? afterBlockId, IEnumerable<OpenXmlElement> elements)
    {
        MoveElementsToInsertionPoint(body, elements.ToList(), afterBlockId);
    }

    private static void MoveElementsToInsertionPoint(Body body, IReadOnlyList<OpenXmlElement> elements, string? afterBlockId)
    {
        if (elements.Count == 0) return;

        if (string.IsNullOrEmpty(afterBlockId))
        {
            foreach (var element in elements)
            {
                if (element.Parent != null) element.Remove();
                AppendBeforeSectPr(body, element);
            }
            return;
        }

        var target = FindElementByBlockId(body, afterBlockId);
        if (target == null)
            throw new ArgumentException(
                $"BlockId '{afterBlockId}' not found in document body.", nameof(afterBlockId));

        var anchor = target;
        foreach (var element in elements)
        {
            if (element.Parent != null) element.Remove();
            body.InsertAfter(element, anchor);
            anchor = element;
        }
    }

    private static Paragraph ResolveParagraphAnchor(Body body, string? afterBlockId)
    {
        if (string.IsNullOrEmpty(afterBlockId))
        {
            var last = body.Elements<Paragraph>().LastOrDefault();
            if (last != null) return last;

            var created = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }));
            AppendBeforeSectPr(body, created);
            return created;
        }

        var target = FindElementByBlockId(body, afterBlockId);
        if (target is not Paragraph paragraph)
            throw new ArgumentException(
                $"BlockId '{afterBlockId}' is not a paragraph-like insertion anchor.", nameof(afterBlockId));

        return paragraph;
    }

    private static string TextPreview(string text) =>
        text.Length <= 50 ? text : text[..47] + "...";

    private static int _seqCounter;
    private static string NextSeqId(string prefix) => $"{prefix}{Interlocked.Increment(ref _seqCounter)}";

    /// <summary>
    /// Copy input file to output path, then open output for editing.
    /// </summary>
    private static WordprocessingDocument CopyAndOpen(string inputPath, string outputPath)
    {
        File.Copy(inputPath, outputPath, overwrite: true);
        return WordprocessingDocument.Open(outputPath, true);
    }

    // ========================================================================
    // Input spec records
    // ========================================================================

    /// <summary>Spec for add paragraph command.</summary>
    public record ParagraphSpec(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("style")] string? Style = null,
        [property: JsonPropertyName("bold")] bool Bold = false,
        [property: JsonPropertyName("italic")] bool Italic = false);

    /// <summary>Spec for add table command.</summary>
    public record TableSpec(
        [property: JsonPropertyName("caption")] string? Caption = null,
        [property: JsonPropertyName("headers")] string[]? Headers = null,
        [property: JsonPropertyName("rows")] string[][]? Rows = null);

    /// <summary>Spec for add image command. Src is the image file path.</summary>
    public record ImageSpec(string Src, string? Caption = null);

    /// <summary>Spec for add math command.</summary>
    public record MathSpec(string Latex, bool Display = false);

    // ========================================================================
    // Result records (all JSON-serializable)
    // ========================================================================

    /// <summary>Result for add paragraph.</summary>
    public record AddParagraphResult(string BlockId, string TextPreview);

    /// <summary>Result for add table. Rows count includes header row.</summary>
    public record AddTableResult(string BlockId, int Rows, int Cols);

    /// <summary>Result for add footnote.</summary>
    public record AddFootnoteResult(string BlockId, string TextPreview);

    /// <summary>Result for add endnote.</summary>
    public record AddEndnoteResult(string BlockId, string TextPreview);

    /// <summary>Result for add image.</summary>
    public record AddImageResult(string BlockId, string ImagePath, int Width, int Height);

    /// <summary>Result for add table of contents.</summary>
    public record AddTocResult(string BlockId, string Title);

    /// <summary>Result for add cross-reference.</summary>
    public record AddXrefResult(string BlockId, string Target, string DisplayText);

    /// <summary>Result for add external hyperlink.</summary>
    public record AddLinkResult(string BlockId, string Url, string DisplayText);

    /// <summary>Result for add bookmark.</summary>
    public record AddBookmarkResult(string BlockId, string Name);

    /// <summary>Result for add comment.</summary>
    public record AddCommentResult(string BlockId, string Author, string TextPreview);

    /// <summary>Result for add math equation.</summary>
    public record AddMathResult(string BlockId, string Latex, bool Display);

    // ========================================================================
    // 1. add paragraph
    // ========================================================================

    /// <summary>
    /// Append a paragraph to the end of the document.
    /// Supports optional style, bold, and italic formatting.
    /// </summary>
    public static AddParagraphResult AddParagraph(string inputPath, string outputPath, ParagraphSpec spec, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(spec.Text))
            throw new ArgumentException("Paragraph text is required.", nameof(spec));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var ppr = new ParagraphProperties();
        if (!string.IsNullOrEmpty(spec.Style))
            ppr.Append(new ParagraphStyleId { Val = spec.Style });

        var rpr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" });
        if (spec.Bold) rpr.Append(new Bold());
        if (spec.Italic) rpr.Append(new Italic());
        rpr.Append(new FontSize { Val = "21" });

        var p = new Paragraph(ppr);
        p.Append(new Run(rpr, new Text(spec.Text) { Space = SpaceProcessingModeValues.Preserve }));
        InsertAfterBlockId(body, afterBlockId, p);
        var blockId = BlockIdForParagraph(body, p);
        return new AddParagraphResult(blockId, TextPreview(spec.Text));
    }

    // ========================================================================
    // 2. add table
    // ========================================================================

    /// <summary>
    /// Append a table with optional caption, header row, and data rows.
    /// Caption appears as a centered paragraph above the table.
    /// Headers array defines the header row cells.
    /// Rows is a jagged array: rows[rowIndex][colIndex].
    /// </summary>
    public static AddTableResult AddTable(string inputPath, string outputPath, TableSpec spec, string? afterBlockId = null)
    {
        // Validate: at least one content source must be provided
        if (spec.Headers == null && spec.Rows == null && string.IsNullOrEmpty(spec.Caption))
            throw new ArgumentException(
                "Table must have at least one of: Caption, Headers, Rows.", nameof(spec));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var headers = spec.Headers ?? Array.Empty<string>();
        var rows = spec.Rows ?? Array.Empty<string[]>();
        int colCount = headers.Length > 0 ? headers.Length
            : (rows.Length > 0 ? rows.Max(r => r.Length) : 0);

        var insertElements = new List<OpenXmlElement>();

        // Caption paragraph
        if (!string.IsNullOrEmpty(spec.Caption))
        {
            var cp = new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId { Val = "BodyTextNoIndent" },
                    new Justification { Val = JustificationValues.Center }));
            cp.Append(new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                    new FontSize { Val = "16" }),
                new Text(spec.Caption)));
            insertElements.Add(cp);
        }

        // Build table
        var t = new Table(
            new TableProperties(
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                    new LeftBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }),
                new TableLayout { Type = TableLayoutValues.Fixed }));

        // TableGrid
        var grid = new TableGrid();
        for (int i = 0; i < colCount; i++)
            grid.Append(new GridColumn());
        t.Append(grid);

        // Header row
        if (headers.Length > 0)
        {
            var hr = new TableRow();
            for (int i = 0; i < colCount; i++)
            {
                string cellText = i < headers.Length ? headers[i] : "";
                hr.Append(MakeCell(cellText, isHeader: true, bottomBorder: true));
            }
            t.Append(hr);
        }

        // Data rows
        foreach (var row in rows)
        {
            var tr = new TableRow();
            for (int i = 0; i < colCount; i++)
            {
                string cellText = i < row.Length ? row[i] : "";
                tr.Append(MakeCell(cellText, isHeader: false, bottomBorder: false));
            }
            t.Append(tr);
        }

        int totalRows = (headers.Length > 0 ? 1 : 0) + rows.Length;
        insertElements.Add(t);
        insertElements.Add(new Paragraph()); // spacing after table
        InsertElementsAfterBlockId(body, afterBlockId, insertElements);
        var blockId = BlockIdForTable(body, t);
        return new AddTableResult(blockId, totalRows, colCount);
    }

    // ========================================================================
    // 3. add footnote
    // ========================================================================

    /// <summary>
    /// Append a footnote. Creates Footnote in FootnotesPart and adds a
    /// FootnoteReference run to the last paragraph in the body.
    /// </summary>
    public static AddFootnoteResult AddFootnote(string inputPath, string outputPath, string text, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Footnote text is required.", nameof(text));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;
        var mainPart = doc.MainDocumentPart!;

        // Get or create FootnotesPart
        var fnPart = mainPart.FootnotesPart ?? mainPart.AddNewPart<FootnotesPart>();
        if (fnPart.Footnotes == null)
        {
            fnPart.Footnotes = new Footnotes(
                new Footnote(new Paragraph()) { Id = 0 },
                new Footnote(new Paragraph()) { Id = -1 });
        }

        // Determine next footnote ID
        int fnId = fnPart.Footnotes.Elements<Footnote>()
            .Select(f => (int?)f.Id?.Value)
            .Where(id => id.HasValue && id.Value > 0)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        // Create footnote
        var fn = new Footnote { Id = fnId };
        fn.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "FootnoteText" }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                    new FontSize { Val = "18" }),
                new Text($"{fnId}. {text}"))));
        fnPart.Footnotes.Append(fn);

        // Add FootnoteReference to the last paragraph in body
        var refRun = new Run(
            new RunProperties(
                new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
                new FontSize { Val = "18" }),
            new FootnoteReference { Id = fnId });

        ResolveParagraphAnchor(body, afterBlockId).Append(refRun);

        var blockId = FormatBlockId("f", fnPart.Footnotes.Elements<Footnote>()
            .Count(f => f.Id?.Value != 0 && f.Id?.Value != -1));
        return new AddFootnoteResult(blockId, TextPreview(text));
    }

    // ========================================================================
    // 4. add endnote
    // ========================================================================

    /// <summary>
    /// Append an endnote. Creates Endnote in EndnotesPart and adds an
    /// EndnoteReference run to the last paragraph in the body.
    /// </summary>
    public static AddEndnoteResult AddEndnote(string inputPath, string outputPath, string text, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Endnote text is required.", nameof(text));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;
        var mainPart = doc.MainDocumentPart!;

        // Get or create EndnotesPart
        var enPart = mainPart.EndnotesPart ?? mainPart.AddNewPart<EndnotesPart>();
        if (enPart.Endnotes == null)
        {
            enPart.Endnotes = new Endnotes(
                new Endnote(new Paragraph()) { Id = 0 },
                new Endnote(new Paragraph()) { Id = -1 });
        }

        // Determine next endnote ID
        int enId = enPart.Endnotes.Elements<Endnote>()
            .Select(e => (int?)e.Id?.Value)
            .Where(id => id.HasValue && id.Value > 0)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        // Create endnote
        var en = new Endnote { Id = enId };
        en.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "EndnoteText" }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                    new FontSize { Val = "18" }),
                new Text($"{enId}. {text}"))));
        enPart.Endnotes.Append(en);

        // Add EndnoteReference to the last paragraph in body
        var refRun = new Run(
            new RunProperties(
                new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
                new FontSize { Val = "18" }),
            new EndnoteReference { Id = enId });

        ResolveParagraphAnchor(body, afterBlockId).Append(refRun);

        var blockId = FormatBlockId("e", enPart.Endnotes.Elements<Endnote>()
            .Count(e => e.Id?.Value != 0 && e.Id?.Value != -1));
        return new AddEndnoteResult(blockId, TextPreview(text));
    }

    // ========================================================================
    // 5. add image
    // ========================================================================

    /// <summary>
    /// Append an image. Uses ImageEmbedder.EmbedSingleImage.
    /// Supported formats: .png, .jpg, .jpeg, .gif, .bmp.
    /// Supports optional --caption text.
    /// </summary>
    public static AddImageResult AddImage(string inputPath, string outputPath, ImageSpec spec, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(spec.Src))
            throw new ArgumentException("Image source path is required.", nameof(spec));

        // Validate image format
        var ext = Path.GetExtension(spec.Src).ToLowerInvariant();
        var validExts = new HashSet<string> { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        if (!validExts.Contains(ext))
            throw new ArgumentException(
                $"Unsupported image format: {ext}. Supported: .png, .jpg, .jpeg, .gif, .bmp");

        if (!File.Exists(spec.Src))
            throw new FileNotFoundException($"Image file not found: {spec.Src}");

        // Get image dimensions
        var (width, height) = ImageHeaderReader.GetDimensions(spec.Src);

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;
        var mainPart = doc.MainDocumentPart!;

        var originalChildCount = body.ChildElements.Count;
        string imagePartId = ImageEmbedder.EmbedSingleImage(
            body, mainPart, spec.Src, spec.Caption);
        MoveAppendedElementsAfter(body, originalChildCount, afterBlockId);

        var blockId = BlockIdForImage(body, imagePartId);
        return new AddImageResult(blockId, spec.Src, width, height);
    }

    // ========================================================================
    // 6. add table of contents
    // ========================================================================

    /// <summary>
    /// Append a table of contents field. Uses TocAndChartBuilder.AppendTableOfContents.
    /// The TOC will display heading entries. In Word, right-click and select
    /// "Update Field" to populate the TOC.
    /// </summary>
    public static AddTocResult AddTableOfContents(string inputPath, string outputPath, string title = "目录", string? afterBlockId = null)
    {
        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var originalChildCount = body.ChildElements.Count;
        TocAndChartBuilder.AppendTableOfContents(body, title);
        var moved = MoveAppendedElementsAfter(body, originalChildCount, afterBlockId);

        var tocParagraph = moved.OfType<Paragraph>().FirstOrDefault(HasTocField)
            ?? body.Elements<Paragraph>().Last(HasTocField);
        var blockId = BlockIdForToc(body, tocParagraph);
        return new AddTocResult(blockId, title);
    }

    // ========================================================================
    // 7. add cross-reference
    // ========================================================================

    /// <summary>
    /// Append a cross-reference (internal hyperlink to a bookmark).
    /// Uses DocumentWriter.CrossReference which creates a hyperlink to
    /// the named bookmark anchor.
    /// </summary>
    public static AddXrefResult AddCrossReference(string inputPath, string outputPath,
        string target, string displayText, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Cross-reference target is required.", nameof(target));
        if (string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("Cross-reference display text is required.", nameof(displayText));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var hyperlink = new Hyperlink { Anchor = target, History = true };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Color { Val = "0563C1" },
            new Underline { Val = UnderlineValues.Single }),
            new Text(displayText)));
        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "BodyTextNoIndent" }),
            hyperlink);
        InsertAfterBlockId(body, afterBlockId, paragraph);

        var blockId = BlockIdForHyperlink(body, hyperlink, "xref");
        return new AddXrefResult(blockId, target, displayText);
    }

    // ========================================================================
    // 8. add external hyperlink
    // ========================================================================

    /// <summary>
    /// Append an external hyperlink. Uses DocumentWriter.Hyperlink which creates
    /// a proper OOXML hyperlink with an external relationship.
    /// </summary>
    public static AddLinkResult AddHyperlink(string inputPath, string outputPath,
        string url, string displayText, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Hyperlink URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("Hyperlink display text is required.", nameof(displayText));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var mainPart = doc.MainDocumentPart!;
        var extRel = mainPart.AddHyperlinkRelationship(new Uri(url), true);
        var hyperlink = new Hyperlink { Id = extRel.Id };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Color { Val = "0563C1" },
            new Underline { Val = UnderlineValues.Single }),
            new Text(displayText)));
        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "BodyTextNoIndent" }),
            hyperlink);
        InsertAfterBlockId(body, afterBlockId, paragraph);

        var blockId = BlockIdForHyperlink(body, hyperlink, "link");
        return new AddLinkResult(blockId, url, displayText);
    }

    // ========================================================================
    // 9. add bookmark
    // ========================================================================

    /// <summary>
    /// Add a bookmark to the last paragraph in the document body.
    /// Wraps the last paragraph with BookmarkStart and BookmarkEnd elements.
    /// </summary>
    public static AddBookmarkResult AddBookmark(string inputPath, string outputPath, string name, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bookmark name is required.", nameof(name));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var id = Math.Abs(name.GetHashCode() % 0x7FFFFFFF).ToString();

        BookmarkStart bookmarkStart;
        var paragraph = ResolveParagraphAnchor(body, afterBlockId);
        var firstChild = paragraph.Elements().FirstOrDefault();
        if (firstChild != null)
        {
            bookmarkStart = new BookmarkStart { Name = name, Id = id };
            paragraph.InsertBefore(bookmarkStart, firstChild);
        }
        else
        {
            bookmarkStart = new BookmarkStart { Name = name, Id = id };
            paragraph.PrependChild(bookmarkStart);
        }

        paragraph.Append(new BookmarkEnd { Id = id });

        var blockId = BlockIdForBookmark(body, bookmarkStart);
        return new AddBookmarkResult(blockId, name);
    }

    // ========================================================================
    // 10. add comment
    // ========================================================================

    /// <summary>
    /// Add a comment to the last paragraph in the document body.
    /// Uses AdvancedFeatures.InsertComment.
    /// </summary>
    public static AddCommentResult AddComment(string inputPath, string outputPath,
        string author, string text, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Comment author is required.", nameof(author));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Comment text is required.", nameof(text));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        var paragraph = ResolveParagraphAnchor(body, afterBlockId);
        AdvancedFeatures.InsertComment(doc, author, text, paragraph);

        var commentsPart = doc.MainDocumentPart!.WordprocessingCommentsPart;
        var blockId = FormatBlockId("c", commentsPart?.Comments?.Elements<Comment>().Count() ?? 1);
        return new AddCommentResult(blockId, author, TextPreview(text));
    }

    // ========================================================================
    // 11. add math equation
    // ========================================================================

    /// <summary>
    /// Add a math equation. Uses MathRenderer.
    /// When Display is false (default), renders inline math wrapped in a paragraph.
    /// When Display is true, renders a centered display equation paragraph.
    /// Supports LaTeX-like syntax for common math constructs.
    /// </summary>
    public static AddMathResult AddMath(string inputPath, string outputPath, MathSpec spec, string? afterBlockId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(spec.Latex))
            throw new ArgumentException("LaTeX math expression is required.", nameof(spec));

        using var doc = CopyAndOpen(inputPath, outputPath);
        var body = doc.MainDocumentPart!.Document.Body!;

        if (spec.Display)
        {
            // Display equation — RenderDisplay returns a centered Paragraph
            var mathPara = MathRenderer.RenderDisplay(spec.Latex);
            InsertAfterBlockId(body, afterBlockId, mathPara);
            body.InsertAfter(new Paragraph(), mathPara); // spacing
            var mathElement = mathPara.Descendants<DocumentFormat.OpenXml.Math.OfficeMath>().First();
            var blockId = BlockIdForMath(body, mathElement);
            return new AddMathResult(blockId, spec.Latex, spec.Display);
        }
        else
        {
            // Inline equation — RenderInline returns OfficeMath, wrap in a Paragraph+Run
            var officeMath = MathRenderer.RenderInline(spec.Latex);
            var inlinePara = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }));
            inlinePara.Append(new Run(officeMath));
            InsertAfterBlockId(body, afterBlockId, inlinePara);
            var blockId = BlockIdForMath(body, officeMath);
            return new AddMathResult(blockId, spec.Latex, spec.Display);
        }
    }

    // ========================================================================
    // Private helpers
    // ========================================================================

    /// <summary>Create a table cell with standard formatting.</summary>
    private static TableCell MakeCell(string text, bool isHeader, bool bottomBorder)
    {
        var tc = new TableCell();
        var tcProps = new TableCellProperties();
        if (bottomBorder)
            tcProps.Append(new TableCellBorders(
                new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tcProps.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        tc.Append(tcProps);

        var p = new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "BodyTextNoIndent" },
                new SpacingBetweenLines { Before = "40", After = "40" },
                new Justification { Val = JustificationValues.Center }));

        var rpr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = isHeader ? "黑体" : "宋体" });
        if (isHeader) rpr.Append(new Bold());
        rpr.Append(new FontSize { Val = "21" });

        p.Append(new Run(rpr, new Text(text)));
        tc.Append(p);
        return tc;
    }
}
