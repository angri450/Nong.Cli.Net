using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// 模板引擎：加载已有 docx 模板，双路提取格式信息。
/// 路 1（文字路）：从段落文字中解析格式描述（如"黑体，四号，居中"）
/// 路 2（格式路）：从实际应用的样式和直接格式中提取参数
/// 两路合并，输出完整的格式规范。
/// </summary>
public class TemplateEngine
{
    /// <summary>分析结果：每段的结构化信息</summary>
    public record ParagraphInfo(
        int Index,
        string StyleId,
        string StyleName,
        string Text,
        FormatInfo Format,
        List<string> FormatHints   // 从文字中提取的格式描述
    );

    /// <summary>格式参数</summary>
    public record FormatInfo(
        string? FontCJK, string? FontLatin,
        string? FontSize,           // half-point
        bool Bold, bool Italic,
        string? Alignment,
        string? FirstLineIndent,
        string? LineSpacing,
        string? SpaceBefore, string? SpaceAfter
    );

    /// <summary>模板分析结果</summary>
    public record TemplateResult(
        string FilePath,
        List<FormatInfo> DefinedStyles,    // styles.xml 中定义的样式
        List<ParagraphInfo> Paragraphs,    // 每段的分析
        List<string> Issues               // 发现的问题
    );

    public static TemplateResult Analyze(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body ?? throw new InvalidOperationException("Document body is missing.");
        var stylesPart = doc.MainDocumentPart.StyleDefinitionsPart;

        // 路 1：提取已定义的样式
        var definedStyles = new List<FormatInfo>();
        if (stylesPart?.Styles != null)
        {
            foreach (var style in stylesPart.Styles.Elements<Style>())
            {
                var fi = ExtractStyleFormat(style);
                if (fi != null) definedStyles.Add(fi);
            }
        }

        // 路 2：逐段分析
        var paragraphs = new List<ParagraphInfo>();
        var issues = new List<string>();
        int i = 0;

        foreach (var para in body.Elements<Paragraph>())
        {
            i++;
            string text = para.InnerText;
            if (string.IsNullOrWhiteSpace(text)) continue;

            string sid = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            string sname = GetStyleName(stylesPart?.Styles, sid);

            // 路 2a：实际应用的格式
            var fmt = ExtractParagraphFormat(para);

            // 路 2b：从文字中解析格式描述
            var hints = ParseFormatHints(text);

            paragraphs.Add(new ParagraphInfo(i, sid, sname, text, fmt, hints));

            // 诊断
            bool noStyle = sid is "" or "Normal";
            bool hasDirect = fmt.Bold || fmt.FontSize != null || fmt.FontCJK != null || fmt.Alignment != null;
            bool hasHints = hints.Count > 0;

            if (noStyle && hasDirect && !hasHints)
                issues.Add($"P{i}: 无样式但有手工格式 — 疑似老师手工排版");
            else if (noStyle && hasHints)
                issues.Add($"P{i}: 无样式但文字含格式说明 — 可能是模板说明段");
            else if (!noStyle && hasDirect)
                issues.Add($"P{i}: 有样式{sid}但同时有手工格式覆盖");
        }

        return new TemplateResult(docxPath, definedStyles, paragraphs, issues);
    }

    /// <summary>从样式定义中提取格式参数</summary>
    static FormatInfo? ExtractStyleFormat(Style style)
    {
        var rpr = style.StyleRunProperties;
        var ppr = style.StyleParagraphProperties;
        if (rpr == null && ppr == null) return null;

        return new FormatInfo(
            rpr?.RunFonts?.EastAsia?.Value,
            rpr?.RunFonts?.Ascii?.Value,
            rpr?.FontSize?.Val?.Value,
            rpr?.Bold?.Val?.Value == true,
            rpr?.Italic?.Val?.Value == true,
            ppr?.Justification?.Val?.Value.ToString(),
            ppr?.Indentation?.FirstLine?.Value,
            ppr?.SpacingBetweenLines?.Line?.Value,
            ppr?.SpacingBetweenLines?.Before?.Value,
            ppr?.SpacingBetweenLines?.After?.Value
        );
    }

    /// <summary>从段落实例中提取实际格式</summary>
    static FormatInfo ExtractParagraphFormat(Paragraph para)
    {
        var ppr = para.ParagraphProperties;
        // 取第一个 Run 的属性作为代表
        var rpr = para.Elements<Run>().FirstOrDefault()?.RunProperties;

        return new FormatInfo(
            rpr?.RunFonts?.EastAsia?.Value,
            rpr?.RunFonts?.Ascii?.Value,
            rpr?.FontSize?.Val?.Value,
            rpr?.Bold?.Val?.Value == true,
            rpr?.Italic?.Val?.Value == true,
            ppr?.Justification?.Val?.Value.ToString(),
            ppr?.Indentation?.FirstLine?.Value,
            ppr?.SpacingBetweenLines?.Line?.Value,
            ppr?.SpacingBetweenLines?.Before?.Value,
            ppr?.SpacingBetweenLines?.After?.Value
        );
    }

    /// <summary>从文字中解析格式描述。
    /// 识别"黑体"、"四号"、"小五号"、"居中"、"加粗"、"首行缩进"等关键词。</summary>
    static List<string> ParseFormatHints(string text)
    {
        var hints = new List<string>();

        // 字体名
        foreach (var fn in _fontNames)
            if (text.Contains(fn)) hints.Add(fn);

        // 字号关键词
        foreach (var (kw, label) in _sizeKeywords)
            if (text.Contains(kw)) hints.Add(label);

        // 样式关键词
        foreach (var (kw, label) in _styleKeywords)
            if (text.Contains(kw)) hints.Add(label);

        return hints;
    }

    /// <summary>尝试将文字中的格式描述映射为具体格式参数</summary>
    public static FormatInfo? InferFormatFromText(string text)
    {
        var hints = ParseFormatHints(text);
        if (hints.Count == 0) return null;

        string? font = null, size = null, align = null;
        bool bold = false;

        foreach (var h in hints)
        {
            if (h is "黑体" or "宋体" or "楷体" or "仿宋") font = h;
            if (h is "Times New Roman") font = "Times New Roman";
            if (h.StartsWith("字号=")) size = h[3..];
            if (h is "居中" or "左对齐" or "两端对齐") align = h;
            if (h == "加粗") bold = true;
        }

        if (font == null && size == null && !bold && align == null) return null;

        // 字号关键词 → half-point
        string? halfPt = size switch
        {
            "四号" => "28", "小四号" => "24", "五号" => "21",
            "小五号" => "18", "六号" => "15", "三号" => "32",
            "小二" => "36", "二号" => "44", _ => null
        };

        string? jc = align switch
        {
            "居中" => "Center", "左对齐" => "Left",
            "两端对齐" => "Both", _ => null
        };

        return new FormatInfo(font, null, halfPt, bold, false, jc, null, null, null, null);
    }

    /// <summary>提取文档中所有图片到指定目录。返回提取的文件路径列表。</summary>
    public static List<string> ExtractImages(string docxPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var extracted = new List<string>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart!;

        // 正文
        ExtractImageParts(mainPart.ImageParts, outputDir, extracted);
        // 页眉
        foreach (var header in mainPart.HeaderParts)
            ExtractImageParts(header.ImageParts, outputDir, extracted);
        // 页脚
        foreach (var footer in mainPart.FooterParts)
            ExtractImageParts(footer.ImageParts, outputDir, extracted);

        return extracted;
    }

    static void ExtractImageParts(IEnumerable<ImagePart> parts, string dir, List<string> list)
    {
        foreach (var imagePart in parts)
        {
            string? ext = imagePart.ContentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tiff",
                "image/x-emf" => ".emf",
                "image/x-wmf" => ".wmf",
                _ => Path.GetExtension(imagePart.Uri.ToString())
            };

            string fileName = $"image_{list.Count + 1}{ext}";
            string path = Path.Combine(dir, fileName);

            using var stream = imagePart.GetStream();
            using var fileStream = File.Create(path);
            stream.CopyTo(fileStream);
            list.Add(path);
        }
    }

    static string GetStyleName(Styles? styles, string id)
    {
        if (styles == null || string.IsNullOrEmpty(id)) return "(none)";
        return styles.Elements<Style>()
            .FirstOrDefault(s => s.StyleId == id)?
            .StyleName?.Val?.Value ?? id;
    }

    static readonly string[] _fontNames = { "黑体", "宋体", "楷体", "仿宋", "Times New Roman", "Arial", "微软雅黑" };
    static readonly (string, string)[] _sizeKeywords = {
        ("四号", "字号=四号"), ("小四号", "字号=小四号"), ("五号", "字号=五号"),
        ("小五号", "字号=小五号"), ("六号", "字号=六号"), ("三号", "字号=三号"),
        ("小二", "字号=小二"), ("二号", "字号=二号")
    };
    static readonly (string, string)[] _styleKeywords = {
        ("加粗", "加粗"), ("居中", "居中"), ("左对齐", "左对齐"),
        ("两端对齐", "两端对齐"), ("首行缩进", "首行缩进"),
        ("单倍行距", "单倍行距"), ("1.5倍行距", "1.5倍行距")
    };
}
