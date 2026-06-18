using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace MultiModalCore;

/// <summary>
/// Image preprocessing for PP-OCRv6 ONNX inference.
/// Det: resize to limit, pad to multiple of 32, BGR normalize.
/// Rec: crop ROI, resize to 48x320, BGR normalize.
/// </summary>
public static class OcrOnnxPreprocess
{
    // ImageNet mean/std (BGR order — same as OpenCV)
    static readonly float[] DetMean = { 0.485f, 0.456f, 0.406f };
    static readonly float[] DetStd  = { 0.229f, 0.224f, 0.225f };

    const float RecScale = 1.0f / 255.0f;

    /// <summary>
    /// Preprocess image for detection model.
    /// Returns (tensor [1,3,H,W], scaleX, scaleY, padLeft, padTop, actualH, actualW).
    /// </summary>
    public static (DenseTensor<float> tensor,
        float scaleX, float scaleY, int padLeft, int padTop,
        int detH, int detW)
        DetPreprocess(SKBitmap bitmap, int limit)
    {
        int ow = bitmap.Width, oh = bitmap.Height;
        float scale = Math.Min((float)limit / ow, (float)limit / oh);
        int newW = Math.Max(32, (int)(ow * scale));
        int newH = Math.Max(32, (int)(oh * scale));

        // Pad to multiple of 32
        int padW = ((newW + 31) / 32) * 32 - newW;
        int padH = ((newH + 31) / 32) * 32 - newH;
        int padLeft = padW / 2, padRight = padW - padLeft;
        int padTop = padH / 2, padBottom = padH - padTop;
        int finalW = newW + padW, finalH = newH + padH;

        float scaleX = (float)ow / newW;
        float scaleY = (float)oh / newH;

        // Resize
        using var resized = bitmap.Resize(new SKImageInfo(newW, newH), SKFilterQuality.Medium);
        if (resized == null) throw new InvalidOperationException("Resize failed");

        // Read BGR pixels
        var pixels = new byte[newW * newH * 3]; // BGR
        ReadBgrPixels(resized, pixels, newW, newH);

        // Build tensor with padding
        var tensor = new DenseTensor<float>(new[] { 1, 3, finalH, finalW });
        for (int c = 0; c < 3; c++)
        {
            float mean = DetMean[c], std = DetStd[c];
            int channelOffset = c * finalH * finalW;
            for (int y = 0; y < finalH; y++)
            {
                for (int x = 0; x < finalW; x++)
                {
                    int srcY = y - padTop;
                    int srcX = x - padLeft;
                    float val;
                    if (srcY >= 0 && srcY < newH && srcX >= 0 && srcX < newW)
                        val = pixels[(srcY * newW + srcX) * 3 + c];
                    else
                        val = mean * 255; // padded — fill with mean*255 then normalize to 0
                    // Actually pad pixels are 0, then normalize
                    if (srcY < 0 || srcY >= newH || srcX < 0 || srcX >= newW)
                        val = 0;
                    tensor[0, c, y, x] = (val / 255.0f - mean) / std;
                }
            }
        }

        return (tensor, scaleX, scaleY, padLeft, padTop, finalH, finalW);
    }

    /// <summary>
    /// Crop ROI from original image and preprocess for recognition model.
    /// Returns (tensor [1,3,48,320]).
    /// </summary>
    public static (DenseTensor<float> tensor, int recW) RecPreprocess(SKBitmap bitmap, DetBox box)
    {
        // Crop with margin
        float margin = 2;
        float x1 = Math.Max(0, box.X1 - margin);
        float y1 = Math.Max(0, box.Y1 - margin);
        float x2 = Math.Min(bitmap.Width - 1, box.X2 + margin);
        float y2 = Math.Min(bitmap.Height - 1, box.Y2 + margin);
        int cw = (int)(x2 - x1), ch = (int)(y2 - y1);
        if (cw <= 0 || ch <= 0)
        {
            // Dummy tensor
            var dummy = new DenseTensor<float>(new[] { 1, 3, 48, 320 });
            for (int i = 0; i < 1 * 3 * 48 * 320; i++)
                dummy.Buffer.Span[i] = 0;
            return (dummy, 320);
        }

        var cropRect = new SKRectI((int)x1, (int)y1, (int)x2, (int)y2);
        using var crop = new SKBitmap(cw, ch);
        if (!bitmap.ExtractSubset(crop, cropRect))
        {
            var dummy = new DenseTensor<float>(new[] { 1, 3, 48, 320 });
            return (dummy, 320);
        }

        // Resize to height=48, preserve aspect ratio, max width=320
        float ratio = 48.0f / ch;
        int rw = Math.Min(320, (int)(cw * ratio));
        rw = Math.Max(4, rw);
        int rh = 48;

        using var resized = crop.Resize(new SKImageInfo(rw, rh), SKFilterQuality.Medium);
        if (resized == null) throw new InvalidOperationException("Rec resize failed");

        var pixels = new byte[rw * rh * 3];
        ReadBgrPixels(resized, pixels, rw, rh);

        var tensor = new DenseTensor<float>(new[] { 1, 3, 48, 320 });
        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 320; x++)
                {
                    float val = (x < rw) ? pixels[(y * rw + x) * 3 + c] : 0;
                    tensor[0, c, y, x] = val * RecScale;
                }
            }
        }

        return (tensor, rw);
    }

    /// <summary>Read BGR byte array from SkiaSharp bitmap.</summary>
    static void ReadBgrPixels(SKBitmap bitmap, byte[] dest, int w, int h)
    {
        // Bitmap must be in a known color type for pixel access
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888);
        using var temp = new SKBitmap(info);
        if (!bitmap.ScalePixels(temp, SKFilterQuality.None))
            throw new InvalidOperationException("Failed to convert bitmap to BGRA");

        var pixmap = temp.PeekPixels() ?? throw new InvalidOperationException("Cannot peek pixels");
        var src = pixmap.GetPixels();

        // Allocate buffer for managed copy
        var rgba = new byte[w * h * 4];
        System.Runtime.InteropServices.Marshal.Copy(src, rgba, 0, rgba.Length);

        // BGRA → BGR
        for (int i = 0; i < w * h; i++)
        {
            dest[i * 3 + 0] = rgba[i * 4 + 0]; // B
            dest[i * 3 + 1] = rgba[i * 4 + 1]; // G
            dest[i * 3 + 2] = rgba[i * 4 + 2]; // R
        }
    }
}
