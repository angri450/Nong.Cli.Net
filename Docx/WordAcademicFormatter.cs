using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Applies deterministic academic-paper formatting to an existing DOCX.
/// This is a pure OpenXML path for agents that would otherwise fall back to
/// python-docx or desktop Word COM for ordinary academic layout polishing.
/// </summary>
public static class WordAcademicFormatter
{
    const string ChineseBodyFont = "宋体";
    const string ChineseHeadingFont = "黑体";
    const string LatinFont = "Times New Roman";
    const string BodyFontSize = "24";
    const string TitleFontSize = "44";
    const string SubtitleFontSize = "24";
    const string Heading1FontSize = "32";
    const string Heading2FontSize = "28";
    const string Heading3FontSize = "26";
    const string TableFontSize = "21";
    const string BodyLineAtLeast = "480";
    const string HeadingLineAtLeast = "560";
    const string TableLineAtLeast = "360";
    const string BodyFirstLineIndent = "480";
    const uint ThreeLineFrameBorderSize = 12; // 1.5 pt in WordprocessingML eighth-points.
    const uint ThreeLineHeaderBorderSize = 6; // 0.75 pt in WordprocessingML eighth-points.
    const string LatinSpeciesPattern = @"[A-Z][a-z]+(?:\s+x)?\s+(?!(?:et|al)\b)[a-z][a-z-]{2,}(?:\s+f\.\s*sp\.?)?";

    static readonly Regex ParenthesizedLatinRegex = new(
        @"(?<=[\u3400-\u9FFF])(\((?=[^)]*\b" + LatinSpeciesPattern + @")[^)]*\)|（(?=[^）]*\b" + LatinSpeciesPattern + @")[^）]*）)",
        RegexOptions.Compiled);

    static readonly Regex BareLatinSpeciesAfterChineseRegex = new(
        @"(?<prefix>[\u3400-\u9FFF])(?<space>[ 　\u00A0]*)(?<latin>" + LatinSpeciesPattern + @")(?<tailspace>[ 　\u00A0]*)(?=cv\.|[\u3400-\u9FFF，。；：、,.!?;:]|$)",
        RegexOptions.Compiled);

    static readonly Regex DuplicatedLatinSpeciesRegex = new(
        @"\b(?<latin>" + LatinSpeciesPattern + @")\k<latin>",
        RegexOptions.Compiled);

    static readonly Regex RepeatedFormaSpecialisRegex = new(
        @"\bf\.\s*sp\.?(?:\s+f\.\s*sp\.?)+",
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

    static readonly Regex ChineseSectionHeadingRegex = new(
        @"^[一二三四五六七八九十百]+[、.．]\S+",
        RegexOptions.Compiled);

    static readonly Regex NumberedHeadingRegex = new(
        @"^\d+(\.\d+){0,2}\s+\S+",
        RegexOptions.Compiled);

    static readonly Regex TableCaptionRegex = new(
        @"^(续表|表)\s*(\d+([-.．]\d+)*|[一二三四五六七八九十百]+)\s*([:：、.．\-－—]|\s|　).+",
        RegexOptions.Compiled);

    public sealed record AcademicFormatResult(
        string Input,
        string Output,
        int ParagraphsFormatted,
        int RunsFormatted,
        int TablesFormatted,
        int HeaderRowsFormatted,
        int LatinParentheticalRunsItalicized,
        List<string> Warnings);

    public static AcademicFormatResult Apply(string inputPath, string outputPath)
    {
        GuardDifferentPaths(inputPath, outputPath);
        File.Copy(inputPath, outputPath, true);

        var warnings = new List<string>();
        int paragraphs = 0;
        int runs = 0;
        int tables = 0;
        int headerRows = 0;
        int italicParentheses = 0;

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("MainDocumentPart is missing.");

        EnsureStyles(mainPart);
        EnsurePage(mainPart);

        var roots = EnumerateEditableRoots(mainPart).ToList();
        foreach (var root in roots)
        {
            var bodyParagraphIndex = 0;
            foreach (var paragraph in root.Descendants<W.Paragraph>())
            {
                paragraphs++;
                var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                var styleName = WordHeadingStyles.GetStyleName(mainPart, styleId);
                var role = ClassifyParagraph(paragraph, styleId, styleName, bodyParagraphIndex);
                FormatParagraph(paragraph, role);

                var paragraphRuns = paragraph.Elements<W.Run>().ToList();
                foreach (var run in paragraphRuns)
                {
                    FormatRun(run, role);
                    runs++;
                }

                italicParentheses += SplitAndItalicizeLatinParentheses(paragraph, role);
                ApplyChemicalFormulaSubscripts(paragraph, role);
                bodyParagraphIndex++;
            }

            foreach (var table in root.Descendants<W.Table>())
            {
                tables++;
                ApplyThreeLineTable(table);
                if (FormatHeaderRow(table))
                    headerRows++;
            }
        }

        mainPart.Document?.Save();
        mainPart.StyleDefinitionsPart?.Styles?.Save();

        return new AcademicFormatResult(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            paragraphs,
            runs,
            tables,
            headerRows,
            italicParentheses,
            warnings);
    }

    static IEnumerable<OpenXmlElement> EnumerateEditableRoots(MainDocumentPart mainPart)
    {
        if (mainPart.Document?.Body != null)
            yield return mainPart.Document.Body;

        foreach (var header in mainPart.HeaderParts.Select(p => p.Header).Where(h => h != null))
            yield return header!;

        foreach (var footer in mainPart.FooterParts.Select(p => p.Footer).Where(f => f != null))
            yield return footer!;

        if (mainPart.FootnotesPart?.Footnotes != null)
            yield return mainPart.FootnotesPart.Footnotes;

        if (mainPart.EndnotesPart?.Endnotes != null)
            yield return mainPart.EndnotesPart.Endnotes;
    }

    static void EnsureStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.StyleDefinitionsPart ?? mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles ??= new W.Styles();

        UpsertParagraphStyle(stylesPart.Styles, "Normal", "Normal", ChineseBodyFont, LatinFont, BodyFontSize,
            W.JustificationValues.Both, firstLine: BodyFirstLineIndent, line: BodyLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast);
        UpsertParagraphStyle(stylesPart.Styles, "BodyTextNoIndent", "Body Text No Indent", ChineseBodyFont, LatinFont, BodyFontSize,
            W.JustificationValues.Both, firstLine: null, line: BodyLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast);
        UpsertParagraphStyle(stylesPart.Styles, "Heading1", "heading 1", ChineseHeadingFont, LatinFont, Heading1FontSize,
            W.JustificationValues.Center, firstLine: null, line: HeadingLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast, bold: true, keepNext: true);
        UpsertParagraphStyle(stylesPart.Styles, "Heading2", "heading 2", ChineseHeadingFont, LatinFont, Heading2FontSize,
            W.JustificationValues.Left, firstLine: null, line: HeadingLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast, bold: true, keepNext: true);
        UpsertParagraphStyle(stylesPart.Styles, "Heading3", "heading 3", ChineseHeadingFont, LatinFont, Heading3FontSize,
            W.JustificationValues.Left, firstLine: null, line: HeadingLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast, bold: true, keepNext: true);
        UpsertParagraphStyle(stylesPart.Styles, "Title", "Title", ChineseHeadingFont, LatinFont, TitleFontSize,
            W.JustificationValues.Center, firstLine: null, line: HeadingLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast, bold: true, keepNext: true);
        UpsertParagraphStyle(stylesPart.Styles, "Caption", "Caption", ChineseBodyFont, LatinFont, TableFontSize,
            W.JustificationValues.Center, firstLine: null, line: TableLineAtLeast, lineRule: W.LineSpacingRuleValues.AtLeast, keepNext: true);
    }

    static void UpsertParagraphStyle(
        W.Styles styles,
        string id,
        string name,
        string eastAsiaFont,
        string latinFont,
        string size,
        W.JustificationValues alignment,
        string? firstLine,
        string? line,
        W.LineSpacingRuleValues lineRule,
        bool bold = false,
        bool keepNext = false)
    {
        var style = styles.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == id);
        if (style == null)
        {
            style = new W.Style { Type = W.StyleValues.Paragraph, StyleId = id };
            styles.Append(style);
        }

        style.StyleName = new W.StyleName { Val = name };

        style.StyleParagraphProperties ??= new W.StyleParagraphProperties();
        RemoveChildrenByLocalName(style.StyleParagraphProperties, "snapToGrid");
        SetOrReplace(style.StyleParagraphProperties, new W.Justification { Val = alignment });
        SetOrReplace(style.StyleParagraphProperties, new W.SpacingBetweenLines { Line = line, LineRule = lineRule, Before = "120", After = "120" });
        if (firstLine != null)
            SetOrReplace(style.StyleParagraphProperties, new W.Indentation { FirstLine = firstLine });
        else
            style.StyleParagraphProperties.RemoveAllChildren<W.Indentation>();
        if (keepNext)
            SetOrReplace(style.StyleParagraphProperties, new W.KeepNext());
        else
            style.StyleParagraphProperties.RemoveAllChildren<W.KeepNext>();

        style.StyleRunProperties ??= new W.StyleRunProperties();
        RemoveChildrenByLocalName(style.StyleRunProperties, "snapToGrid");
        SetOrReplace(style.StyleRunProperties, new W.RunFonts { Ascii = latinFont, HighAnsi = latinFont, EastAsia = eastAsiaFont });
        SetOrReplace(style.StyleRunProperties, new W.FontSize { Val = size });
        if (bold)
            SetOrReplace(style.StyleRunProperties, new W.Bold());
        else
            style.StyleRunProperties.RemoveAllChildren<W.Bold>();
    }

    static void EnsurePage(MainDocumentPart mainPart)
    {
        var body = mainPart.Document?.Body;
        if (body == null) return;

        foreach (var existingSection in body.Descendants<W.SectionProperties>())
            RemoveChildrenByLocalName(existingSection, "docGrid");

        var section = body.Elements<W.SectionProperties>().LastOrDefault();
        if (section == null)
        {
            section = new W.SectionProperties();
            body.Append(section);
        }

        SetOrReplace(section, new W.PageSize { Width = 11906, Height = 16838 });
        SetOrReplace(section, new W.PageMargin
        {
            Top = 1440,
            Right = 1440,
            Bottom = 1440,
            Left = 1440,
            Header = 720,
            Footer = 720,
            Gutter = 0,
        });
        RemoveChildrenByLocalName(section, "docGrid");
    }

    static void FormatParagraph(W.Paragraph paragraph, ParagraphRole role)
    {
        paragraph.ParagraphProperties ??= new W.ParagraphProperties();
        RemoveChildrenByLocalName(paragraph.ParagraphProperties, "snapToGrid");

        switch (role.Kind)
        {
            case ParagraphKind.Empty:
                SetParagraphStyle(paragraph.ParagraphProperties, "Normal");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Left });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "0", After = "0", Line = BodyLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });
                paragraph.ParagraphProperties.RemoveAllChildren<W.KeepNext>();
                break;
            case ParagraphKind.Title:
                SetParagraphStyle(paragraph.ParagraphProperties, "Title");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Center });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "720", After = "240", Line = "640", LineRule = W.LineSpacingRuleValues.AtLeast });
                SetOrReplace(paragraph.ParagraphProperties, new W.KeepNext());
                break;
            case ParagraphKind.Subtitle:
                SetParagraphStyle(paragraph.ParagraphProperties, "BodyTextNoIndent");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Center });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "0", After = "480", Line = BodyLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });
                paragraph.ParagraphProperties.RemoveAllChildren<W.KeepNext>();
                break;
            case ParagraphKind.Heading:
                SetParagraphStyle(paragraph.ParagraphProperties, $"Heading{Math.Clamp(role.HeadingLevel, 1, 3)}");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification
                {
                    Val = role.HeadingLevel == 1 ? W.JustificationValues.Center : W.JustificationValues.Left,
                });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines
                {
                    Before = role.HeadingLevel == 1 ? "240" : "160",
                    After = "120",
                    Line = HeadingLineAtLeast,
                    LineRule = W.LineSpacingRuleValues.AtLeast,
                });
                SetOrReplace(paragraph.ParagraphProperties, new W.KeepNext());
                break;
            case ParagraphKind.TableCaption:
                SetParagraphStyle(paragraph.ParagraphProperties, "Caption");
                RemoveChildrenByLocalName(paragraph.ParagraphProperties, "shd");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Center });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "120", After = "60", Line = TableLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });
                SetOrReplace(paragraph.ParagraphProperties, new W.KeepNext());
                break;
            case ParagraphKind.List:
                SetParagraphStyle(paragraph.ParagraphProperties, "Normal");
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Both });
                SetOrReplace(paragraph.ParagraphProperties, new W.Indentation { Left = BodyFirstLineIndent, Hanging = BodyFirstLineIndent });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "0", After = "120", Line = BodyLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });
                paragraph.ParagraphProperties.RemoveAllChildren<W.KeepNext>();
                break;
            default:
                SetParagraphStyle(paragraph.ParagraphProperties, "Normal");
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification { Val = W.JustificationValues.Both });
                SetOrReplace(paragraph.ParagraphProperties, new W.Indentation { FirstLine = BodyFirstLineIndent });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "0", After = "120", Line = BodyLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });
                paragraph.ParagraphProperties.RemoveAllChildren<W.KeepNext>();
                break;
        }

        FormatParagraphMark(paragraph.ParagraphProperties, role);
    }

    static void SetParagraphStyle(W.ParagraphProperties properties, string styleId)
    {
        SetOrReplace(properties, new W.ParagraphStyleId { Val = styleId });
    }

    static void FormatRun(W.Run run, ParagraphRole role, ItalicMode italicMode = ItalicMode.Preserve)
    {
        run.RunProperties ??= new W.RunProperties();
        RemoveChildrenByLocalName(run.RunProperties, "snapToGrid");
        if (role.Kind == ParagraphKind.TableCaption)
            RemoveChildrenByLocalName(run.RunProperties, "shd");
        SetOrReplace(run.RunProperties, new W.RunFonts
        {
            Ascii = LatinFont,
            HighAnsi = LatinFont,
            EastAsia = role.UsesHeadingFont ? ChineseHeadingFont : ChineseBodyFont,
        });
        SetOrReplace(run.RunProperties, new W.FontSize { Val = SizeFor(role) });

        if (role.Bold)
            SetOrReplace(run.RunProperties, new W.Bold());
        else
            run.RunProperties.RemoveAllChildren<W.Bold>();

        if (italicMode == ItalicMode.On)
        {
            SetOrReplace(run.RunProperties, new W.Italic());
        }
        else if (italicMode == ItalicMode.Off)
        {
            run.RunProperties.RemoveAllChildren<W.Italic>();
            run.RunProperties.RemoveAllChildren<W.ItalicComplexScript>();
        }
    }

    static void FormatParagraphMark(W.ParagraphProperties properties, ParagraphRole role)
    {
        properties.ParagraphMarkRunProperties ??= new W.ParagraphMarkRunProperties();
        RemoveChildrenByLocalName(properties.ParagraphMarkRunProperties, "snapToGrid");
        SetOrReplace(properties.ParagraphMarkRunProperties, new W.RunFonts
        {
            Ascii = LatinFont,
            HighAnsi = LatinFont,
            EastAsia = role.UsesHeadingFont ? ChineseHeadingFont : ChineseBodyFont,
        });
        SetOrReplace(properties.ParagraphMarkRunProperties, new W.FontSize { Val = SizeFor(role) });
        if (role.Kind == ParagraphKind.TableCaption)
            RemoveChildrenByLocalName(properties.ParagraphMarkRunProperties, "shd");

        if (role.Bold)
            SetOrReplace(properties.ParagraphMarkRunProperties, new W.Bold());
        else
            properties.ParagraphMarkRunProperties.RemoveAllChildren<W.Bold>();
    }

    static int SplitAndItalicizeLatinParentheses(W.Paragraph paragraph, ParagraphRole role)
    {
        if (TryRewriteTextOnlyParagraphLatinParentheses(paragraph, role, out var paragraphChanges))
            return paragraphChanges;

        var changed = 0;
        foreach (var run in paragraph.Elements<W.Run>().ToList())
        {
            if (run.Elements<W.Text>().Count() != 1)
                continue;

            var textElement = run.GetFirstChild<W.Text>();
            var text = NormalizeDuplicatedLatinSpecies(textElement?.Text ?? "");
            if (!HasAcademicLatinCandidate(text))
                continue;

            var pieces = SplitAcademicLatinPieces(text);
            if (pieces.Count <= 1)
                continue;

            foreach (var (piece, italic) in pieces)
            {
                var clone = (W.Run)run.CloneNode(true);
                clone.RemoveAllChildren<W.Text>();
                FormatRun(clone, role, italic ? ItalicMode.On : ItalicMode.Off);
                clone.Append(new W.Text(piece) { Space = SpaceProcessingModeValues.Preserve });
                run.InsertBeforeSelf(clone);
                if (italic) changed++;
            }
            run.Remove();
        }

        return changed;
    }

    static bool TryRewriteTextOnlyParagraphLatinParentheses(W.Paragraph paragraph, ParagraphRole role, out int changed)
    {
        changed = 0;

        var children = paragraph.ChildElements
            .Where(child => child is not W.ParagraphProperties)
            .ToList();
        if (children.Count == 0 || children.Any(child => child is not W.Run))
            return false;

        var runs = children.Cast<W.Run>().ToList();
        if (runs.Any(run => !IsTextOnlyRun(run)))
            return false;

        var text = NormalizeDuplicatedLatinSpecies(string.Concat(runs.Select(run => run.GetFirstChild<W.Text>()?.Text ?? "")));
        if (!HasAcademicLatinCandidate(text))
            return false;

        var pieces = SplitAcademicLatinPieces(text);
        if (pieces.Count <= 1)
            return false;

        var firstRun = runs[0];
        foreach (var (piece, italic) in pieces)
        {
            var nextRun = (W.Run)firstRun.CloneNode(true);
            nextRun.RemoveAllChildren<W.Text>();
            FormatRun(nextRun, role, italic ? ItalicMode.On : ItalicMode.Off);
            nextRun.Append(new W.Text(piece) { Space = SpaceProcessingModeValues.Preserve });
            firstRun.InsertBeforeSelf(nextRun);
            if (italic) changed++;
        }

        foreach (var run in runs)
            run.Remove();

        return true;
    }

    static bool IsTextOnlyRun(W.Run run)
    {
        var contentChildren = run.ChildElements
            .Where(child => child is not W.RunProperties)
            .ToList();
        return contentChildren.Count == 1 && contentChildren[0] is W.Text;
    }

    static bool HasAcademicLatinCandidate(string text) =>
        ParenthesizedLatinRegex.IsMatch(text)
        || BareLatinSpeciesAfterChineseRegex.IsMatch(text);

    static List<(string Text, bool Italic)> SplitAcademicLatinPieces(string text)
    {
        var pieces = new List<(string Text, bool Italic)>();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var parenthesizedMatch = ParenthesizedLatinRegex.Match(text, cursor);
            var bareMatch = BareLatinSpeciesAfterChineseRegex.Match(text, cursor);
            if (!parenthesizedMatch.Success && !bareMatch.Success)
                break;

            var useBare = !parenthesizedMatch.Success
                || (bareMatch.Success && bareMatch.Index < parenthesizedMatch.Index);
            var match = useBare ? bareMatch : parenthesizedMatch;

            if (match.Index > cursor)
                pieces.Add((text[cursor..match.Index], false));

            if (useBare)
            {
                pieces.Add((match.Groups["prefix"].Value, false));
                pieces.Add(("（", false));
                pieces.Add((match.Groups["latin"].Value, true));
                var nextIndex = match.Index + match.Length;
                var closesBeforeCultivar = match.Groups["tailspace"].Value.Length > 0
                    && text.AsSpan(nextIndex).StartsWith("cv.".AsSpan(), StringComparison.Ordinal);
                pieces.Add((closesBeforeCultivar ? "） " : "）", false));
            }
            else
            {
                var parenthesized = match.Value;
                if (parenthesized.Length >= 2)
                {
                    pieces.Add((parenthesized[..1], false));
                    pieces.Add((parenthesized[1..^1], true));
                    pieces.Add((parenthesized[^1..], false));
                }
                else
                {
                    pieces.Add((parenthesized, false));
                }
            }

            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
            pieces.Add((text[cursor..], false));

        return pieces.Where(p => p.Text.Length > 0).ToList();
    }

    static string NormalizeDuplicatedLatinSpecies(string text)
    {
        if (text.Length == 0)
            return text;

        var normalized = DuplicatedLatinSpeciesRegex.Replace(text, "${latin}");
        normalized = RepeatedFormaSpecialisRegex.Replace(normalized, "f. sp.");
        return normalized;
    }

    static void ApplyChemicalFormulaSubscripts(W.Paragraph paragraph, ParagraphRole role)
    {
        foreach (var run in paragraph.Elements<W.Run>().ToList())
        {
            if (run.Elements<W.Text>().Count() != 1)
                continue;

            var textElement = run.GetFirstChild<W.Text>();
            var text = textElement?.Text ?? "";
            if (!MightContainChemicalFormula(text))
                continue;

            var pieces = SplitChemicalFormulaPieces(text);
            if (pieces.Count <= 1 || !pieces.Any(p => p.Subscript))
                continue;

            foreach (var piece in pieces)
            {
                var clone = (W.Run)run.CloneNode(true);
                clone.RemoveAllChildren<W.Text>();
                FormatRun(clone, role, ItalicMode.Preserve);
                ApplySubscript(clone, piece.Subscript);
                clone.Append(new W.Text(piece.Text) { Space = SpaceProcessingModeValues.Preserve });
                run.InsertBeforeSelf(clone);
            }

            run.Remove();
        }
    }

    static bool MightContainChemicalFormula(string text) =>
        text.Any(char.IsDigit) && ChemicalFormulaCandidateRegex.IsMatch(text);

    static List<ChemicalPiece> SplitChemicalFormulaPieces(string text)
    {
        var pieces = new List<ChemicalPiece>();
        var cursor = 0;

        foreach (Match match in ChemicalFormulaCandidateRegex.Matches(text))
        {
            var formula = match.Groups["formula"].Value;
            if (!TryTokenizeChemicalFormula(formula, out var formulaPieces))
                continue;

            if (match.Index > cursor)
                pieces.Add(new ChemicalPiece(text[cursor..match.Index], false));

            pieces.AddRange(formulaPieces);
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
            pieces.Add(new ChemicalPiece(text[cursor..], false));

        return pieces.Where(p => p.Text.Length > 0).ToList();
    }

    static bool TryTokenizeChemicalFormula(string formula, out List<ChemicalPiece> pieces)
    {
        pieces = new List<ChemicalPiece>();
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
                pieces.Add(new ChemicalPiece(formula[i].ToString(), false));
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
            pieces.Add(new ChemicalPiece(symbol, false));
            i++;

            var numberStart = i;
            while (i < formula.Length && char.IsDigit(formula[i]))
                i++;

            if (i > numberStart)
            {
                numberCount++;
                pieces.Add(new ChemicalPiece(formula[numberStart..i], true));
            }
        }

        if (numberCount == 0)
            return false;

        if (elementCount == 1 && !SingleElementSubscriptFormulas.Contains(formula.TrimEnd('+', '-')))
            return false;

        return true;
    }

    static void ApplySubscript(W.Run run, bool subscript)
    {
        run.RunProperties ??= new W.RunProperties();
        run.RunProperties.RemoveAllChildren<W.VerticalTextAlignment>();
        if (subscript)
        {
            SetOrReplace(run.RunProperties, new W.VerticalTextAlignment
            {
                Val = W.VerticalPositionValues.Subscript,
            });
        }
    }

    static void ApplyThreeLineTable(W.Table table)
    {
        table.TableProperties ??= new W.TableProperties();
        RemoveChildrenByLocalName(table.TableProperties, "tblLook");
        table.TableProperties.RemoveAllChildren<W.TableStyle>();
        table.TableProperties.RemoveAllChildren<W.Shading>();
        SetOrReplace(table.TableProperties, new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" });
        SetOrReplace(table.TableProperties, new W.TableJustification { Val = W.TableRowAlignmentValues.Center });
        SetOrReplace(table.TableProperties, new W.TableLayout { Type = W.TableLayoutValues.Fixed });
        SetOrReplace(table.TableProperties, new W.TableCellMarginDefault(
            new W.TopMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellLeftMargin { Width = 120, Type = W.TableWidthValues.Dxa },
            new W.BottomMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellRightMargin { Width = 120, Type = W.TableWidthValues.Dxa }));
        SetOrReplace(table.TableProperties, new W.TableBorders(
            new W.TopBorder { Val = W.BorderValues.Single, Size = ThreeLineFrameBorderSize, Color = "000000" },
            new W.LeftBorder { Val = W.BorderValues.Nil },
            new W.BottomBorder { Val = W.BorderValues.Single, Size = ThreeLineFrameBorderSize, Color = "000000" },
            new W.RightBorder { Val = W.BorderValues.Nil },
            new W.InsideHorizontalBorder { Val = W.BorderValues.Nil },
            new W.InsideVerticalBorder { Val = W.BorderValues.Nil }));

        EnsureTableGrid(table);

        var columnCount = table.Elements<W.TableRow>()
            .Select(row => row.Elements<W.TableCell>().Count())
            .DefaultIfEmpty(1)
            .Max();
        var cellWidth = Math.Max(1, 5000 / Math.Max(1, columnCount)).ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var cell in table.Descendants<W.TableCell>())
        {
            cell.TableCellProperties ??= new W.TableCellProperties();
            cell.TableCellProperties.RemoveAllChildren<W.Shading>();
            SetOrReplace(cell.TableCellProperties, new W.TableCellWidth { Type = W.TableWidthUnitValues.Pct, Width = cellWidth });
            SetOrReplace(cell.TableCellProperties, new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center });
            SetOrReplace(cell.TableCellProperties, new W.TableCellMargin(
                new W.TopMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
                new W.LeftMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa },
                new W.BottomMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
                new W.RightMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa }));

            foreach (var paragraph in cell.Elements<W.Paragraph>())
            {
                paragraph.ParagraphProperties ??= new W.ParagraphProperties();
                RemoveChildrenByLocalName(paragraph.ParagraphProperties, "snapToGrid");
                paragraph.ParagraphProperties.RemoveAllChildren<W.Shading>();
                var text = paragraph.InnerText.Trim();
                SetOrReplace(paragraph.ParagraphProperties, new W.Justification
                {
                    Val = ShouldLeftAlignTableText(text) ? W.JustificationValues.Left : W.JustificationValues.Center,
                });
                SetOrReplace(paragraph.ParagraphProperties, new W.Indentation { FirstLine = "0" });
                SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines { Before = "60", After = "60", Line = TableLineAtLeast, LineRule = W.LineSpacingRuleValues.AtLeast });

                foreach (var run in paragraph.Elements<W.Run>())
                    FormatTableRun(run);
            }
        }
    }

    static void EnsureTableGrid(W.Table table)
    {
        var colCount = table.Elements<W.TableRow>()
            .Select(row => row.Elements<W.TableCell>().Count())
            .DefaultIfEmpty(0)
            .Max();
        if (colCount <= 0) return;

        table.RemoveAllChildren<W.TableGrid>();
        var grid = new W.TableGrid();
        var colWidth = Math.Max(1, 9000 / colCount).ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < colCount; i++)
            grid.Append(new W.GridColumn { Width = colWidth });

        var props = table.GetFirstChild<W.TableProperties>();
        if (props != null)
            table.InsertAfter(grid, props);
        else
            table.PrependChild(grid);
    }

    static bool FormatHeaderRow(W.Table table)
    {
        var firstRow = table.Elements<W.TableRow>().FirstOrDefault();
        if (firstRow == null) return false;

        firstRow.TableRowProperties ??= new W.TableRowProperties();
        SetOrReplace(firstRow.TableRowProperties, new W.TableHeader());

        foreach (var cell in firstRow.Elements<W.TableCell>())
        {
            cell.TableCellProperties ??= new W.TableCellProperties();
            cell.TableCellProperties.RemoveAllChildren<W.Shading>();
            SetOrReplace(cell.TableCellProperties, new W.TableCellBorders(
                new W.BottomBorder { Val = W.BorderValues.Single, Size = ThreeLineHeaderBorderSize, Color = "000000" }));

            foreach (var run in cell.Descendants<W.Run>())
            {
                FormatTableRun(run);
                run.RunProperties ??= new W.RunProperties();
                SetOrReplace(run.RunProperties, new W.Bold());
            }
        }

        return true;
    }

    static ParagraphRole ClassifyParagraph(W.Paragraph paragraph, string? styleId, string? styleName, int paragraphIndex)
    {
        var text = paragraph.InnerText.Trim();
        if (text.Length == 0)
            return ParagraphRole.Empty;

        var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
        var sharedHeadingLevel = WordHeadingStyles.GetHeadingLevel(styleId, styleName, outlineLevel, text);
        if (sharedHeadingLevel is >= 1 and <= 3)
            return ParagraphRole.Heading(sharedHeadingLevel.Value);

        if (!string.IsNullOrWhiteSpace(styleId))
        {
            if (styleId.Equals("Title", StringComparison.OrdinalIgnoreCase))
                return ParagraphRole.Title;
            if (styleId.Equals("Caption", StringComparison.OrdinalIgnoreCase))
                return ParagraphRole.TableCaption;
        }

        if (paragraphIndex <= 2 && LooksLikeCoverTitle(text))
            return ParagraphRole.Title;

        if (paragraphIndex <= 4 && LooksLikeCoverSubtitle(text))
            return ParagraphRole.Subtitle;

        if (LooksLikeTableCaption(text))
            return ParagraphRole.TableCaption;

        if (ChineseSectionHeadingRegex.IsMatch(text))
            return ParagraphRole.Heading(1);

        if (NumberedHeadingRegex.IsMatch(text))
            return ParagraphRole.Heading(text.Count(c => c == '.') >= 2 ? 3 : 2);

        if (LooksLikeListText(text))
            return ParagraphRole.List;

        return ParagraphRole.Body;
    }

    static bool LooksLikeCoverTitle(string text)
    {
        if (text.Length < 8 || text.Length > 80)
            return false;

        return text.Contains("方案书", StringComparison.Ordinal)
            || text.Contains("计划书", StringComparison.Ordinal)
            || text.Contains("报告", StringComparison.Ordinal)
            || text.Contains("论文", StringComparison.Ordinal)
            || text.Contains("工作站", StringComparison.Ordinal);
    }

    static bool LooksLikeCoverSubtitle(string text) =>
        text.Contains("编制日期", StringComparison.Ordinal)
        || text.Contains("日期", StringComparison.Ordinal)
        || text.Contains("学院", StringComparison.Ordinal)
        || text.Contains("大学", StringComparison.Ordinal)
        || text.Contains("集团", StringComparison.Ordinal);

    static bool LooksLikeListText(string text) =>
        text.StartsWith("• ", StringComparison.Ordinal)
        || Regex.IsMatch(text, @"^[(（]?\d+[)）、.．]\s*\S+");

    static bool LooksLikeTableCaption(string text) =>
        TableCaptionRegex.IsMatch(text);

    static bool ShouldLeftAlignTableText(string text) =>
        text.Length >= 12
        || text.Contains('，')
        || text.Contains('。')
        || text.Contains(';')
        || text.Contains('；')
        || text.Contains('：');

    static void FormatTableRun(W.Run run)
    {
        run.RunProperties ??= new W.RunProperties();
        RemoveChildrenByLocalName(run.RunProperties, "snapToGrid");
        run.RunProperties.RemoveAllChildren<W.Shading>();
        SetOrReplace(run.RunProperties, new W.RunFonts
        {
            Ascii = LatinFont,
            HighAnsi = LatinFont,
            EastAsia = ChineseBodyFont,
        });
        SetOrReplace(run.RunProperties, new W.FontSize { Val = TableFontSize });
    }

    static string SizeFor(ParagraphRole role) => role.Kind switch
    {
        ParagraphKind.Title => TitleFontSize,
        ParagraphKind.Subtitle => SubtitleFontSize,
        ParagraphKind.Heading when role.HeadingLevel == 1 => Heading1FontSize,
        ParagraphKind.Heading when role.HeadingLevel == 2 => Heading2FontSize,
        ParagraphKind.Heading => Heading3FontSize,
        ParagraphKind.TableCaption => TableFontSize,
        _ => BodyFontSize,
    };

    readonly record struct ParagraphRole(ParagraphKind Kind, int HeadingLevel)
    {
        public static ParagraphRole Empty => new(ParagraphKind.Empty, 0);
        public static ParagraphRole Body => new(ParagraphKind.Body, 0);
        public static ParagraphRole List => new(ParagraphKind.List, 0);
        public static ParagraphRole Title => new(ParagraphKind.Title, 1);
        public static ParagraphRole Subtitle => new(ParagraphKind.Subtitle, 0);
        public static ParagraphRole TableCaption => new(ParagraphKind.TableCaption, 0);
        public static ParagraphRole Heading(int level) => new(ParagraphKind.Heading, level);

        public bool UsesHeadingFont => Kind is ParagraphKind.Title or ParagraphKind.Heading;
        public bool Bold => Kind is ParagraphKind.Title or ParagraphKind.Heading;
    }

    enum ParagraphKind
    {
        Empty,
        Body,
        List,
        Title,
        Subtitle,
        Heading,
        TableCaption,
    }

    enum ItalicMode
    {
        Preserve,
        On,
        Off,
    }

    readonly record struct ChemicalPiece(string Text, bool Subscript);

    static void SetOrReplace<TContainer, TChild>(TContainer container, TChild child)
        where TContainer : OpenXmlCompositeElement
        where TChild : OpenXmlElement
    {
        container.RemoveAllChildren<TChild>();
        container.Append(child);
    }

    static void RemoveChildrenByLocalName(OpenXmlCompositeElement container, string localName)
    {
        foreach (var child in container.ChildElements.Where(e => e.LocalName == localName).ToList())
            child.Remove();
    }

    static void GuardDifferentPaths(string inputPath, string outputPath)
    {
        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output paths must be different.");
    }
}
