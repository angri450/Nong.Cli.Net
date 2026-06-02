using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Nong.Inspect;

/// <summary>
/// GB/T 7714 学术论文样式预设。包含完整的中英文样式定义和参考文献编号格式。
/// </summary>
public static class Gbt7714Style
{
    /// <summary>默认硬编码样式（GB/T 7714 标准）。</summary>
    public static void BuildAll(Styles styles)
    {
        S(styles, "Normal", "Normal", "宋体", "Times New Roman", "21", JustificationValues.Both, "420", "240");
        S(styles, "BodyTextNoIndent", "Body Text No Indent", "宋体", "Times New Roman", "21", JustificationValues.Both);
        S(styles, "Title", "Title", "黑体", "Times New Roman", "28", JustificationValues.Center, line: "360", bold: false);
        S(styles, "SubTitle", "Sub Title", "黑体", "Times New Roman", "32", JustificationValues.Center, line: "340");
        S(styles, "Heading1", "heading 1", "黑体", "Times New Roman", "32", JustificationValues.Left, line: "320", bold: true);
        S(styles, "Heading2", "heading 2", "黑体", "Times New Roman", "28", JustificationValues.Left, line: "300", bold: true);
        S(styles, "Heading3", "heading 3", "黑体", "Times New Roman", "24", JustificationValues.Left, line: "280", bold: true);
        S(styles, "AbstractTitle", "Abstract Title", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240", bold: true);
        S(styles, "Abstract", "Abstract", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "BibHeading", "Bib Heading", "宋体", "Times New Roman", "18", JustificationValues.Left, line: "240", bold: true);
        S(styles, "ReferenceText", "Reference Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240", numId: 1);
        S(styles, "FootnoteText", "Footnote Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "FootnoteReference", "Footnote Reference", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "EndnoteText", "Endnote Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "EnglishTitle", "English Title", "Times New Roman", "Times New Roman", "28", JustificationValues.Center, line: "360", bold: true);
    }

    /// <summary>参考文献编号列表：[1], [2], [3]...</summary>
    public static void BuildNumbering(Numbering num)
    {
        var abs = new AbstractNum(
            new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = NumberFormatValues.Decimal },
                new LevelText { Val = "[%1]" },
                new ParagraphProperties(new Indentation { Left = "420", Hanging = "420" })
            ) { LevelIndex = 0 }
        ) { AbstractNumberId = 1 };
        num.Append(abs);
        num.Append(new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
    }

    static void S(Styles ss, string id, string nm, string cjk, string lat, string sz, JustificationValues al, string? indent = null, string? line = null, bool bold = false, int numId = 0)
    {
        var st = new Style { Type = StyleValues.Paragraph, StyleId = id, StyleName = new StyleName { Val = nm } };
        var ppr = new StyleParagraphProperties(new Justification { Val = al });
        if (indent != null) ppr.Indentation = new Indentation { FirstLine = indent };
        if (line != null) ppr.SpacingBetweenLines = new SpacingBetweenLines { Line = line, LineRule = LineSpacingRuleValues.Auto };
        if (numId > 0) ppr.NumberingProperties = new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId });
        st.Append(ppr);
        var rpr = new StyleRunProperties(new RunFonts { Ascii = lat, HighAnsi = lat, EastAsia = cjk }, new FontSize { Val = sz });
        if (bold) rpr.Bold = new Bold();
        st.Append(rpr);
        ss.Append(st);
    }
}
