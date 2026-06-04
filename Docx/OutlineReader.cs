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

            int? level = null;
            string? styleId = null;

            // Check paragraph style for heading styles
            var paraStyle = props.ParagraphStyleId;
            if (paraStyle?.Val?.Value is string sid && sid.Length > 0)
            {
                styleId = sid;
                level = MapHeadingStyleToLevel(sid);
            }

            // Fallback: check outlineLvl property (0-based in OOXML, we convert to 1-based)
            if (level == null && props.OutlineLevel?.Val?.Value is int outlineLvl && outlineLvl >= 0)
            {
                level = outlineLvl + 1;
                styleId ??= $"outlineLvl{outlineLvl}";
            }

            if (level == null) continue;

            var text = para.InnerText.Trim();
            if (text.Length == 0) continue;

            headingCounter++;
            items.Add(new OutlineItem(level.Value, text, styleId ?? "unknown", $"h{headingCounter:D4}"));
        }

        return new OutlineResult(items, items.Count);
    }

    private static int? MapHeadingStyleToLevel(string styleId)
    {
        // Common heading style IDs in Chinese and English documents
        return styleId switch
        {
            "Heading1" or "1" or "heading1" or "标题1" or "标题 1" or "Heading 1" => 1,
            "Heading2" or "2" or "heading2" or "标题2" or "标题 2" or "Heading 2" => 2,
            "Heading3" or "3" or "heading3" or "标题3" or "标题 3" or "Heading 3" => 3,
            _ => TryParseHeadingPrefix(styleId)
        };
    }

    private static int? TryParseHeadingPrefix(string styleId)
    {
        // Handle Heading4-Heading9 and other "Heading" + digit patterns
        if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            var numSpan = styleId.AsSpan(7);
            if (int.TryParse(numSpan, out int hNum) && hNum >= 1 && hNum <= 9)
                return hNum;
        }
        // Handle "标题4" - "标题9" patterns
        if (styleId.StartsWith("标题", StringComparison.OrdinalIgnoreCase))
        {
            var numSpan = styleId.AsSpan(2).TrimStart();
            if (int.TryParse(numSpan, out int hNum) && hNum >= 1 && hNum <= 9)
                return hNum;
        }
        return null;
    }
}
