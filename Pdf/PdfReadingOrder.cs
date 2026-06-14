namespace PdfCore;

public static class PdfReadingOrder
{
    public static PdfReadingOrderDiagnostics BuildDiagnostics(PdfDocumentModel document)
    {
        var diagnostics = new PdfReadingOrderDiagnostics();
        foreach (var page in document.Pages.OrderBy(p => p.Page))
        {
            diagnostics.Pages.Add(new PdfReadingOrderPage
            {
                Page = page.Page,
                BlockIds = document.Blocks
                    .Where(b => b.Page == page.Page)
                    .OrderBy(b => b.Index)
                    .Select(b => b.BlockId)
                    .ToList(),
                Method = page.ReadingOrderMethod
            });
        }

        if (document.Source.Classification is "hybrid" or "scan")
        {
            diagnostics.Issues.Add("Reading order for image-heavy PDFs is approximate until OCR/layout regions are available.");
        }

        return diagnostics;
    }
}
