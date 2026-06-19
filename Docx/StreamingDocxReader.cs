using System.Xml;

namespace DocxCore;

/// <summary>
/// V12.2: Streaming DOCX reader — reads paragraph-by-paragraph using XmlReader.
/// Avoids loading the entire document into memory (anti-OOM for large files).
/// </summary>
public static class StreamingDocxReader
{
    /// <summary>Stream paragraphs from a DOCX file. Yields one paragraph at a time.</summary>
    public static IEnumerable<string> ReadParagraphs(string docxPath)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(docxPath);
        var docEntry = zip.GetEntry("word/document.xml");
        if (docEntry == null) yield break;

        using var stream = docEntry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = false, IgnoreWhitespace = true });

        bool inParagraph = false;
        var paraText = new System.Text.StringBuilder();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "p" && reader.NamespaceURI.Contains("wordprocessing"))
                {
                    inParagraph = true;
                    paraText.Clear();
                }
                else if (reader.LocalName == "t" && inParagraph && reader.NamespaceURI.Contains("wordprocessing"))
                {
                    if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                        paraText.Append(reader.Value);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p" && inParagraph)
            {
                inParagraph = false;
                if (paraText.Length > 0)
                    yield return paraText.ToString();
            }
        }
    }

    /// <summary>Estimate word count from a DOCX without reading all content.</summary>
    public static long EstimateWordCount(string docxPath)
    {
        long count = 0;
        foreach (var p in ReadParagraphs(docxPath))
            count += p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return count;
    }
}
