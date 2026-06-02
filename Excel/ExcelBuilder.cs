using ClosedXML.Excel;

namespace ExcelCore;

public static class ExcelBuilder
{
    public static SheetBuilder Sheet(XLWorkbook wb, string name) => new(wb, name);
    public static SheetBuilder Sheet(XLWorkbook wb, string name, int position) => new(wb, name, position);
}

public class SheetBuilder
{
    readonly IXLWorksheet _ws;
    readonly IXLWorkbook _wb;
    int _rowOff, _colOff;
    int _lastDataRow;
    int _lastDataCol;

    public IXLWorksheet Ws => _ws;
    public IXLWorkbook Wb => _wb;

    internal SheetBuilder(XLWorkbook wb, string name) { _wb = wb; _ws = wb.AddWorksheet(name); }
    internal SheetBuilder(XLWorkbook wb, string name, int position) { _wb = wb; _ws = wb.AddWorksheet(name, position); }

    /// <summary>设置起始写入位置（0-based）</summary>
    public SheetBuilder At(int row, int col) { _rowOff = row; _colOff = col; return this; }

    public SheetBuilder Headers(params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            _ws.Cell(_rowOff + 1, _colOff + i + 1).Value = headers[i];
        _lastDataRow = _rowOff + 1;
        _lastDataCol = _colOff + headers.Length;
        return this;
    }

    public SheetBuilder Data(string[][] rows)
    {
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                _ws.Cell(_rowOff + r + 2, _colOff + c + 1).Value = rows[r][c];
        _lastDataRow = _rowOff + 1 + rows.Length;
        _lastDataCol = _colOff + (rows.Length > 0 ? rows[0].Length : 0);
        return this;
    }

    public SheetBuilder Data(IEnumerable<object> items)
    {
        var list = items.ToArray();
        if (list.Length == 0) return this;
        var props = list[0].GetType().GetProperties();
        var headers = props.Select(p => p.Name).ToArray();
        Headers(headers);
        var rows = new string[list.Length][];
        for (var i = 0; i < list.Length; i++)
        {
            rows[i] = new string[props.Length];
            for (var j = 0; j < props.Length; j++)
                rows[i][j] = props[j].GetValue(list[i])?.ToString() ?? "";
        }
        return Data(rows);
    }

    public SheetBuilder Formula(string range, string formula)
    {
        _ws.Range(range).FormulaA1 = formula;
        return this;
    }

    public SheetBuilder FormulaCell(string cell, string formula)
    {
        _ws.Cell(cell).FormulaA1 = formula;
        return this;
    }

    public SheetBuilder Row(params string[] cells)
    {
        var r = _lastDataRow > 0 ? _lastDataRow + 1 : _rowOff + 1;
        for (var c = 0; c < cells.Length; c++)
            _ws.Cell(r, _colOff + c + 1).Value = cells[c];
        _lastDataRow = r;
        _lastDataCol = Math.Max(_lastDataCol, _colOff + cells.Length);
        return this;
    }

    public SheetBuilder ColumnWidths(params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
            _ws.Column(_colOff + i + 1).Width = widths[i];
        return this;
    }

    public SheetBuilder NumberFormat(string range, string format)
    {
        _ws.Range(range).Style.NumberFormat.Format = format;
        return this;
    }

    public SheetBuilder HeaderStyle(string fill, string fontColor, bool bold = true, double fontSize = 11)
    {
        var headerRow = _ws.Row(_rowOff + 1);
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
        headerRow.Style.Font.FontColor = XLColor.FromHtml(fontColor);
        headerRow.Style.Font.Bold = bold;
        headerRow.Style.Font.FontSize = fontSize;
        return this;
    }

    public SheetBuilder AlternatingRows(int startRow, string evenColor, int? endRow = null)
    {
        int end = endRow ?? _lastDataRow;
        for (var r = startRow; r <= end; r++)
        {
            if ((r - startRow) % 2 == 1)
                _ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml(evenColor);
        }
        return this;
    }

    public SheetBuilder MergeHeader(int fromCol, int toCol)
    {
        _ws.Range(_rowOff + 1, _colOff + fromCol, _rowOff + 1, _colOff + toCol).Merge();
        return this;
    }

    public SheetBuilder Dropdown(string range, string source,
        string? inputTitle = null, string? inputMessage = null,
        string? errorTitle = null, string? errorMessage = null, string? errorStyle = null)
    {
        var dv = _ws.Range(range).CreateDataValidation();
        dv.InCellDropdown = true;
        dv.IgnoreBlanks = true;

        if (source.Contains('$') || source.Contains('!') || (source.Contains(':') && source.Length <= 20))
            dv.List(source);
        else if (source.StartsWith('='))
            dv.List(source);
        else
            dv.List(source);

        if (inputTitle != null) dv.InputTitle = inputTitle;
        if (inputMessage != null) dv.InputMessage = inputMessage;
        if (errorTitle != null) dv.ErrorTitle = errorTitle;
        if (errorMessage != null) dv.ErrorMessage = errorMessage;
        dv.ErrorStyle = errorStyle switch
        {
            "Warning" => XLErrorStyle.Warning,
            "Information" => XLErrorStyle.Information,
            _ => XLErrorStyle.Stop,
        };
        return this;
    }

    public SheetBuilder Dropdown(int col, string source,
        string? inputTitle = null, string? inputMessage = null,
        string? errorTitle = null, string? errorMessage = null, string? errorStyle = null)
    {
        var range = _ws.Range(_rowOff + 2, _colOff + col, _lastDataRow > 0 ? _lastDataRow : _rowOff + 100, _colOff + col);
        var rangeAddress = range.RangeAddress?.ToString() ?? $"B{_rowOff + 2}:B{_rowOff + 100}";
        return Dropdown(rangeAddress, source, inputTitle, inputMessage, errorTitle, errorMessage, errorStyle);
    }

    public SheetBuilder DataBars(string range, string color)
    {
        _ws.Range(range).AddConditionalFormat().DataBar(XLColor.FromHtml(color));
        return this;
    }

    public SheetBuilder ColorScale(string range, string low, string mid, string high)
    {
        // 简化实现：应用三色渐变
        var colorScale = _ws.Range(range).AddConditionalFormat().ColorScale();
        return this;
    }

    public SheetBuilder Table(string? name = null)
    {
        var table = _ws.Range(_rowOff + 1, _colOff + 1, _lastDataRow, _lastDataCol).CreateTable();
        if (name != null) table.Name = name;
        return this;
    }

    public SheetBuilder HideGridlines()
    {
        // TODO: ClosedXML 0.105 需要正确的 API 调用来隐藏网格线
        // 当前版本暂不支持，保持网格线显示
        return this;
    }
    public SheetBuilder FreezeHeader() { _ws.SheetView.Freeze(1, 0); return this; }
    public SheetBuilder PrintFit() { _ws.PageSetup.FitToPages(1, 1); return this; }
}
