namespace ChartCore;

using SkiaSharp;

public static class ChartCombine
{
    /// <summary>水平合并多张图表，可选标注 A/B/C</summary>
    public static void MergeHorizontal(string[] paths, string[]? labels, string outPath,
        int panelHeight = 0, int gap = 10, float labelFontSize = 18, string? labelFont = null)
    {
        labelFont ??= FontHelper.GetCjkFamilyName();
        if (paths.Length == 0)
            throw new ArgumentException("At least one image path required.");

        var images = new List<SKBitmap>();
        foreach (var path in paths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Chart image not found: {path}");
            images.Add(SKBitmap.Decode(path));
        }

        bool hasLabels = labels != null && labels.Length == paths.Length;
        int labelW = hasLabels ? 60 : 0;
        int totalGap = gap * (images.Count - 1);

        int height = panelHeight > 0 ? panelHeight : images.Max(img => img.Height);
        int totalWidth = labelW + images.Sum(img => img.Width) + totalGap;

        using var surface = SKSurface.Create(new SKImageInfo(totalWidth, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        int x = 0;
        for (int i = 0; i < images.Count; i++)
        {
            // 标注
            if (hasLabels && labels![i] != null)
            {
                using var labelTypeface = SKTypeface.FromFamilyName(labelFont);
                using var font = new SKFont(labelTypeface ?? SKTypeface.Default, labelFontSize);
                using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                float textWidth = font.MeasureText(labels[i]);
                canvas.DrawText(labels[i], x + (labelW - textWidth) / 2, height / 2 + labelFontSize / 3, font, paint);
            }
            x += labelW;

            // 缩放图片到统一高度
            var img = images[i];
            float scale = (float)height / img.Height;
            int scaledW = (int)(img.Width * scale);
            var destRect = new SKRect(x, 0, x + scaledW, height);
            canvas.DrawBitmap(img, destRect);

            x += scaledW + gap;
            img.Dispose();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
        using var outputImage = surface.Snapshot();
        using var data = outputImage.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(outPath);
        data.SaveTo(fs);
    }
}
