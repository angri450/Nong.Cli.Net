using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.Json;

namespace DocxCore;

/// <summary>
/// 页面布局构建器。A4 尺寸、页边距、页眉页脚引用。
/// 用法：var sectPr = new SectionBuilder().A4().Margins("2.5cm","2.5cm","3cm","2.5cm").Build();
/// </summary>
public class SectionBuilder
{
    string _width = "11906";  // A4 width in twips
    string _height = "16838"; // A4 height in twips
    string _marginTop = "1134";
    string _marginBottom = "1134";
    string _marginLeft = "1134";
    string _marginRight = "1134";
    HeaderReference? _headerRef;
    FooterReference? _footerRef;
    TitlePage? _titlePage;

    /// <summary>A4 (210×297mm)。</summary>
    public SectionBuilder A4()
    {
        _width = "11906";
        _height = "16838";
        return this;
    }

    /// <summary>Letter (8.5×11")。</summary>
    public SectionBuilder Letter()
    {
        _width = "12240";
        _height = "15840";
        return this;
    }

    /// <summary>设置四边页边距，单位为 cm 或 twips。支持 "2cm" 或 "1134" 格式。</summary>
    public SectionBuilder Margins(string top, string bottom, string left, string right)
    {
        _marginTop = ParseMargin(top);
        _marginBottom = ParseMargin(bottom);
        _marginLeft = ParseMargin(left);
        _marginRight = ParseMargin(right);
        return this;
    }

    /// <summary>设置四边统一页边距。</summary>
    public SectionBuilder Margins(string all) => Margins(all, all, all, all);

    /// <summary>首页不同（封面页）。</summary>
    public SectionBuilder DifferentFirstPage()
    {
        _titlePage = new TitlePage();
        return this;
    }

    /// <summary>绑定页眉引用。</summary>
    public SectionBuilder WithHeader(string headerPartId)
    {
        _headerRef = new HeaderReference { Type = HeaderFooterValues.Default, Id = headerPartId };
        return this;
    }

    /// <summary>绑定页脚引用。</summary>
    public SectionBuilder WithFooter(string footerPartId)
    {
        _footerRef = new FooterReference { Type = HeaderFooterValues.Default, Id = footerPartId };
        return this;
    }

    /// <summary>构建 SectionProperties。</summary>
    public SectionProperties Build()
    {
        var sp = new SectionProperties(
            new PageSize { Width = (ushort)int.Parse(_width), Height = (ushort)int.Parse(_height), Orient = PageOrientationValues.Portrait },
            new PageMargin
            {
                Top = int.Parse(_marginTop),
                Bottom = int.Parse(_marginBottom),
                Left = (ushort)int.Parse(_marginLeft),
                Right = (ushort)int.Parse(_marginRight),
                Header = 851,  // default ~1.5cm
                Footer = 851,
            });
        if (_headerRef != null) sp.Append(_headerRef);
        if (_footerRef != null) sp.Append(_footerRef);
        if (_titlePage != null) sp.Append(_titlePage);
        return sp;
    }

    /// <summary>从 JSON page 节点加载布局。</summary>
    public SectionBuilder FromJson(JsonElement page)
    {
        if (page.TryGetProperty("size", out var size) && size.GetString() == "Letter") Letter();
        if (page.TryGetProperty("marginTop", out var mt)) _marginTop = ParseMargin(mt.GetString() ?? "2.5cm");
        if (page.TryGetProperty("marginBottom", out var mb)) _marginBottom = ParseMargin(mb.GetString() ?? "2.5cm");
        if (page.TryGetProperty("marginLeft", out var ml)) _marginLeft = ParseMargin(ml.GetString() ?? "2.5cm");
        if (page.TryGetProperty("marginRight", out var mr)) _marginRight = ParseMargin(mr.GetString() ?? "2.5cm");
        return this;
    }

    static string ParseMargin(string value)
    {
        if (string.IsNullOrEmpty(value)) return "1134";
        if (value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value[..^2], out var cm))
                return ((int)Math.Round(cm / 2.54 * 1440)).ToString();
        }
        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value[..^2], out var mm))
                return ((int)Math.Round(mm / 25.4 * 1440)).ToString();
        }
        return value; // assume twips
    }
}
