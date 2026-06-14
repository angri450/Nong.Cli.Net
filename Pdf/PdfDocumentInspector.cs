using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfCore;

public static class PdfDocumentInspector
{
    public static PdfCheckResult Check(string pdfPath)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        var fullPath = Path.GetFullPath(pdfPath);

        try
        {
            using var document = PdfDocument.Open(fullPath);
            var result = new PdfCheckResult
            {
                Input = Path.GetFileName(pdfPath),
                FullPath = fullPath,
                FileSize = new FileInfo(fullPath).Length,
                Sha256 = PdfUtilities.Sha256(fullPath),
                PageCount = document.NumberOfPages,
            };

            foreach (var page in document.GetPages())
            {
                var textChars = CountMeaningfulChars(page.Text);
                var imageCoverage = EstimateImageCoverage(page);
                var textQuality = AnalyzeTextQuality(page);
                var pageCheck = new PdfPageCheck
                {
                    Page = page.Number,
                    Width = page.Width,
                    Height = page.Height,
                    TextCharCount = textChars,
                    ImageCount = page.NumberOfImages,
                    ImageCoverageRatio = imageCoverage,
                    SuspiciousTextRatio = textQuality.SuspiciousRatio,
                    SuspectFonts = textQuality.SuspectFonts,
                };
                result.Pages.Add(pageCheck);
            }

            result.TextCharCount = result.Pages.Sum(p => p.TextCharCount);
            result.TextCharsPerPage = result.PageCount == 0 ? 0 : result.TextCharCount / (double)result.PageCount;
            result.ImageCount = result.Pages.Sum(p => p.ImageCount);
            result.ImageCoverageRatio = result.Pages.Count == 0 ? 0 : result.Pages.Average(p => p.ImageCoverageRatio);
            result.SuspiciousTextRatio = result.Pages.Count == 0 ? 0 : result.Pages.Average(p => p.SuspiciousTextRatio);
            result.SuspectFonts = result.Pages
                .SelectMany(p => p.SuspectFonts)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f)
                .ToList();
            result.HasTextLayer = result.TextCharCount > 0;

            Classify(result);
            return result;
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex) when (IsUnsupportedPdfException(ex))
        {
            throw new PdfProcessingException(PdfErrorKind.UnsupportedFormat, $"Cannot parse PDF: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to inspect PDF: {ex.Message}", ex);
        }
    }

    internal static int CountMeaningfulChars(string? text) =>
        string.IsNullOrEmpty(text)
            ? 0
            : text.Count(c => !char.IsWhiteSpace(c) && !char.IsControl(c));

    internal static double EstimateImageCoverage(Page page)
    {
        var pageArea = Math.Max(1, page.Width * page.Height);
        double imageArea = 0;
        try
        {
            foreach (var image in page.GetImages())
            {
                var box = image.BoundingBox;
                if (double.IsFinite(box.Area) && box.Area > 0)
                    imageArea += Math.Min(pageArea, box.Area);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // PdfPig may throw for corrupted streams, unsupported filters, or
            // missing resources. Fall back to a coarse per-image-count estimate
            // rather than a silent 100% coverage that would misclassify the PDF.
            var imgCount = page.NumberOfImages;
            return imgCount > 0 ? Math.Clamp(imgCount * 0.02, 0.01, 0.50) : 0;
        }

        return Math.Clamp(imageArea / pageArea, 0, 1);
    }

    static PdfTextQualitySummary AnalyzeTextQuality(Page page)
    {
        try
        {
            return PdfTextQuality.AnalyzeWords(page.GetWords());
        }
        catch
        {
            return new PdfTextQualitySummary();
        }
    }

    static void Classify(PdfCheckResult result)
    {
        if (result.PageCount == 0)
        {
            result.Classification = "unknown";
            result.RecommendedMode = "auto";
            result.RenderRequired = true;
            result.Warnings.Add("PDF has no pages.");
            return;
        }

        var highText = result.TextCharsPerPage >= 80;
        var tinyText = result.TextCharsPerPage < 20;
        var highImage = result.ImageCoverageRatio >= 0.55;
        var mediumImage = result.ImageCoverageRatio >= 0.20;

        if (highText && !highImage)
        {
            result.Classification = mediumImage ? "hybrid" : "text";
            result.RecommendedMode = result.Classification;
            result.RenderRequired = mediumImage;
        }
        else if (!result.HasTextLayer || (tinyText && highImage))
        {
            result.Classification = "scan";
            result.RecommendedMode = "ocr";
            result.RenderRequired = true;
        }
        else
        {
            result.Classification = "hybrid";
            result.RecommendedMode = "hybrid";
            result.RenderRequired = true;
        }

        if (result.HasTextLayer && tinyText && result.ImageCoverageRatio > 0)
        {
            result.Warnings.Add("A tiny text layer exists, but image coverage is high; do not treat this as text-only.");
        }

        if (!result.HasTextLayer)
        {
            result.Warnings.Add("No useful text layer found. Local OCR or cloud OCR is required for readable content.");
        }

        if (result.SuspectFonts.Count > 0)
        {
            result.Warnings.Add("Possible custom-encoded or corrupted PDF text font(s): " + string.Join(", ", result.SuspectFonts.Take(8)));
        }

        if (result.SuspiciousTextRatio > 0.20)
        {
            result.Warnings.Add($"Suspicious text ratio is high ({result.SuspiciousTextRatio:P0}); verify with pdf render or OCR.");
        }
    }

    static bool IsUnsupportedPdfException(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
            || text.Contains("password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not a valid", StringComparison.OrdinalIgnoreCase)
            || text.Contains("header", StringComparison.OrdinalIgnoreCase);
    }
}
