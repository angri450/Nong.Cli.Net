using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Reads text content from a .docx file without truncation.
/// Separate from WordPreview which has a 3000-char diagnostic focus.
/// </summary>
public static class WordTextReader
{
    public static WordTextResult Read(string docxPath)
    {
        var paragraphs = new List<string>();
        var tables = new List<string>();
        var footnotes = new List<string>();
        var endnotes = new List<string>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return new WordTextResult("", paragraphs, tables, footnotes, endnotes);

        // Body paragraphs
        foreach (var para in body.Elements<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                paragraphs.Add(text);
        }

        // Tables
        foreach (var table in body.Elements<Table>())
        {
            var rows = new List<string>();
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>()
                    .Select(c => c.InnerText.Trim())
                    .Where(t => t.Length > 0);
                var rowText = string.Join("\t", cells);
                if (rowText.Length > 0) rows.Add(rowText);
            }
            if (rows.Count > 0) tables.Add(string.Join("\n", rows));
        }

        // Footnotes
        var fnPart = doc.MainDocumentPart?.FootnotesPart;
        if (fnPart?.Footnotes != null)
        {
            foreach (var fn in fnPart.Footnotes.Elements<Footnote>())
            {
                var text = fn.InnerText.Trim();
                if (text.Length > 0) footnotes.Add(text);
            }
        }

        // Endnotes
        var enPart = doc.MainDocumentPart?.EndnotesPart;
        if (enPart?.Endnotes != null)
        {
            foreach (var en in enPart.Endnotes.Elements<Endnote>())
            {
                var text = en.InnerText.Trim();
                if (text.Length > 0) endnotes.Add(text);
            }
        }

        var allText = string.Join("\n", paragraphs);
        if (tables.Count > 0) allText += "\n\n" + string.Join("\n\n", tables);
        if (footnotes.Count > 0) allText += "\n\n--- Footnotes ---\n" + string.Join("\n", footnotes);
        if (endnotes.Count > 0) allText += "\n\n--- Endnotes ---\n" + string.Join("\n", endnotes);

        return new WordTextResult(allText.Trim(), paragraphs, tables, footnotes, endnotes);
    }
}

public sealed record WordTextResult(
    string Text,
    List<string> Paragraphs,
    List<string> Tables,
    List<string> Footnotes,
    List<string> Endnotes
);
