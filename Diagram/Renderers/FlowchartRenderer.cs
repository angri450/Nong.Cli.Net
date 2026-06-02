using SkiaSharp;
using DiagramCore.Models;
using DiagramCore.Layout;
using SkiaSharp.HarfBuzz;

namespace DiagramCore.Renderers;

public class FlowchartRenderer : IRenderer
{
    private readonly Graph _graph;
    private readonly SugiyamaLayout _layout = new();
    private readonly BioIconRenderer _iconRenderer = new();
    private const float Padding = 30f;

    public FlowchartRenderer(Graph graph)
    {
        _graph = graph;
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
        _layout.Layout(_graph, width);

        // 计算内容边界框（布局后的节点 + 标题）
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var node in _graph.Nodes)
        {
            minX = Math.Min(minX, node.X);
            minY = Math.Min(minY, node.Y);
            maxX = Math.Max(maxX, node.X + node.Width);
            maxY = Math.Max(maxY, node.Y + node.Height);
        }
        if (!string.IsNullOrEmpty(_graph.Title))
            minY -= 30; // 标题在顶部

        double ox = -minX + Padding;
        double oy = -minY + Padding;
        int outW = Math.Max(200, (int)Math.Ceiling(maxX - minX + Padding * 2));
        int outH = Math.Max(100, (int)Math.Ceiling(maxY - minY + Padding * 2));

        var bitmap = new SKBitmap(outW, outH);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Translate((float)ox, (float)oy);

        var cjkFont = SKTypeface.FromFamilyName(FontHelper.GetCjkFamilyName());

        // 边
        using var edgePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#666666"),
            StrokeWidth = 2,
            IsAntialias = true
        };

        foreach (var edge in _graph.Edges)
        {
            var from = _graph.GetNode(edge.From);
            var to = _graph.GetNode(edge.To);
            if (from == null || to == null) continue;

            var x1 = (float)(from.X + from.Width / 2);
            var y1 = (float)(from.Y + from.Height);
            var x2 = (float)(to.X + to.Width / 2);
            var y2 = (float)to.Y;

            canvas.DrawLine(x1, y1, x2, y2, edgePaint);
            if (edge.HasArrow)
                DrawArrow(canvas, x1, y1, x2, y2, edgePaint);
        }

        // 节点
        using var nodePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 14,
            TextAlign = SKTextAlign.Center,
            Typeface = cjkFont,
        };

        foreach (var node in _graph.Nodes)
        {
            if (TryParseIconShape(node, out var iconCat, out var iconNm))
            {
                var iconSize = (float)Math.Min(node.Width, node.Height) * 0.7f;
                var iconX = (float)(node.X + (node.Width - iconSize) / 2);
                var iconY = (float)(node.Y + (node.Height - iconSize) / 2);
                _iconRenderer.RenderIcon(canvas, iconCat!, iconNm!, iconX, iconY, iconSize, node.StrokeColor);
            }
            else
            {
                nodePaint.Color = SKColor.Parse(node.FillColor);
                var rect = new SKRect((float)node.X, (float)node.Y,
                    (float)(node.X + node.Width), (float)(node.Y + node.Height));
                canvas.DrawRoundRect(rect, 8, 8, nodePaint);

                nodePaint.Style = SKPaintStyle.Stroke;
                nodePaint.Color = SKColor.Parse(node.StrokeColor);
                nodePaint.StrokeWidth = 2;
                canvas.DrawRoundRect(rect, 8, 8, nodePaint);
                nodePaint.Style = SKPaintStyle.Fill;
            }

            textPaint.Color = SKColor.Parse(node.TextColor);
            var textBounds = new SKRect();
            textPaint.MeasureText(node.Label, ref textBounds);
            canvas.DrawShapedText(node.Label,
                (float)(node.X + node.Width / 2),
                (float)(node.Y + node.Height / 2 + textBounds.Height / 4),
                textPaint);
        }

        // 标题（画在裁剪后画布顶部中央）
        if (!string.IsNullOrEmpty(_graph.Title))
        {
            canvas.Save();
            canvas.ResetMatrix();
            textPaint.TextSize = 20;
            textPaint.Color = SKColors.Black;
            canvas.DrawShapedText(_graph.Title, outW / 2f, 24, textPaint);
            canvas.Restore();
        }

        return bitmap;
    }

    private void DrawArrow(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint)
    {
        double angle = Math.Atan2(y2 - y1, x2 - x1);
        double arrowLength = 15;
        double arrowAngle = Math.PI / 6;
        var ax1 = (float)(x2 - arrowLength * Math.Cos(angle - arrowAngle));
        var ay1 = (float)(y2 - arrowLength * Math.Sin(angle - arrowAngle));
        var ax2 = (float)(x2 - arrowLength * Math.Cos(angle + arrowAngle));
        var ay2 = (float)(y2 - arrowLength * Math.Sin(angle + arrowAngle));
        canvas.DrawLine(x2, y2, ax1, ay1, paint);
        canvas.DrawLine(x2, y2, ax2, ay2, paint);
    }

    private static bool TryParseIconShape(GraphNode node, out string? category, out string? name)
    {
        if (!string.IsNullOrEmpty(node.IconCategory) && !string.IsNullOrEmpty(node.IconName))
        {
            category = node.IconCategory;
            name = node.IconName;
            return true;
        }
        if (node.Shape.StartsWith("icon:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = node.Shape.Split(':');
            if (parts.Length >= 3)
            {
                category = parts[1];
                name = parts[2];
                return true;
            }
        }
        category = null;
        name = null;
        return false;
    }
}
