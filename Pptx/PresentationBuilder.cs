using ShapeCrawler;

namespace PptxCore;

public sealed class PresentationBuilder : IDisposable
{
    private readonly IPresentation _pres;
    private readonly List<string> _notes = new();
    private ThemePreset? _theme;
    private bool _showPageNumbers = true;
    private readonly Dictionary<int, List<ShapeStyle>> _pendingStyles = new();
    private bool _disposed;

    internal record ShapeStyle(int ShapeId, int? RotationDegrees);
    internal void TrackShape(int slideIdx, int shapeId, int? rotation = null)
    {
        if (rotation == null) return;
        if (!_pendingStyles.ContainsKey(slideIdx)) _pendingStyles[slideIdx] = new List<ShapeStyle>();
        _pendingStyles[slideIdx].Add(new ShapeStyle(shapeId, rotation));
    }

    internal PresentationBuilder(IPresentation pres) => _pres = pres;

    public PresentationBuilder Theme(ThemePreset theme) { _theme = theme; return this; }
    public PresentationBuilder PageNumbers(bool show = true) { _showPageNumbers = show; return this; }
    public PresentationBuilder Notes(params string[] lines) { _notes.AddRange(lines); return this; }

    public SlideHelper AddSlide()
    {
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        RemoveAllPlaceholders(slide);
        return new SlideHelper(slide, this) { Theme = _theme };
    }

    public PresentationBuilder AddTitleSlide(Action<TitleSlideBuilder> configure)
    {
        var opt = new TitleSlideBuilder(); configure(opt);
        _pres.Slides.Add(1); var slide = _pres.Slides[_pres.Slides.Count - 1];
        RemoveAllPlaceholders(slide);

        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY, LayoutSystem.ContentWidth, 80, title);
        StyleHeading(slide.Shapes[slide.Shapes.Count - 1], LayoutSystem.FontSizes.H1, opt.TitleColorHex);

        if (!string.IsNullOrEmpty(opt.SubtitleText))
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.SubtitleY, LayoutSystem.ContentWidth, 40, opt.SubtitleText);
            StyleSubtitle(slide.Shapes[slide.Shapes.Count - 1]);
        }
        if (!string.IsNullOrEmpty(opt.AuthorText))
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.AuthorY, LayoutSystem.ContentWidth, 30, opt.AuthorText);
            StyleBody(slide.Shapes[slide.Shapes.Count - 1], LayoutSystem.FontSizes.Body_SM);
        }
        if (_theme != null)
        {
            slide.Shapes.AddShape(LayoutSystem.Margin_X, LayoutSystem.Cover.DecorationY, LayoutSystem.Decoration.AccentLineWidth, LayoutSystem.Decoration.AccentLineHeight);
            slide.Shapes[slide.Shapes.Count - 1].Fill.SetColor(_theme.Accent1);
        }
        return this;
    }

    public PresentationBuilder AddContentSlide(Action<ContentSlideBuilder> configure)
    {
        var opt = new ContentSlideBuilder(); configure(opt);
        _pres.Slides.Add(1); var slide = _pres.Slides[_pres.Slides.Count - 1];
        RemoveAllPlaceholders(slide);

        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Content.TitleY, LayoutSystem.ContentWidth, 60, title);
        StyleHeading(slide.Shapes[slide.Shapes.Count - 1], LayoutSystem.FontSizes.H2, null);

        if (_theme != null)
        {
            slide.Shapes.AddShape(LayoutSystem.Margin_X, LayoutSystem.Content.BodyY - LayoutSystem.Spacing_SM, LayoutSystem.Decoration.AccentLineWidth, LayoutSystem.Decoration.AccentLineHeight);
            slide.Shapes[slide.Shapes.Count - 1].Fill.SetColor(_theme.Accent1);
        }

        int y = LayoutSystem.Content.BodyY;
        foreach (var bt in opt.BulletItems)
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X + LayoutSystem.Content.BulletIndent, y, LayoutSystem.ContentWidth - LayoutSystem.Content.BulletIndent, LayoutSystem.Content.BulletLineHeight, bt);
            StyleBullet(slide.Shapes[slide.Shapes.Count - 1]);
            y += LayoutSystem.Content.BulletLineHeight;
        }
        if (_showPageNumbers) AddPageNumber(slide);
        return this;
    }

    public PresentationBuilder AddTableSlide(Action<TableSlideBuilder> configure)
    {
        var opt = new TableSlideBuilder(); configure(opt);
        _pres.Slides.Add(1); var slide = _pres.Slides[_pres.Slides.Count - 1];
        RemoveAllPlaceholders(slide);

        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Content.TitleY, LayoutSystem.ContentWidth, 60, title);
        StyleHeading(slide.Shapes[slide.Shapes.Count - 1], LayoutSystem.FontSizes.H2, null);

        if (opt.TableData != null && opt.TableData.Length > 0)
        {
            int rows = opt.TableData.Length, cols = opt.TableData[0].Length;
            slide.Shapes.AddTable(LayoutSystem.Margin_X, LayoutSystem.Content.BodyY, cols, rows);
            var table = slide.Shapes[slide.Shapes.Count - 1].Table!;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols && c < opt.TableData[r].Length; c++)
                    table[r, c].TextBox?.SetText(opt.TableData[r][c]);
            StyleTable(table, rows, cols);
        }
        if (_showPageNumbers) AddPageNumber(slide);
        return this;
    }

    public PresentationBuilder AddChartSlide(Action<ChartSlideBuilder> configure)
    {
        var opt = new ChartSlideBuilder(); configure(opt);
        _pres.Slides.Add(1); var slide = _pres.Slides[_pres.Slides.Count - 1];
        RemoveAllPlaceholders(slide);

        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Chart.TitleY, LayoutSystem.ContentWidth, 60, title);
        StyleHeading(slide.Shapes[slide.Shapes.Count - 1], LayoutSystem.FontSizes.H2, null);

        if (opt.PieData != null)
            slide.Shapes.AddPieChart(LayoutSystem.Margin_X, LayoutSystem.Chart.ChartY, LayoutSystem.Chart.ChartWidth, LayoutSystem.Chart.ChartHeight, opt.PieData, "Series 1");
        else if (opt.BarData != null)
            slide.Shapes.AddBarChart(LayoutSystem.Margin_X, LayoutSystem.Chart.ChartY, LayoutSystem.Chart.ChartWidth, LayoutSystem.Chart.ChartHeight, opt.BarData, "Series 1");

        if (_showPageNumbers) AddPageNumber(slide);
        return this;
    }

    public string Save(string path)
    {
        if (_pres.Slides.Count == 0) _pres.Slides.Add(1);
        if (_theme != null && _pres.MasterSlides.Any()) _theme.ApplyToMasterSlide(_pres.MasterSlides[0]);
        if (_notes.Count > 0) _pres.Slides[_pres.Slides.Count - 1].AddNotes(_notes);
        _pres.Save(path);
        PostProcessOoxml(path);
        return Path.GetFullPath(path);
    }

    private void PostProcessOoxml(string path)
    {
        try
        {
            var a = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main");
            var cjk = _theme?.BodyCJK;
            using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                string xml; using (var r = new System.IO.StreamReader(entry.Open())) xml = r.ReadToEnd();
                var doc = System.Xml.Linq.XDocument.Parse(xml); bool changed = false;

                if (cjk != null)
                    foreach (var rPr in doc.Descendants(a + "rPr"))
                        if (rPr.Element(a + "ea") == null)
                        { rPr.Add(new System.Xml.Linq.XElement(a + "ea", new System.Xml.Linq.XAttribute("typeface", cjk))); changed = true; }

                var m = System.Text.RegularExpressions.Regex.Match(entry.Name, @"\d+");
                if (m.Success && _pendingStyles.TryGetValue(int.Parse(m.Value) - 1, out var styles))
                    foreach (var st in styles.Where(s => s.RotationDegrees != null))
                    {
                        var cNv = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "cNvPr" && e.Parent?.Name.LocalName == "nvSpPr" && (int?)e.Attribute("id") == st.ShapeId);
                        if (cNv == null) continue;
                        var xf = cNv.Parent?.Parent?.Elements().FirstOrDefault(e => e.Name.LocalName == "spPr")?.Element(a + "xfrm");
                        if (xf != null) { xf.SetAttributeValue("rot", st.RotationDegrees!.Value * 60000); changed = true; }
                    }

                if (changed) { entry.Delete(); using var w = new System.IO.StreamWriter(zip.CreateEntry(entry.FullName).Open()); w.Write(doc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)); }
            }
        }
        catch (Exception ex)
        {
            _notes.Add($"PostProcessOoxml: {ex.GetType().Name} — {ex.Message}");
        }
    }

    private static void RemoveAllPlaceholders(IUserSlide slide)
    {
        var toRemove = new List<IShape>();
        foreach (var s in slide.Shapes) if (s.PlaceholderType is PlaceholderType) toRemove.Add(s);
        foreach (var s in toRemove) try { s.Remove(); } catch (Exception ex) { Console.Error.WriteLine($"[PresentationBuilder] RemovePlaceholder: {ex.GetType().Name}"); }
    }

    private void StyleHeading(IShape shape, decimal fs, string? co)
    {
        if (_theme == null) return;
        try { if (shape.TextBox is not { Paragraphs.Count: > 0 } tb || tb.Paragraphs[0].Portions.Count == 0) return; var f = tb.Paragraphs[0].Portions[0].Font; f.LatinName = _theme.HeadFont; f.EastAsianName = _theme.HeadCJK; f.Size = fs; f.IsBold = true; f.Color.Set(!string.IsNullOrEmpty(co) ? co : _theme.Accent1); } catch (Exception ex) { _notes.Add($"StyleHeading: {ex.GetType().Name}"); }
    }

    private void StyleSubtitle(IShape shape)
    {
        if (_theme == null) return;
        try { if (shape.TextBox is not { Paragraphs.Count: > 0 } tb || tb.Paragraphs[0].Portions.Count == 0) return; var f = tb.Paragraphs[0].Portions[0].Font; f.LatinName = _theme.BodyFont; f.EastAsianName = _theme.BodyCJK; f.Size = 20m; f.Color.Set(_theme.Accent2); } catch (Exception ex) { _notes.Add($"StyleSubtitle: {ex.GetType().Name}"); }
    }

    private void StyleBody(IShape shape, decimal fs)
    {
        if (_theme == null) return;
        try { if (shape.TextBox is not { Paragraphs.Count: > 0 } tb || tb.Paragraphs[0].Portions.Count == 0) return; var f = tb.Paragraphs[0].Portions[0].Font; f.LatinName = _theme.BodyFont; f.EastAsianName = _theme.BodyCJK; f.Size = fs; f.Color.Set(_theme.Dark1); } catch (Exception ex) { _notes.Add($"StyleBody: {ex.GetType().Name}"); }
    }

    private void StyleBullet(IShape shape)
    {
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            foreach (var p in tb.Paragraphs)
            {
                p.Bullet.Type = BulletType.Character; p.Bullet.Character = "•"; p.Bullet.Size = 70;
                if (p.Portions.Count > 0) { var f = p.Portions[0].Font; f.Size = 18m; f.Color.Set(_theme?.Dark1 ?? "1A1A1A"); if (_theme != null) { f.LatinName = _theme.BodyFont; f.EastAsianName = _theme.BodyCJK; } }
            }
        }
        catch (Exception ex) { _notes.Add($"StyleBullet: {ex.GetType().Name}"); }
    }

    private void StyleTable(ITable table, int rows, int cols)
    {
        try { table.StyleOptions.HasHeaderRow = true; table.StyleOptions.HasBandedRows = true; } catch (Exception ex) { _notes.Add($"StyleTable-options: {ex.GetType().Name}"); }
        if (_theme == null) return;
        try
        {
            try { table.UpdateFill(_theme.Light1); } catch (Exception ex) { _notes.Add($"StyleTable-fill: {ex.GetType().Name}"); }
            for (int c = 0; c < cols; c++) try { var cell = table[0, c]; cell.Fill.SetColor(_theme.Accent1); if (cell.TextBox is { Paragraphs.Count: > 0 } ctb && ctb.Paragraphs[0].Portions.Count > 0) { var f = ctb.Paragraphs[0].Portions[0].Font; f.LatinName = _theme.HeadFont; f.EastAsianName = _theme.HeadCJK; f.Size = 16m; f.IsBold = true; f.Color.Set(_theme.Light1); } } catch (Exception ex) { _notes.Add($"StyleTable-header: {ex.GetType().Name}"); }
            for (int r = 1; r < rows; r++) { if (r % 2 == 1) continue; for (int c = 0; c < cols; c++) try { table[r, c].Fill.SetColor(_theme.Light2); } catch (Exception ex) { _notes.Add($"StyleTable-row: {ex.GetType().Name}"); } }
        }
        catch (Exception ex) { _notes.Add($"StyleTable: {ex.GetType().Name}"); }
    }

    private void AddPageNumber(IUserSlide slide)
    {
        try
        {
            slide.Shapes.AddTextBox(LayoutSystem.SlideWidth - LayoutSystem.Margin_X - 50, LayoutSystem.SlideHeight - LayoutSystem.Margin_Y - 20, 50, 20, _pres.Slides.Count.ToString());
            var s = slide.Shapes[slide.Shapes.Count - 1];
            if (s.TextBox is { Paragraphs.Count: > 0 } tb && tb.Paragraphs[0].Portions.Count > 0) { var f = tb.Paragraphs[0].Portions[0].Font; f.Size = LayoutSystem.FontSizes.Caption; f.Color.Set("999999"); if (_theme != null) { f.LatinName = _theme.BodyFont; f.EastAsianName = _theme.BodyCJK; } }
        }
        catch (Exception ex) { _notes.Add($"PageNumber: {ex.GetType().Name}"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _pres.Dispose(); } catch { /* best effort */ }
    }
}

