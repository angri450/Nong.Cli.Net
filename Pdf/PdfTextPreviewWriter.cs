using System.Text;

namespace PdfCore;

public static class PdfTextPreviewWriter
{
    public static string Write(PdfDocumentModel document)
    {
        var sb = new StringBuilder();
        foreach (var block in document.Blocks.OrderBy(b => b.Index))
        {
            if (block.Kind == "pageBreak")
            {
                sb.AppendLine();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(block.Text))
                sb.AppendLine(block.Text.Trim());
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}
