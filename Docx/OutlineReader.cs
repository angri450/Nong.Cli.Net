using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Extracts document outline: headings with level and text.
/// Detects Heading1-3 paragraph styles AND outlineLvl property.
/// Uses stable IDs: h0001+ for headings, sequential by document order.
/// </summary>
public static class OutlineReader
{
    public static OutlineResult ReadOutline(string docxPath)
    {
        var items = new List<OutlineItem>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return new OutlineResult(items, 0);

        int headingCounter = 0;
        foreach (var para in body.Elements<Paragraph>())
        {
            var props = para.ParagraphProperties;
            if (props == null) continue;

            string? styleId = null;
            string? styleName = null;

            // Check paragraph style for heading styles
            var paraStyle = props.ParagraphStyleId;
            if (paraStyle?.Val?.Value is string sid && sid.Length > 0)
            {
                styleId = sid;
                styleName = WordHeadingStyles.GetStyleName(doc.MainDocumentPart, sid);
            }

            var outlineLvl = props.OutlineLevel?.Val?.Value;
            var level = WordHeadingStyles.GetHeadingLevel(styleId, styleName, outlineLvl);
            if (level == null && outlineLvl.HasValue)
                styleId ??= $"outlineLvl{outlineLvl}";

            if (level == null) continue;

            var text = para.InnerText.Trim();
            if (text.Length == 0) continue;

            headingCounter++;
            items.Add(new OutlineItem(level.Value, text, styleId ?? "unknown", $"h{headingCounter:D4}"));
        }

        return new OutlineResult(items, items.Count);
    }
}
