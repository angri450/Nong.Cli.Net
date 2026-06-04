using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Finds all tracked changes (insertions, deletions, moves) in a .docx document.
/// Each revision gets a stable revisionId (rev0001+) and blockId of its containing paragraph.
/// </summary>
public static class RevisionReader
{
    private const int SnippetMaxChars = 80;

    public static RevisionListResult ReadRevisions(string docxPath)
    {
        var snippets = new List<RevisionSnippet>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
            return new RevisionListResult(0, 0, 0, 0, snippets);

        int revCounter = 0;
        int insertions = 0;
        int deletions = 0;
        int moves = 0;

        int hCounter = 0, pCounter = 0;

        // Walk all paragraphs in body (including nested in tables/SDTs) in document order
        foreach (var para in body.Descendants<Paragraph>())
        {
            string blockId = AssignBlockId(para, ref hCounter, ref pCounter);

            // 1. Insertions: w:ins elements within this paragraph
            foreach (var ins in para.Descendants<Inserted>())
            {
                insertions++;
                var text = ins.InnerText;
                var snippet = Truncate(text, SnippetMaxChars);
                var author = ins.Author?.Value ?? "";
                var date = ins.Date?.Value.ToString("o") ?? "";
                if (snippet.Length > 0)
                {
                    revCounter++;
                    snippets.Add(new RevisionSnippet(
                        RevisionId: $"rev{revCounter:D4}",
                        BlockId: blockId,
                        Type: "insertion",
                        Snippet: snippet,
                        Author: author,
                        Date: date
                    ));
                }
            }

            // 2. Deletions: w:del elements within this paragraph
            foreach (var del in para.Descendants<Deleted>())
            {
                deletions++;
                var text = del.InnerText;
                var snippet = Truncate(text, SnippetMaxChars);
                var author = del.Author?.Value ?? "";
                var date = del.Date?.Value.ToString("o") ?? "";
                if (snippet.Length > 0)
                {
                    revCounter++;
                    snippets.Add(new RevisionSnippet(
                        RevisionId: $"rev{revCounter:D4}",
                        BlockId: blockId,
                        Type: "deletion",
                        Snippet: snippet,
                        Author: author,
                        Date: date
                    ));
                }
            }

            // 3. Moves: run-level MoveFrom/MoveTo markers within this paragraph
            foreach (var run in para.Descendants<Run>())
            {
                var rp = run.RunProperties;
                if (rp == null) continue;

                bool isMoveFrom = rp.GetFirstChild<MoveFrom>() != null;
                bool isMoveTo = rp.GetFirstChild<MoveTo>() != null;
                if (!isMoveFrom && !isMoveTo) continue;

                moves++;
                string typeLabel = isMoveFrom ? "moveFrom" : "moveTo";
                var text = run.InnerText;
                var snippet = Truncate(text, SnippetMaxChars);

                // Author/date from the marker element
                var marker = isMoveFrom
                    ? (OpenXmlElement)rp.GetFirstChild<MoveFrom>()!
                    : rp.GetFirstChild<MoveTo>()!;
                var author = (marker as MoveFrom)?.Author?.Value
                    ?? (marker as MoveTo)?.Author?.Value
                    ?? "";
                var moveDate = (marker as MoveFrom)?.Date?.Value;
                var date = moveDate?.ToString("o") ?? "";
                if (date.Length == 0)
                {
                    moveDate = (marker as MoveTo)?.Date?.Value;
                    date = moveDate?.ToString("o") ?? "";
                }

                if (snippet.Length > 0)
                {
                    revCounter++;
                    snippets.Add(new RevisionSnippet(
                        RevisionId: $"rev{revCounter:D4}",
                        BlockId: blockId,
                        Type: typeLabel,
                        Snippet: snippet,
                        Author: author,
                        Date: date
                    ));
                }
            }
        }

        // Count paragraph-level MoveFromRangeStart/MoveToRangeStart markers
        int paragraphMoves = body.Descendants<MoveFromRangeStart>().Count()
            + body.Descendants<MoveToRangeStart>().Count();
        if (paragraphMoves > 0)
        {
            moves += paragraphMoves;
        }

        int total = insertions + deletions + moves;

        return new RevisionListResult(total, insertions, deletions, moves, snippets);
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

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var trimmed = text.Trim();
        return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars] + "...";
    }
}
