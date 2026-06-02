using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// OOXML 重建器——参照党政公文排版 skill 的完整 pPr/rPr 重建策略。
/// 不修补已有属性，而是整棵扔掉后按规范从头构建。
/// 这是消除 WPS/Office 遗留样式污染的唯一可靠方法。
/// </summary>
public static class StyleRebuilder
{
    /// <summary>重建段落属性：丢掉旧的 pPr，用给定参数重建。</summary>
    public static void RebuildParagraphProperties(Paragraph para, string styleId, string? firstLineIndent = null, string? lineSpacing = null, JustificationValues? align = null)
    {
        // 扔掉旧的
        var old = para.ParagraphProperties;
        if (old != null) para.RemoveChild(old);

        // 新建
        var ppr = new ParagraphProperties();
        ppr.ParagraphStyleId = new ParagraphStyleId { Val = styleId };
        if (firstLineIndent != null) ppr.Indentation = new Indentation { FirstLine = firstLineIndent };
        if (lineSpacing != null) ppr.SpacingBetweenLines = new SpacingBetweenLines { Line = lineSpacing, LineRule = LineSpacingRuleValues.Auto };
        if (align != null) ppr.Justification = new Justification { Val = align.Value };

        ppr.KeepNext = null; // 显式清除 Word 遗留的 keepNext
        ppr.KeepLines = null;
        ppr.WidowControl = null;

        para.InsertAt(ppr, 0);
    }

    /// <summary>重建 Run 属性：丢掉旧的 rPr，用给定参数重建。</summary>
    public static void RebuildRunProperties(Run run, string fontLatin, string fontCJK, string fontSize, bool bold = false, bool italic = false)
    {
        var old = run.RunProperties;
        if (old != null) run.RemoveChild(old);

        var rpr = new RunProperties();
        rpr.RunFonts = new RunFonts
        {
            Ascii = fontLatin, HighAnsi = fontLatin,
            EastAsia = fontCJK, ComplexScript = fontCJK
        };
        rpr.FontSize = new FontSize { Val = fontSize };
        rpr.FontSizeComplexScript = new FontSizeComplexScript { Val = fontSize };
        if (bold) rpr.Bold = new Bold();
        else rpr.Bold = new Bold { Val = false }; // 显式关闭，防继承
        if (italic) rpr.Italic = new Italic();
        else rpr.Italic = new Italic { Val = false };

        // 清除主题字体（这是 WPS/Office 样式污染的主要来源）
        rpr.RunFonts.AsciiTheme = null;
        rpr.RunFonts.HighAnsiTheme = null;
        rpr.RunFonts.EastAsiaTheme = null;
        rpr.RunFonts.ComplexScriptTheme = null;

        run.InsertAt(rpr, 0);
    }

    /// <summary>对文档中所有段落统一重建样式。</summary>
    public static void RebuildAllParagraphs(WordprocessingDocument doc, string bodyFontCJK = "宋体", string bodyFontLatin = "Times New Roman", string bodyFontSize = "21", string indent = "420", string lineSpacing = "312")
    {
        var body = doc.MainDocumentPart?.Document?.Body ?? throw new InvalidOperationException("Document body is missing.");
        foreach (var para in body.Elements<Paragraph>())
        {
            var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            if (styleId is "Normal" or "")
            {
                RebuildParagraphProperties(para, "Normal", indent, lineSpacing, JustificationValues.Both);
                foreach (var run in para.Elements<Run>())
                    RebuildRunProperties(run, bodyFontLatin, bodyFontCJK, bodyFontSize);
            }
        }
    }
}
