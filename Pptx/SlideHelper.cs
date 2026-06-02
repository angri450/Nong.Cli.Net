using ShapeCrawler;

namespace PptxCore;

public sealed class SlideHelper
{
    private readonly IUserSlide _slide;
    private readonly PresentationBuilder? _builder;
    internal ThemePreset? Theme { get; set; }

    internal SlideHelper(IUserSlide slide, PresentationBuilder? builder = null)
    {
        _slide = slide;
        _builder = builder;
    }

    public IUserSlide Slide => _slide;

    public PresentationBuilder EndSlide()
    {
        if (_builder == null) throw new InvalidOperationException("SlideHelper not from PresentationBuilder");
        return _builder;
    }

    // ── Basic shape methods ──

    public SlideHelper TextBox(string text, int x, int y, int w, int h,
        int fontSize = 18, string fontName = "", bool bold = false,
        string colorHex = "", string align = "Left")
    {
        _slide.Shapes.AddTextBox(x, y, w, h, text);
        var shape = _slide.Shapes[_slide.Shapes.Count - 1];
        if (shape.TextBox is { Paragraphs.Count: > 0 } tb && tb.Paragraphs[0].Portions.Count > 0)
        {
            try { tb.LeftMargin = 8; tb.RightMargin = 8; tb.TopMargin = 4; tb.BottomMargin = 4; } catch { }
            try { tb.Paragraphs[0].HorizontalAlignment = align switch { "Center" => ShapeCrawler.TextHorizontalAlignment.Center, "Right" => ShapeCrawler.TextHorizontalAlignment.Right, _ => ShapeCrawler.TextHorizontalAlignment.Left }; } catch { }
            var font = tb.Paragraphs[0].Portions[0].Font;
            if (Theme != null) { font.LatinName = fontName.Length > 0 ? fontName : Theme.BodyFont; font.EastAsianName = Theme.BodyCJK; font.Size = fontSize; font.IsBold = bold; font.Color.Set(colorHex.Length > 0 ? colorHex : Theme.Dark1); }
            else { font.Size = fontSize; font.IsBold = bold; if (fontName.Length > 0) font.LatinName = fontName; }
            try { var sp = tb.Paragraphs[0].Spacing; sp.BeforeSpacing = 4; sp.AfterSpacing = 4; } catch { }
        }
        return this;
    }

    public SlideHelper Shape(Geometry geometry, int x, int y, int w, int h,
        string fillHex = "", string text = "", string? borderColor = null, int? rotation = null)
    {
        var scGeom = MapGeometry(geometry);
        if (!string.IsNullOrEmpty(text)) _slide.Shapes.AddShape(x, y, w, h, scGeom, text);
        else _slide.Shapes.AddShape(x, y, w, h, scGeom);
        var shape = _slide.Shapes[_slide.Shapes.Count - 1];
        if (!string.IsNullOrEmpty(fillHex)) { try { shape.Fill.SetColor(fillHex); } catch { } }
        else if (Theme != null) { try { shape.Fill.SetColor(Theme.Accent1); } catch { } }
        if (!string.IsNullOrEmpty(text) && Theme != null) { try { Theme.StyleBody(shape, 14m); } catch { } }
        if (borderColor != null) { try { shape.Outline.SetHexColor(borderColor); shape.Outline.Weight = 1; } catch { } }
        if (rotation != null) { _builder?.TrackShape(_slide.Number - 1, shape.Id, rotation); }
        return this;
    }

    public SlideHelper Background(string colorHex) { try { _slide.Fill.SetColor(colorHex); } catch { } return this; }

    // ── Decoration helpers ──

    public SlideHelper HorizontalLine(int x, int y, int w, int h = 2, string? color = null)
    { Shape(Geometry.Rectangle, x, y, w, h, fillHex: color ?? Theme?.Accent1 ?? "1F4E79"); return this; }

    public SlideHelper VerticalLine(int x, int y, int h, int w = 2, string? color = null)
    { Shape(Geometry.Rectangle, x, y, w, h, fillHex: color ?? Theme?.Accent1 ?? "1F4E79"); return this; }

    // ── Layout modes ──

    public SlideHelper TwoColumns(string title, string leftContent, string rightContent)
    {
        int cw = LayoutSystem.TwoColumn.ColumnWidth, cs = LayoutSystem.TwoColumn.ColumnSpacing;
        TextBox(title, LayoutSystem.Margin_X, LayoutSystem.Content.TitleY, LayoutSystem.ContentWidth, LayoutSystem.Content.TitleHeight, fontSize: 32, bold: true);
        TextBox(leftContent, LayoutSystem.Margin_X, LayoutSystem.Content.BodyY, cw, 360, fontSize: 16);
        TextBox(rightContent, LayoutSystem.Margin_X + cw + cs, LayoutSystem.Content.BodyY, cw, 360, fontSize: 16);
        return this;
    }

    public SlideHelper Cards(string title, params (string cardTitle, string cardBody)[] cards)
    {
        if (cards.Length == 0 || cards.Length > 4) return this;
        TextBox(title, LayoutSystem.Margin_X, LayoutSystem.Content.TitleY, LayoutSystem.ContentWidth, LayoutSystem.Content.TitleHeight, fontSize: 32, bold: true);
        int cs = LayoutSystem.Spacing_SM, cw = (LayoutSystem.ContentWidth - cs * (cards.Length - 1)) / cards.Length;
        int ch = LayoutSystem.Card.DefaultHeight, cx = LayoutSystem.Margin_X, cy = LayoutSystem.Content.BodyY;
        foreach (var (ct, cb) in cards)
        {
            Shape(Geometry.RoundedRectangle, cx, cy, cw, ch, fillHex: Theme?.Light2 ?? "F5F5F5", borderColor: Theme?.Accent2 ?? "2E75B6");
            TextBox(ct, cx + LayoutSystem.Card.Padding, cy + LayoutSystem.Card.Padding, cw - 2 * LayoutSystem.Card.Padding, LayoutSystem.Card.TitleHeight, fontSize: 20, bold: true);
            TextBox(cb, cx + LayoutSystem.Card.Padding, cy + LayoutSystem.Card.Padding + LayoutSystem.Card.TitleHeight, cw - 2 * LayoutSystem.Card.Padding, LayoutSystem.Card.BodyHeight, fontSize: 14);
            cx += cw + cs;
        }
        return this;
    }

    public SlideHelper BigNumber(string number, string description, string? unit = null)
    {
        TextBox(number, LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY, LayoutSystem.ContentWidth, 120, fontSize: 72, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        if (!string.IsNullOrEmpty(unit)) TextBox(unit, LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY + 130, LayoutSystem.ContentWidth, 40, fontSize: 24, colorHex: Theme?.Accent2 ?? "2E75B6");
        TextBox(description, LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY + 200, LayoutSystem.ContentWidth, 80, fontSize: 20);
        return this;
    }

    public SlideHelper Quote(string quoteText, string attribution)
    {
        TextBox("\"", LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY, 80, 80, fontSize: 96, colorHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(quoteText, LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY + 80, LayoutSystem.ContentWidth, 200, fontSize: 24, fontName: Theme?.BodyFont ?? "Calibri");
        TextBox($"- {attribution}", LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY + 300, LayoutSystem.ContentWidth, 40, fontSize: 18, colorHex: Theme?.Accent2 ?? "2E75B6");
        return this;
    }

    // ── Gravity-field layouts ──

    public SlideHelper SingleFocus(string mainContent, string? subtitle = null)
    {
        int cy = LayoutSystem.Cover.TitleY, ch = 200;
        TextBox(mainContent, LayoutSystem.Margin_X, cy, LayoutSystem.ContentWidth, ch, fontSize: 48, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        if (!string.IsNullOrEmpty(subtitle)) TextBox(subtitle, LayoutSystem.Margin_X, cy + ch + LayoutSystem.Spacing_MD, LayoutSystem.ContentWidth, LayoutSystem.Cover.SubtitleHeight, fontSize: 20, colorHex: Theme?.Dark2 ?? "444444");
        return this;
    }

    public SlideHelper Symmetric(string leftTitle, string leftContent, string rightTitle, string rightContent)
    {
        int cw = LayoutSystem.TwoColumn.ColumnWidth, cs = LayoutSystem.TwoColumn.ColumnSpacing, y = LayoutSystem.Content.BodyY;
        TextBox(leftTitle, LayoutSystem.Margin_X, y, cw, 40, fontSize: 24, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(leftContent, LayoutSystem.Margin_X, y + 50, cw, 350, fontSize: 16);
        int rx = LayoutSystem.Margin_X + cw + cs;
        TextBox(rightTitle, rx, y, cw, 40, fontSize: 24, bold: true, colorHex: Theme?.Accent2 ?? "2E75B6");
        TextBox(rightContent, rx, y + 50, cw, 350, fontSize: 16);
        return this;
    }

    public SlideHelper Asymmetric(string mainTitle, string mainContent, string sideTitle, string sideContent)
    {
        int mw = (int)(LayoutSystem.ContentWidth * 0.66), sw = LayoutSystem.ContentWidth - mw - LayoutSystem.Spacing_MD, y = LayoutSystem.Content.BodyY;
        TextBox(mainTitle, LayoutSystem.Margin_X, y, mw, 40, fontSize: 28, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(mainContent, LayoutSystem.Margin_X, y + 50, mw, 350, fontSize: 18);
        int sx = LayoutSystem.Margin_X + mw + LayoutSystem.Spacing_MD;
        TextBox(sideTitle, sx, y, sw, 40, fontSize: 20, bold: true, colorHex: Theme?.Accent2 ?? "2E75B6");
        TextBox(sideContent, sx, y + 50, sw, 350, fontSize: 14);
        return this;
    }

    public SlideHelper ThreeColumn(string c1t, string c1c, string c2t, string c2c, string c3t, string c3c)
    {
        int cw = LayoutSystem.ThreeColumn.ColumnWidth, cs = LayoutSystem.ThreeColumn.ColumnSpacing, y = LayoutSystem.Content.BodyY;
        TextBox(c1t, LayoutSystem.Margin_X, y, cw, 40, fontSize: 22, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(c1c, LayoutSystem.Margin_X, y + 50, cw, 350, fontSize: 16);
        int c2x = LayoutSystem.Margin_X + cw + cs;
        TextBox(c2t, c2x, y, cw, 40, fontSize: 22, bold: true, colorHex: Theme?.Accent2 ?? "2E75B6");
        TextBox(c2c, c2x, y + 50, cw, 350, fontSize: 16);
        int c3x = c2x + cw + cs;
        TextBox(c3t, c3x, y, cw, 40, fontSize: 22, bold: true, colorHex: Theme?.Accent3 ?? "4A90D9");
        TextBox(c3c, c3x, y + 50, cw, 350, fontSize: 16);
        return this;
    }

    public SlideHelper PrimarySecondary(string mainTitle, string mainContent, params (string title, string content)[] supportingItems)
    {
        if (supportingItems.Length == 0) return this;
        int mw = (int)(LayoutSystem.ContentWidth * 0.55), sw = LayoutSystem.ContentWidth - mw - LayoutSystem.Spacing_MD, y = LayoutSystem.Content.BodyY;
        TextBox(mainTitle, LayoutSystem.Margin_X, y, mw, 40, fontSize: 28, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(mainContent, LayoutSystem.Margin_X, y + 50, mw, 350, fontSize: 18);
        int sx = LayoutSystem.Margin_X + mw + LayoutSystem.Spacing_MD, ih = 350 / Math.Max(supportingItems.Length, 1);
        for (int i = 0; i < supportingItems.Length && i < 3; i++)
        {
            var (t, c) = supportingItems[i]; int iy = y + i * (ih + LayoutSystem.Spacing_SM);
            TextBox(t, sx, iy, sw, 30, fontSize: 18, bold: true, colorHex: Theme?.Accent2 ?? "2E75B6");
            TextBox(c, sx, iy + 35, sw, ih - 45, fontSize: 14);
        }
        return this;
    }

    public SlideHelper HeroTop(string heroTitle, string? heroContent, params (string title, string value)[] bottomCards)
    {
        int hh = 200, hy = LayoutSystem.Content.TitleY;
        Shape(Geometry.RoundedRectangle, LayoutSystem.Margin_X, hy, LayoutSystem.ContentWidth, hh, fillHex: Theme?.Accent1 ?? "1F4E79");
        TextBox(heroTitle, LayoutSystem.Margin_X + 30, hy + 30, LayoutSystem.ContentWidth - 60, 80, fontSize: 36, bold: true, colorHex: "FFFFFF");
        if (!string.IsNullOrEmpty(heroContent)) TextBox(heroContent, LayoutSystem.Margin_X + 30, hy + 120, LayoutSystem.ContentWidth - 60, 60, fontSize: 18, colorHex: "FFFFFF");
        if (bottomCards.Length > 0 && bottomCards.Length <= 4)
        {
            int cs = LayoutSystem.Spacing_SM, cw = (LayoutSystem.ContentWidth - cs * (bottomCards.Length - 1)) / bottomCards.Length;
            int cy = hy + hh + LayoutSystem.Spacing_MD, ch = 160;
            for (int i = 0; i < bottomCards.Length; i++)
            {
                var (t, v) = bottomCards[i]; int cx = LayoutSystem.Margin_X + i * (cw + cs);
                Shape(Geometry.RoundedRectangle, cx, cy, cw, ch, fillHex: Theme?.Light2 ?? "F5F5F5", borderColor: Theme?.Accent2 ?? "2E75B6");
                TextBox(v, cx + LayoutSystem.Card.Padding, cy + 30, cw - 2 * LayoutSystem.Card.Padding, 60, fontSize: 32, bold: true, colorHex: Theme?.Accent1 ?? "1F4E79");
                TextBox(t, cx + LayoutSystem.Card.Padding, cy + 100, cw - 2 * LayoutSystem.Card.Padding, 40, fontSize: 14, colorHex: Theme?.Dark2 ?? "444444");
            }
        }
        return this;
    }

    private static ShapeCrawler.Geometry MapGeometry(PptxCore.Geometry geo) => geo switch
    {
        PptxCore.Geometry.Rectangle => ShapeCrawler.Geometry.Rectangle, PptxCore.Geometry.RoundedRectangle => ShapeCrawler.Geometry.RoundedRectangle,
        PptxCore.Geometry.Ellipse => ShapeCrawler.Geometry.Ellipse, PptxCore.Geometry.Triangle => ShapeCrawler.Geometry.Triangle,
        PptxCore.Geometry.Diamond => ShapeCrawler.Geometry.Diamond, PptxCore.Geometry.Arrow => ShapeCrawler.Geometry.RightArrow,
        PptxCore.Geometry.Line => ShapeCrawler.Geometry.Line, _ => ShapeCrawler.Geometry.Rectangle
    };
}

public enum Geometry { Rectangle, RoundedRectangle, Ellipse, Triangle, Diamond, Arrow, Line }
