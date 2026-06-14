using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

public sealed record WordFormatAuditResult
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "nong-word/format-audit/v1";

    [JsonPropertyName("input")]
    public string Input { get; init; } = "";

    [JsonPropertyName("profile")]
    public string Profile { get; init; } = "academic";

    [JsonPropertyName("statusLevel")]
    public string StatusLevel { get; init; } = "pass";

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("summary")]
    public WordFormatAuditSummary Summary { get; init; } = new();

    [JsonPropertyName("headings")]
    public WordFormatHeadingAudit Headings { get; init; } = new();

    [JsonPropertyName("body")]
    public WordFormatBodyAudit Body { get; init; } = new();

    [JsonPropertyName("fonts")]
    public WordFormatFontAudit Fonts { get; init; } = new();

    [JsonPropertyName("lineSpacing")]
    public WordFormatLineSpacingAudit LineSpacing { get; init; } = new();

    [JsonPropertyName("tables")]
    public WordFormatTableAudit Tables { get; init; } = new();

    [JsonPropertyName("latinNames")]
    public WordFormatLatinNameAudit LatinNames { get; init; } = new();

    [JsonPropertyName("chemistry")]
    public WordFormatChemistryAudit Chemistry { get; init; } = new();

    [JsonPropertyName("issues")]
    public List<WordFormatIssue> Issues { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();
}

public sealed record WordFormatAuditSummary
{
    [JsonPropertyName("paragraphs")]
    public int Paragraphs { get; init; }

    [JsonPropertyName("nonEmptyParagraphs")]
    public int NonEmptyParagraphs { get; init; }

    [JsonPropertyName("headings")]
    public int Headings { get; init; }

    [JsonPropertyName("bodyParagraphs")]
    public int BodyParagraphs { get; init; }

    [JsonPropertyName("tables")]
    public int Tables { get; init; }

    [JsonPropertyName("issues")]
    public int Issues { get; init; }
}

public sealed record WordFormatHeadingAudit
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("byLevel")]
    public Dictionary<string, int> ByLevel { get; init; } = new();

    [JsonPropertyName("samples")]
    public List<WordFormatHeadingSample> Samples { get; init; } = new();

    [JsonPropertyName("suspectedHeadingsWithoutStyle")]
    public List<WordFormatParagraphSample> SuspectedHeadingsWithoutStyle { get; init; } = new();
}

public sealed record WordFormatHeadingSample
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("styleId")]
    public string? StyleId { get; init; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; init; }

    [JsonPropertyName("fontEastAsia")]
    public string? FontEastAsia { get; init; }

    [JsonPropertyName("fontAscii")]
    public string? FontAscii { get; init; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; init; }

    [JsonPropertyName("alignment")]
    public string? Alignment { get; init; }

    [JsonPropertyName("lineSpacing")]
    public string? LineSpacing { get; init; }

    [JsonPropertyName("lineRule")]
    public string? LineRule { get; init; }

    [JsonPropertyName("before")]
    public string? Before { get; init; }

    [JsonPropertyName("after")]
    public string? After { get; init; }

    [JsonPropertyName("bold")]
    public bool Bold { get; init; }

    [JsonPropertyName("keepNext")]
    public bool KeepNext { get; init; }
}

public sealed record WordFormatBodyAudit
{
    [JsonPropertyName("paragraphs")]
    public int Paragraphs { get; init; }

    [JsonPropertyName("twoCharFirstLineIndent")]
    public int TwoCharFirstLineIndent { get; init; }

    [JsonPropertyName("justified")]
    public int Justified { get; init; }

    [JsonPropertyName("samples")]
    public List<WordFormatParagraphSample> Samples { get; init; } = new();

    [JsonPropertyName("commonFirstLineIndents")]
    public Dictionary<string, int> CommonFirstLineIndents { get; init; } = new();

    [JsonPropertyName("commonAlignments")]
    public Dictionary<string, int> CommonAlignments { get; init; } = new();
}

public sealed record WordFormatParagraphSample
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("styleId")]
    public string? StyleId { get; init; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; init; }

    [JsonPropertyName("fontEastAsia")]
    public string? FontEastAsia { get; init; }

    [JsonPropertyName("fontAscii")]
    public string? FontAscii { get; init; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; init; }

    [JsonPropertyName("alignment")]
    public string? Alignment { get; init; }

    [JsonPropertyName("firstLineIndent")]
    public string? FirstLineIndent { get; init; }

    [JsonPropertyName("lineSpacing")]
    public string? LineSpacing { get; init; }

    [JsonPropertyName("lineRule")]
    public string? LineRule { get; init; }
}

public sealed record WordFormatFontAudit
{
    [JsonPropertyName("eastAsiaFonts")]
    public Dictionary<string, int> EastAsiaFonts { get; init; } = new();

    [JsonPropertyName("asciiFonts")]
    public Dictionary<string, int> AsciiFonts { get; init; } = new();

    [JsonPropertyName("fontSizes")]
    public Dictionary<string, int> FontSizes { get; init; } = new();

    [JsonPropertyName("missingRunFontCount")]
    public int MissingRunFontCount { get; init; }
}

public sealed record WordFormatLineSpacingAudit
{
    [JsonPropertyName("paragraphRules")]
    public Dictionary<string, int> ParagraphRules { get; init; } = new();

    [JsonPropertyName("paragraphLines")]
    public Dictionary<string, int> ParagraphLines { get; init; } = new();

    [JsonPropertyName("documentGridDetected")]
    public bool DocumentGridDetected { get; init; }

    [JsonPropertyName("documentGridType")]
    public string? DocumentGridType { get; init; }
}

public sealed record WordFormatTableAudit
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("threeLineLike")]
    public int ThreeLineLike { get; init; }

    [JsonPropertyName("withVerticalBorders")]
    public int WithVerticalBorders { get; init; }

    [JsonPropertyName("withShading")]
    public int WithShading { get; init; }

    [JsonPropertyName("headerRowsRepeated")]
    public int HeaderRowsRepeated { get; init; }

    [JsonPropertyName("samples")]
    public List<WordFormatTableSample> Samples { get; init; } = new();
}

public sealed record WordFormatTableSample
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("rows")]
    public int Rows { get; init; }

    [JsonPropertyName("columns")]
    public int Columns { get; init; }

    [JsonPropertyName("threeLineLike")]
    public bool ThreeLineLike { get; init; }

    [JsonPropertyName("topBorderSize")]
    public uint? TopBorderSize { get; init; }

    [JsonPropertyName("headerBottomBorderSize")]
    public uint? HeaderBottomBorderSize { get; init; }

    [JsonPropertyName("bottomBorderSize")]
    public uint? BottomBorderSize { get; init; }

    [JsonPropertyName("leftBorder")]
    public string? LeftBorder { get; init; }

    [JsonPropertyName("rightBorder")]
    public string? RightBorder { get; init; }

    [JsonPropertyName("insideHorizontal")]
    public string? InsideHorizontal { get; init; }

    [JsonPropertyName("insideVertical")]
    public string? InsideVertical { get; init; }

    [JsonPropertyName("headerRowsRepeated")]
    public bool HeaderRowsRepeated { get; init; }

    [JsonPropertyName("shadingCount")]
    public int ShadingCount { get; init; }

    [JsonPropertyName("cellFirstLineIndentCount")]
    public int CellFirstLineIndentCount { get; init; }

    [JsonPropertyName("reflowRecommendation")]
    public string? ReflowRecommendation { get; init; }
}

public sealed record WordFormatLatinNameAudit
{
    [JsonPropertyName("candidates")]
    public int Candidates { get; init; }

    [JsonPropertyName("italicized")]
    public int Italicized { get; init; }

    [JsonPropertyName("samples")]
    public List<WordFormatLatinNameSample> Samples { get; init; } = new();
}

public sealed record WordFormatLatinNameSample
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("latinName")]
    public string LatinName { get; init; } = "";

    [JsonPropertyName("insideParentheses")]
    public bool InsideParentheses { get; init; }

    [JsonPropertyName("italic")]
    public bool Italic { get; init; }

    [JsonPropertyName("fontAscii")]
    public string? FontAscii { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed record WordFormatChemistryAudit
{
    [JsonPropertyName("candidates")]
    public int Candidates { get; init; }

    [JsonPropertyName("subscripted")]
    public int Subscripted { get; init; }

    [JsonPropertyName("samples")]
    public List<WordFormatChemistrySample> Samples { get; init; } = new();
}

public sealed record WordFormatChemistrySample
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("formula")]
    public string Formula { get; init; } = "";

    [JsonPropertyName("subscriptedDigits")]
    public bool SubscriptedDigits { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed record WordFormatIssue
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    [JsonPropertyName("blockId")]
    public string? BlockId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

public static class WordFormatAuditor
{
    const uint ThreeLineFrameBorderSize = 12;
    const uint ThreeLineHeaderBorderSize = 6;
    const string ChineseBodyFont = "宋体";
    const string ChineseHeadingFont = "黑体";
    const string LatinFont = "Times New Roman";
    const string BodyFontSize = "24";
    const string BodyFirstLineIndent = "480";

    static readonly Regex LatinSpeciesRegex = new(
        @"\b[A-Z][a-z]+(?:\s+x)?\s+(?!(?:et|al)\b)[a-z][a-z-]{2,}(?:\s+f\.\s*sp\.?)?",
        RegexOptions.Compiled);

    static readonly Regex ChemicalFormulaCandidateRegex = new(
        @"(?<![A-Za-z0-9])(?<formula>[A-Z][A-Za-z0-9]*[+-]?)(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    static readonly HashSet<string> ChemicalElementSymbols = new(StringComparer.Ordinal)
    {
        "H", "He", "Li", "Be", "B", "C", "N", "O", "F", "Ne",
        "Na", "Mg", "Al", "Si", "P", "S", "Cl", "Ar", "K", "Ca",
        "Sc", "Ti", "V", "Cr", "Mn", "Fe", "Co", "Ni", "Cu", "Zn",
        "Ga", "Ge", "As", "Se", "Br", "Kr", "Rb", "Sr", "Y", "Zr",
        "Nb", "Mo", "Tc", "Ru", "Rh", "Pd", "Ag", "Cd", "In", "Sn",
        "Sb", "Te", "I", "Xe", "Cs", "Ba", "La", "Ce", "Pr", "Nd",
        "Pm", "Sm", "Eu", "Gd", "Tb", "Dy", "Ho", "Er", "Tm", "Yb",
        "Lu", "Hf", "Ta", "W", "Re", "Os", "Ir", "Pt", "Au", "Hg",
        "Tl", "Pb", "Bi", "Po", "At", "Rn", "Fr", "Ra", "Ac", "Th",
        "Pa", "U", "Np", "Pu", "Am", "Cm", "Bk", "Cf", "Es", "Fm",
        "Md", "No", "Lr", "Rf", "Db", "Sg", "Bh", "Hs", "Mt", "Ds",
        "Rg", "Cn", "Nh", "Fl", "Mc", "Lv", "Ts", "Og",
    };

    static readonly HashSet<string> SingleElementSubscriptFormulas = new(StringComparer.Ordinal)
    {
        "H2", "N2", "O2", "F2", "Cl2", "Br2", "I2",
    };

    public static WordFormatAuditResult Audit(string docxPath, string profile = "academic")
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        return Audit(doc, profile, Path.GetFullPath(docxPath));
    }

    public static WordFormatAuditResult Audit(
        WordprocessingDocument doc,
        string profile = "academic",
        string? input = null)
    {
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("MainDocumentPart is missing.");
        var body = mainPart.Document?.Body;
        if (body == null)
            throw new InvalidOperationException("Document body is missing.");

        var paragraphs = new List<ParagraphAudit>();
        var issues = new List<WordFormatIssue>();
        var warnings = new List<string>();
        var fonts = new FontAccumulator();
        var lineRules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lineValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstLineIndents = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var alignments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var latinSamples = new List<WordFormatLatinNameSample>();
        var chemistrySamples = new List<WordFormatChemistrySample>();

        var headingByLevel = new Dictionary<string, int>(StringComparer.Ordinal);
        var headingSamples = new List<WordFormatHeadingSample>();
        var bodySamples = new List<WordFormatParagraphSample>();
        var suspectedHeadings = new List<WordFormatParagraphSample>();
        int headingCount = 0;
        int bodyCount = 0;
        int nonEmptyParagraphCount = 0;
        int twoCharFirstLineIndent = 0;
        int justified = 0;
        int paragraphIndex = 0;

        foreach (var paragraph in body.Elements<W.Paragraph>())
        {
            paragraphIndex++;
            var text = paragraph.InnerText.Trim();
            if (text.Length > 0)
                nonEmptyParagraphCount++;

            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var styleName = WordHeadingStyles.GetStyleName(mainPart, styleId);
            var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
            var directHeadingLevel = WordHeadingStyles.GetHeadingLevel(styleId, styleName, outlineLevel, text);
            var suspectedHeadingLevel = directHeadingLevel ?? WordHeadingStyles.GetHeadingLevelFromText(text);
            var isTitle = IsStyle(styleId, styleName, "Title");
            var isSubtitle = IsStyle(styleId, styleName, "BodyTextNoIndent") && IsCenteredMetadataLine(text);
            var isCaption = IsStyle(styleId, styleName, "Caption") || LooksLikeTableCaption(text);
            var isReference = LooksLikeReference(text);
            var format = ExtractParagraphFormat(paragraph);
            paragraphs.Add(new ParagraphAudit($"p{paragraphIndex:D4}", text, styleId, styleName, directHeadingLevel, suspectedHeadingLevel, format));

            AccumulateParagraphFormat(format, lineRules, lineValues, firstLineIndents, alignments);
            fonts.Add(format.FontEastAsia, format.FontAscii, format.FontSize, format.MissingRunFont);

            if (directHeadingLevel.HasValue && text.Length > 0)
            {
                headingCount++;
                var levelKey = directHeadingLevel.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Increment(headingByLevel, levelKey);
                var blockId = $"h{headingCount:D4}";
                if (headingSamples.Count < 20)
                {
                    headingSamples.Add(new WordFormatHeadingSample
                    {
                        BlockId = blockId,
                        Level = directHeadingLevel.Value,
                        Text = TrimPreview(text),
                        StyleId = styleId,
                        StyleName = styleName,
                        FontEastAsia = format.FontEastAsia,
                        FontAscii = format.FontAscii,
                        FontSize = format.FontSize,
                        Alignment = format.Alignment,
                        LineSpacing = format.LineSpacing,
                        LineRule = format.LineRule,
                        Before = format.Before,
                        After = format.After,
                        Bold = format.Bold,
                        KeepNext = format.KeepNext,
                    });
                }

                AddHeadingIssues(issues, blockId, directHeadingLevel.Value, format);
            }
            else if (text.Length > 0)
            {
                var blockId = $"p{paragraphIndex:D4}";
                if (isTitle || isSubtitle || isReference)
                {
                    // Cover titles, metadata lines, and references are visible-format
                    // evidence, but they are not body paragraphs.
                }
                else if (suspectedHeadingLevel.HasValue)
                {
                    if (suspectedHeadings.Count < 20)
                        suspectedHeadings.Add(ToParagraphSample(blockId, text, styleId, styleName, format));
                    issues.Add(new WordFormatIssue
                    {
                        Id = "suspected_heading_without_style",
                        Severity = "warning",
                        BlockId = blockId,
                        Message = "Paragraph text looks like a heading but it is not backed by heading style or outline level.",
                    });
                }
                else if (!isCaption)
                {
                    bodyCount++;
                    if (HasBodyOrListIndent(format))
                        twoCharFirstLineIndent++;
                    if (string.Equals(format.Alignment, "both", StringComparison.OrdinalIgnoreCase))
                        justified++;
                    if (bodySamples.Count < 12)
                        bodySamples.Add(ToParagraphSample(blockId, text, styleId, styleName, format));

                    AddBodyIssues(issues, blockId, format);
                }
            }

            if (!isReference)
            {
                CollectLatinNameSamples(paragraph, paragraphIndex, latinSamples, issues);
                CollectChemistrySamples(paragraph, paragraphIndex, chemistrySamples, issues);
            }
        }

        var tableSamples = new List<WordFormatTableSample>();
        var tables = body.Elements<W.Table>().ToList();
        var threeLineLike = 0;
        var withVerticalBorders = 0;
        var withShading = 0;
        var headerRowsRepeated = 0;
        for (var i = 0; i < tables.Count; i++)
        {
            var sample = AuditTable(tables[i], $"t{i + 1:D4}");
            tableSamples.Add(sample);
            if (sample.ThreeLineLike) threeLineLike++;
            if (!IsNilBorder(sample.LeftBorder) || !IsNilBorder(sample.RightBorder) || !IsNilBorder(sample.InsideVertical)) withVerticalBorders++;
            if (sample.ShadingCount > 0) withShading++;
            if (sample.HeaderRowsRepeated) headerRowsRepeated++;
            AddTableIssues(issues, sample);
        }

        var documentGrid = body.Descendants<W.DocGrid>().LastOrDefault();
        if (documentGrid != null)
        {
            warnings.Add("Document grid is present and may affect perceived line spacing in Word.");
            issues.Add(new WordFormatIssue
            {
                Id = "document_grid_detected",
                Severity = "warning",
                Message = "Document grid is present; Word may squeeze or override paragraph line spacing.",
            });
        }

        var statusLevel = issues.Any(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            ? "fail"
            : issues.Any(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                ? "warn"
                : "pass";
        var score = Math.Max(0, 100 - issues.Count(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)) * 15
                                - issues.Count(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)) * 5);

        return new WordFormatAuditResult
        {
            Input = input ?? "",
            Profile = profile,
            StatusLevel = statusLevel,
            Score = score,
            Summary = new WordFormatAuditSummary
            {
                Paragraphs = paragraphs.Count,
                NonEmptyParagraphs = nonEmptyParagraphCount,
                Headings = headingCount,
                BodyParagraphs = bodyCount,
                Tables = tables.Count,
                Issues = issues.Count,
            },
            Headings = new WordFormatHeadingAudit
            {
                Total = headingCount,
                ByLevel = headingByLevel,
                Samples = headingSamples,
                SuspectedHeadingsWithoutStyle = suspectedHeadings,
            },
            Body = new WordFormatBodyAudit
            {
                Paragraphs = bodyCount,
                TwoCharFirstLineIndent = twoCharFirstLineIndent,
                Justified = justified,
                Samples = bodySamples,
                CommonFirstLineIndents = TopCounts(firstLineIndents),
                CommonAlignments = TopCounts(alignments),
            },
            Fonts = new WordFormatFontAudit
            {
                EastAsiaFonts = TopCounts(fonts.EastAsiaFonts),
                AsciiFonts = TopCounts(fonts.AsciiFonts),
                FontSizes = TopCounts(fonts.FontSizes),
                MissingRunFontCount = fonts.MissingRunFontCount,
            },
            LineSpacing = new WordFormatLineSpacingAudit
            {
                ParagraphRules = TopCounts(lineRules),
                ParagraphLines = TopCounts(lineValues),
                DocumentGridDetected = documentGrid != null,
                DocumentGridType = documentGrid?.Type?.InnerText,
            },
            Tables = new WordFormatTableAudit
            {
                Total = tables.Count,
                ThreeLineLike = threeLineLike,
                WithVerticalBorders = withVerticalBorders,
                WithShading = withShading,
                HeaderRowsRepeated = headerRowsRepeated,
                Samples = tableSamples.Take(20).ToList(),
            },
            LatinNames = new WordFormatLatinNameAudit
            {
                Candidates = latinSamples.Count,
                Italicized = latinSamples.Count(s => s.Italic),
                Samples = latinSamples.Take(20).ToList(),
            },
            Chemistry = new WordFormatChemistryAudit
            {
                Candidates = chemistrySamples.Count,
                Subscripted = chemistrySamples.Count(s => s.SubscriptedDigits),
                Samples = chemistrySamples.Take(20).ToList(),
            },
            Issues = issues,
            Warnings = warnings,
        };
    }

    static ParagraphFormat ExtractParagraphFormat(W.Paragraph paragraph)
    {
        var pPr = paragraph.ParagraphProperties;
        var spacing = pPr?.SpacingBetweenLines;
        var indent = pPr?.Indentation;
        var firstRunProperties = paragraph.Elements<W.Run>()
            .Select(r => r.RunProperties)
            .FirstOrDefault(rPr => rPr != null);

        var runFonts = firstRunProperties?.RunFonts;
        var fontSize = firstRunProperties?.FontSize?.Val?.Value;
        var missingRunFont = firstRunProperties != null && runFonts == null;
        return new ParagraphFormat
        {
            FontEastAsia = runFonts?.EastAsia?.Value,
            FontAscii = runFonts?.Ascii?.Value ?? runFonts?.HighAnsi?.Value,
            FontSize = fontSize,
            Alignment = AlignmentValue(pPr?.Justification),
            FirstLineIndent = indent?.FirstLine?.Value?.ToString(),
            LeftIndent = indent?.Left?.Value?.ToString(),
            HangingIndent = indent?.Hanging?.Value?.ToString(),
            LineSpacing = spacing?.Line?.Value,
            LineRule = LineRuleValue(spacing),
            Before = spacing?.Before?.Value,
            After = spacing?.After?.Value,
            Bold = firstRunProperties?.Bold != null,
            Italic = firstRunProperties?.Italic != null,
            KeepNext = pPr?.KeepNext != null,
            MissingRunFont = missingRunFont,
        };
    }

    static void AddHeadingIssues(List<WordFormatIssue> issues, string blockId, int level, ParagraphFormat format)
    {
        if (!string.Equals(format.FontEastAsia, ChineseHeadingFont, StringComparison.Ordinal))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "heading_east_asia_font",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Heading should normally use {ChineseHeadingFont}; found {format.FontEastAsia ?? "missing"}.",
            });
        }

        if (!string.Equals(format.FontAscii, LatinFont, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "heading_ascii_font",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Heading Latin font should normally use {LatinFont}; found {format.FontAscii ?? "missing"}.",
            });
        }

        if (!format.Bold)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "heading_not_bold",
                Severity = "warning",
                BlockId = blockId,
                Message = "Heading is not bold in direct OOXML evidence.",
            });
        }

        var expectedAlignment = level == 1 ? "center" : "left";
        if (!string.Equals(format.Alignment, expectedAlignment, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "heading_alignment",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Heading level {level} expected alignment {expectedAlignment}; found {format.Alignment ?? "missing"}.",
            });
        }

        if (format.FirstLineIndent != null)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "heading_first_line_indent",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Heading should not have first-line indent; found {format.FirstLineIndent}.",
            });
        }
    }

    static void AddBodyIssues(List<WordFormatIssue> issues, string blockId, ParagraphFormat format)
    {
        if (!string.Equals(format.FontEastAsia, ChineseBodyFont, StringComparison.Ordinal))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "body_east_asia_font",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Body Chinese font should normally use {ChineseBodyFont}; found {format.FontEastAsia ?? "missing"}.",
            });
        }

        if (!string.Equals(format.FontAscii, LatinFont, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "body_ascii_font",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Body Latin font should normally use {LatinFont}; found {format.FontAscii ?? "missing"}.",
            });
        }

        if (!string.Equals(format.FontSize, BodyFontSize, StringComparison.Ordinal))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "body_font_size",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Body font size should normally be {BodyFontSize} half-points; found {format.FontSize ?? "missing"}.",
            });
        }

        if (!string.Equals(format.Alignment, "both", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "body_alignment",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Body paragraph should normally be justified; found {format.Alignment ?? "missing"}.",
            });
        }

        if (!HasBodyOrListIndent(format))
        {
            issues.Add(new WordFormatIssue
            {
                Id = "body_first_line_indent",
                Severity = "warning",
                BlockId = blockId,
                Message = $"Body paragraph should normally use two-character first-line indent ({BodyFirstLineIndent} dxa) or list hanging indent; found firstLine={format.FirstLineIndent ?? "missing"}, left={format.LeftIndent ?? "missing"}, hanging={format.HangingIndent ?? "missing"}.",
            });
        }
    }

    static WordFormatTableSample AuditTable(W.Table table, string blockId)
    {
        var rows = table.Elements<W.TableRow>().ToList();
        var columnCount = rows.Select(r => r.Elements<W.TableCell>().Count()).DefaultIfEmpty(0).Max();
        var borders = table.TableProperties?.TableBorders;
        var headerRow = rows.FirstOrDefault();
        var firstHeaderCell = headerRow?.Elements<W.TableCell>().FirstOrDefault();
        var headerBottom = firstHeaderCell?.TableCellProperties?.TableCellBorders?.BottomBorder;
        var shadingCount = table.Descendants<W.Shading>().Count();
        var cellFirstLineIndent = table.Descendants<W.Paragraph>()
            .Count(p => p.ParagraphProperties?.Indentation?.FirstLine?.Value is string value && value != "0");

        var top = borders?.TopBorder;
        var bottom = borders?.BottomBorder;
        var insideV = borders?.InsideVerticalBorder;
        var left = borders?.LeftBorder;
        var right = borders?.RightBorder;

        var topSize = top?.Size?.Value;
        var bottomSize = bottom?.Size?.Value;
        var headerBottomSize = headerBottom?.Size?.Value;
        var headerRepeated = headerRow?.TableRowProperties?.Elements<W.TableHeader>().Any() == true;
        var threeLineLike = IsSingleBorder(top, ThreeLineFrameBorderSize)
            && IsSingleBorder(bottom, ThreeLineFrameBorderSize)
            && IsSingleBorder(headerBottom, ThreeLineHeaderBorderSize)
            && IsNilBorderValue(left)
            && IsNilBorderValue(right)
            && IsNilBorderValue(insideV);

        return new WordFormatTableSample
        {
            BlockId = blockId,
            Rows = rows.Count,
            Columns = columnCount,
            ThreeLineLike = threeLineLike,
            TopBorderSize = topSize,
            HeaderBottomBorderSize = headerBottomSize,
            BottomBorderSize = bottomSize,
            LeftBorder = BorderValue(left),
            RightBorder = BorderValue(right),
            InsideHorizontal = BorderValue(borders?.InsideHorizontalBorder),
            InsideVertical = BorderValue(insideV),
            HeaderRowsRepeated = headerRepeated,
            ShadingCount = shadingCount,
            CellFirstLineIndentCount = cellFirstLineIndent,
            ReflowRecommendation = GetReflowRecommendation(rows.Count, columnCount),
        };
    }

    static void AddTableIssues(List<WordFormatIssue> issues, WordFormatTableSample table)
    {
        if (!table.ThreeLineLike)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "table_not_three_line",
                Severity = "warning",
                BlockId = table.BlockId,
                Message = "Table does not match three-line evidence: top 1.5pt, header bottom 0.75pt, bottom 1.5pt, no vertical borders.",
            });
        }

        if (table.ShadingCount > 0)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "table_shading",
                Severity = "warning",
                BlockId = table.BlockId,
                Message = $"Table contains {table.ShadingCount} shading element(s); academic three-line tables usually avoid shaded titles/cells.",
            });
        }

        if (table.CellFirstLineIndentCount > 0)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "table_cell_first_line_indent",
                Severity = "warning",
                BlockId = table.BlockId,
                Message = $"Table contains {table.CellFirstLineIndentCount} paragraph(s) with first-line indent; table cell text should normally not indent by two spaces.",
            });
        }

        if (table.ReflowRecommendation != null)
        {
            issues.Add(new WordFormatIssue
            {
                Id = "table_reflow_recommended",
                Severity = "warning",
                BlockId = table.BlockId,
                Message = table.ReflowRecommendation,
            });
        }
    }

    static void CollectLatinNameSamples(
        W.Paragraph paragraph,
        int paragraphIndex,
        List<WordFormatLatinNameSample> samples,
        List<WordFormatIssue> issues)
    {
        var blockId = $"p{paragraphIndex:D4}";
        foreach (var run in paragraph.Elements<W.Run>())
        {
            var runText = string.Concat(run.Elements<W.Text>().Select(t => t.Text));
            if (runText.Length == 0)
                continue;

            foreach (Match match in LatinSpeciesRegex.Matches(runText))
            {
                var italic = run.RunProperties?.Italic != null;
                var asciiFont = run.RunProperties?.RunFonts?.Ascii?.Value
                    ?? run.RunProperties?.RunFonts?.HighAnsi?.Value;
                var insideParentheses = IsInsideParentheses(paragraph.InnerText, match.Value);
                samples.Add(new WordFormatLatinNameSample
                {
                    BlockId = blockId,
                    LatinName = match.Value,
                    InsideParentheses = insideParentheses,
                    Italic = italic,
                    FontAscii = asciiFont,
                    Text = TrimPreview(paragraph.InnerText),
                });

                if (insideParentheses && !italic)
                {
                    issues.Add(new WordFormatIssue
                    {
                        Id = "latin_name_not_italic",
                        Severity = "warning",
                        BlockId = blockId,
                        Message = $"Latin scientific name in parentheses should be italic Times New Roman; found non-italic: {match.Value}.",
                    });
                }
            }
        }
    }

    static void CollectChemistrySamples(
        W.Paragraph paragraph,
        int paragraphIndex,
        List<WordFormatChemistrySample> samples,
        List<WordFormatIssue> issues)
    {
        if (!paragraph.InnerText.Any(char.IsDigit))
            return;

        var chars = FlattenParagraphChars(paragraph);
        if (chars.Count == 0)
            return;

        var text = new string(chars.Select(c => c.Text).ToArray());
        foreach (Match match in ChemicalFormulaCandidateRegex.Matches(text))
        {
            var formula = match.Groups["formula"].Value;
            if (!IsChemicalFormulaWithSubscriptDigits(formula))
                continue;

            var digits = Enumerable.Range(match.Index, match.Length)
                .Where(i => char.IsDigit(chars[i].Text))
                .ToArray();
            if (digits.Length == 0)
                continue;

            var subscripted = digits.All(i => chars[i].IsSubscript);
            var blockId = $"p{paragraphIndex:D4}";
            samples.Add(new WordFormatChemistrySample
            {
                BlockId = blockId,
                Formula = formula,
                SubscriptedDigits = subscripted,
                Text = TrimPreview(text),
            });

            if (!subscripted)
            {
                issues.Add(new WordFormatIssue
                {
                    Id = "chemical_formula_digits_not_subscript",
                    Severity = "warning",
                    BlockId = blockId,
                    Message = $"Chemical formula digits should be subscripted: {formula}.",
                });
            }
        }
    }

    static List<RunChar> FlattenParagraphChars(W.Paragraph paragraph)
    {
        var chars = new List<RunChar>();
        foreach (var run in paragraph.Elements<W.Run>())
        {
            var isSubscript = run.RunProperties?.VerticalTextAlignment?.Val?.Value
                == W.VerticalPositionValues.Subscript;
            foreach (var text in run.Elements<W.Text>())
            {
                foreach (var ch in text.Text)
                    chars.Add(new RunChar(ch, isSubscript));
            }
        }

        return chars;
    }

    static bool IsChemicalFormulaWithSubscriptDigits(string formula)
    {
        if (formula.Length < 2 || !formula.Any(char.IsDigit) || !char.IsUpper(formula[0]))
            return false;

        var normalized = formula.TrimEnd('+', '-');
        if (normalized.Length != formula.Length && normalized.Count(char.IsDigit) == 0)
            return false;

        var elementCount = 0;
        var numberCount = 0;
        var i = 0;
        while (i < formula.Length)
        {
            if (i == formula.Length - 1 && (formula[i] == '+' || formula[i] == '-'))
            {
                i++;
                continue;
            }

            if (!char.IsUpper(formula[i]))
                return false;

            var symbol = formula[i].ToString();
            if (i + 1 < formula.Length && char.IsLower(formula[i + 1]))
            {
                symbol += formula[i + 1];
                i++;
            }

            if (!ChemicalElementSymbols.Contains(symbol))
                return false;

            elementCount++;
            i++;

            var numberStart = i;
            while (i < formula.Length && char.IsDigit(formula[i]))
                i++;
            if (i > numberStart)
                numberCount++;
        }

        if (numberCount == 0)
            return false;

        if (elementCount == 1 && !SingleElementSubscriptFormulas.Contains(formula.TrimEnd('+', '-')))
            return false;

        return true;
    }

    static bool IsInsideParentheses(string paragraphText, string latinName)
    {
        var index = paragraphText.IndexOf(latinName, StringComparison.Ordinal);
        if (index < 0)
            return false;

        var before = paragraphText[..index];
        var after = paragraphText[(index + latinName.Length)..];
        var open = before.LastIndexOfAny(['(', '（']);
        if (open < 0)
            return false;

        var closeBefore = before.LastIndexOfAny([')', '）']);
        if (closeBefore > open)
            return false;

        var close = after.IndexOfAny([')', '）']);
        return close >= 0;
    }

    static string? GetReflowRecommendation(int rows, int columns)
    {
        if (rows > 35)
            return "Long table: consider word table-reflow --max-rows to create continuation tables with repeated headers.";
        if (columns > 6)
            return "Wide table: consider word table-reflow --max-cols and --repeat-left-cols to split column groups.";
        return null;
    }

    static void AccumulateParagraphFormat(
        ParagraphFormat format,
        Dictionary<string, int> lineRules,
        Dictionary<string, int> lineValues,
        Dictionary<string, int> firstLineIndents,
        Dictionary<string, int> alignments)
    {
        if (format.LineRule != null) Increment(lineRules, format.LineRule);
        if (format.LineSpacing != null) Increment(lineValues, format.LineSpacing);
        if (format.FirstLineIndent != null) Increment(firstLineIndents, format.FirstLineIndent);
        if (format.Alignment != null) Increment(alignments, format.Alignment);
    }

    static WordFormatParagraphSample ToParagraphSample(
        string blockId,
        string text,
        string? styleId,
        string? styleName,
        ParagraphFormat format) => new()
        {
            BlockId = blockId,
            Text = TrimPreview(text),
            StyleId = styleId,
            StyleName = styleName,
            FontEastAsia = format.FontEastAsia,
            FontAscii = format.FontAscii,
            FontSize = format.FontSize,
            Alignment = format.Alignment,
            FirstLineIndent = format.FirstLineIndent,
            LineSpacing = format.LineSpacing,
            LineRule = format.LineRule,
        };

    static bool LooksLikeTableCaption(string text) =>
        text.StartsWith("表", StringComparison.Ordinal)
        || text.StartsWith("续表", StringComparison.Ordinal);

    static bool IsStyle(string? styleId, string? styleName, string expected) =>
        string.Equals(styleId, expected, StringComparison.OrdinalIgnoreCase)
        || string.Equals(styleName, expected, StringComparison.OrdinalIgnoreCase);

    static bool IsCenteredMetadataLine(string text) =>
        text.Contains("编制日期", StringComparison.Ordinal)
        || text.Contains("日期", StringComparison.Ordinal)
        || text.Contains("学院", StringComparison.Ordinal)
        || text.Contains("大学", StringComparison.Ordinal)
        || text.Contains("集团", StringComparison.Ordinal)
        || text.Contains("重点实验室", StringComparison.Ordinal);

    static bool LooksLikeReference(string text) =>
        Regex.IsMatch(text.TrimStart(), @"^\[\d+\]\s+\S+");

    static bool HasBodyOrListIndent(ParagraphFormat format) =>
        string.Equals(format.FirstLineIndent, BodyFirstLineIndent, StringComparison.Ordinal)
        || (!string.IsNullOrWhiteSpace(format.LeftIndent)
            && !string.IsNullOrWhiteSpace(format.HangingIndent));

    static bool IsSingleBorder(W.BorderType? border, uint expectedSize) =>
        border?.Val?.Value == W.BorderValues.Single
        && border.Size?.Value == expectedSize;

    static bool IsNilBorderValue(W.BorderType? border) =>
        border == null
        || border.Val?.Value == W.BorderValues.Nil
        || border.Val?.Value == W.BorderValues.None;

    static string? AlignmentValue(W.Justification? justification)
    {
        var value = justification?.Val?.Value;
        if (value == null) return null;
        if (value == W.JustificationValues.Center) return "center";
        if (value == W.JustificationValues.Left) return "left";
        if (value == W.JustificationValues.Right) return "right";
        if (value == W.JustificationValues.Both) return "both";
        if (value == W.JustificationValues.Distribute) return "distribute";
        return justification?.Val?.InnerText;
    }

    static string? LineRuleValue(W.SpacingBetweenLines? spacing)
    {
        var value = spacing?.LineRule?.Value;
        if (value == null) return null;
        if (value == W.LineSpacingRuleValues.Auto) return "auto";
        if (value == W.LineSpacingRuleValues.Exact) return "exact";
        if (value == W.LineSpacingRuleValues.AtLeast) return "atLeast";
        return spacing?.LineRule?.InnerText;
    }

    static string? BorderValue(W.BorderType? border)
    {
        var value = border?.Val?.Value;
        if (value == null) return null;
        if (value == W.BorderValues.Single) return "single";
        if (value == W.BorderValues.Nil) return "nil";
        if (value == W.BorderValues.None) return "none";
        if (value == W.BorderValues.Double) return "double";
        return border?.Val?.InnerText;
    }

    static bool IsNilBorder(string? value) =>
        value == null
        || value.Equals("nil", StringComparison.OrdinalIgnoreCase)
        || value.Equals("none", StringComparison.OrdinalIgnoreCase);

    static Dictionary<string, int> TopCounts(Dictionary<string, int> source) =>
        source
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    static void Increment(Dictionary<string, int> counts, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;
    }

    static string TrimPreview(string text)
    {
        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        return normalized.Length <= 120 ? normalized : normalized[..117] + "...";
    }

    sealed record ParagraphAudit(
        string BlockId,
        string Text,
        string? StyleId,
        string? StyleName,
        int? DirectHeadingLevel,
        int? SuspectedHeadingLevel,
        ParagraphFormat Format);

    sealed record RunChar(char Text, bool IsSubscript);

    sealed record ParagraphFormat
    {
        public string? FontEastAsia { get; init; }
        public string? FontAscii { get; init; }
        public string? FontSize { get; init; }
        public string? Alignment { get; init; }
        public string? FirstLineIndent { get; init; }
        public string? LeftIndent { get; init; }
        public string? HangingIndent { get; init; }
        public string? LineSpacing { get; init; }
        public string? LineRule { get; init; }
        public string? Before { get; init; }
        public string? After { get; init; }
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public bool KeepNext { get; init; }
        public bool MissingRunFont { get; init; }
    }

    sealed class FontAccumulator
    {
        public Dictionary<string, int> EastAsiaFonts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> AsciiFonts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> FontSizes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int MissingRunFontCount { get; private set; }

        public void Add(string? eastAsiaFont, string? asciiFont, string? fontSize, bool missingRunFont)
        {
            if (eastAsiaFont != null) Increment(EastAsiaFonts, eastAsiaFont);
            if (asciiFont != null) Increment(AsciiFonts, asciiFont);
            if (fontSize != null) Increment(FontSizes, fontSize);
            if (missingRunFont) MissingRunFontCount++;
        }
    }
}
