using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

public class ParagraphBuilder
{
    readonly Paragraph _p = new();
    ParagraphProperties? _ppr;
    readonly List<Run> _runs = new();
    readonly DocxTabStops _tabStops = new();

    public ParagraphBuilder Style(string id) { Ppr().ParagraphStyleId = new ParagraphStyleId { Val = id }; return this; }
    public ParagraphBuilder Align(JustificationValues v) { Ppr().Justification = new Justification { Val = v }; return this; }
    public ParagraphBuilder FirstLineIndent(string val) { Ppr().Indentation = new Indentation { FirstLine = val }; return this; }
    public ParagraphBuilder LineSpacing(string val, LineSpacingRuleValues? rule = null) { Ppr().SpacingBetweenLines = new SpacingBetweenLines { Line = val, LineRule = rule ?? LineSpacingRuleValues.Auto }; return this; }
    public ParagraphBuilder SpaceBefore(string val) { (Ppr().SpacingBetweenLines ??= new SpacingBetweenLines()).Before = val; return this; }
    public ParagraphBuilder SpaceAfter(string val) { (Ppr().SpacingBetweenLines ??= new SpacingBetweenLines()).After = val; return this; }
    public ParagraphBuilder KeepNext() { Ppr().KeepNext = new KeepNext(); return this; }
    public ParagraphBuilder PageBreakBefore() { Ppr().PageBreakBefore = new PageBreakBefore(); return this; }

    public ParagraphBuilder Text(string t) { _runs.Add(new Run(new Text(t))); return this; }
    public ParagraphBuilder Run(string t, Action<RunProperties>? config = null)
    {
        var rpr = new RunProperties(); config?.Invoke(rpr);
        _runs.Add(new Run(rpr.HasChildren ? rpr : null!, new Text(t))); return this;
    }
    public ParagraphBuilder Sup(string t) => Run(t, r => { r.VerticalTextAlignment = new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }; r.FontSize = new FontSize { Val = "18" }; });
    public ParagraphBuilder Bold(string t) => Run(t, r => { r.Bold = new Bold(); });

    public ParagraphBuilder TabStop(double positionCm, TabAlignment alignment, TabLeader leader = TabLeader.None)
    {
        _tabStops.Add(new TabStopSpec(positionCm, alignment, leader));
        return this;
    }

    public Paragraph Build()
    {
        _tabStops.ApplyTo(Ppr());
        if (_ppr != null) _p.Append(_ppr);
        foreach (var r in _runs) _p.Append(r);
        return _p;
    }
    ParagraphProperties Ppr() { _ppr ??= new ParagraphProperties(); return _ppr; }
}

public class TableBuilder
{
    readonly Table _t = new();
    readonly TableProperties _tpr = new();
    readonly TableGrid _grid = new();
    readonly List<TableRow> _rows = new();
    readonly List<HorizontalMerge> _pendingMerges = new();
    readonly List<VMerge> _pendingVMerges = new();

    record HorizontalMerge(int Row, int Col1, int Col2);
    record VMerge(int Col, int Row1, int Row2);

    public TableBuilder WidthPct(int pct = 100) { _tpr.Append(new TableWidth { Type = TableWidthUnitValues.Pct, Width = (pct * 50).ToString() }); return this; }
    public TableBuilder AutoWidth() { _tpr.Append(new TableLayout { Type = TableLayoutValues.Fixed }); return this; }

    /// <summary>应用 Word 内置表格样式。如 Style(TableStyles.LightGridAccent1)。</summary>
    public TableBuilder Style(string styleId) { _tpr.Append(new TableStyle { Val = styleId }); return this; }

    public TableBuilder ThreeLineBorders(uint thick = 6, uint thin = 4)
    {
        _tpr.Append(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = thick, Color = "000000" },
            new BottomBorder { Val = BorderValues.Single, Size = thick, Color = "000000" },
            new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));
        return this;
    }

    public TableBuilder Columns(params int[] widths) { foreach (var w in widths) _grid.Append(new GridColumn { Width = w.ToString() }); return this; }
    public TableBuilder HeaderRow(params string[] cells) { var row = new TableRow(); foreach (var c in cells) row.Append(MakeCell(c, true, true)); _rows.Add(row); return this; }
    public TableBuilder DataRow(params string[] cells) { var row = new TableRow(); foreach (var c in cells) row.Append(MakeCell(c, false, false)); _rows.Add(row); return this; }
    public TableBuilder Headers(params string[] cells) => HeaderRow(cells);
    public TableBuilder Row(params string[] cells) => DataRow(cells);

    /// <summary>Merge cells horizontally: merge columns col1 through col2 (inclusive) on the given row.</summary>
    public TableBuilder MergeHorizontal(int row, int col1, int col2)
    {
        _pendingMerges.Add(new HorizontalMerge(row, col1, col2));
        return this;
    }

    /// <summary>Merge cells vertically: merge rows row1 through row2 (inclusive) on the given column.</summary>
    public TableBuilder MergeVertical(int col, int row1, int row2)
    {
        _pendingVMerges.Add(new VMerge(col, row1, row2));
        return this;
    }

    /// <summary>Merge a rectangular range of cells.</summary>
    public TableBuilder MergeRange(int row1, int col1, int row2, int col2)
    {
        MergeHorizontal(row1, col1, col2);
        if (row2 > row1)
        {
            for (int c = col1; c <= col2; c++)
                MergeVertical(c, row1, row2);
        }
        return this;
    }

    /// <summary>Split a merged cell back into individual cells.</summary>
    public TableBuilder SplitAt(int row, int col)
    {
        if (row >= 0 && row < _rows.Count)
        {
            var cells = _rows[row].Elements<TableCell>().ToList();
            if (col >= 0 && col < cells.Count)
            {
                var cell = cells[col];
                var tcPr = cell.TableCellProperties;
                int span = 1;
                if (tcPr != null)
                {
                    span = tcPr.GridSpan?.Val?.Value ?? 1;
                    tcPr.GridSpan?.Remove();
                    tcPr.VerticalMerge?.Remove();
                }
                // Insert additional cells to fill the span
                for (int i = 1; i < span; i++)
                {
                    var newCell = MakeCell("", false, false);
                    _rows[row].InsertAfter(newCell, cell);
                    cell = newCell;
                }
            }
        }
        return this;
    }

    /// <summary>Reconstruct a TableBuilder from an existing table for editing.</summary>
    public static TableBuilder FromExisting(Table table)
    {
        var builder = new TableBuilder();
        // Copy table properties
        var existingTpr = table.GetFirstChild<TableProperties>();
        if (existingTpr != null)
        {
            foreach (var child in existingTpr.CloneNode(true).ChildElements)
                builder._tpr.Append(child.CloneNode(true));
        }
        // Copy grid
        var existingGrid = table.GetFirstChild<TableGrid>();
        if (existingGrid != null)
        {
            foreach (var gc in existingGrid.Elements<GridColumn>())
                builder._grid.Append(new GridColumn { Width = gc.Width?.Value?.ToString() });
        }
        // Copy rows
        foreach (var row in table.Elements<TableRow>())
        {
            builder._rows.Add((TableRow)row.CloneNode(true));
        }
        return builder;
    }

    public Table Build()
    {
        // Process pending horizontal merges
        foreach (var m in _pendingMerges)
        {
            if (m.Row >= 0 && m.Row < _rows.Count)
            {
                var row = _rows[m.Row];
                var cells = row.Elements<TableCell>().ToList();
                if (m.Col1 >= 0 && m.Col2 < cells.Count && m.Col1 < m.Col2)
                {
                    var targetCell = cells[m.Col1];
                    var tcPr = targetCell.TableCellProperties;
                    if (tcPr == null) { tcPr = new TableCellProperties(); targetCell.PrependChild(tcPr); }
                    var existingSpan = tcPr.GridSpan;
                    if (existingSpan != null) existingSpan.Remove();
                    tcPr.Append(new GridSpan { Val = m.Col2 - m.Col1 + 1 });

                    // Remove merged cells
                    for (int c = m.Col2; c > m.Col1; c--)
                        cells[c].Remove();
                }
            }
        }

        // Process pending vertical merges
        foreach (var vm in _pendingVMerges)
        {
            if (vm.Row1 >= 0 && vm.Row2 < _rows.Count && vm.Row1 < vm.Row2)
            {
                for (int r = vm.Row1; r <= vm.Row2; r++)
                {
                    var cells = _rows[r].Elements<TableCell>().ToList();
                    if (vm.Col >= 0 && vm.Col < cells.Count)
                    {
                        var tcPr = cells[vm.Col].TableCellProperties;
                        if (tcPr == null) { tcPr = new TableCellProperties(); cells[vm.Col].PrependChild(tcPr); }
                        if (r == vm.Row1)
                            tcPr.VerticalMerge = new DocumentFormat.OpenXml.Wordprocessing.VerticalMerge { Val = MergedCellValues.Restart };
                        else
                            tcPr.VerticalMerge = new DocumentFormat.OpenXml.Wordprocessing.VerticalMerge(); // continue
                    }
                }
            }
        }

        _t.Append(_tpr);
        _t.Append(_grid);
        foreach (var r in _rows) _t.Append(r);
        return _t;
    }

    static TableCell MakeCell(string tx, bool isHeader, bool bottomBorder)
    {
        var tc = new TableCell();
        var tcProps = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bottomBorder) tcProps.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tc.Append(tcProps);
        var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "BodyTextNoIndent" }, new Justification { Val = JustificationValues.Center }, new SpacingBetweenLines { Before = "40", After = "40" }));
        if (isHeader) p.Append(new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new FontSize { Val = "21" }), new Text(tx)));
        else p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "21" }), new Text(tx)));
        tc.Append(p); return tc;
    }
}

public class HeaderFooterBuilder
{
    readonly HeaderPart _header; readonly FooterPart _footer; readonly MainDocumentPart _main;

    public HeaderFooterBuilder(WordprocessingDocument doc) { _main = doc.MainDocumentPart!; _header = _main.AddNewPart<HeaderPart>(); _footer = _main.AddNewPart<FooterPart>(); }

    public HeaderFooterBuilder PageNumberFooter(string fontCJK = "宋体", string fontSize = "21")
    {
        _footer.Footer = new Footer();
        var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK }, new FontSize { Val = fontSize }), new FieldChar { FieldCharType = FieldCharValues.Begin }));
        p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK }, new FontSize { Val = fontSize }), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK }, new FontSize { Val = fontSize }), new FieldChar { FieldCharType = FieldCharValues.End }));
        _footer.Footer.Append(p); return this;
    }

    public HeaderFooterBuilder SetForSection(SectionProperties sectPr)
    {
        string hId = _main.GetIdOfPart(_header); string fId = _main.GetIdOfPart(_footer);
        sectPr.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = hId });
        sectPr.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = fId });
        return this;
    }
}
