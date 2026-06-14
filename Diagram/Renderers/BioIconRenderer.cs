using System.Xml.Linq;
using SkiaSharp;

namespace DiagramCore.Renderers;

/// <summary>
/// Renders Bioicons SVG icons onto an SKCanvas using SkiaSharp path rendering.
/// Parses SVG elements (path, circle, ellipse, line, rect, polygon) and draws them
/// with color override, scaling, and rotation support.
/// </summary>
public class BioIconRenderer
{
    private const float DefaultViewBox = 64f;

    /// <summary>
    /// Renders a Bioicons icon at the specified position on the canvas.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="category">Icon category (e.g. Biology, Chemistry).</param>
    /// <param name="name">Icon name without extension (e.g. cell, dna).</param>
    /// <param name="x">Left position of the icon bounding box.</param>
    /// <param name="y">Top position of the icon bounding box.</param>
    /// <param name="size">Width and height of the rendered icon in pixels.</param>
    /// <param name="color">Hex color string to override the icon stroke/fill (e.g. "#FF0000").</param>
    public void RenderIcon(SKCanvas canvas, string category, string name,
        float x, float y, float size, string color)
    {
        var svg = Bioicons.IconProvider.GetSvg(category, name);
        RenderSvg(canvas, svg, x, y, size, size, color);
    }

    /// <summary>
    /// Renders a Bioicons icon centered at the specified point.
    /// </summary>
    public void RenderIconCentered(SKCanvas canvas, string category, string name,
        float cx, float cy, float size, string color)
    {
        RenderIcon(canvas, category, name, cx - size / 2, cy - size / 2, size, color);
    }

    /// <summary>
    /// Renders a Bioicons icon with rotation applied around its center.
    /// </summary>
    public void RenderIconRotated(SKCanvas canvas, string category, string name,
        float x, float y, float size, string color, float rotationDegrees)
    {
        canvas.Save();
        var centerX = x + size / 2;
        var centerY = y + size / 2;
        canvas.RotateDegrees(rotationDegrees, centerX, centerY);
        RenderIcon(canvas, category, name, x, y, size, color);
        canvas.Restore();
    }

    private void RenderSvg(SKCanvas canvas, string svgContent,
        float x, float y, float width, float height, string overrideColor)
    {
        var doc = XDocument.Parse(svgContent);
        var root = doc.Root;
        if (root == null) return;

        var viewBox = ParseViewBox(root.Attribute("viewBox")?.Value);
        var scaleX = width / viewBox.Width;
        var scaleY = height / viewBox.Height;

        canvas.Save();
        canvas.Translate(x, y);
        canvas.Scale(scaleX, scaleY);

        var overrideSkColor = ParseColor(overrideColor, SKColors.Black);

        foreach (var element in root.Elements())
        {
            RenderElement(canvas, element, overrideSkColor);
        }

        canvas.Restore();
    }

    private static (float X, float Y, float Width, float Height) ParseViewBox(string? viewBox)
    {
        if (string.IsNullOrEmpty(viewBox))
            return (0, 0, DefaultViewBox, DefaultViewBox);

        var parts = viewBox.Split(' ', ',');
        if (parts.Length == 4 &&
            float.TryParse(parts[0], out var vx) &&
            float.TryParse(parts[1], out var vy) &&
            float.TryParse(parts[2], out var vw) &&
            float.TryParse(parts[3], out var vh))
        {
            return (vx, vy, vw, vh);
        }

        return (0, 0, DefaultViewBox, DefaultViewBox);
    }

    private void RenderElement(SKCanvas canvas, XElement element, SKColor overrideColor)
    {
        var tagName = element.Name.LocalName;

        switch (tagName)
        {
            case "path":
                RenderPath(canvas, element, overrideColor);
                break;
            case "circle":
                RenderCircle(canvas, element, overrideColor);
                break;
            case "ellipse":
                RenderEllipse(canvas, element, overrideColor);
                break;
            case "line":
                RenderLine(canvas, element, overrideColor);
                break;
            case "rect":
                RenderRect(canvas, element, overrideColor);
                break;
            case "polygon":
                RenderPolygon(canvas, element, overrideColor);
                break;
            case "g":
                foreach (var child in element.Elements())
                    RenderElement(canvas, child, overrideColor);
                break;
        }
    }

    private void RenderPath(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var d = el.Attribute("d")?.Value;
        if (string.IsNullOrEmpty(d)) return;

        using var path = SKPath.ParseSvgPathData(d);
        DrawElement(canvas, path, el, overrideColor);
    }

    private void RenderCircle(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var cx = AttrFloat(el, "cx", 0);
        var cy = AttrFloat(el, "cy", 0);
        var r = AttrFloat(el, "r", 0);
        if (r <= 0) return;

        using var path = new SKPath();
        path.AddCircle(cx, cy, r);
        DrawElement(canvas, path, el, overrideColor);
    }

    private void RenderEllipse(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var cx = AttrFloat(el, "cx", 0);
        var cy = AttrFloat(el, "cy", 0);
        var rx = AttrFloat(el, "rx", 0);
        var ry = AttrFloat(el, "ry", 0);
        if (rx <= 0 || ry <= 0) return;

        using var path = new SKPath();
        path.AddOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry));
        DrawElement(canvas, path, el, overrideColor);
    }

    private void RenderLine(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var x1 = AttrFloat(el, "x1", 0);
        var y1 = AttrFloat(el, "y1", 0);
        var x2 = AttrFloat(el, "x2", 0);
        var y2 = AttrFloat(el, "y2", 0);

        using var path = new SKPath();
        path.MoveTo(x1, y1);
        path.LineTo(x2, y2);
        DrawElement(canvas, path, el, overrideColor, forceStroke: true);
    }

    private void RenderRect(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var x = AttrFloat(el, "x", 0);
        var y = AttrFloat(el, "y", 0);
        var w = AttrFloat(el, "width", 0);
        var h = AttrFloat(el, "height", 0);
        var rx = AttrFloat(el, "rx", 0);
        if (w <= 0 || h <= 0) return;

        using var path = new SKPath();
        if (rx > 0)
            path.AddRoundRect(new SKRect(x, y, x + w, y + h), rx, rx);
        else
            path.AddRect(new SKRect(x, y, x + w, y + h));

        DrawElement(canvas, path, el, overrideColor);
    }

    private void RenderPolygon(SKCanvas canvas, XElement el, SKColor overrideColor)
    {
        var pointsStr = el.Attribute("points")?.Value;
        if (string.IsNullOrEmpty(pointsStr)) return;

        var coords = pointsStr.Split(' ', ',')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => float.TryParse(s, out var v) ? v : 0f)
            .ToArray();

        if (coords.Length < 4) return;

        using var path = new SKPath();
        path.MoveTo(coords[0], coords[1]);
        for (int i = 2; i + 1 < coords.Length; i += 2)
        {
            path.LineTo(coords[i], coords[i + 1]);
        }
        path.Close();

        DrawElement(canvas, path, el, overrideColor);
    }

    private void DrawElement(SKCanvas canvas, SKPath path, XElement el,
        SKColor overrideColor, bool forceStroke = false)
    {
        var fill = el.Attribute("fill")?.Value;
        var stroke = el.Attribute("stroke")?.Value;
        var strokeWidth = AttrFloat(el, "stroke-width", 0);
        var opacity = AttrFloat(el, "opacity", 1.0f);

        // Draw fill if present and not "none"
        if (!forceStroke && !string.IsNullOrEmpty(fill) && fill != "none")
        {
            var fillColor = ApplyOpacity(overrideColor, opacity);
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = fillColor,
                IsAntialias = true
            };
            canvas.DrawPath(path, paint);
        }

        // Draw stroke if present
        if ((!string.IsNullOrEmpty(stroke) && stroke != "none") || forceStroke)
        {
            var strokeColor = ApplyOpacity(overrideColor, opacity);
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = strokeColor,
                StrokeWidth = strokeWidth > 0 ? strokeWidth : 2f,
                IsAntialias = true
            };
            canvas.DrawPath(path, paint);
        }
    }

    private static SKColor ApplyOpacity(SKColor color, float opacity)
    {
        if (opacity >= 1.0f) return color;
        var alpha = (byte)(color.Alpha * Math.Clamp(opacity, 0f, 1f));
        return color.WithAlpha(alpha);
    }

    private static float AttrFloat(XElement el, string name, float defaultValue)
    {
        var val = el.Attribute(name)?.Value;
        return float.TryParse(val, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Renders a sheet of all available Bioicons organized by category.
    /// </summary>
    public static void RenderIconSheet(string outputPath, int width = 1200, int height = 800)
    {
        var categories = Bioicons.IconProvider.GetCategories();
        var renderer = new BioIconRenderer();

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var cjkFont = SKTypeface.FromFamilyName(FontHelper.GetCjkFamilyName());
        using var cjkFontBold = SKTypeface.FromFamilyName(FontHelper.GetCjkFamilyName(), SKFontStyle.Bold);

        using var titlePaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 24,
            Color = SKColor.Parse("#1A1A1A"),
            Typeface = cjkFontBold
        };

        using var categoryPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 16,
            Color = SKColor.Parse("#2C5F7D"),
            Typeface = cjkFont
        };

        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 10,
            Color = SKColor.Parse("#666666"),
            TextAlign = SKTextAlign.Center,
            Typeface = cjkFont,
        };

        canvas.DrawText("Bioicons Icon Sheet", width / 2 - 100, 35, titlePaint);

        const float iconSize = 48f;
        const float iconSpacing = 70f;
        const float categoryHeight = 100f;
        const float leftMargin = 30f;
        float currentY = 60f;

        foreach (var category in categories)
        {
            var icons = Bioicons.IconProvider.GetIcons(category);
            if (icons.Count == 0) continue;

            // Category header
            canvas.DrawText(category, leftMargin, currentY + 16, categoryPaint);
            currentY += 28;

            // Draw icons in a row (wrap if needed)
            float currentX = leftMargin;
            var maxPerRow = (int)((width - 2 * leftMargin) / iconSpacing);
            int col = 0;

            foreach (var iconName in icons)
            {
                if (col >= maxPerRow)
                {
                    col = 0;
                    currentX = leftMargin;
                    currentY += categoryHeight;
                }

                renderer.RenderIcon(canvas, category, iconName, currentX, currentY, iconSize, "#333333");

                // Draw icon name below
                canvas.DrawText(iconName, currentX + iconSize / 2, currentY + iconSize + 14, labelPaint);

                currentX += iconSpacing;
                col++;
            }

            currentY += categoryHeight + 10;

            // If next category would go off screen, stop
            if (currentY > height - categoryHeight) break;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    private static SKColor ParseColor(string hex, SKColor fallback)
    {
        if (SKColor.TryParse(hex, out var c)) return c;
        return fallback;
    }
}
