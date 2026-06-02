using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace ExcelCore;

/// <summary>
/// 将 ClosedXML 中未被 ExcelBuilder 覆盖的高级功能封装为流畅的 Builder API。
/// 提供数据透视表、图片、迷你图、自动筛选、批注、超链接、富文本、
/// 工作表保护、定义名称、条件格式、排序、工作表管理和打印设置等扩展方法。
/// </summary>
public static class AdvancedBuilder
{
    // ========================================================================
    // 1. PivotTableBuilder
    // ========================================================================

    /// <summary>
    /// 在工作表上创建数据透视表构建器。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="name">数据透视表名称。</param>
    /// <param name="targetCell">数据透视表左上角单元格。</param>
    /// <param name="dataRange">数据源范围。</param>
    /// <returns>用于链式配置数据透视表的构建器。</returns>
    public static PivotTableBuilder CreatePivotTable(
        this IXLWorksheet ws, string name, IXLCell targetCell, IXLRange dataRange)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentNullException.ThrowIfNull(targetCell);
        ArgumentNullException.ThrowIfNull(dataRange);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var pt = ws.PivotTables.Add(name, targetCell, dataRange);
        return new PivotTableBuilder(pt);
    }

    // ========================================================================
    // 2. PictureBuilder
    // ========================================================================

    /// <summary>
    /// 向工作表添加图片并返回构建器，以便链式设置位置、大小和放置方式。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="imagePath">图片文件路径。</param>
    /// <returns>用于链式配置图片的构建器。</returns>
    public static PictureBuilder AddPicture(this IXLWorksheet ws, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var pic = ws.AddPicture(imagePath);
        return new PictureBuilder(pic);
    }

    // ========================================================================
    // 3. SparklineBuilder
    // ========================================================================

    /// <summary>
    /// 向工作表添加迷你图并返回构建器，以便链式设置类型、样式和标记。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="locationCell">迷你图所在单元格地址。</param>
    /// <param name="sourceRange">迷你图数据源范围地址。</param>
    /// <returns>用于链式配置迷你图的构建器。</returns>
    public static SparklineBuilder AddSparkline(
        this IXLWorksheet ws, string locationCell, string sourceRange)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationCell);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRange);

        var group = ws.SparklineGroups.Add(locationCell, sourceRange);
        return new SparklineBuilder(group);
    }

    // ========================================================================
    // 4. AutoFilterBuilder
    // ========================================================================

    /// <summary>
    /// 为工作表上的指定范围设置自动筛选并返回构建器。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="range">自动筛选范围地址。</param>
    /// <returns>用于链式配置自动筛选的构建器。</returns>
    public static AutoFilterBuilder SetAutoFilter(this IXLWorksheet ws, string range)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(range);

        var filter = ws.Range(range).SetAutoFilter();
        return new AutoFilterBuilder(filter);
    }

    // ========================================================================
    // 5. CommentBuilder
    // ========================================================================

    /// <summary>
    /// 为单元格添加批注。
    /// </summary>
    /// <param name="cell">目标单元格。</param>
    /// <param name="text">批注文本。</param>
    /// <param name="author">批注作者（可选）。</param>
    /// <returns>创建的批注对象。</returns>
    public static IXLComment AddComment(this IXLCell cell, string text, string? author = null)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(text);

        var comment = cell.CreateComment();
        comment.AddText(text);
        if (author != null)
            comment.Author = author;
        return comment;
    }

    // ========================================================================
    // 6. HyperlinkBuilder
    // ========================================================================

    /// <summary>
    /// 为单元格添加外部超链接。
    /// </summary>
    /// <param name="cell">目标单元格。</param>
    /// <param name="address">超链接地址（URL 或文件路径）。</param>
    /// <param name="tooltip">鼠标悬停提示文本（可选）。</param>
    /// <returns>当前单元格，以便链式调用。</returns>
    public static IXLCell AddHyperlink(this IXLCell cell, string address, string? tooltip = null)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var hl = new XLHyperlink(address, tooltip ?? string.Empty);
        cell.SetHyperlink(hl);
        return cell;
    }

    /// <summary>
    /// 为单元格添加内部跳转链接（指向工作簿内的单元格）。
    /// </summary>
    /// <param name="cell">目标单元格。</param>
    /// <param name="cellAddress">内部单元格地址（如 "Sheet1!A1"）。</param>
    /// <param name="tooltip">鼠标悬停提示文本（可选）。</param>
    /// <returns>当前单元格，以便链式调用。</returns>
    public static IXLCell AddInternalLink(this IXLCell cell, string cellAddress, string? tooltip = null)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrWhiteSpace(cellAddress);

        var hl = new XLHyperlink(cellAddress, tooltip ?? string.Empty);
        cell.SetHyperlink(hl);
        return cell;
    }

    // ========================================================================
    // 7. RichTextBuilder
    // ========================================================================

    /// <summary>
    /// 为单元格创建富文本构建器，支持多段文本与不同格式。
    /// </summary>
    /// <param name="cell">目标单元格。</param>
    /// <returns>用于链式配置富文本的构建器。</returns>
    public static RichTextBuilder CreateRichText(this IXLCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        var rt = cell.CreateRichText();
        return new RichTextBuilder(rt);
    }

    // ========================================================================
    // 8. ProtectionBuilder
    // ========================================================================

    /// <summary>
    /// 保护工作表。可选择设置密码和允许用户操作的元素。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="password">保护密码（可选）。</param>
    /// <param name="allowedElements">允许用户操作的元素集合。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet ProtectSheet(
        this IXLWorksheet ws,
        string? password = null,
        XLSheetProtectionElements allowedElements = XLSheetProtectionElements.SelectEverything)
    {
        ArgumentNullException.ThrowIfNull(ws);

        if (password != null)
            ws.Protect(password, XLProtectionAlgorithm.Algorithm.SimpleHash, allowedElements);
        else
            ws.Protect(allowedElements);

        return ws;
    }

    /// <summary>
    /// 取消工作表保护。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="password">保护密码（若设置了密码保护）。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet UnprotectSheet(this IXLWorksheet ws, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(ws);

        if (password != null)
            ws.Unprotect(password);
        else
            ws.Unprotect();

        return ws;
    }

    // ========================================================================
    // 9. DefinedNameBuilder
    // ========================================================================

    /// <summary>
    /// 在工作表级别添加定义名称。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="name">定义名称。</param>
    /// <param name="rangeAddress">引用的范围地址。</param>
    /// <param name="comment">备注（可选）。</param>
    /// <returns>创建的定义名称对象。</returns>
    public static IXLDefinedName AddNamedRange(
        this IXLWorksheet ws, string name, string rangeAddress, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeAddress);

        if (comment != null)
            return ws.DefinedNames.Add(name, rangeAddress, comment);
        return ws.DefinedNames.Add(name, rangeAddress);
    }

    /// <summary>
    /// 在工作簿级别添加定义名称。
    /// </summary>
    /// <param name="wb">目标工作簿。</param>
    /// <param name="name">定义名称。</param>
    /// <param name="rangeAddress">引用的范围地址。</param>
    /// <param name="comment">备注（可选）。</param>
    /// <returns>创建的定义名称对象。</returns>
    public static IXLDefinedName AddNamedRange(
        this IXLWorkbook wb, string name, string rangeAddress, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(wb);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeAddress);

        if (comment != null)
            return wb.DefinedNames.Add(name, rangeAddress, comment);
        return wb.DefinedNames.Add(name, rangeAddress);
    }

    // ========================================================================
    // 10. ConditionalFormatBuilder
    // ========================================================================

    /// <summary>
    /// 为指定范围创建条件格式构建器。
    /// </summary>
    /// <param name="range">目标范围。</param>
    /// <returns>用于链式配置条件格式的构建器。</returns>
    public static ConditionalFormatBuilder ConditionalFormat(this IXLRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        var cf = range.AddConditionalFormat();
        return new ConditionalFormatBuilder(cf);
    }

    // ========================================================================
    // 11. SortBuilder
    // ========================================================================

    /// <summary>
    /// 对指定范围按某一列排序。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="range">排序范围地址。</param>
    /// <param name="column">排序依据的列号（从 1 开始）。</param>
    /// <param name="order">排序顺序。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet SortBy(
        this IXLWorksheet ws, string range, int column,
        XLSortOrder order = XLSortOrder.Ascending)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(range);

        ws.Range(range).Sort(column, order);
        return ws;
    }

    // ========================================================================
    // 12. Sheet organization
    // ========================================================================

    /// <summary>
    /// 重命名工作表。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="newName">新名称。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet Rename(this IXLWorksheet ws, string newName)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        ws.Name = newName;
        return ws;
    }

    /// <summary>
    /// 将工作表移动到指定位置。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="position">新位置（从 1 开始）。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet MoveTo(this IXLWorksheet ws, int position)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.Position = position;
        return ws;
    }

    /// <summary>
    /// 隐藏工作表。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet Hide(this IXLWorksheet ws)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.Visibility = XLWorksheetVisibility.Hidden;
        return ws;
    }

    /// <summary>
    /// 取消隐藏工作表。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet Unhide(this IXLWorksheet ws)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.Visibility = XLWorksheetVisibility.Visible;
        return ws;
    }

    // ========================================================================
    // 13. Print setup
    // ========================================================================

    /// <summary>
    /// 设置工作表的打印区域。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="range">打印区域地址。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet SetPrintArea(this IXLWorksheet ws, string range)
    {
        ArgumentNullException.ThrowIfNull(ws);
        ArgumentException.ThrowIfNullOrWhiteSpace(range);

        ws.PageSetup.PrintAreas.Add(range);
        return ws;
    }

    /// <summary>
    /// 设置页面打印方向。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="orientation">页面方向。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet SetPageOrientation(this IXLWorksheet ws, XLPageOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.PageSetup.PageOrientation = orientation;
        return ws;
    }

    /// <summary>
    /// 设置打印纸张大小。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="pageSize">纸张大小。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet SetPageSize(this IXLWorksheet ws, XLPaperSize pageSize)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.PageSetup.PaperSize = pageSize;
        return ws;
    }

    /// <summary>
    /// 设置页面边距（单位由 ClosedXML 默认决定，通常为英寸）。
    /// </summary>
    /// <param name="ws">目标工作表。</param>
    /// <param name="top">上边距。</param>
    /// <param name="bottom">下边距。</param>
    /// <param name="left">左边距。</param>
    /// <param name="right">右边距。</param>
    /// <returns>当前工作表，以便链式调用。</returns>
    public static IXLWorksheet SetMargins(
        this IXLWorksheet ws, double top, double bottom, double left, double right)
    {
        ArgumentNullException.ThrowIfNull(ws);

        ws.PageSetup.Margins.Top = top;
        ws.PageSetup.Margins.Bottom = bottom;
        ws.PageSetup.Margins.Left = left;
        ws.PageSetup.Margins.Right = right;
        return ws;
    }
}

// ============================================================================
// PivotTableBuilder
// ============================================================================

/// <summary>
/// 数据透视表的流畅构建器，支持行标签、列标签、值字段、筛选器、布局和主题设置。
/// </summary>
public class PivotTableBuilder
{
    readonly IXLPivotTable _pt;

    internal PivotTableBuilder(IXLPivotTable pt) { _pt = pt; }

    /// <summary>获取底层数据透视表对象。</summary>
    public IXLPivotTable PivotTable => _pt;

    /// <summary>
    /// 添加行标签字段。
    /// </summary>
    /// <param name="field">字段名（数据源列名）。</param>
    /// <param name="customName">自定义显示名称（可选）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder RowLabel(string field, string? customName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (customName != null)
            _pt.RowLabels.Add(field, customName);
        else
            _pt.RowLabels.Add(field);
        return this;
    }

    /// <summary>
    /// 添加列标签字段。
    /// </summary>
    /// <param name="field">字段名（数据源列名）。</param>
    /// <param name="customName">自定义显示名称（可选）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder ColumnLabel(string field, string? customName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (customName != null)
            _pt.ColumnLabels.Add(field, customName);
        else
            _pt.ColumnLabels.Add(field);
        return this;
    }

    /// <summary>
    /// 添加值字段并指定汇总方式。
    /// </summary>
    /// <param name="field">字段名（数据源列名）。</param>
    /// <param name="summary">汇总函数（默认为 Sum）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder Value(string field, XLPivotSummary summary = XLPivotSummary.Sum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        var v = _pt.Values.Add(field);
        v.SummaryFormula = summary;
        return this;
    }

    /// <summary>
    /// 添加筛选器字段。
    /// </summary>
    /// <param name="field">字段名（数据源列名）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder Filter(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        _pt.ReportFilters.Add(field);
        return this;
    }

    /// <summary>
    /// 设置数据透视表布局。
    /// </summary>
    /// <param name="layout">布局类型。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder Layout(XLPivotLayout layout)
    {
        _pt.Layout = layout;
        return this;
    }

    /// <summary>
    /// 设置是否显示行列总计。
    /// </summary>
    /// <param name="show">是否显示。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder ShowGrandTotals(bool show)
    {
        _pt.ShowGrandTotalsRows = show;
        _pt.ShowGrandTotalsColumns = show;
        return this;
    }

    /// <summary>
    /// 设置数据透视表主题样式。
    /// </summary>
    /// <param name="theme">主题样式。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PivotTableBuilder Theme(XLPivotTableTheme theme)
    {
        _pt.Theme = theme;
        return this;
    }
}

// ============================================================================
// PictureBuilder
// ============================================================================

/// <summary>
/// 图片的流畅构建器，支持位置、缩放和大小设置。
/// </summary>
public class PictureBuilder
{
    readonly IXLPicture _pic;

    internal PictureBuilder(IXLPicture pic) { _pic = pic; }

    /// <summary>获取底层图片对象。</summary>
    public IXLPicture Picture => _pic;

    /// <summary>
    /// 将图片移动到指定单元格位置。
    /// </summary>
    /// <param name="cell">目标单元格。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PictureBuilder MoveTo(IXLCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        _pic.MoveTo(cell);
        return this;
    }

    /// <summary>
    /// 将图片移动到指定像素坐标。
    /// </summary>
    /// <param name="left">左边距（像素）。</param>
    /// <param name="top">上边距（像素）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PictureBuilder MoveTo(int left, int top)
    {
        _pic.MoveTo(left, top);
        return this;
    }

    /// <summary>
    /// 按比例缩放图片。
    /// </summary>
    /// <param name="factor">缩放因子（1.0 为原始大小）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PictureBuilder Scale(double factor)
    {
        _pic.Scale(factor);
        return this;
    }

    /// <summary>
    /// 设置图片的精确宽度和高度（像素）。
    /// </summary>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PictureBuilder WithSize(int width, int height)
    {
        _pic.WithSize(width, height);
        return this;
    }

    /// <summary>
    /// 设置图片的放置方式。
    /// </summary>
    /// <param name="placement">放置方式。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public PictureBuilder WithPlacement(XLPicturePlacement placement)
    {
        _pic.WithPlacement(placement);
        return this;
    }
}

// ============================================================================
// SparklineBuilder
// ============================================================================

/// <summary>
/// 迷你图的流畅构建器，支持类型、样式、标记和线宽设置。
/// </summary>
public class SparklineBuilder
{
    readonly IXLSparklineGroup _group;

    internal SparklineBuilder(IXLSparklineGroup group) { _group = group; }

    /// <summary>获取底层迷你图组对象。</summary>
    public IXLSparklineGroup SparklineGroup => _group;

    /// <summary>
    /// 设置迷你图类型（折线图、柱形图或堆叠图）。
    /// </summary>
    /// <param name="type">迷你图类型。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public SparklineBuilder Type(XLSparklineType type)
    {
        _group.Type = type;
        return this;
    }

    /// <summary>
    /// 设置迷你图样式。
    /// </summary>
    /// <param name="style">迷你图样式。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public SparklineBuilder Style(IXLSparklineStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _group.Style = style;
        return this;
    }

    /// <summary>
    /// 设置迷你图上显示的标记类型。
    /// </summary>
    /// <param name="markers">标记类型（可按位组合）。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public SparklineBuilder ShowMarkers(XLSparklineMarkers markers)
    {
        _group.ShowMarkers = markers;
        return this;
    }

    /// <summary>
    /// 设置迷你图的线条粗细。
    /// </summary>
    /// <param name="weight">线条粗细值。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public SparklineBuilder LineWeight(double weight)
    {
        _group.LineWeight = weight;
        return this;
    }
}

// ============================================================================
// AutoFilterBuilder
// ============================================================================

/// <summary>
/// 自动筛选的流畅构建器，支持排序和列级筛选条件。
/// </summary>
public class AutoFilterBuilder
{
    readonly IXLAutoFilter _filter;

    internal AutoFilterBuilder(IXLAutoFilter filter) { _filter = filter; }

    /// <summary>获取底层自动筛选对象。</summary>
    public IXLAutoFilter AutoFilter => _filter;

    /// <summary>
    /// 按指定列排序。
    /// </summary>
    /// <param name="column">列号（从 1 开始）。</param>
    /// <param name="order">排序顺序。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public AutoFilterBuilder Sort(int column, XLSortOrder order = XLSortOrder.Ascending)
    {
        _filter.Sort(column, order);
        return this;
    }

    /// <summary>
    /// 获取指定列的筛选配置构建器。
    /// </summary>
    /// <param name="columnNumber">列号（从 1 开始）。</param>
    /// <returns>列筛选构建器。</returns>
    public AutoFilterColumnBuilder Column(int columnNumber)
    {
        return new AutoFilterColumnBuilder(_filter.Column(columnNumber), this);
    }
}

/// <summary>
/// 自动筛选的列级筛选条件构建器。
/// </summary>
public class AutoFilterColumnBuilder
{
    readonly IXLFilterColumn _col;
    readonly AutoFilterBuilder _parent;

    internal AutoFilterColumnBuilder(IXLFilterColumn col, AutoFilterBuilder parent)
    {
        _col = col;
        _parent = parent;
    }

    /// <summary>
    /// 筛选显示前 N 项。
    /// </summary>
    /// <param name="n">项数。</param>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder Top(int n)
    {
        _col.Top(n);
        return _parent;
    }

    /// <summary>
    /// 筛选显示后 N 项。
    /// </summary>
    /// <param name="n">项数。</param>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder Bottom(int n)
    {
        _col.Bottom(n);
        return _parent;
    }

    /// <summary>
    /// 筛选显示高于平均值的行。
    /// </summary>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder AboveAverage()
    {
        _col.AboveAverage();
        return _parent;
    }

    /// <summary>
    /// 筛选显示低于平均值的行。
    /// </summary>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder BelowAverage()
    {
        _col.BelowAverage();
        return _parent;
    }

    /// <summary>
    /// 筛选包含指定文本的行。
    /// </summary>
    /// <param name="value">要包含的文本。</param>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder Contains(string value)
    {
        _col.Contains(value);
        return _parent;
    }

    /// <summary>
    /// 筛选大于指定值的行。
    /// </summary>
    /// <param name="value">比较值。</param>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder GreaterThan(XLCellValue value)
    {
        _col.GreaterThan(value);
        return _parent;
    }

    /// <summary>
    /// 筛选小于指定值的行。
    /// </summary>
    /// <param name="value">比较值。</param>
    /// <returns>父级构建器，以便链式调用。</returns>
    public AutoFilterBuilder LessThan(XLCellValue value)
    {
        _col.LessThan(value);
        return _parent;
    }
}

// ============================================================================
// RichTextBuilder
// ============================================================================

/// <summary>
/// 富文本的流畅构建器，支持多段文本和不同格式设置。
/// </summary>
public class RichTextBuilder
{
    readonly IXLRichText _rt;
    IXLRichString? _last;

    internal RichTextBuilder(IXLRichText rt) { _rt = rt; }

    /// <summary>获取底层富文本对象。</summary>
    public IXLRichText RichText => _rt;

    /// <summary>
    /// 添加一段文本。
    /// </summary>
    /// <param name="text">要添加的文本。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder AddText(string text)
    {
        _last = _rt.AddText(text);
        return this;
    }

    /// <summary>
    /// 将最近添加的文本设为粗体。
    /// </summary>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder Bold()
    {
        _last?.SetBold();
        return this;
    }

    /// <summary>
    /// 将最近添加的文本设为斜体。
    /// </summary>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder Italic()
    {
        _last?.SetItalic();
        return this;
    }

    /// <summary>
    /// 设置最近添加的文本的字号。
    /// </summary>
    /// <param name="size">字号大小。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder FontSize(double size)
    {
        _last?.SetFontSize(size);
        return this;
    }

    /// <summary>
    /// 设置最近添加的文本的字体颜色。
    /// </summary>
    /// <param name="color">字体颜色。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder FontColor(XLColor color)
    {
        if (_last != null)
            _last.FontColor = color;
        return this;
    }

    /// <summary>
    /// 设置最近添加的文本的字体名称。
    /// </summary>
    /// <param name="name">字体名称。</param>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder FontName(string name)
    {
        _last?.SetFontName(name);
        return this;
    }

    /// <summary>
    /// 将最近添加的文本设为下划线。
    /// </summary>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder Underline()
    {
        _last?.SetUnderline();
        return this;
    }

    /// <summary>
    /// 将最近添加的文本设为删除线。
    /// </summary>
    /// <returns>当前构建器，以便链式调用。</returns>
    public RichTextBuilder Strikethrough()
    {
        _last?.SetStrikethrough();
        return this;
    }
}

// ============================================================================
// ConditionalFormatBuilder
// ============================================================================

/// <summary>
/// 条件格式的流畅构建器，支持常见条件规则、图标集、数据条和色阶。
/// </summary>
public class ConditionalFormatBuilder
{
    readonly IXLConditionalFormat _cf;

    internal ConditionalFormatBuilder(IXLConditionalFormat cf) { _cf = cf; }

    /// <summary>获取底层条件格式对象。</summary>
    public IXLConditionalFormat ConditionalFormat => _cf;

    /// <summary>
    /// 当单元格包含指定文本时触发格式。返回 IXLStyle 以配置样式。
    /// </summary>
    /// <param name="value">要匹配的文本。</param>
    /// <returns>样式对象，可链式设置字体、填充等属性。</returns>
    public IXLStyle WhenContains(string value) => _cf.WhenContains(value);

    /// <summary>
    /// 当单元格等于指定文本时触发格式。
    /// </summary>
    /// <param name="value">要匹配的值。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenEquals(string value) => _cf.WhenEquals(value);

    /// <summary>
    /// 当单元格数值大于指定值时触发格式。
    /// </summary>
    /// <param name="value">比较值。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenGreaterThan(double value) => _cf.WhenGreaterThan(value);

    /// <summary>
    /// 当单元格数值小于指定值时触发格式。
    /// </summary>
    /// <param name="value">比较值。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenLessThan(double value) => _cf.WhenLessThan(value);

    /// <summary>
    /// 当单元格数值在指定范围内时触发格式。
    /// </summary>
    /// <param name="min">最小值。</param>
    /// <param name="max">最大值。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenBetween(double min, double max) => _cf.WhenBetween(min, max);

    /// <summary>
    /// 当单元格为空白时触发格式。
    /// </summary>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsBlank() => _cf.WhenIsBlank();

    /// <summary>
    /// 当单元格包含错误值时触发格式。
    /// </summary>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsError() => _cf.WhenIsError();

    /// <summary>
    /// 当单元格值重复时触发格式。
    /// </summary>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsDuplicate() => _cf.WhenIsDuplicate();

    /// <summary>
    /// 当单元格值唯一时触发格式。
    /// </summary>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsUnique() => _cf.WhenIsUnique();

    /// <summary>
    /// 当单元格文本以指定字符串开头时触发格式。
    /// </summary>
    /// <param name="value">前缀字符串。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenStartsWith(string value) => _cf.WhenStartsWith(value);

    /// <summary>
    /// 当单元格文本以指定字符串结尾时触发格式。
    /// </summary>
    /// <param name="value">后缀字符串。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenEndsWith(string value) => _cf.WhenEndsWith(value);

    /// <summary>
    /// 当单元格值在前 N 名时触发格式。
    /// </summary>
    /// <param name="n">排名数量。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsTop(int n) => _cf.WhenIsTop(n);

    /// <summary>
    /// 当单元格值在后 N 名时触发格式。
    /// </summary>
    /// <param name="n">排名数量。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsBottom(int n) => _cf.WhenIsBottom(n, XLTopBottomType.Items);

    /// <summary>
    /// 当自定义公式为真时触发格式。
    /// </summary>
    /// <param name="formula">公式表达式。</param>
    /// <returns>样式对象。</returns>
    public IXLStyle WhenIsTrue(string formula) => _cf.WhenIsTrue(formula);

    /// <summary>
    /// 添加图标集条件格式。
    /// </summary>
    /// <param name="style">图标集样式。</param>
    /// <returns>图标集配置对象，可继续添加阈值。</returns>
    public IXLCFIconSet IconSet(XLIconSetStyle style)
    {
        return _cf.IconSet(style);
    }

    /// <summary>
    /// 添加数据条条件格式。
    /// </summary>
    /// <param name="color">数据条颜色。</param>
    /// <returns>数据条最小值配置对象。</returns>
    public IXLCFDataBarMin DataBar(XLColor color)
    {
        return _cf.DataBar(color);
    }

    /// <summary>
    /// 添加双色色阶条件格式。
    /// </summary>
    /// <param name="low">低值颜色。</param>
    /// <param name="high">高值颜色。</param>
    /// <returns>当前构建器。</returns>
    public ConditionalFormatBuilder ColorScale(XLColor low, XLColor high)
    {
        _cf.ColorScale().LowestValue(low).HighestValue(high);
        return this;
    }

    /// <summary>
    /// 添加三色色阶条件格式。
    /// </summary>
    /// <param name="low">低值颜色。</param>
    /// <param name="mid">中间值颜色。</param>
    /// <param name="high">高值颜色。</param>
    /// <returns>当前构建器。</returns>
    public ConditionalFormatBuilder ColorScale(XLColor low, XLColor mid, XLColor high)
    {
        var cs = _cf.ColorScale();
        cs.LowestValue(low)
          .Midpoint(XLCFContentType.Percent, 50.0, mid)
          .HighestValue(high);
        return this;
    }
}
