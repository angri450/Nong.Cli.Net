using SkiaSharp;
using DiagramCore.Models;
using DiagramCore.Layout;
using SkiaSharp.HarfBuzz;

namespace DiagramCore.Renderers;

public class TreeRenderer : IRenderer
{
    private readonly NewickTree _root;
    private readonly TreeLayout _layout = new();
    private readonly bool _radial;
    private const float Padding = 30f;

    public TreeRenderer(NewickTree root, bool radial = false)
    {
        _root = root;
        _radial = radial;
    }

    public void Render(string outputPath, int width = 800, int height = 600)
    {
        using var bitmap = RenderToBitmap(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    public SKBitmap RenderToBitmap(int width = 800, int height = 600)
    {
        using var cjkFont = SKTypeface.FromFamilyName(FontHelper.GetCjkFamilyName());

        if (_radial)
        {
            double radius = Math.Min(width, height) * 0.35;
            _layout.LayoutRadial(_root, width / 2, height / 2, radius);
        }
        else
        {
            _layout.LayoutRectangular(_root, Padding, Padding);
        }

        // 计算文本边界
        using var measurePaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 14,
            Typeface = cjkFont,
        };

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        CollectBounds(_root, measurePaint, ref minX, ref minY, ref maxX, ref maxY);

        // 不会再有空白——边界框精确匹配内容
        double contentW = maxX - minX + Padding * 2;
        double contentH = maxY - minY + Padding * 2;
        double ox = -minX + Padding;
        double oy = -minY + Padding;
        int outW = Math.Max(100, (int)Math.Ceiling(contentW));
        int outH = Math.Max(80, (int)Math.Ceiling(contentH));

        var bitmap = new SKBitmap(outW, outH);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // 偏移画布
        canvas.Translate((float)ox, (float)oy);

        // 分支
        using var branchPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = ParseColor("#333333", SKColors.DarkGray),
            StrokeWidth = 2,
            IsAntialias = true
        };
        DrawBranches(canvas, _root, branchPaint);

        // 标签
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 14,
            Color = SKColors.Black,
            Typeface = cjkFont,
        };
        DrawLabels(canvas, _root, textPaint);

        return bitmap;
    }

    private void DrawBranches(SKCanvas canvas, NewickTree node, SKPaint paint)
    {
        foreach (var child in node.Children)
        {
            if (_radial)
            {
                canvas.DrawLine((float)node.X, (float)node.Y, (float)child.X, (float)child.Y, paint);
            }
            else
            {
                canvas.DrawLine((float)node.X, (float)child.Y, (float)child.X, (float)child.Y, paint);
                canvas.DrawLine((float)node.X, (float)node.Y, (float)node.X, (float)child.Y, paint);
            }
            DrawBranches(canvas, child, paint);
        }
    }

    private void DrawLabels(SKCanvas canvas, NewickTree node, SKPaint paint)
    {
        if (node.IsLeaf)
        {
            float labelW = paint.MeasureText(node.Name);
            canvas.DrawShapedText(node.Name, (float)node.X + 5, (float)node.Y + 4, paint);
        }
        else
        {
            foreach (var child in node.Children)
                DrawLabels(canvas, child, paint);
        }
    }

    private void CollectBounds(NewickTree node, SKPaint paint,
        ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        minX = Math.Min(minX, node.X);
        minY = Math.Min(minY, node.Y);
        maxX = Math.Max(maxX, node.X);
        maxY = Math.Max(maxY, node.Y);

        if (node.IsLeaf && !string.IsNullOrEmpty(node.Name))
        {
            float labelW = paint.MeasureText(node.Name);
            maxX = Math.Max(maxX, node.X + 5 + labelW);
            maxY = Math.Max(maxY, node.Y + 16);
        }

        foreach (var child in node.Children)
            CollectBounds(child, paint, ref minX, ref minY, ref maxX, ref maxY);
    }

    private static SKColor ParseColor(string hex, SKColor fallback)
    {
        if (SKColor.TryParse(hex, out var c)) return c;
        return fallback;
    }
}
