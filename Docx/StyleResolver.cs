using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

public sealed record ResolvedParagraphProperties(
    JustificationValues? Alignment,
    double? IndentLeftCm, double? IndentRightCm, double? IndentFirstLineCm,
    double? LineSpacingCm, double? SpaceBeforePt, double? SpaceAfterPt,
    bool? KeepNext, bool? KeepLines, bool? PageBreakBefore,
    string? StyleId, bool IsFromDirect, bool IsFromStyle, bool IsFromDefaults);

public sealed class StyleResolver
{
    private readonly WordprocessingDocument _doc;

    public StyleResolver(WordprocessingDocument doc) => _doc = doc;

    public ResolvedParagraphProperties ResolveParagraph(Paragraph p)
    {
        var styles = _doc.MainDocumentPart?.StyleDefinitionsPart?.Styles;
        var docDefaults = styles?.DocDefaults;

        // Start with docDefaults
        double? lineSpacingCm = null;
        JustificationValues? alignment = null;
        double? indentLeft = null, indentRight = null, indentFirstLine = null;
        double? spaceBefore = null, spaceAfter = null;
        bool? keepNext = null, keepLines = null, pageBreakBefore = null;
        bool isFromDefaults = false, isFromStyle = false, isFromDirect = false;

        // 1. docDefaults
        var defPPr = docDefaults?.ParagraphPropertiesDefault?.GetFirstChild<ParagraphProperties>();
        if (defPPr != null)
        {
            ApplyPPr(defPPr, ref alignment, ref indentLeft, ref indentRight, ref indentFirstLine,
                ref lineSpacingCm, ref spaceBefore, ref spaceAfter, ref keepNext, ref keepLines,
                ref pageBreakBefore);
            isFromDefaults = true;
        }

        // 2. Paragraph style (basedOn recursive not implemented yet — just direct style)
        var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId != null && styles != null)
        {
            var style = styles.Elements<Style>()
                .FirstOrDefault(s => s.StyleId?.Value == styleId && s.Type == StyleValues.Paragraph);
            if (style?.StyleParagraphProperties != null)
            {
                ApplyPPr(style.StyleParagraphProperties, ref alignment, ref indentLeft, ref indentRight,
                    ref indentFirstLine, ref lineSpacingCm, ref spaceBefore, ref spaceAfter,
                    ref keepNext, ref keepLines, ref pageBreakBefore);
                isFromStyle = true;
            }
        }

        // 3. Direct paragraph properties (highest priority)
        var directPPr = p.ParagraphProperties;
        if (directPPr != null)
        {
            ApplyPPr(directPPr, ref alignment, ref indentLeft, ref indentRight, ref indentFirstLine,
                ref lineSpacingCm, ref spaceBefore, ref spaceAfter, ref keepNext, ref keepLines,
                ref pageBreakBefore);
            isFromDirect = HasDirectFormatting(directPPr);
        }

        return new ResolvedParagraphProperties(
            alignment, indentLeft, indentRight, indentFirstLine,
            lineSpacingCm, spaceBefore, spaceAfter,
            keepNext, keepLines, pageBreakBefore,
            styleId, isFromDirect, isFromStyle, isFromDefaults);
    }

    static void ApplyPPr(OpenXmlElement pPr,
        ref JustificationValues? align,
        ref double? left, ref double? right, ref double? firstLine,
        ref double? lineSpacing,
        ref double? before, ref double? after,
        ref bool? keepNext, ref bool? keepLines, ref bool? pageBreakBefore)
    {
        // Alignment
        var jc = pPr is ParagraphProperties pp ? pp.Justification : pPr.GetFirstChild<Justification>();
        if (jc?.Val?.Value is JustificationValues jv)
            align = jv;

        // Indentation
        var ind = pPr is ParagraphProperties pp2 ? pp2.Indentation : pPr.GetFirstChild<Indentation>();
        if (ind != null)
        {
            if (int.TryParse(ind.Left, out var l)) left = l / 567.0;
            if (int.TryParse(ind.Right, out var r)) right = r / 567.0;
            if (int.TryParse(ind.FirstLine, out var fl)) firstLine = fl / 567.0;
        }

        // Line spacing
        var sp = pPr is ParagraphProperties pp3 ? pp3.SpacingBetweenLines : pPr.GetFirstChild<SpacingBetweenLines>();
        if (sp?.Line != null && int.TryParse(sp.Line, out var lv))
        {
            var rule = sp.LineRule?.Value ?? LineSpacingRuleValues.Auto;
            if (rule == LineSpacingRuleValues.Auto)
                lineSpacing = lv / 240.0; // twips → lines (240 twips = 1 line)
            else
                lineSpacing = lv / 20.0; // twips → points (for exact/atLeast)
        }

        // Space before/after (twips → points)
        if (sp?.Before != null && int.TryParse(sp.Before, out var sb))
            before = sb / 20.0;
        if (sp?.After != null && int.TryParse(sp.After, out var sa))
            after = sa / 20.0;

        // KeepNext, KeepLines, PageBreakBefore
        if (pPr is ParagraphProperties pp4)
        {
            if (pp4.KeepNext != null) keepNext = true;
            if (pp4.KeepLines != null) keepLines = true;
            if (pp4.PageBreakBefore != null) pageBreakBefore = true;
        }
    }

    static bool HasDirectFormatting(ParagraphProperties pPr)
    {
        return pPr.Justification != null ||
               pPr.Indentation != null ||
               pPr.SpacingBetweenLines != null ||
               pPr.KeepNext != null ||
               pPr.KeepLines != null ||
               pPr.PageBreakBefore != null;
    }

    public ResolvedParagraphProperties ResolveCell(TableCell tc)
    {
        var styles = _doc.MainDocumentPart?.StyleDefinitionsPart?.Styles;
        var docDefaults = styles?.DocDefaults;

        JustificationValues? alignment = null;
        double? indentLeft = null, indentRight = null, indentFirstLine = null;
        double? lineSpacingCm = null, spaceBefore = null, spaceAfter = null;
        bool? keepNext = null, keepLines = null, pageBreakBefore = null;
        bool isFromDefaults = false, isFromStyle = false, isFromDirect = false;

        // 1. docDefaults
        var defPPr = docDefaults?.ParagraphPropertiesDefault?.GetFirstChild<ParagraphProperties>();
        if (defPPr != null)
        {
            ApplyPPr(defPPr, ref alignment, ref indentLeft, ref indentRight, ref indentFirstLine,
                ref lineSpacingCm, ref spaceBefore, ref spaceAfter, ref keepNext, ref keepLines,
                ref pageBreakBefore);
            isFromDefaults = true;
        }

        // 2. tableStyle
        var table = tc.Ancestors<Table>().FirstOrDefault();
        if (table != null && styles != null)
        {
            var tblPr = table.GetFirstChild<TableProperties>();
            var tblStyleId = tblPr?.TableStyle?.Val?.Value;
            if (tblStyleId != null)
            {
                var tblStyle = styles.Elements<Style>()
                    .FirstOrDefault(s => s.StyleId?.Value == tblStyleId && s.Type == StyleValues.Table);
                if (tblStyle?.StyleParagraphProperties != null)
                {
                    ApplyPPr(tblStyle.StyleParagraphProperties, ref alignment, ref indentLeft, ref indentRight,
                        ref indentFirstLine, ref lineSpacingCm, ref spaceBefore, ref spaceAfter,
                        ref keepNext, ref keepLines, ref pageBreakBefore);
                    isFromStyle = true;
                }
            }
        }

        // 4. Direct cell/paragraph properties
        var directPPr = tc.GetFirstChild<Paragraph>()?.ParagraphProperties;
        if (directPPr != null)
        {
            ApplyPPr(directPPr, ref alignment, ref indentLeft, ref indentRight, ref indentFirstLine,
                ref lineSpacingCm, ref spaceBefore, ref spaceAfter, ref keepNext, ref keepLines,
                ref pageBreakBefore);
            isFromDirect = HasDirectFormatting(directPPr);
        }

        return new ResolvedParagraphProperties(
            alignment, indentLeft, indentRight, indentFirstLine,
            lineSpacingCm, spaceBefore, spaceAfter,
            keepNext, keepLines, pageBreakBefore,
            null, isFromDirect, isFromStyle, isFromDefaults);
    }
}
