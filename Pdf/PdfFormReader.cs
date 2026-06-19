namespace PdfCore;

/// <summary>
/// V12.1: PDF form field reader. Uses Poppler/PDFium for form detection.
/// Note: Docnet vendor doesn't expose PDFium's FPDF_GetFormFieldCount — 
/// form field extraction limited until vendor upgrade.
/// Windows-only.
/// </summary>
public static class PdfFormReader
{
    /// <summary>Extract form fields from a PDF. Currently returns empty — pending PDFium vendor upgrade.</summary>
    public static PdfFormResult ReadFields(string pdfPath)
    {
        var result = new PdfFormResult();
        result.Warnings.Add("Form field extraction requires PDFium vendor upgrade (FPDF_GetFormFieldCount not exposed). Use Adobe Acrobat or poppler for form inspection.");
        return result;
    }
}

public sealed class PdfFormResult
{
    public List<PdfFormField> Fields { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class PdfFormField
{
    public int Page { get; set; }
    public string Type { get; set; } = "";
    public double[] Rect { get; set; } = [];
    public string Name { get; set; } = "";
}
