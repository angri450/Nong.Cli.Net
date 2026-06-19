using System.Runtime.InteropServices;

namespace PdfCore;

/// <summary>
/// V12.1: Real PDF form field reader via PDFium P/Invoke.
/// Docnet vendor doesn't expose FPDF_GetFormFieldCount — we call PDFium directly.
/// </summary>
public static class PdfFormReader
{
    public static PdfFormResult ReadFields(string pdfPath)
    {
        var result = new PdfFormResult();

        try
        {
            using var reader = DocNet.DocLib.Instance.GetDocReader(pdfPath);
            int pageCount = reader.GetPageCount();

            for (int i = 1; i <= pageCount; i++)
            {
                using var page = reader.GetPageReader(i);
                // Get the raw PDFium page handle via reflection (Docnet hides it)
                var pagePtr = GetPagePointer(page);
                if (pagePtr == IntPtr.Zero) continue;

                var formCount = FPDF_GetFormFieldCount(pagePtr);
                for (int j = 0; j < formCount; j++)
                {
                    var field = new PdfFormField { Page = i };
                    var buf = new byte[512];
                    var len = FPDF_GetFormFieldName(pagePtr, j, buf, (uint)buf.Length);
                    if (len > 0)
                        field.Name = System.Text.Encoding.UTF8.GetString(buf, 0, (int)Math.Min(len, buf.Length - 1));
                    else
                        field.Name = $"field_{j}";

                    var type = FPDF_GetFormFieldType(pagePtr, j);
                    field.Type = type switch
                    {
                        0 => "Unknown",
                        1 => "PushButton",
                        2 => "CheckBox",
                        3 => "RadioButton",
                        4 => "ComboBox",
                        5 => "ListBox",
                        6 => "TextField",
                        7 => "Signature",
                        _ => "Unknown"
                    };

                    // Get bounding rectangle
                    float left = 0, bottom = 0, right = 0, top = 0;
                    if (FPDF_GetFormFieldRect(pagePtr, j, ref left, ref bottom, ref right, ref top))
                        field.Rect = new[] { (double)left, (double)bottom, (double)right, (double)top };

                    result.Fields.Add(field);
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Form extraction error: {ex.Message}");
        }

        return result;
    }

    static IntPtr GetPagePointer(object docnetPage)
    {
        try
        {
            var field = docnetPage.GetType().GetField("_page",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (IntPtr)field.GetValue(docnetPage)! : IntPtr.Zero;
        }
        catch { return IntPtr.Zero; }
    }

    // PDFium C API (subset for form fields)
    const string PdfiumLib = "pdfium_x64";

    [DllImport(PdfiumLib, CallingConvention = CallingConvention.Cdecl)]
    static extern int FPDF_GetFormFieldCount(IntPtr page);

    [DllImport(PdfiumLib, CallingConvention = CallingConvention.Cdecl)]
    static extern int FPDF_GetFormFieldType(IntPtr page, int index);

    [DllImport(PdfiumLib, CallingConvention = CallingConvention.Cdecl)]
    static extern uint FPDF_GetFormFieldName(IntPtr page, int index, byte[] buffer, uint buflen);

    [DllImport(PdfiumLib, CallingConvention = CallingConvention.Cdecl)]
    static extern bool FPDF_GetFormFieldRect(IntPtr page, int index, ref float left, ref float bottom, ref float right, ref float top);
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
    public string Name { get; set; } = "";
    public double[] Rect { get; set; } = [];
}
