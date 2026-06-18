using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using System.Text.Json;

namespace DocxCore;

/// <summary>
/// Builds a DOCX directly from authored NongMark text.
/// This is the long-document generation path for agents: write NongMark once,
/// then create the Word package in one deterministic OpenXML pass.
/// </summary>
public sealed class NongMarkDocumentBuilder
{
    readonly List<string> _warnings = new();
    W.Body _body = null!;
    MainDocumentPart _mainPart = null!;
    string _baseDir = "";
    string? _lastHeadingText;
    int _paragraphs;
    int _headings;
    int _tables;
    int _images;
    int _equations;
    int _references;
    int _footnotes;
    int _endnotes;
    // Per-paragraph font state, reset each paragraph
    string _fontEastAsia = "宋体";
    string _fontAscii = "Times New Roman";
    string _fontSizeHalfPt = "21";   // 10.5pt = 五号, OOXML uses half-points

    // Bug 8: format specs and style→block mappings from frontmatter
    readonly Dictionary<string, NongMarkFormatSpec> _formats = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<string>> _styleToBlocks = new(StringComparer.Ordinal);
    // Maps blockId → Paragraph reference for style application
    readonly Dictionary<string, W.Paragraph> _blockIdToParagraph = new(StringComparer.Ordinal);
    int _blockSeq;

    // V5: pending tab stops for the next paragraph (from frontmatter tabs: field)
    DocxTabStops? _pendingTabStops;

    static readonly Regex AttributeRegex = new(
        @"(?<key>[\w-]+)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s}]+))",
        RegexOptions.Compiled);

    static readonly Regex BlockIdRegex = new(@"^[pmtcdine]\d{4}$", RegexOptions.Compiled);

    /// <summary>Format specification for a named style (Bug 8).</summary>
    sealed record NongMarkFormatSpec
    {
        public string? FontEastAsia;
        public string? FontAscii;
        public string? FontSizePt;
        public bool Bold;
        public bool Italic;
        public string? Alignment;      // left, center, right, both
        public string? SpacingBefore;   // twips
        public string? SpacingAfter;    // twips
        public string? LineSpacing;     // twips
        public string? LineRule;        // auto, exact, atLeast
        public string? Color;
    }

    public static NongMarkBuildResult Build(string inputPath, string outputPath)
    {
        var builder = new NongMarkDocumentBuilder();
        return builder.BuildInternal(inputPath, outputPath);
    }

    NongMarkBuildResult BuildInternal(string inputPath, string outputPath)
    {
        _baseDir = Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Directory.GetCurrentDirectory();
        var lines = File.ReadAllLines(inputPath);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        _mainPart = doc.AddMainDocumentPart();
        _mainPart.Document = new W.Document(new W.Body());
        _body = _mainPart.Document.Body!;

        var stylesPart = _mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new W.Styles();
        StyleBuilder.BuildAll(stylesPart.Styles);
        stylesPart.Styles.Save();

        var contentStart = SkipFrontMatterAndApplyTitle(lines);
        ProcessLines(lines.Skip(contentStart).ToArray());

        // Bug 8: apply format styles from frontmatter to matching paragraphs
        ApplyFormatStyles();

        AppendSectionProperties();
        _mainPart.Document.Save();

        return new NongMarkBuildResult(
            Input: Path.GetFullPath(inputPath),
            Output: Path.GetFullPath(outputPath),
            Blocks: _paragraphs + _headings + _tables + _images + _equations + _references + _footnotes + _endnotes,
            Paragraphs: _paragraphs,
            Headings: _headings,
            Tables: _tables,
            Images: _images,
            Equations: _equations,
            References: _references,
            Footnotes: _footnotes,
            Endnotes: _endnotes,
            Warnings: _warnings);
    }

    int SkipFrontMatterAndApplyTitle(string[] lines)
    {
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return 0;

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var frontLines = new List<string>();
        var i = 1;
        for (; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "---")
            {
                i++;
                break;
            }
            frontLines.Add(lines[i]); // preserve original indentation
        }

        // Parse frontmatter with indent-aware parsing (Bug 8)
        ParseFrontMatterBlock(frontLines, metadata, _formats, _styleToBlocks);

        if (metadata.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
            AppendTitle(title);
        if (metadata.TryGetValue("author", out var author) && !string.IsNullOrWhiteSpace(author))
            AppendCentered(author, "BodyTextNoIndent");
        if (metadata.TryGetValue("date", out var date) && !string.IsNullOrWhiteSpace(date))
            AppendCentered(date, "BodyTextNoIndent");

        return i;
    }

    /// <summary>
    /// Bug 8: indent-aware frontmatter parser for nested format/style blocks.
    /// Lines at indent 0 become top-level keys (title, author, date, format, styles).
    /// Lines indented by 2+ spaces become child keys of the most recent parent.
    /// </summary>
    void ParseFrontMatterBlock(
        List<string> lines,
        Dictionary<string, string> metadata,
        Dictionary<string, NongMarkFormatSpec> formats,
        Dictionary<string, List<string>> styleToBlocks)
    {
        string? currentSection = null;
        string? currentFormatName = null;
        NongMarkFormatSpec? currentSpec = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            int indent = rawLine.Length - rawLine.TrimStart().Length;

            // Top-level key
            if (indent == 0 && line.Contains(':'))
            {
                currentSection = null;
                currentFormatName = null;
                currentSpec = null;
                var colon = line.IndexOf(':');
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (value.Length > 0)
                {
                    metadata[key] = value;
                }
                else
                {
                    // Nested section: format, styles, etc.
                    currentSection = key;
                }
                continue;
            }

            // Indented: second-level key (format name) or third-level property
            if (indent >= 2 && currentSection == "format" && line.Contains(':'))
            {
                var colon = line.IndexOf(':');
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (value.Length > 0)
                {
                    // Property of current format spec
                    if (currentSpec != null)
                        ApplyFormatProperty(currentSpec, key, value);
                }
                else
                {
                    // New format name
                    currentFormatName = key;
                    currentSpec = new NongMarkFormatSpec();
                    formats[currentFormatName] = currentSpec;
                }
                continue;
            }

            if (indent >= 2 && currentSection == "styles" && line.Contains(':'))
            {
                var colon = line.IndexOf(':');
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                var blockIds = ParseBlockIdList(value);
                if (blockIds.Count > 0)
                    styleToBlocks[key] = blockIds;
                continue;
            }

            // Indented but unknown section — treat as simple key:value
            if (indent >= 2 && currentSection != null && line.Contains(':'))
            {
                continue; // ignore unrecognized nested keys
            }
        }
    }

    static List<string> ParseBlockIdList(string value)
    {
        var list = new List<string>();
        // Match p0001, m0001, t0001, etc.
        foreach (Match m in BlockIdRegex.Matches(value))
            list.Add(m.Value);
        return list;
    }

    static void ApplyFormatProperty(NongMarkFormatSpec spec, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "fonteastasia": spec.FontEastAsia = value; break;
            case "fontascii": spec.FontAscii = value; break;
            case "fontsizep": case "fontsizept": spec.FontSizePt = value; break;
            case "bold": spec.Bold = ParseBool(value); break;
            case "italic": spec.Italic = ParseBool(value); break;
            case "alignment": spec.Alignment = value; break;
            case "spacingbefore": spec.SpacingBefore = value; break;
            case "spacingafter": spec.SpacingAfter = value; break;
            case "linespacing": spec.LineSpacing = value; break;
            case "linerule": spec.LineRule = value; break;
            case "color": spec.Color = value; break;
        }
    }

    static bool ParseBool(string v) =>
        v.Equals("true", StringComparison.OrdinalIgnoreCase)
        || v == "1" || v.Equals("yes", StringComparison.OrdinalIgnoreCase);

    void ProcessLines(IReadOnlyList<string> lines)
    {
        var paragraph = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph(paragraph);
                continue;
            }

            if (trimmed.StartsWith("<!--", StringComparison.Ordinal) && trimmed.EndsWith("-->", StringComparison.Ordinal))
                continue;

            if (trimmed.StartsWith(":::", StringComparison.Ordinal) && trimmed != ":::")
            {
                FlushParagraph(paragraph);
                var header = trimmed[3..].Trim();
                var blockLines = new List<string>();
                var closed = false;
                while (++i < lines.Count)
                {
                    if (lines[i].Trim() == ":::")
                    {
                        closed = true;
                        break;
                    }
                    blockLines.Add(lines[i]);
                }

                if (!closed)
                    throw new InvalidDataException($"NongMark block '{header}' is missing closing :::.");

                AppendBlock(header, blockLines);
                continue;
            }

            if (trimmed == "$$")
            {
                FlushParagraph(paragraph);
                var mathLines = new List<string>();
                var closed = false;
                while (++i < lines.Count)
                {
                    if (lines[i].Trim() == "$$")
                    {
                        closed = true;
                        break;
                    }
                    mathLines.Add(lines[i]);
                }
                if (!closed)
                    throw new InvalidDataException("Display equation is missing closing $$.");
                AppendEquation(string.Join(Environment.NewLine, mathLines).Trim(), display: true);
                continue;
            }

            if (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal) && trimmed.Length > 4)
            {
                FlushParagraph(paragraph);
                AppendEquation(trimmed[2..^2].Trim(), display: true);
                continue;
            }

            if (TryParseHeading(trimmed, out var level, out var heading))
            {
                FlushParagraph(paragraph);
                AppendHeading(heading, level);
                continue;
            }

            if (TryParseImage(trimmed, out var caption, out var path))
            {
                FlushParagraph(paragraph);
                AppendImage(path, caption);
                continue;
            }

            if (IsPipeTableLine(trimmed))
            {
                FlushParagraph(paragraph);
                var tableLines = new List<string> { line };
                while (i + 1 < lines.Count && IsPipeTableLine(lines[i + 1].Trim()))
                    tableLines.Add(lines[++i]);
                AppendTable(null, ParsePipeTable(tableLines));
                continue;
            }

            if (IsListLine(trimmed))
            {
                FlushParagraph(paragraph);
                AppendParagraph("• " + trimmed[2..].Trim(), "Normal");
                continue;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph(paragraph);
    }

    void AppendBlock(string header, IReadOnlyList<string> blockLines)
    {
        var (kind, attrs) = ParseBlockHeader(header);
        var content = string.Join(Environment.NewLine, blockLines).Trim();

        switch (kind.ToLowerInvariant())
        {
            case "page":
            case "section":
                ProcessLines(blockLines);
                break;
            case "title":
                ResetParagraphState();
                AppendTitle(content);
                break;
            case "heading":
                ResetParagraphState();
                AppendHeading(content, GetInt(attrs, "level", 1));
                break;
            case "paragraph":
                ApplyParagraphAttrs(attrs);
                AppendParagraph(JoinParagraphLines(blockLines), attrs.TryGetValue("style", out var style) ? style : "Normal");
                ResetParagraphState();
                break;
            case "table":
                AppendTable(attrs.TryGetValue("caption", out var tableCaption) ? tableCaption : null, ParsePipeTable(blockLines));
                break;
            case "image":
            case "figure":
                if (!attrs.TryGetValue("src", out var src) && !attrs.TryGetValue("path", out src))
                    throw new ArgumentException("NongMark image block requires src= or path=.");
                AppendImage(src, attrs.TryGetValue("caption", out var figCaption) ? figCaption : null);
                break;
            case "math":
            case "equation":
                AppendEquation(content, GetBool(attrs, "display", true));
                break;
            case "toc":
                TocAndChartBuilder.AppendTableOfContents(_body, attrs.TryGetValue("title", out var tocTitle) ? tocTitle : "目录");
                break;
            case "footnote":
                AppendFootnote(content);
                break;
            case "endnote":
                AppendEndnote(content);
                break;
            case "references":
            case "reference":
            case "bibliography":
                AppendReferences(blockLines, attrs);
                break;
            case "pagebreak":
            case "break":
                AppendPageBreak();
                break;
            case "warning":
                _warnings.Add(content);
                break;
            default:
                _warnings.Add($"Unknown NongMark block '{kind}' was rendered as a paragraph.");
                AppendParagraph(JoinParagraphLines(blockLines), "Normal");
                break;
        }
    }

    void FlushParagraph(List<string> paragraph)
    {
        if (paragraph.Count == 0) return;
        // V5: extract frontmatter lines (key: value) before joining text
        _pendingTabStops = null;
        var contentLines = new List<string>();
        foreach (var line in paragraph)
        {
            var trimmed = line.Trim();
            if (TryParseFrontmatterLine(trimmed, out var key, out var value))
            {
                if (key == "tabs")
                    _pendingTabStops = ParseTabsFrontmatter(value);
            }
            else
            {
                contentLines.Add(line);
            }
        }
        var text = JoinParagraphLines(contentLines);
        if (string.IsNullOrWhiteSpace(text)) return;
        AppendParagraph(text, "Normal");
        paragraph.Clear();
    }

    static bool TryParseFrontmatterLine(string line, out string key, out string value)
    {
        key = "";
        value = "";
        var colon = line.IndexOf(':');
        if (colon <= 0 || colon >= line.Length - 1) return false;
        key = line[..colon].Trim();
        if (key.Contains(' ')) return false;
        value = line[(colon + 1)..].Trim();
        return key.Length > 0 && value.Length > 0;
    }

    static DocxTabStops? ParseTabsFrontmatter(string value)
    {
        var inner = value.Trim('[', ']', ' ');
        if (string.IsNullOrEmpty(inner)) return null;
        var tabs = new DocxTabStops();
        foreach (var part in inner.Split(','))
        {
            var spec = ParseTabStopSpec(part.Trim());
            if (spec != null) tabs.Add(spec);
        }
        return tabs.Stops.Count > 0 ? tabs : null;
    }

    static TabStopSpec? ParseTabStopSpec(string part)
    {
        var parts = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var posStr = parts[0];
        if (!posStr.EndsWith("cm") || !double.TryParse(posStr[..^2], out var posCm)) return null;

        var alignment = TabAlignment.Left;
        var leader = TabLeader.None;

        for (int i = 1; i < parts.Length; i++)
        {
            var token = parts[i].ToLowerInvariant();
            switch (token)
            {
                case "left": alignment = TabAlignment.Left; break;
                case "center": alignment = TabAlignment.Center; break;
                case "right": alignment = TabAlignment.Right; break;
                case "decimal": alignment = TabAlignment.Decimal; break;
                case "bar": alignment = TabAlignment.Bar; break;
                case "num": alignment = TabAlignment.Num; break;
                case "dot": leader = TabLeader.Dot; break;
                case "hyphen": leader = TabLeader.Hyphen; break;
                case "underscore": leader = TabLeader.Underscore; break;
                case "heavy": leader = TabLeader.Heavy; break;
                case "middledot": leader = TabLeader.MiddleDot; break;
                case "none": leader = TabLeader.None; break;
            }
        }

        return new TabStopSpec(posCm, alignment, leader);
    }

    static string JoinParagraphLines(IEnumerable<string> lines) =>
        string.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0)).Trim();

    static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = "";
        var i = 0;
        while (i < line.Length && line[i] == '#') i++;
        if (i is < 1 or > 6 || i >= line.Length || line[i] != ' ')
            return false;

        level = Math.Min(i, 3);
        text = StripTrailingAttributes(line[(i + 1)..]).Trim();
        return text.Length > 0;
    }

    static string StripTrailingAttributes(string text)
    {
        var trimmed = text.TrimEnd();
        if (!trimmed.EndsWith("}", StringComparison.Ordinal)) return trimmed;
        var open = trimmed.LastIndexOf('{');
        return open > 0 ? trimmed[..open].TrimEnd() : trimmed;
    }

    static bool TryParseImage(string line, out string caption, out string path)
    {
        caption = "";
        path = "";
        if (!line.StartsWith("![", StringComparison.Ordinal)) return false;
        var closeAlt = line.IndexOf("](", StringComparison.Ordinal);
        if (closeAlt < 2 || !line.EndsWith(")", StringComparison.Ordinal)) return false;
        caption = line[2..closeAlt].Trim();
        path = line[(closeAlt + 2)..^1].Trim();
        return path.Length > 0;
    }

    static bool IsListLine(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal);

    static bool IsPipeTableLine(string line) =>
        line.StartsWith("|", StringComparison.Ordinal) && line.EndsWith("|", StringComparison.Ordinal)
        && line.Count(c => c == '|') >= 2;

    static List<string[]> ParsePipeTable(IEnumerable<string> lines)
    {
        var rows = new List<string[]>();
        foreach (var line in lines)
        {
            if (!IsPipeTableLine(line.Trim())) continue;
            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            // Bug 9: preserve empty-cell rows — an all-empty row is still a valid table row.
            // Only skip if there are zero cells (malformed line).
            if (cells.Length == 0) continue;
            if (cells.All(IsTableSeparatorCell)) continue;
            rows.Add(cells);
        }

        if (rows.Count == 0)
            throw new ArgumentException("NongMark table has no rows.");
        return rows;
    }

    static bool IsTableSeparatorCell(string value) =>
        value.Length > 0 && value.All(c => c is '-' or ':' or ' ');

    static (string kind, Dictionary<string, string> attrs) ParseBlockHeader(string header)
    {
        var brace = header.IndexOf('{');
        var kindPart = brace >= 0 ? header[..brace].Trim() : header.Trim();
        var kind = kindPart.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("NongMark block kind is empty.");

        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (brace >= 0)
        {
            var end = header.LastIndexOf('}');
            var rawAttrs = end > brace ? header[(brace + 1)..end] : header[(brace + 1)..];
            foreach (Match match in AttributeRegex.Matches(rawAttrs))
            {
                var value = match.Groups["dq"].Success ? match.Groups["dq"].Value
                    : match.Groups["sq"].Success ? match.Groups["sq"].Value
                    : match.Groups["bare"].Value;
                attrs[match.Groups["key"].Value] = value;
            }
        }

        return (kind, attrs);
    }

    static int GetInt(IReadOnlyDictionary<string, string> attrs, string key, int fallback) =>
        attrs.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    static bool GetBool(IReadOnlyDictionary<string, string> attrs, string key, bool fallback) =>
        attrs.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;

    void AppendTitle(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = "Title" },
                new W.Justification { Val = W.JustificationValues.Center }));
        AppendInlineRuns(paragraph, text, defaultBold: true);
        AppendBeforeSectPr(paragraph);
        TrackBlock(paragraph, "p");
        _headings++;
        _lastHeadingText = text;
    }

    void AppendHeading(string text, int level)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        level = Math.Clamp(level, 1, 3);
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = $"Heading{level}" }));
        AppendInlineRuns(paragraph, text, defaultBold: true);
        AppendBeforeSectPr(paragraph);
        TrackBlock(paragraph, "h");
        _headings++;
        _lastHeadingText = text;
    }

    void AppendCentered(string text, string styleId)
    {
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = styleId },
                new W.Justification { Val = W.JustificationValues.Center }));
        AppendInlineRuns(paragraph, text);
        AppendBeforeSectPr(paragraph);
        TrackBlock(paragraph, "p");
        _paragraphs++;
    }

    void AppendParagraph(string text, string styleId)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId }));
        AppendInlineRuns(paragraph, text);
        AppendBeforeSectPr(paragraph);
        TrackBlock(paragraph, "p");
        _paragraphs++;

        // V5: apply pending tab stops from frontmatter
        if (_pendingTabStops != null && _pendingTabStops.Stops.Count > 0)
        {
            var ppr = paragraph.GetFirstChild<W.ParagraphProperties>();
            if (ppr == null) { ppr = new W.ParagraphProperties(); paragraph.PrependChild(ppr); }
            _pendingTabStops.ApplyTo(ppr);
            _pendingTabStops = null;
        }
    }

    /// <summary>Track paragraph blockId for style application (Bug 8).</summary>
    void TrackBlock(W.Paragraph para, string prefix)
    {
        _blockSeq++;
        var blockId = $"{prefix}{_blockSeq:D4}";
        _blockIdToParagraph[blockId] = para;
    }

    /// <summary>Apply format specs to matching paragraphs after document body is built (Bug 8).</summary>
    void ApplyFormatStyles()
    {
        if (_formats.Count == 0 || _styleToBlocks.Count == 0) return;

        foreach (var (styleName, blockIds) in _styleToBlocks)
        {
            if (!_formats.TryGetValue(styleName, out var spec)) continue;

            foreach (var blockId in blockIds)
            {
                if (!_blockIdToParagraph.TryGetValue(blockId, out var para)) continue;
                ApplySpecToParagraph(para, spec);
            }
        }
    }

    void ApplySpecToParagraph(W.Paragraph para, NongMarkFormatSpec spec)
    {
        var ppr = para.GetFirstChild<W.ParagraphProperties>();
        if (ppr == null)
        {
            ppr = new W.ParagraphProperties();
            para.PrependChild(ppr);
        }

        // Alignment
        if (!string.IsNullOrEmpty(spec.Alignment))
        {
            var existingJc = ppr.GetFirstChild<W.Justification>();
            existingJc?.Remove();
            ppr.Append(new W.Justification { Val = spec.Alignment.ToLowerInvariant() switch
            {
                "left" => W.JustificationValues.Left,
                "right" => W.JustificationValues.Right,
                "center" => W.JustificationValues.Center,
                "both" => W.JustificationValues.Both,
                _ => W.JustificationValues.Left
            }});
        }

        // Spacing
        if (!string.IsNullOrEmpty(spec.SpacingBefore) || !string.IsNullOrEmpty(spec.SpacingAfter) ||
            !string.IsNullOrEmpty(spec.LineSpacing))
        {
            var sp = ppr.GetFirstChild<W.SpacingBetweenLines>();
            if (sp == null)
            {
                sp = new W.SpacingBetweenLines();
                ppr.Append(sp);
            }
            if (!string.IsNullOrEmpty(spec.SpacingBefore) && int.TryParse(spec.SpacingBefore, out var sb))
                sp.Before = sb.ToString();
            if (!string.IsNullOrEmpty(spec.SpacingAfter) && int.TryParse(spec.SpacingAfter, out var sa))
                sp.After = sa.ToString();
            if (!string.IsNullOrEmpty(spec.LineSpacing) && int.TryParse(spec.LineSpacing, out var ls))
            {
                sp.Line = ls.ToString();
                if (!string.IsNullOrEmpty(spec.LineRule))
                    sp.LineRule = spec.LineRule.ToLowerInvariant() switch
                    {
                        "exact" => W.LineSpacingRuleValues.Exact,
                        "atleast" => W.LineSpacingRuleValues.AtLeast,
                        _ => W.LineSpacingRuleValues.Auto
                    };
            }
        }

        // Run-level formatting: font, size, bold, italic, color
        foreach (var run in para.Descendants<W.Run>())
        {
            var rpr = run.GetFirstChild<W.RunProperties>();
            if (rpr == null)
            {
                rpr = new W.RunProperties();
                run.PrependChild(rpr);
            }

            if (!string.IsNullOrEmpty(spec.FontEastAsia) || !string.IsNullOrEmpty(spec.FontAscii))
            {
                var rf = rpr.GetFirstChild<W.RunFonts>();
                if (rf == null)
                {
                    rf = new W.RunFonts();
                    rpr.Append(rf);
                }
                if (!string.IsNullOrEmpty(spec.FontEastAsia))
                    rf.EastAsia = spec.FontEastAsia;
                if (!string.IsNullOrEmpty(spec.FontAscii))
                {
                    rf.Ascii = spec.FontAscii;
                    rf.HighAnsi = spec.FontAscii;
                }
            }

            if (!string.IsNullOrEmpty(spec.FontSizePt) && double.TryParse(spec.FontSizePt, out var fsPt))
            {
                var rfs = rpr.GetFirstChild<W.FontSize>();
                rfs?.Remove();
                rpr.Append(new W.FontSize { Val = ((int)(fsPt * 2)).ToString() });
                rpr.Append(new W.FontSizeComplexScript { Val = ((int)(fsPt * 2)).ToString() });
            }

            if (spec.Bold)
            {
                if (rpr.GetFirstChild<W.Bold>() == null)
                    rpr.Append(new W.Bold());
            }
            if (spec.Italic)
            {
                if (rpr.GetFirstChild<W.Italic>() == null)
                    rpr.Append(new W.Italic());
            }

            if (!string.IsNullOrEmpty(spec.Color))
            {
                var c = rpr.GetFirstChild<W.Color>();
                c?.Remove();
                rpr.Append(new W.Color { Val = spec.Color.StartsWith("#") ? spec.Color[1..] : spec.Color });
            }
        }
    }

    void AppendReferences(IReadOnlyList<string> lines, IReadOnlyDictionary<string, string> attrs)
    {
        var refs = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        if (refs.Length == 0) return;

        var title = attrs.TryGetValue("title", out var explicitTitle) ? explicitTitle : "参考文献";
        var shouldAddHeading = !string.Equals(_lastHeadingText, title, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_lastHeadingText, "References", StringComparison.OrdinalIgnoreCase);
        if (shouldAddHeading)
            AppendHeading(title, GetInt(attrs, "level", 1));

        foreach (var reference in refs)
        {
            var paragraph = new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = "Normal" },
                    new W.Indentation { Left = "420", Hanging = "420" }));
            AppendInlineRuns(paragraph, reference);
            AppendBeforeSectPr(paragraph);
            _references++;
        }
    }

    void AppendTable(string? caption, List<string[]> rows)
    {
        if (!string.IsNullOrWhiteSpace(caption))
            AppendCentered(caption, "BodyTextNoIndent");

        var colCount = rows.Max(r => r.Length);
        var table = new W.Table(
            new W.TableProperties(
                new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 6, Color = "000000" },
                    new W.LeftBorder { Val = W.BorderValues.None },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Color = "000000" },
                    new W.RightBorder { Val = W.BorderValues.None },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Color = "000000" },
                    new W.InsideVerticalBorder { Val = W.BorderValues.None }),
                new W.TableLayout { Type = W.TableLayoutValues.Fixed }));

        var grid = new W.TableGrid();
        for (var i = 0; i < colCount; i++)
            grid.Append(new W.GridColumn());
        table.Append(grid);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new W.TableRow();
            for (var col = 0; col < colCount; col++)
            {
                var value = col < rows[rowIndex].Length ? rows[rowIndex][col] : "";
                row.Append(MakeCell(value, rowIndex == 0));
            }
            table.Append(row);
        }

        AppendBeforeSectPr(table);
        AppendBeforeSectPr(new W.Paragraph());
        _tables++;
    }

    W.TableCell MakeCell(string text, bool header)
    {
        var cell = new W.TableCell(
            new W.TableCellProperties(
                new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center }));

        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = "BodyTextNoIndent" },
                new W.Justification { Val = W.JustificationValues.Center },
                new W.SpacingBetweenLines { Before = "40", After = "40" }));
        AppendInlineRuns(paragraph, text, defaultBold: header);
        cell.Append(paragraph);
        return cell;
    }

    void AppendImage(string imagePath, string? caption)
    {
        var fullPath = ResolvePath(imagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Image file not found: {imagePath}", fullPath);

        ImageEmbedder.EmbedSingleImage(_body, _mainPart, fullPath, caption);
        _images++;
    }

    void AppendEquation(string latex, bool display)
    {
        if (string.IsNullOrWhiteSpace(latex)) return;
        if (display)
            AppendBeforeSectPr(MathRenderer.RenderDisplay(latex));
        else
        {
            var paragraph = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Normal" }));
            paragraph.Append(new W.Run(MathRenderer.RenderInline(latex)));
            AppendBeforeSectPr(paragraph);
        }
        _equations++;
    }

    void AppendFootnote(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var fnPart = _mainPart.FootnotesPart ?? _mainPart.AddNewPart<FootnotesPart>();
        fnPart.Footnotes ??= new W.Footnotes(
            new W.Footnote(new W.Paragraph()) { Id = 0 },
            new W.Footnote(new W.Paragraph()) { Id = -1 });

        var id = fnPart.Footnotes.Elements<W.Footnote>()
            .Select(f => (int?)f.Id?.Value)
            .Where(n => n.HasValue && n.Value > 0)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        fnPart.Footnotes.Append(new W.Footnote(
            new W.Paragraph(new W.Run(new W.Text(text)))) { Id = id });
        fnPart.Footnotes.Save();

        var paragraph = new W.Paragraph(new W.Run(new W.FootnoteReference { Id = id }));
        AppendBeforeSectPr(paragraph);
        _footnotes++;
    }

    void AppendEndnote(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var enPart = _mainPart.EndnotesPart ?? _mainPart.AddNewPart<EndnotesPart>();
        enPart.Endnotes ??= new W.Endnotes(
            new W.Endnote(new W.Paragraph()) { Id = 0 },
            new W.Endnote(new W.Paragraph()) { Id = -1 });

        var id = enPart.Endnotes.Elements<W.Endnote>()
            .Select(e => (int?)e.Id?.Value)
            .Where(n => n.HasValue && n.Value > 0)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        enPart.Endnotes.Append(new W.Endnote(
            new W.Paragraph(new W.Run(new W.Text(text)))) { Id = id });
        enPart.Endnotes.Save();

        var paragraph = new W.Paragraph(new W.Run(new W.EndnoteReference { Id = id }));
        AppendBeforeSectPr(paragraph);
        _endnotes++;
    }

    void AppendPageBreak()
    {
        AppendBeforeSectPr(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
    }

    void AppendInlineRuns(W.Paragraph paragraph, string text, bool defaultBold = false)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (TryAppendLink(paragraph, text, ref index))
                continue;

            if (text.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end > index + 2)
                {
                    AppendRun(paragraph, text[(index + 2)..end], bold: true);
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] == '*')
            {
                var end = text.IndexOf('*', index + 1);
                if (end > index + 1)
                {
                    AppendRun(paragraph, text[(index + 1)..end], italic: true, bold: defaultBold);
                    index = end + 1;
                    continue;
                }
            }

            var next = NextInlineMarker(text, index + 1);
            AppendPlainTextRuns(paragraph, text[index..next], bold: defaultBold);
            index = next;
        }
    }

    bool TryAppendLink(W.Paragraph paragraph, string text, ref int index)
    {
        if (text[index] != '[') return false;
        var close = text.IndexOf("](", index, StringComparison.Ordinal);
        if (close <= index + 1) return false;
        var end = text.IndexOf(')', close + 2);
        if (end <= close + 2) return false;

        var label = text[(index + 1)..close];
        var url = text[(close + 2)..end];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var rel = _mainPart.AddHyperlinkRelationship(uri, true);
        var hyperlink = new W.Hyperlink { Id = rel.Id, History = true };
        hyperlink.Append(MakeRun(label, bold: false, italic: false, hyperlink: true));
        paragraph.Append(hyperlink);
        index = end + 1;
        return true;
    }

    static int NextInlineMarker(string text, int start)
    {
        var nextBold = text.IndexOf("**", start, StringComparison.Ordinal);
        var nextItalic = text.IndexOf('*', start);
        var nextLink = text.IndexOf('[', start);
        return new[] { nextBold, nextItalic, nextLink }
            .Where(i => i >= 0)
            .DefaultIfEmpty(text.Length)
            .Min();
    }

    void AppendRun(W.Paragraph paragraph, string text, bool bold = false, bool italic = false)
    {
        if (text.Length == 0) return;
        paragraph.Append(MakeRun(text, bold, italic, hyperlink: false));
    }

    void AppendPlainTextRuns(W.Paragraph paragraph, string text, bool bold)
    {
        var index = 0;
        while (index < text.Length)
        {
            var open = FindNextParenthesis(text, index, out var closeChar);
            if (open < 0)
            {
                AppendRun(paragraph, text[index..], bold);
                return;
            }

            if (open > index)
                AppendRun(paragraph, text[index..open], bold);

            var close = text.IndexOf(closeChar, open + 1);
            if (close < 0)
            {
                AppendRun(paragraph, text[open..], bold);
                return;
            }

            var inner = text[(open + 1)..close];
            AppendRun(paragraph, text[open].ToString(), bold);
            AppendRun(paragraph, inner, bold, italic: ContainsLatin(inner));
            AppendRun(paragraph, text[close].ToString(), bold);
            index = close + 1;
        }
    }

    static int FindNextParenthesis(string text, int start, out char closeChar)
    {
        var cjk = text.IndexOf('（', start);
        var ascii = text.IndexOf('(', start);
        if (cjk < 0 && ascii < 0)
        {
            closeChar = ')';
            return -1;
        }

        if (cjk >= 0 && (ascii < 0 || cjk < ascii))
        {
            closeChar = '）';
            return cjk;
        }

        closeChar = ')';
        return ascii;
    }

    static bool ContainsLatin(string text) =>
        text.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    W.Run MakeRun(string text, bool bold, bool italic, bool hyperlink)
    {
        var props = new W.RunProperties(
            new W.RunFonts { Ascii = _fontAscii, HighAnsi = _fontAscii, EastAsia = _fontEastAsia });
        if (bold) props.Append(new W.Bold());
        if (italic) props.Append(new W.Italic());
        if (hyperlink)
        {
            props.Append(new W.Color { Val = "0563C1" });
            props.Append(new W.Underline { Val = W.UnderlineValues.Single });
        }
        props.Append(new W.FontSize { Val = _fontSizeHalfPt });
        return new W.Run(props, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    void ApplyParagraphAttrs(IReadOnlyDictionary<string, string> attrs)
    {
        if (attrs.TryGetValue("font", out var font))
            _fontEastAsia = font;
        if (attrs.TryGetValue("fontAscii", out var fontAscii))
            _fontAscii = fontAscii;
        if (attrs.TryGetValue("size", out var sizeStr) && double.TryParse(sizeStr, out var sizePt))
            _fontSizeHalfPt = ((int)(sizePt * 2)).ToString();
        if (attrs.TryGetValue("sizeHalfPt", out var shp))
            _fontSizeHalfPt = shp;
    }

    void ResetParagraphState()
    {
        _fontEastAsia = "宋体";
        _fontAscii = "Times New Roman";
        _fontSizeHalfPt = "21";
    }

    string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_baseDir, path));

    void AppendSectionProperties()
    {
        if (_body.Elements<W.SectionProperties>().Any()) return;
        _body.Append(new W.SectionProperties(
            new W.PageSize { Width = 11906, Height = 16838 },
            new W.PageMargin { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440, Header = 720, Footer = 720, Gutter = 0 }));
    }

    void AppendBeforeSectPr(OpenXmlElement element)
    {
        var sectionProperties = _body.Elements<W.SectionProperties>().LastOrDefault();
        if (sectionProperties == null)
            _body.Append(element);
        else
            _body.InsertBefore(element, sectionProperties);
    }
}

public sealed record NongMarkBuildResult(
    string Input,
    string Output,
    int Blocks,
    int Paragraphs,
    int Headings,
    int Tables,
    int Images,
    int Equations,
    int References,
    int Footnotes,
    int Endnotes,
    List<string> Warnings);
