using SkiaSharp;

namespace ImagingCore;

/// <summary>
/// Pure .NET image analysis and content-aware cropping using SkiaSharp.
/// Scans pixel rows/columns from each edge inward to detect content boundaries.
/// No AI / cloud dependency.
/// </summary>
public class ImageProcessor
{
    /// <summary>Default variance threshold — rows/columns below this are considered blank.</summary>
    const float DefaultVarianceThreshold = 2.0f;

    /// <summary>Default safety margin as fraction of original dimension (5%).</summary>
    const float DefaultSafetyMargin = 0.05f;

    // ---- Public API ----

    /// <summary>
    /// Analyze an image and detect content boundaries by scanning from each edge.
    /// </summary>
    public ImageContentBounds Analyze(byte[] imageBytes)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to decode image.");

        int w = bitmap.Width, h = bitmap.Height;
        var pixels = bitmap.Pixels;

        int cropLeft = ScanLeft(pixels, w, h);
        int cropTop = ScanTop(pixels, w, h);
        int cropRight = ScanRight(pixels, w, h);
        int cropBottom = ScanBottom(pixels, w, h);

        return new ImageContentBounds
        {
            OriginalWidth = w,
            OriginalHeight = h,
            CropLeft = cropLeft,
            CropTop = cropTop,
            CropRight = cropRight,
            CropBottom = cropBottom
        };
    }

    /// <summary>
    /// Analyze, apply safety margin, and crop in one step.
    /// </summary>
    public byte[] AutoCrop(byte[] imageBytes, float safetyMargin = DefaultSafetyMargin, SKEncodedImageFormat? format = null)
    {
        var bounds = Analyze(imageBytes);
        bounds = ApplySafetyMargin(bounds, safetyMargin);

        if (!bounds.HasCropMargins)
            return imageBytes; // nothing to crop

        return Crop(imageBytes, bounds, format);
    }

    /// <summary>
    /// Crop an image to the specified bounds and re-encode.
    /// </summary>
    public byte[] Crop(byte[] imageBytes, ImageContentBounds bounds, SKEncodedImageFormat? format = null)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to decode image.");

        var cropRect = new SKRectI(bounds.CropLeft, bounds.CropTop,
            bitmap.Width - bounds.CropRight, bitmap.Height - bounds.CropBottom);

        using var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
        bitmap.ExtractSubset(cropped, cropRect);

        var fmt = format ?? DetectFormat(imageBytes);
        using var data = cropped.Encode(fmt, 90);
        return data.ToArray();
    }

    // ---- Scanning ----

    static int ScanLeft(SKColor[] pixels, int w, int h)
    {
        for (int x = 0; x < w / 3; x++) // stop at 33% — don't scan past content-rich center
        {
            if (!IsRowBlank(pixels, w, h, x))
                return x;
        }
        return 0;
    }

    static int ScanTop(SKColor[] pixels, int w, int h)
    {
        for (int y = 0; y < h / 3; y++)
        {
            if (!IsRowBlank_H(pixels, w, y))
                return y;
        }
        return 0;
    }

    static int ScanRight(SKColor[] pixels, int w, int h)
    {
        for (int x = w - 1; x >= w * 2 / 3; x--)
        {
            if (!IsRowBlank(pixels, w, h, x))
                return w - 1 - x;
        }
        return 0;
    }

    static int ScanBottom(SKColor[] pixels, int w, int h)
    {
        for (int y = h - 1; y >= h * 2 / 3; y--)
        {
            if (!IsRowBlank_H(pixels, w, y))
                return h - 1 - y;
        }
        return 0;
    }

    static bool IsRowBlank(SKColor[] pixels, int w, int h, int x)
    {
        // Check a vertical column at position x for content
        double sum = 0;
        int count = h;
        for (int y = 0; y < h; y++)
        {
            var c = pixels[y * w + x];
            sum += (c.Red + c.Green + c.Blue) / 3.0;
        }
        double mean = sum / count;
        double variance = 0;
        for (int y = 0; y < h; y++)
        {
            var c = pixels[y * w + x];
            double val = (c.Red + c.Green + c.Blue) / 3.0;
            variance += (val - mean) * (val - mean);
        }
        variance /= count;
        return variance < DefaultVarianceThreshold;
    }

    static bool IsRowBlank_H(SKColor[] pixels, int w, int y)
    {
        // Check a horizontal row at position y for content
        double sum = 0;
        for (int x = 0; x < w; x++)
        {
            var c = pixels[y * w + x];
            sum += (c.Red + c.Green + c.Blue) / 3.0;
        }
        double mean = sum / w;
        double variance = 0;
        for (int x = 0; x < w; x++)
        {
            var c = pixels[y * w + x];
            double val = (c.Red + c.Green + c.Blue) / 3.0;
            variance += (val - mean) * (val - mean);
        }
        variance /= w;
        return variance < DefaultVarianceThreshold;
    }

    // ---- Helpers ----

    static ImageContentBounds ApplySafetyMargin(ImageContentBounds b, float margin)
    {
        int leftMargin = (int)(b.OriginalWidth * margin);
        int topMargin = (int)(b.OriginalHeight * margin);

        return b with
        {
            CropLeft = Math.Max(0, b.CropLeft - leftMargin),
            CropTop = Math.Max(0, b.CropTop - topMargin),
            CropRight = Math.Max(0, b.CropRight - leftMargin),
            CropBottom = Math.Max(0, b.CropBottom - topMargin)
        };
    }

    static SKEncodedImageFormat DetectFormat(byte[] bytes)
    {
        // Simple magic-number detection
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8) return SKEncodedImageFormat.Jpeg;
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return SKEncodedImageFormat.Png;
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return SKEncodedImageFormat.Gif;
        return SKEncodedImageFormat.Png; // fallback
    }
}
