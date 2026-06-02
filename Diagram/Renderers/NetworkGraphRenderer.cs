using SkiaSharp;
using DiagramCore.Models;
using DiagramCore.Layout;
using SkiaSharp.HarfBuzz;
using System.Linq;

namespace DiagramCore.Renderers;

public class NetworkGraphRenderer : IRenderer
{
    private readonly Graph _graph;
    private readonly ForceDirectedLayout _layout = new();
    private const float BaseFontSize = 14f;
    private const float Padding = 24f;

    public NetworkGraphRenderer(Graph graph)
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
        var cjkFont = SKTypeface.FromFamilyName(FontHelper.GetCjkFamilyName());

        // 测量文本，计算节点半径
        var nodeRadii = new Dictionary<string, double>();
        using var measurePaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = BaseFontSize,
            Typeface = cjkFont,
        };

        foreach (var node in _graph.Nodes)
            nodeRadii[node.Id] = MeasureNodeRadius(node.Label, measurePaint);

        // 估算紧凑布局画布（节点直径总和 + 间距）
        double totalDiameter = nodeRadii.Values.Sum(r => r * 2);
        double maxRadius = nodeRadii.Values.DefaultIfEmpty(30).Max();
        double gapEstimate = _graph.Nodes.Count > 1 ? (_graph.Nodes.Count - 1) * 100 : 0;
        double layoutW = totalDiameter + gapEstimate + 40;
        double layoutH = maxRadius * 4 + 40;

        // 力导向布局
        _layout.Layout(_graph, layoutW, layoutH, nodeRadii);

        // 计算内容边界框
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var node in _graph.Nodes)
        {
            double r = nodeRadii[node.Id];
            minX = Math.Min(minX, node.X - r);
            minY = Math.Min(minY, node.Y - r);
            maxX = Math.Max(maxX, node.X + r);
            maxY = Math.Max(maxY, node.Y + r);
        }

        // 标题区域（画在裁剪后画布顶部，不计入 maxX，仅调整 minY）
        if (!string.IsNullOrEmpty(_graph.Title))
            minY -= 30;

        // 裁剪画布到内容区域
        double contentW = maxX - minX + Padding * 2;
        double contentH = maxY - minY + Padding * 2;
        double offsetX = -minX + Padding;
        double offsetY = -minY + Padding;

        // 确保最小尺寸
        int outW = Math.Max(200, (int)Math.Ceiling(contentW));
        int outH = Math.Max(150, (int)Math.Ceiling(contentH));

        var bitmap = new SKBitmap(outW, outH);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // 绘制边
        using var edgePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#CCCCCC"),
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        foreach (var edge in _graph.Edges)
        {
            var from = _graph.GetNode(edge.From);
            var to = _graph.GetNode(edge.To);
            if (from == null || to == null) continue;
            canvas.DrawLine(
                (float)(from.X + offsetX), (float)(from.Y + offsetY),
                (float)(to.X + offsetX), (float)(to.Y + offsetY), edgePaint);
        }

        // 绘制节点
        using var nodePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var nodeStrokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = BaseFontSize,
            Typeface = cjkFont,
            TextAlign = SKTextAlign.Center,
            Color = SKColor.Parse("#1A1A1A"),
        };

        foreach (var node in _graph.Nodes)
        {
            float r = (float)(nodeRadii.TryGetValue(node.Id, out var rad) ? rad : 40);
            float cx = (float)(node.X + offsetX);
            float cy = (float)(node.Y + offsetY);

            nodePaint.Color = SKColor.Parse(node.FillColor);
            canvas.DrawCircle(cx, cy, r, nodePaint);

            nodeStrokePaint.Color = SKColor.Parse(node.StrokeColor);
            canvas.DrawCircle(cx, cy, r, nodeStrokePaint);

            var lines = node.Label.Split('\n');
            float lineHeight = 18f;
            float totalH = lines.Length * lineHeight;
            float startY = cy - totalH / 2f + lineHeight * 0.7f;

            if (lines.Length == 1)
            {
                // 自适应字号：文本不超出圆半径
                float maxTextWidth = r * 2f - 12f;
                float textWidth = labelPaint.MeasureText(lines[0]);
                labelPaint.TextSize = textWidth > 0 ? Math.Min(BaseFontSize, maxTextWidth / textWidth * BaseFontSize) : BaseFontSize;
                canvas.DrawShapedText(lines[0], cx, cy + 4, labelPaint);
            }
            else
            {
                labelPaint.TextSize = Math.Min(12f, BaseFontSize - 2f);
                for (int i = 0; i < lines.Length; i++)
                    canvas.DrawShapedText(lines[i], cx, startY + i * lineHeight, labelPaint);
            }
        }

        // 标题
        if (!string.IsNullOrEmpty(_graph.Title))
        {
            using var titlePaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = 18,
                Color = SKColors.Black,
                Typeface = cjkFont,
                TextAlign = SKTextAlign.Center,
            };
            canvas.DrawShapedText(_graph.Title, outW / 2f, 24, titlePaint);
        }

        return bitmap;
    }

    private static double MeasureNodeRadius(string label, SKPaint measurePaint)
    {
        if (string.IsNullOrEmpty(label)) return 32;
        var lines = label.Split('\n');
        float maxTextWidth = 0;
        foreach (var line in lines)
            maxTextWidth = Math.Max(maxTextWidth, measurePaint.MeasureText(line));

        float textHeight = lines.Length * 18f;
        return Math.Max(32, Math.Max(maxTextWidth / 2f + 16f, textHeight / 2f + 12f));
    }

    /// <summary>将所有节点向中心收拢，减少空白</summary>
    private static void TightenPositions(Graph graph, double factor)
    {
        if (graph.Nodes.Count == 0) return;
        double cx = graph.Nodes.Average(n => n.X);
        double cy = graph.Nodes.Average(n => n.Y);
        foreach (var node in graph.Nodes)
        {
            node.X = cx + (node.X - cx) * factor;
            node.Y = cy + (node.Y - cy) * factor;
        }
    }
}
