using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Nong.Inspect;

/// <summary>
/// 公文写作器。支持通知、报告、请示、函等公文类型。
/// 链式 API，每个方法对应公文的一个组成部分。
/// </summary>
public class OfficialDocWriter
{
    readonly Body _body;
    readonly DocumentWriter _w;

    public OfficialDocWriter(Body body) { _body = body; _w = new DocumentWriter(body); }
    public OfficialDocWriter(Body body, WordprocessingDocument doc) { _body = body; _w = new DocumentWriter(body, doc); }

    /// <summary>红头（发文机关标识，红色大字居中）。</summary>
    public OfficialDocWriter RedHeader(string text)
    {
        var p = NewP(JustificationValues.Center);
        p.Append(Run(text, "36", bold: true, color: "FF0000"));
        _body.Append(p);
        return this;
    }

    /// <summary>发文字号（如 "国发〔2026〕1号"）。</summary>
    public OfficialDocWriter DocNumber(string text)
    {
        var p = NewP(JustificationValues.Center);
        p.Append(Run(text, "18"));
        _body.Append(p);
        return this;
    }

    /// <summary>公文标题（居中，二号黑体）。</summary>
    public OfficialDocWriter Title(string text)
    {
        var p = NewP(JustificationValues.Center);
        p.Append(Run(text, "28", bold: true));
        _body.Append(p);
        return this;
    }

    /// <summary>主送机关（顶格，三号仿宋）。</summary>
    public OfficialDocWriter Recipient(string text)
    {
        var p = NewP(JustificationValues.Left);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>正文段落（两端对齐，三号仿宋，首行缩进）。</summary>
    public OfficialDocWriter Body(string text)
    {
        var p = NewP(JustificationValues.Both, indent: "420");
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>结束语（如"特此通知""以上请示妥否，请批示"）。</summary>
    public OfficialDocWriter Closing(string text)
    {
        var p = NewP(JustificationValues.Left);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>发文机关署名（右对齐）。</summary>
    public OfficialDocWriter Signature(string text)
    {
        var p = NewP(JustificationValues.Right);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>成文日期（右对齐）。</summary>
    public OfficialDocWriter Date(string text)
    {
        var p = NewP(JustificationValues.Right);
        p.Append(Run(text, "21"));
        _body.Append(p);
        return this;
    }

    /// <summary>空白分隔行。</summary>
    public OfficialDocWriter Blank()
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

    static Run Run(string t, string fs, bool bold = false, string? color = null)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "仿宋", HighAnsi = "仿宋", EastAsia = "仿宋" });
        if (bold) rpr.Append(new Bold());
        if (color != null) rpr.Append(new Color { Val = color });
        rpr.Append(new FontSize { Val = fs });
        return new Run(rpr, new Text(t));
    }
}
