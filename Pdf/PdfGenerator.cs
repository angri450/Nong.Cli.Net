using SkiaSharp;

namespace PdfCore;

/// <summary>
/// V12.1: Structured PDF generator using SkiaSharp (replaces PdfPig PdfDocumentBuilder).
/// Windows-only: uses system fonts for CJK support.
/// </summary>
public static class PdfGenerator
{
    /// <summary>Create a PDF from text blocks with CJK font support.</summary>
    public static void CreateTextPdf(string outputPath, IReadOnlyList<PdfTextBlock> blocks,
        float pageW = 595, float pageH = 842, float margin = 60, float lineHeight = 14)
    {
        using var doc = SKDocument.CreatePdf(outputPath);
        var typeface = GetCjkTypeface();
        var paintNormal = new SKPaint { IsAntialias = true, Color = SKColors.Black, TextSize = 10, Typeface = typeface };
        var paintBold = new SKPaint { IsAntialias = true, Color = SKColors.Black, TextSize = 14, Typeface = typeface };

        var canvas = doc.BeginPage(pageW, pageH);
        float y = margin;
        int lineOnPage = 0, pageNum = 1;
        float usableH = pageH - 2 * margin;
        int maxLines = (int)(usableH / lineHeight);

        foreach (var block in blocks)
        {
            var paint = block.IsHeading ? paintBold : paintNormal;
            foreach (var line in WrapLines(block.Text, 80))
            {
                if (lineOnPage >= maxLines)
                {
                    DrawPageNumber(canvas, pageNum, margin, pageH);
                    doc.EndPage();
                    pageNum++;
                    canvas = doc.BeginPage(pageW, pageH);
                    y = margin; lineOnPage = 0;
                }
                canvas.DrawText(line, margin, y + paint.TextSize, paint);
                y += lineHeight + 2; lineOnPage++;
            }
        }
        DrawPageNumber(canvas, pageNum, margin, pageH);
        doc.EndPage();
        doc.Close();
    }

    static SKTypeface GetCjkTypeface()
    {
        return SKTypeface.FromFamilyName("Microsoft YaHei")
            ?? SKTypeface.FromFamilyName("SimSun")
            ?? SKTypeface.FromFamilyName("SimHei")
            ?? SKTypeface.FromFamilyName("Arial")
            ?? SKTypeface.Default;
    }

    static IEnumerable<string> WrapLines(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        int start = 0;
        while (start < text.Length)
        {
            int len = Math.Min(maxChars, text.Length - start);
            yield return text.Substring(start, len);
            start += len;
        }
    }

    static void DrawPageNumber(SKCanvas canvas, int page, float margin, float pageH)
    {
        var paint = new SKPaint { IsAntialias = true, Color = SKColors.Gray, TextSize = 8 };
        paint.Typeface = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        canvas.DrawText($"{page}", margin, pageH - margin + 10, paint);
    }
}

/// <summary>Simple text block for PDF generation.</summary>
public sealed record PdfTextBlock(string Text, bool IsHeading = false);
