using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Nong.Inspect;

/// <summary>
/// 信函写作器。支持商务信函、行政信函、邀请函等。
/// 链式 API，每个方法对应信函的一个组成部分。
/// </summary>
public class LetterWriter
{
    readonly Body _body;
    readonly DocumentWriter _w;

    public LetterWriter(Body body) { _body = body; _w = new DocumentWriter(body); }
    public LetterWriter(Body body, WordprocessingDocument doc) { _body = body; _w = new DocumentWriter(body, doc); }

    /// <summary>发信日期（右对齐）。</summary>
    public LetterWriter Date(string text)
    {
        var p = NewP(JustificationValues.Right);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>收信人/收信单位（左对齐，加粗）。</summary>
    public LetterWriter Recipient(string text)
    {
        var p = NewP(JustificationValues.Left);
        p.Append(Run(text, "21", bold: true));
        _body.Append(p);
        return this;
    }

    /// <summary>事由/主题（居中，加粗大字）。</summary>
    public LetterWriter Subject(string text)
    {
        var p = NewP(JustificationValues.Center);
        p.Append(Run(text, "24", bold: true));
        _body.Append(p);
        return this;
    }

    /// <summary>正文段落（两端对齐，首行缩进）。</summary>
    public LetterWriter Body(string text)
    {
        var p = NewP(JustificationValues.Both, indent: "420");
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>敬语/结束语（如"此致 敬礼""顺颂 商祺"）。</summary>
    public LetterWriter Closing(string text)
    {
        var p = NewP(JustificationValues.Left);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>署名（右对齐，加粗）。</summary>
    public LetterWriter Signature(string text)
    {
        var p = NewP(JustificationValues.Right);
        p.Append(Run(text, "21", bold: true));
        _body.Append(p);
        return this;
    }

    /// <summary>空白分隔行。</summary>
    public LetterWriter Blank()
    {
        _body.Append(new Paragraph());
        return this;
    }

    // === helpers ===

    static Paragraph NewP(JustificationValues align, string? indent = null)
    {
        var ppr = new ParagraphProperties(new Justification { Val = align });
        if (indent != null) ppr.Indentation = new Indentation { FirstLine = indent };
        return new Paragraph(ppr);
    }

    static Run Run(string t, string fs, bool bold = false)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "宋体", HighAnsi = "宋体", EastAsia = "宋体" },
            new FontSize { Val = fs });
        if (bold) rpr.Append(new Bold());
        return new Run(rpr, new Text(t));
    }
}
