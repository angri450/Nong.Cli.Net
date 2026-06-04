using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Reads WordprocessingCommentsPart from a .docx file.
/// If the document has no comments part, returns an empty array.
/// Uses stable comment IDs: c0001+ (numbering from OpenXML id where possible).
/// </summary>
public static class CommentReader
{
    public static CommentListResult ReadComments(string docxPath)
    {
        var comments = new List<CommentInfo>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var commentsPart = doc.MainDocumentPart?.WordprocessingCommentsPart;
        if (commentsPart?.Comments == null)
            return new CommentListResult(comments, "0 comments");

        var body = doc.MainDocumentPart?.Document?.Body;

        // Build anchor info: comment id -> (blockId, anchorText)
        var anchorMap = body != null
            ? CollectAnchorMap(body)
            : new Dictionary<string, (string BlockId, string AnchorText)>();

        int cIndex = 0;
        foreach (var comment in commentsPart.Comments.Elements<Comment>())
        {
            cIndex++;

            var openXmlId = comment.Id?.Value ?? "";
            var author = comment.Author?.Value ?? "";
            DateTime? dt = comment.Date?.Value;
            var date = dt?.ToString("o") ?? "";

            // Collect text from paragraphs within the comment
            var textBuilder = new StringBuilder();
            foreach (var cmtPara in comment.Elements<Paragraph>())
            {
                if (textBuilder.Length > 0) textBuilder.Append('\n');
                textBuilder.Append(cmtPara.InnerText);
            }
            var text = textBuilder.ToString().Trim();

            // Stable comment ID: prefer c{num:D4} from OpenXML integer id, else sequential
            string commentId = FormatCommentId(openXmlId, cIndex);

            // Lookup anchor info
            string? anchorBlockId = null;
            string? anchorText = null;
            if (anchorMap.TryGetValue(openXmlId, out var anchor))
            {
                anchorBlockId = anchor.BlockId;
                anchorText = anchor.AnchorText;
            }

            comments.Add(new CommentInfo(
                Id: commentId,
                Author: author,
                Date: date,
                Text: text,
                AnchorBlockId: anchorBlockId,
                AnchorText: anchorText
            ));
        }

        string summary = comments.Count == 0
            ? "0 comments"
            : $"{comments.Count} comment{(comments.Count == 1 ? "" : "s")}";

        return new CommentListResult(comments, summary);
    }

    private static string FormatCommentId(string openXmlId, int fallbackIndex)
    {
        // Prefer OpenXML integer ID formatted as c0001+
        if (int.TryParse(openXmlId, out int cNum))
            return $"c{cNum:D4}";

        // If non-empty but non-integer, use raw id
        if (!string.IsNullOrEmpty(openXmlId))
            return openXmlId;

        // Fallback to sequential
        return $"c{fallbackIndex:D4}";
    }

    /// <summary>
    /// Iterates body paragraphs to build a map of comment id -> (blockId, anchorText).
    /// Assigns stable block IDs (h0001+/p0001+) to paragraphs along the way.
    /// </summary>
    private static Dictionary<string, (string BlockId, string AnchorText)> CollectAnchorMap(Body body)
    {
        var map = new Dictionary<string, (string BlockId, string AnchorText)>();

        int hCounter = 0, pCounter = 0;
        foreach (var para in body.Elements<Paragraph>())
        {
            string blockId = AssignBlockId(para, ref hCounter, ref pCounter);

            foreach (var crs in para.Descendants<CommentRangeStart>())
            {
                var commentId = crs.Id?.Value;
                if (string.IsNullOrEmpty(commentId)) continue;

                // Collect anchor text from siblings between CommentRangeStart and CommentRangeEnd
                var sb = new StringBuilder();
                var current = crs.NextSibling();
                while (current != null)
                {
                    if (current is CommentRangeEnd end && end.Id?.Value == commentId)
                        break;

                    sb.Append(current.InnerText);
                    current = current.NextSibling();
                }

                var anchorText = sb.ToString().Trim();

                // Track the first anchor for each comment id (a comment may have multiple ranges)
                if (!map.ContainsKey(commentId))
                {
                    map[commentId] = (blockId, anchorText.Length > 0 ? anchorText : string.Empty);
                }
            }
        }

        return map;
    }

    /// <summary>Assign a stable block ID (h0001+ or p0001+) to a paragraph.</summary>
    private static string AssignBlockId(Paragraph para, ref int hCounter, ref int pCounter)
    {
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        bool isHeading = IsHeadingStyle(styleId)
            || (para.ParagraphProperties?.OutlineLevel?.Val?.Value is int ol && ol >= 0);

        if (isHeading)
            return $"h{++hCounter:D4}";
        else
            return $"p{++pCounter:D4}";
    }

    private static bool IsHeadingStyle(string? styleId) => styleId switch
    {
        "Heading1" or "1" or "heading1" or "标题1" or "标题 1" or "Heading 1" => true,
        "Heading2" or "2" or "heading2" or "标题2" or "标题 2" or "Heading 2" => true,
        "Heading3" or "3" or "heading3" or "标题3" or "标题 3" or "Heading 3" => true,
        _ => !string.IsNullOrEmpty(styleId) && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase),
    };
}
