using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace MultiModalCore;

public sealed record LocalOcrInputPreflightResult
{
    public bool ShouldSkip { get; set; }
    public string Classification { get; set; } = "text_candidate";
    public string Reason { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double WhitespaceRatio { get; set; }
    public int RegionCount { get; set; }
    public int LargestRegionPixelCount { get; set; }
    public double LargestRegionRatio { get; set; }
    public double GraphicRatio { get; set; }
    public double DarkRatio { get; set; }
    public double ContentAspectRatio { get; set; }
    public BarcodeDetection? Barcode { get; set; }
}

public sealed record BarcodeDetection
{
    public string Format { get; set; } = "";
    public string TextPreview { get; set; } = "";
    public int TextLength { get; set; }
}

public static class LocalOcrInputPreflight
{
    public static LocalOcrInputPreflightResult Analyze(string imagePath)
    {
        var layout = new ImageAnalyzer().Analyze(imagePath, targetWidth: 80);
        var barcode = TryDecodeBarcode(imagePath);
        var nonWhite = layout.BlackPixelCount + layout.GraphicPixelCount + layout.EdgePixelCount;
        var largest = layout.Regions.OrderByDescending(r => r.PixelCount).FirstOrDefault();
        var largestRatio = nonWhite == 0 || largest == null ? 0 : largest.PixelCount / (double)nonWhite;
        var graphicRatio = nonWhite == 0 ? 0 : layout.GraphicPixelCount / (double)nonWhite;
        var darkRatio = nonWhite == 0 ? 0 : (layout.BlackPixelCount + layout.EdgePixelCount) / (double)nonWhite;
        var aspect = layout.ContentHeight > 0 ? layout.ContentWidth / (double)layout.ContentHeight : 0;

        var result = new LocalOcrInputPreflightResult
        {
            Width = layout.OriginalWidth,
            Height = layout.OriginalHeight,
            WhitespaceRatio = layout.WhitespaceRatio,
            RegionCount = layout.Regions.Count,
            LargestRegionPixelCount = largest?.PixelCount ?? 0,
            LargestRegionRatio = largestRatio,
            GraphicRatio = graphicRatio,
            DarkRatio = darkRatio,
            ContentAspectRatio = aspect,
            Barcode = barcode,
        };

        if (barcode != null)
        {
            result.ShouldSkip = true;
            result.Classification = "barcode_or_qr";
            result.Reason = $"ZXing decoded a {barcode.Format} code; PP-OCR text recognition is not the right engine for barcode/QR decoding.";
            result.Recommendation = "Use the decoded barcode/QR value or inspect the image as an asset. Rerun with --force only if surrounding text OCR is explicitly required.";
            return result;
        }

        if (layout.OriginalWidth < 80 || layout.OriginalHeight < 80)
        {
            result.Reason = "Image is small; preflight did not classify it as a non-text graphic.";
            return result;
        }

        // 启发式检测从 blocking 改为 warning：考试试卷等密集文字图像常被误判，
        // 仅在结果中标记分类和建议，不阻断 OCR 推理。
        if (LooksLikeQrOrCodeGraphic(layout, largestRatio, graphicRatio, darkRatio, aspect))
        {
            result.Classification = "qr_or_code_like_graphic";
            result.Reason = "The image is dominated by one dense high-contrast graphic region, which is typical of QR/code images and may not be useful input for PP-OCR text recognition.";
            result.Recommendation = "Local OCR will proceed, but results may be poor if the image is indeed a QR code or pure graphic. Use a QR/barcode decoder for codes, or ocr analyze-image for structure QA.";
            return result;
        }

        if (LooksLikeGraphicOnlyImage(layout, largestRatio, graphicRatio))
        {
            result.Classification = "graphic_heavy_non_text";
            result.Reason = "The image appears graphic-heavy with few text-like regions for local text OCR.";
            result.Recommendation = "Local OCR will proceed, but text yield may be low. Use ocr analyze-image for structure QA, or a domain-specific decoder for codes/charts.";
            return result;
        }

        result.Reason = "Image passed local OCR preflight.";
        return result;
    }

    static BarcodeDetection? TryDecodeBarcode(string imagePath)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imagePath);
            if (bitmap == null)
                return null;

            var pixels = ToRgb24(bitmap);
            var source = new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGB24);
            var binary = new BinaryBitmap(new HybridBinarizer(source));
            var decoded = new QRCodeReader().decode(binary, BarcodeDecodeHints);
            if (decoded == null || string.IsNullOrWhiteSpace(decoded.Text))
                return null;

            return new BarcodeDetection
            {
                Format = decoded.BarcodeFormat.ToString(),
                TextPreview = decoded.Text.Length > 120 ? decoded.Text[..120] : decoded.Text,
                TextLength = decoded.Text.Length,
            };
        }
        catch
        {
            return null;
        }
    }

    static readonly Dictionary<DecodeHintType, object> BarcodeDecodeHints = new()
    {
        [DecodeHintType.TRY_HARDER] = true,
    };

    static byte[] ToRgb24(SKBitmap bitmap)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height * 3];
        var index = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                pixels[index++] = pixel.Red;
                pixels[index++] = pixel.Green;
                pixels[index++] = pixel.Blue;
            }
        }
        return pixels;
    }

    static bool LooksLikeQrOrCodeGraphic(ImageLayout layout, double largestRatio, double graphicRatio, double darkRatio, double aspect)
    {
        var nonWhiteRatio = 1.0 - layout.WhitespaceRatio;
        return layout.Regions.Count <= 8
            && largestRatio >= 0.72
            && (graphicRatio >= 0.70 || darkRatio >= 0.70)
            && nonWhiteRatio >= 0.22
            && layout.WhitespaceRatio <= 0.70
            && aspect is >= 0.45 and <= 2.2;
    }

    static bool LooksLikeGraphicOnlyImage(ImageLayout layout, double largestRatio, double graphicRatio)
    {
        var nonWhiteRatio = 1.0 - layout.WhitespaceRatio;
        return layout.Regions.Count <= 3
            && largestRatio >= 0.82
            && graphicRatio >= 0.75
            && nonWhiteRatio >= 0.20
            && layout.BlackPixelCount < layout.GraphicPixelCount * 0.15;
    }
}
