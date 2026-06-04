using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Word 文档写入器。原子操作级别的链式 API，不绑定任何特定文体。
/// 论文写作请使用 Nong.Genre.PaperWriter。
/// 用法：new DocumentWriter(body, doc).Table(...).Footnote("脚注").Hyperlink(url, text)
///
/// 如需脚注/尾注支持，传入 WordprocessingDocument：
/// new DocumentWriter(body, doc).Footnote("脚注内容")
/// </summary>
public class DocumentWriter
{
    readonly Body _body;
    readonly WordprocessingDocument? _doc;
    FootnotesPart? _fnPart;
    EndnotesPart? _enPart;
    int _fnId, _enId;

    /// <summary>基础构造（无脚注/尾注支持）。</summary>
    public DocumentWriter(Body body)
    {
        _body = body;
        _doc = null;
    }

    /// <summary>完整构造，支持脚注、尾注、图片等高级功能。</summary>
    public DocumentWriter(Body body, WordprocessingDocument doc)
    {
        _body = body;
        _doc = doc;
    }

    // ===== 标题 / 段落 =====

    /// <summary>文档主标题。居中、加粗、大号。</summary>
    public DocumentWriter Title(string text)
    {
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Title" }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" },
            new FontSize { Val = "32" },
            new Bold()),
            new Text(text)));
        _body.Append(p);
        return this;
    }

    /// <summary>标题段落。level 1-3 对应 Heading1-Heading3。</summary>
    public DocumentWriter Heading(string text, int level = 1)
    {
        var styleId = level switch
        {
            1 => "Heading1",
            2 => "Heading2",
            3 => "Heading3",
            _ => "Heading1"
        };
        var fontSize = level switch
        {
            1 => "28",
            2 => "24",
            3 => "22",
            _ => "28"
        };
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = styleId }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" },
            new FontSize { Val = fontSize },
            new Bold()),
            new Text(text)));
        _body.Append(p);
        return this;
    }

    /// <summary>正文段落。</summary>
    public DocumentWriter Body(string text)
    {
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
            new FontSize { Val = "21" }),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        _body.Append(p);
        return this;
    }

    // ===== 表格 =====

    /// <summary>三线表。</summary>
    public DocumentWriter Table(string caption, int num, string[] headers, string[][] rows)
    {
        var cp = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        cp.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "16" }), new Text($"表{num}  {caption}")));
        _body.Append(cp);

        var t = new Table();
        t.Append(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                Top(BorderValues.Single, 6), Bottom(BorderValues.Single, 6),
                Left(BorderValues.None), Right(BorderValues.None),
                InsideH(BorderValues.None), InsideV(BorderValues.None)),
            new TableLayout { Type = TableLayoutValues.Fixed }));
        var g = new TableGrid();
        foreach (var _ in headers) g.Append(new GridColumn());
        t.Append(g);

        var hr = new TableRow();
        foreach (var h in headers) hr.Append(MakeCell(h, true, true));
        t.Append(hr);

        foreach (var r in rows)
        {
            var tr = new TableRow();
            foreach (var c in r) tr.Append(MakeCell(c, false, false));
            t.Append(tr);
        }
        _body.Append(t);
        _body.Append(new Paragraph());
        return this;
    }

    // ===== 脚注 =====

    /// <summary>添加脚注。需要传入 WordprocessingDocument 构造。脚注编号自动递增。</summary>
    public DocumentWriter Footnote(string text)
    {
        EnsureDoc();
        _fnId++;
        var fnPart = GetFootnotesPart();
        var fn = new Footnote { Id = _fnId };
        fn.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "FootnoteText" }),
            new Run(new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new FontSize { Val = "18" }),
                new Text($"{_fnId}. {text}"))));
        fnPart.Footnotes!.Append(fn);

        // 在正文中插入脚注引用
        var refRun = new Run(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
            new FontSize { Val = "18" }),
            new FootnoteReference { Id = _fnId });
        var refPara = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "FootnoteReference" }), refRun);
        _body.Append(refPara);

        return this;
    }

    // ===== 尾注 =====

    /// <summary>添加尾注。需要传入 WordprocessingDocument 构造。尾注编号自动递增。</summary>
    public DocumentWriter Endnote(string text)
    {
        EnsureDoc();
        _enId++;
        var enPart = GetEndnotesPart();
        var en = new Endnote { Id = _enId };
        en.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "EndnoteText" }),
            new Run(new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new FontSize { Val = "18" }),
                new Text($"{_enId}. {text}"))));
        enPart.Endnotes!.Append(en);

        var refRun = new Run(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
            new FontSize { Val = "18" }),
            new EndnoteReference { Id = _enId });
        var refPara = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "FootnoteReference" }), refRun);
        _body.Append(refPara);

        return this;
    }

    // ===== 交叉引用 =====

    /// <summary>内部交叉引用（超链接到书签）。如 CrossReference("_Toc001", "见表 1")。</summary>
    public DocumentWriter CrossReference(string bookmarkName, string displayText)
    {
        var hyperlink = new Hyperlink { Anchor = bookmarkName, History = true };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Underline { Val = UnderlineValues.Single }),
            new Text(displayText)));
        _body.Append(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" }), hyperlink));
        return this;
    }

    /// <summary>外部超链接。</summary>
    public DocumentWriter Hyperlink(string url, string displayText)
    {
        // External hyperlinks need a relationship
        if (_doc == null)
        {
            // Fallback: just output the URL as text
            var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }));
            p.Append(new Run(new Text($"{displayText} ({url})")));
            _body.Append(p);
            return this;
        }

        var mainPart = _doc.MainDocumentPart!;
        var extRel = mainPart.AddHyperlinkRelationship(new Uri(url), true);
        var hyperlink = new Hyperlink { Id = extRel.Id };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Underline { Val = UnderlineValues.Single },
            new Color { Val = "0563C1" }),
            new Text(displayText)));
        _body.Append(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" }), hyperlink));
        return this;
    }

    // ===== 书签 =====

    /// <summary>在当前位置插入书签，供交叉引用使用。</summary>
    public DocumentWriter Bookmark(string name)
    {
        var id = Math.Abs(name.GetHashCode() % 0x7FFFFFFF).ToString();
        var p = new Paragraph(
            new BookmarkStart { Name = name, Id = id },
            new BookmarkEnd { Id = id });
        _body.Append(p);
        return this;
    }

    // ===== 目录 =====

    /// <summary>插入目录页（含"目录"标题 + TOC 域 + 分页符）。</summary>
    public DocumentWriter TableOfContents(string title = "目录")
    {
        TocAndChartBuilder.AppendTableOfContents(_body, title);
        return this;
    }

    // ===== 图表 =====

    /// <summary>插入柱状图。</summary>
    public DocumentWriter BarChart(string title, string[] categories, double[] values, string seriesName = "系列 1")
    {
        if (_doc == null) throw new InvalidOperationException("Chart requires WordprocessingDocument constructor.");
        TocAndChartBuilder.AppendBarChart(_body, _doc.MainDocumentPart!, title, categories, values, seriesName);
        return this;
    }

    // ===== 表格样式 =====

    /// <summary>为最后一个表格设置 Word 内置样式。</summary>
    public DocumentWriter TableStyle(string styleId)
    {
        var lastTable = _body.Elements<Table>().LastOrDefault();
        if (lastTable != null)
        {
            var tpr = lastTable.Elements<TableProperties>().FirstOrDefault();
            if (tpr == null) { tpr = new TableProperties(); lastTable.InsertAt(tpr, 0); }
            tpr.Append(new TableStyle { Val = styleId });
        }
        return this;
    }

    // === helpers ===

    void EnsureDoc()
    {
        if (_doc == null)
            throw new InvalidOperationException(
                "Footnote/Endnote support requires WordprocessingDocument. Use 'new DocumentWriter(body, doc)' constructor.");
    }

    FootnotesPart GetFootnotesPart()
    {
        if (_fnPart != null) return _fnPart;
        var mainPart = _doc!.MainDocumentPart!;
        _fnPart = mainPart.FootnotesPart ?? mainPart.AddNewPart<FootnotesPart>();
        if (_fnPart.Footnotes == null)
        {
            _fnPart.Footnotes = new Footnotes(
                new Footnote(new Paragraph()) { Id = 0 },
                new Footnote(new Paragraph()) { Id = -1 });
        }
        return _fnPart;
    }

    EndnotesPart GetEndnotesPart()
    {
        if (_enPart != null) return _enPart;
        var mainPart = _doc!.MainDocumentPart!;
        _enPart = mainPart.EndnotesPart ?? mainPart.AddNewPart<EndnotesPart>();
        if (_enPart.Endnotes == null)
        {
            _enPart.Endnotes = new Endnotes(
                new Endnote(new Paragraph()) { Id = 0 },
                new Endnote(new Paragraph()) { Id = -1 });
        }
        return _enPart;
    }

    static TopBorder Top(BorderValues v, uint sz = 6) => new() { Val = v, Size = sz, Color = "000000" };
    static BottomBorder Bottom(BorderValues v, uint sz = 6) => new() { Val = v, Size = sz, Color = "000000" };
    static LeftBorder Left(BorderValues v) => new() { Val = v };
    static RightBorder Right(BorderValues v) => new() { Val = v };
    static InsideHorizontalBorder InsideH(BorderValues v) => new() { Val = v };
    static InsideVerticalBorder InsideV(BorderValues v) => new() { Val = v };

    static TableCell MakeCell(string tx, bool header, bool bottomBorder)
    {
        var tc = new TableCell();
        var tcProps = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bottomBorder) tcProps.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tc.Append(tcProps);
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" },
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "40", After = "40" }));
        if (header)
            p.Append(new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new FontSize { Val = "21" }), new Text(tx)));
        else
            p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "21" }), new Text(tx)));
        tc.Append(p);
        return tc;
    }
}
