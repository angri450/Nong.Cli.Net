using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShapeCrawler;

namespace PptxCore;

public static class SlidePreview
{
    public static PreviewResult Preview(string path)
    {
        using var pres = new Presentation(path);
        return Preview(pres);
    }

    public static PreviewResult Preview(IPresentation pres)
    {
        var sb = new StringBuilder();
        var warnings = new List<string>();

        sb.AppendLine($"Slides: {pres.Slides.Count}  Size: {pres.SlideWidth}x{pres.SlideHeight}pt");
        sb.AppendLine(new string('-', 60));

        for (int i = 0; i < pres.Slides.Count; i++)
        {
            var slide = pres.Slides[i];
            sb.AppendLine($"\n--- Slide {i + 1} ---");
            foreach (var shape in slide.Shapes)
            {
                string name = shape.Name;
                string type = shape.ContentType.ToString();
                string text = shape.TextBox?.Text ?? "";
                string extra = "";

                if (shape.Table != null)
                {
                    type = "Table";
                    extra = $" ({shape.Table.Rows.Count}r x {shape.Table.Columns.Count}c)";
                }
                else if (shape.PieChart != null || shape.BarChart != null || shape.ColumnChart != null)
                {
                    type = "Chart";
                }

                sb.AppendLine($"  [{type}] {name}");
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine($"    \"{Truncate(text, 120)}\"");

                try
                {
                    if (shape.TextBox is { Paragraphs.Count: > 0 })
                    {
                        var para = shape.TextBox.Paragraphs[0];
                        if (para.Portions.Count > 0)
                        {
                            var font = para.Portions[0].Font;
                            var parts = new List<string>();
                            if (font.Size > 0) parts.Add($"{font.Size}pt");
                            if (font.IsBold) parts.Add("Bold");
                            if (!string.IsNullOrEmpty(font.LatinName)) parts.Add(font.LatinName);
                            if (!string.IsNullOrEmpty(font.EastAsianName) && font.EastAsianName != font.LatinName)
                                parts.Add($"CJK={font.EastAsianName}");
                            try
                            {
                                if (font.Color is { } c && !string.IsNullOrEmpty(c.Hex))
                                    parts.Add($"#{c.Hex}");
                            }
                            catch (Exception ex) { warnings.Add($"font-color: {ex.GetType().Name}"); }
                            if (parts.Count > 0)
                                sb.AppendLine($"    Font: {string.Join(" ", parts)}");
                        }
                    }
                }
                catch (Exception ex) { warnings.Add($"font-info: {ex.GetType().Name}"); }
                if (!string.IsNullOrEmpty(extra))
                    sb.AppendLine($"    {extra}");

                if (string.IsNullOrWhiteSpace(text) && shape.Table == null
                    && shape.PieChart == null && shape.BarChart == null && shape.ColumnChart == null)
                    warnings.Add($"Slide {i + 1}: empty shape '{name}'");
            }

            if (slide.Notes?.Text is { Length: > 0 })
                sb.AppendLine($"  Notes: {Truncate(slide.Notes.Text, 200)}");
        }

        if (pres.Slides.Count == 0)
            warnings.Add("Presentation has 0 slides");

        return new PreviewResult(sb.ToString().TrimEnd(), warnings);
    }

    public static ShapeMapResult ShapeMap(string path)
    {
        using var pres = new Presentation(path);
        return ShapeMap(pres);
    }

    public static ShapeMapResult ShapeMap(IPresentation pres)
    {
        var slides = new List<ShapeMapSlide>();
        for (int i = 0; i < pres.Slides.Count; i++)
        {
            var slide = pres.Slides[i];
            var shapes = new List<ShapeMapShape>();

            foreach (var shape in slide.Shapes)
            {
                string? placeholder = shape.PlaceholderType?.ToString()?.ToLowerInvariant();

                string? fontName = null;
                decimal? fontSize = null;
                try
                {
                    if (shape.TextBox is { Paragraphs.Count: > 0 })
                    {
                        var para = shape.TextBox.Paragraphs[0];
                        if (para.Portions.Count > 0)
                        {
                            var font = para.Portions[0].Font;
                            fontName = font.LatinName;
                            fontSize = font.Size;
                        }
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine($"[SlidePreview] shape-map: {ex.GetType().Name}"); }
                // ShapeCrawler 0.79.2 doesn't expose X/Y directly — use SDK element as fallback
                shapes.Add(new ShapeMapShape(
                    Name: shape.Name,
                    Type: shape.ContentType.ToString(),
                    Placeholder: placeholder,
                    Text: shape.TextBox?.Text ?? "",
                    FontSize: fontSize,
                    FontName: fontName,
                    X: 0,
                    Y: 0,
                    W: (int)shape.Width,
                    H: (int)shape.Height
                ));
            }

            slides.Add(new ShapeMapSlide(
                Index: i + 1,
                Layout: "",
                Shapes: shapes
            ));
        }

        var root = new ShapeMapRoot(
            SlideCount: pres.Slides.Count,
            SlideWidth: (int)pres.SlideWidth,
            SlideHeight: (int)pres.SlideHeight,
            Slides: slides
        );

        var json = JsonSerializer.Serialize(root, ShapeMapJsonContext.Default.ShapeMapRoot);
        return new ShapeMapResult(json);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";

    public sealed class PreviewResult : IEquatable<PreviewResult>
    {
        public string Text { get; set; }
        public List<string> Warnings { get; set; }

        public PreviewResult(string text, List<string> warnings)
        {
            Text = text;
            Warnings = warnings;
        }

        public override string ToString() => Text;

        public void Deconstruct(out string text, out List<string> warnings)
        {
            text = Text; warnings = Warnings;
        }

        public bool Equals(PreviewResult? other) =>
            other is not null && Text == other.Text;
        public override bool Equals(object? obj) => Equals(obj as PreviewResult);
        public override int GetHashCode() => Text.GetHashCode();
        public static bool operator ==(PreviewResult? l, PreviewResult? r) => l?.Equals(r) ?? r is null;
        public static bool operator !=(PreviewResult? l, PreviewResult? r) => !(l == r);
    }
}

// --- ShapeMap types (new in v1.0.2) ---

public sealed class ShapeMapResult
{
    public string Json { get; }
    internal ShapeMapResult(string json) => Json = json;
    public override string ToString() => Json;
}

public sealed record ShapeMapRoot(
    int SlideCount,
    int SlideWidth,
    int SlideHeight,
    List<ShapeMapSlide> Slides
);

public sealed record ShapeMapSlide(
    int Index,
    string Layout,
    List<ShapeMapShape> Shapes
);

public sealed record ShapeMapShape(
    string Name,
    string Type,
    string? Placeholder,
    string Text,
    decimal? FontSize,
    string? FontName,
    int X,
    int Y,
    int W,
    int H
);

[JsonSerializable(typeof(ShapeMapRoot))]
[JsonSerializable(typeof(ShapeMapSlide))]
[JsonSerializable(typeof(ShapeMapShape))]
internal partial class ShapeMapJsonContext : JsonSerializerContext { }
