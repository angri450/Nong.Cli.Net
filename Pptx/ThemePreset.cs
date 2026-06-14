using System.Text.Json;
using ShapeCrawler;

namespace PptxCore;

public sealed class ThemePreset : IEquatable<ThemePreset>
{
    // --- Platform defaults — override these before creating any ThemePreset ---
    // On Linux/macOS, set to available fonts before using PPTX creation:
    //   ThemePreset.DefaultBodyCJK = "Noto Sans CJK SC";
    //   ThemePreset.DefaultHeadCJK = "Noto Sans CJK SC";
    //   ThemePreset.DefaultBodyFont = "DejaVu Sans";
    //   ThemePreset.DefaultHeadFont = "DejaVu Sans";

    /// <summary>Default latin body font. Change before creating presets.</summary>
    public static string DefaultBodyFont { get; set; } = "Calibri";
    /// <summary>Default latin heading font.</summary>
    public static string DefaultHeadFont { get; set; } = "Calibri";
    /// <summary>Default CJK body font. Windows: 微软雅黑, Linux: Noto Sans CJK SC, macOS: PingFang SC.</summary>
    public static string DefaultBodyCJK { get; set; } = "微软雅黑";
    /// <summary>Default CJK heading font.</summary>
    public static string DefaultHeadCJK { get; set; } = "微软雅黑";

    public string Accent1 { get; set; } = "1F4E79";
    public string Accent2 { get; set; } = "2E75B6";
    public string Accent3 { get; set; } = "4A90D9";
    public string Dark1 { get; set; } = "1A1A1A";
    public string Light1 { get; set; } = "FFFFFF";
    public string Dark2 { get; set; } = "333333";
    public string Light2 { get; set; } = "F5F5F5";
    public string BodyFont { get; set; } = DefaultBodyFont;
    public string HeadFont { get; set; } = DefaultHeadFont;
    public string BodyCJK { get; set; } = DefaultBodyCJK;
    public string HeadCJK { get; set; } = DefaultHeadCJK;

    public ThemePreset() { }

    public ThemePreset(string accent1, string accent2, string accent3,
        string dark1, string light1, string dark2, string light2,
        string bodyFont, string headFont, string bodyCJK, string headCJK)
    {
        Accent1 = accent1; Accent2 = accent2; Accent3 = accent3;
        Dark1 = dark1; Light1 = light1; Dark2 = dark2; Light2 = light2;
        BodyFont = bodyFont; HeadFont = headFont;
        BodyCJK = bodyCJK; HeadCJK = headCJK;
    }

    // --- 6 built-in presets (matching v1.0.1) ---

    public static ThemePreset Professional => new()
    {
        Accent1 = "1F4E79", Accent2 = "2E75B6", Accent3 = "4A90D9",
        Dark1 = "1A1A1A", Light1 = "FFFFFF", Dark2 = "333333", Light2 = "F5F5F5",
        BodyFont = "Calibri", HeadFont = "Calibri", BodyCJK = "微软雅黑", HeadCJK = "微软雅黑"
    };

    public static ThemePreset Academic => new()
    {
        Accent1 = "8B0000", Accent2 = "B22222", Accent3 = "CD5C5C",
        Dark1 = "2F2F2F", Light1 = "FFFFFF", Dark2 = "444444", Light2 = "F8F8F8",
        BodyFont = "Times New Roman", HeadFont = "Times New Roman", BodyCJK = "宋体", HeadCJK = "黑体"
    };

    public static ThemePreset Modern => new()
    {
        Accent1 = "2B5B84", Accent2 = "3FA34D", Accent3 = "7B4B94",
        Dark1 = "1C1C1C", Light1 = "FFFFFF", Dark2 = "3A3A3A", Light2 = "F4F6F9",
        BodyFont = "Segoe UI", HeadFont = "Segoe UI", BodyCJK = "微软雅黑", HeadCJK = "微软雅黑"
    };

    public static ThemePreset Minimal => new()
    {
        Accent1 = "555555", Accent2 = "888888", Accent3 = "BBBBBB",
        Dark1 = "222222", Light1 = "FFFFFF", Dark2 = "444444", Light2 = "F8F8F8",
        BodyFont = "Helvetica", HeadFont = "Helvetica", BodyCJK = "微软雅黑", HeadCJK = "微软雅黑"
    };

    public static ThemePreset Warm => new()
    {
        Accent1 = "E67E22", Accent2 = "F39C12", Accent3 = "F1C40F",
        Dark1 = "2C2416", Light1 = "FFFEF9", Dark2 = "4A3F2A", Light2 = "FFF8EC",
        BodyFont = "Georgia", HeadFont = "Georgia", BodyCJK = "黑体", HeadCJK = "黑体"
    };

    public static ThemePreset Cool => new()
    {
        Accent1 = "2980B9", Accent2 = "1ABC9C", Accent3 = "16A085",
        Dark1 = "1A2836", Light1 = "FFFFFF", Dark2 = "2C3E50", Light2 = "F4F8FB",
        BodyFont = "Calibri", HeadFont = "Calibri", BodyCJK = "微软雅黑", HeadCJK = "微软雅黑"
    };

    // --- JSON loader ---

    public static ThemePreset BuildFromJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var theme = JsonSerializer.Deserialize<JsonTheme>(json, opts)
                    ?? throw new InvalidOperationException($"Failed to parse theme from {jsonPath}");
        return new ThemePreset(
            theme.Accent1, theme.Accent2, theme.Accent3,
            theme.Dark1, theme.Light1, theme.Dark2, theme.Light2,
            theme.BodyFont, theme.HeadFont, theme.BodyCJK, theme.HeadCJK);
    }

    private sealed class JsonTheme
    {
        public string Accent1 { get; set; } = "";
        public string Accent2 { get; set; } = "";
        public string Accent3 { get; set; } = "";
        public string Dark1 { get; set; } = "";
        public string Light1 { get; set; } = "";
        public string Dark2 { get; set; } = "";
        public string Light2 { get; set; } = "";
        public string BodyFont { get; set; } = "";
        public string HeadFont { get; set; } = "";
        public string BodyCJK { get; set; } = "";
        public string HeadCJK { get; set; } = "";
    }

    public void Deconstruct(out string accent1, out string accent2, out string accent3,
        out string dark1, out string light1, out string dark2, out string light2,
        out string bodyFont, out string headFont, out string bodyCJK, out string headCJK)
    {
        accent1 = Accent1; accent2 = Accent2; accent3 = Accent3;
        dark1 = Dark1; light1 = Light1; dark2 = Dark2; light2 = Light2;
        bodyFont = BodyFont; headFont = HeadFont; bodyCJK = BodyCJK; headCJK = HeadCJK;
    }

    public override string ToString() => $"Theme({Accent1}/{Accent2}/{Accent3})";

    // ── Shape styling helpers ──

    /// <summary>Apply heading style to a shape's first paragraph.</summary>
    public void StyleTitle(IShape shape, decimal fontSize = 32)
    {
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = HeadFont;
            font.EastAsianName = HeadCJK;
            font.Size = fontSize;
            font.IsBold = true;
            font.Color.Set(Accent1);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ThemePreset] StyleTitle: {ex.GetType().Name}"); }
    }

    /// <summary>Apply body text style to a shape's first paragraph.</summary>
    public void StyleBody(IShape shape, decimal fontSize = 18)
    {
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = BodyFont;
            font.EastAsianName = BodyCJK;
            font.Size = fontSize;
            font.Color.Set(Dark1);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ThemePreset] StyleBody: {ex.GetType().Name}"); }
    }

    /// <summary>Apply bullet styling to a paragraph.</summary>
    public void StyleBullet(IParagraph para)
    {
        try
        {
            para.Bullet.Type = BulletType.Character;
            para.Bullet.Character = "•";
            para.Bullet.Size = 70;
            para.SetFontName(BodyCJK);
            para.SetFontSize(18);
            para.SetFontColor(Dark1);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ThemePreset] StyleBullet: {ex.GetType().Name}"); }
    }

    /// <summary>Inject theme into a presentation's master slide.</summary>
    public void ApplyToMasterSlide(IMasterSlide master)
    {
        try
        {
            var cs = master.Theme.ColorScheme;
            cs.Dark1 = Dark1;
            cs.Light1 = Light1;
            cs.Dark2 = Dark2;
            cs.Light2 = Light2;
            cs.Accent1 = Accent1;
            cs.Accent2 = Accent2;
            cs.Accent3 = Accent3;

            var fs = master.Theme.FontScheme;
            fs.HeadLatinFont = HeadFont;
            fs.BodyLatinFont = BodyFont;
            fs.HeadEastAsianFont = HeadCJK;
            fs.BodyEastAsianFont = BodyCJK;
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ThemePreset] ApplyToMasterSlide: {ex.GetType().Name}"); }
    }

    public bool Equals(ThemePreset? other) =>
        other is not null && Accent1 == other.Accent1 && Accent2 == other.Accent2 && HeadCJK == other.HeadCJK;
    public override bool Equals(object? obj) => Equals(obj as ThemePreset);
    public override int GetHashCode() => HashCode.Combine(Accent1, Accent2, Accent3);

    public static bool operator ==(ThemePreset? l, ThemePreset? r) => l?.Equals(r) ?? r is null;
    public static bool operator !=(ThemePreset? l, ThemePreset? r) => !(l == r);
}
