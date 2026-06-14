using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;

namespace PdfCore;

public static class PdfPageRenderer
{
    public static PdfRenderResult Render(string pdfPath, string outputDir, int dpi = 200)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        ValidateDpi(dpi);

        Directory.CreateDirectory(outputDir);
        var scale = dpi / 72.0;

        try
        {
            using var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scale));
            var result = new PdfRenderResult
            {
                OutputDir = Path.GetFullPath(outputDir),
                PageCount = reader.GetPageCount(),
                Dpi = dpi,
            };

            for (var i = 0; i < result.PageCount; i++)
            {
                using var pageReader = reader.GetPageReader(i);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var bytes = pageReader.GetImage();
                var relative = $"page-{i + 1:D4}.png";
                var outPath = Path.Combine(outputDir, relative);
                using var bitmap = CreateBgraBitmap(bytes, width, height);
                WritePng(bitmap, outPath);
                result.Pages.Add(new PdfRenderedPage
                {
                    Page = i + 1,
                    Path = relative,
                    Width = width,
                    Height = height,
                });
            }

            PdfUtilities.WriteJson(Path.Combine(outputDir, "manifest.json"), result);
            return result;
        }
        catch (DllNotFoundException ex)
        {
            throw new PdfProcessingException(PdfErrorKind.DependencyMissing, $"PDFium native runtime is unavailable: {ex.Message}", ex);
        }
        catch (BadImageFormatException ex)
        {
            throw new PdfProcessingException(PdfErrorKind.DependencyMissing, $"PDFium native runtime architecture mismatch: {ex.Message}", ex);
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to render PDF pages: {ex.Message}", ex);
        }
    }

    public static PdfRenderedCrop RenderCrop(
        string pdfPath,
        int pageNumber,
        double pageWidthPt,
        double pageHeightPt,
        IReadOnlyList<double> bboxPt,
        string outputPath,
        int dpi = 300,
        int paddingPx = 2)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        ValidateDpi(dpi);
        if (pageNumber < 1)
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Page number must be 1 or greater.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        var scale = dpi / 72.0;
        try
        {
            using var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scale));
            if (pageNumber > reader.GetPageCount())
                throw new PdfProcessingException(PdfErrorKind.ValidationFailed, $"Page {pageNumber} is outside the PDF page range.");

            using var pageReader = reader.GetPageReader(pageNumber - 1);
            var renderedWidth = pageReader.GetPageWidth();
            var renderedHeight = pageReader.GetPageHeight();
            var bytes = pageReader.GetImage();
            using var bitmap = CreateBgraBitmap(bytes, renderedWidth, renderedHeight);
            var rect = ToPixelCropRect(bboxPt, pageWidthPt, pageHeightPt, renderedWidth, renderedHeight, paddingPx);

            var cropInfo = new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var crop = new SKBitmap(cropInfo);
            using (var canvas = new SKCanvas(crop))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(bitmap, rect, new SKRect(0, 0, rect.Width, rect.Height));
            }

            WritePng(crop, outputPath);
            return new PdfRenderedCrop
            {
                Page = pageNumber,
                Path = Path.GetFullPath(outputPath),
                Width = rect.Width,
                Height = rect.Height,
                Dpi = dpi,
                SourceBbox = bboxPt.Select(v => Math.Round(v, 3)).ToArray(),
                PixelBbox =
                [
                    rect.Left,
                    rect.Top,
                    rect.Right,
                    rect.Bottom,
                ],
            };
        }
        catch (DllNotFoundException ex)
        {
            throw new PdfProcessingException(PdfErrorKind.DependencyMissing, $"PDFium native runtime is unavailable: {ex.Message}", ex);
        }
        catch (BadImageFormatException ex)
        {
            throw new PdfProcessingException(PdfErrorKind.DependencyMissing, $"PDFium native runtime architecture mismatch: {ex.Message}", ex);
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to render PDF crop: {ex.Message}", ex);
        }
    }

    static void ValidateDpi(int dpi)
    {
        if (dpi is < 72 or > 600)
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "DPI must be between 72 and 600.");
    }

    static SKBitmap CreateBgraBitmap(byte[] bytes, int width, int height)
    {
        CompositeTransparentPixelsOverWhite(bytes);
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
        return bitmap;
    }

    static void WritePng(SKBitmap bitmap, string outPath)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(outPath);
        data.SaveTo(stream);
    }

    static SKRectI ToPixelCropRect(
        IReadOnlyList<double> bboxPt,
        double pageWidthPt,
        double pageHeightPt,
        int renderedWidth,
        int renderedHeight,
        int paddingPx)
    {
        if (bboxPt.Count < 4)
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Crop bbox must contain left,bottom,right,top.");
        if (!IsFinitePositive(pageWidthPt) || !IsFinitePositive(pageHeightPt))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "PDF page dimensions are invalid.");

        var left = Math.Min(bboxPt[0], bboxPt[2]);
        var bottom = Math.Min(bboxPt[1], bboxPt[3]);
        var right = Math.Max(bboxPt[0], bboxPt[2]);
        var top = Math.Max(bboxPt[1], bboxPt[3]);
        if (!IsFinite(left) || !IsFinite(bottom) || !IsFinite(right) || !IsFinite(top) || right <= left || top <= bottom)
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Crop bbox is invalid.");

        var scaleX = renderedWidth / pageWidthPt;
        var scaleY = renderedHeight / pageHeightPt;
        var x0 = (int)Math.Floor((left * scaleX) - paddingPx);
        var y0 = (int)Math.Floor(((pageHeightPt - top) * scaleY) - paddingPx);
        var x1 = (int)Math.Ceiling((right * scaleX) + paddingPx);
        var y1 = (int)Math.Ceiling(((pageHeightPt - bottom) * scaleY) + paddingPx);

        x0 = Math.Clamp(x0, 0, renderedWidth);
        y0 = Math.Clamp(y0, 0, renderedHeight);
        x1 = Math.Clamp(x1, 0, renderedWidth);
        y1 = Math.Clamp(y1, 0, renderedHeight);

        if (x1 <= x0 || y1 <= y0)
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Crop bbox is outside the rendered page.");
        return new SKRectI(x0, y0, x1, y1);
    }

    static bool IsFinitePositive(double value) => IsFinite(value) && value > 0;

    static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    static void CompositeTransparentPixelsOverWhite(byte[] bgra)
    {
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            var alpha = bgra[i + 3];
            if (alpha == 255)
                continue;

            bgra[i] = CompositeChannel(bgra[i], alpha);
            bgra[i + 1] = CompositeChannel(bgra[i + 1], alpha);
            bgra[i + 2] = CompositeChannel(bgra[i + 2], alpha);
            bgra[i + 3] = 255;
        }
    }

    static byte CompositeChannel(byte channel, byte alpha) =>
        (byte)Math.Clamp(((channel * alpha) + (255 * (255 - alpha))) / 255, 0, 255);
}
