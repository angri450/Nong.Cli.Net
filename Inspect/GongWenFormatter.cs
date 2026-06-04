using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Nong.Inspect;

/// <summary>
/// 中国党政机关公文 / 学术论文排版格式化器。
/// 按用户规范实现：
///   标题  方正小标宋简体 二号 不加粗 居中
///   一级  黑体 三号 不加粗 段前段后6磅 行距1.5倍 首行缩进2字符
///   二级  楷体_GB2312 三号 加粗 行距1.5倍 首行缩进2字符
///   三级  仿宋_GB2312 三号 加粗 行距1.5倍 首行缩进2字符
///   正文  仿宋_GB2312 三号 不加粗 行距1.5倍 首行缩进2字符
///   脚注  仿宋_GB2312 四号 单倍行距 不缩进
///   页码  封面无页码；目录≥2页用罗马数字；正文从首页起阿拉伯数字，仿宋小四居中
/// </summary>
public class GongWenFormatter
{
    // ====== 字号（OpenXML 为 half-points）======
    public const string FontSizeErhao = "44";    // 二号 22pt
    public const string FontSizeSanhao = "32";   // 三号 16pt
    public const string FontSizeSihao = "28";    // 四号 14pt
    public const string FontSizeXiaosi = "24";   // 小四 12pt

    // ====== 行距 ======
    public const string LineSpacing15 = "360";    // 1.5倍
    public const string LineSpacingSingle = "240"; // 单倍
    public const string LineSpacingTitle = "560";  // 标题固定值28磅

    // 段前段后 6 磅 = 120 DXA
    public const string SpacingBeforeAfter = "120";

    // 首行缩进 2 字符（OpenXML 的 firstLineChars 单位是 1/100 字符，故 200=2 字符）
    public const int FirstLineChars = 200;

    // ====== 样式 ID ======
    // 标题类直接复用 Word 内置 styleId（Title / Heading1 / Heading2 / Heading3），
    // 这样 Word / WPS 的导航窗格 / 大纲视图按内置样式识别，不需要再做外部映射。
    // 原稿若已有同名样式，AddOrReplace 会先删再建，保证格式规范。
    public const string StyleIdTitle = "Title";
    public const string StyleIdHeading1 = "Heading1";
    public const string StyleIdHeading2 = "Heading2";
    public const string StyleIdHeading3 = "Heading3";
    public const string StyleIdBody = "GongWenBody";
    public const string StyleIdFootnote = "GongWenFootnote";

    // ====== 字体名 ======
    // 关键：用中文名（中文 Windows 上字体以中文名注册），Word 才能正确匹配到；
    // 配合显式 w:hint="eastAsia"，并在重建 rFonts 时不写 *Theme 属性以避免主题覆盖。
    public const string FontTitle = "方正小标宋简体";
    public const string FontH1 = "黑体";
    public const string FontH2 = "楷体_GB2312";
    public const string FontH3 = "仿宋_GB2312";
    public const string FontBody = "仿宋_GB2312";
    public const string FontFootnote = "仿宋_GB2312";
    public const string FontPageNumber = "仿宋_GB2312";

    private enum ParaRole { Unclassified, Title, Cover, TocMarker, TocItem, H1, H2, H3, Body }
    private enum NumberScheme { None, ChineseTopLevel, DecimalMultiLevel }

    private sealed class ClassifiedPara
    {
        public Paragraph Element = null!;
        public int Index;
        public string Text = "";
        public ParaRole Role = ParaRole.Unclassified;
        // 若原稿 numPr 自动编号会渲染出前缀（如 "1."、"一、"），在此固化为文本，
        // 清 numPr 后再作为文字前置到段落开头，保留可见编号
        public string? NumberPrefix;
    }

    private sealed class DocumentStructure
    {
        public List<ClassifiedPara> Paragraphs = new();
        public int? TitleIndex;
        public int? TocStartIndex;
        public int? TocEndIndex;
        public int BodyStartIndex;
        public int EstimatedTocPageCount;
        public NumberScheme Scheme;
    }

    // ====== 入口 ======

    private FormatOptions _options = new();

    public void FormatDocument(WordprocessingDocument doc, FormatOptions? options = null)
    {
        _options = options ?? new FormatOptions();

        var mainPart = doc.MainDocumentPart;
        if (mainPart == null) return;
        var body = mainPart.Document?.Body;
        if (body == null) return;

        SanitizePartRoots(mainPart);
        CreateGongWenStyles(mainPart);

        var topLevelParas = body.Elements<Paragraph>().ToList();

        // 关键：NumberingResolver 必须在 Analyze 之前跑——
        // 原稿的"1. 紧张局势..."里"1."是 Word 自动编号动态渲染的，原段文本只有"紧张局势..."。
        // 用户填 --h2-marker "1" 指望匹配可见的"1.xxx"，必须把渲染前缀先拼进分类器看到的 text。
        var resolver = new NumberingResolver();
        resolver.Load(mainPart.NumberingDefinitionsPart);
        var prefixes = new Dictionary<int, string?>();
        for (int i = 0; i < topLevelParas.Count; i++)
        {
            prefixes[i] = resolver.AdvanceAndFormat(topLevelParas[i]);
        }

        var structure = Analyze(topLevelParas, prefixes);

        ApplyFormatting(structure);
        ApplyFootnoteFormatting(mainPart);

        SetupSections(mainPart, body, structure);

        SanitizePartRoots(mainPart);
        mainPart.Document.Save();
    }

    private static void SanitizePartRoots(MainDocumentPart mainPart)
    {
        if (mainPart.Document?.Body != null)
            SanitizeRoot(mainPart.Document.Body);
        if (mainPart.StyleDefinitionsPart?.Styles != null)
            SanitizeRoot(mainPart.StyleDefinitionsPart.Styles);
        if (mainPart.NumberingDefinitionsPart?.Numbering != null)
            SanitizeRoot(mainPart.NumberingDefinitionsPart.Numbering);
        if (mainPart.DocumentSettingsPart?.Settings != null)
            SanitizeRoot(mainPart.DocumentSettingsPart.Settings);

        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header != null) SanitizeRoot(headerPart.Header);
        }

        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer != null) SanitizeRoot(footerPart.Footer);
        }

        if (mainPart.FootnotesPart?.Footnotes != null)
            SanitizeRoot(mainPart.FootnotesPart.Footnotes);
        if (mainPart.EndnotesPart?.Endnotes != null)
            SanitizeRoot(mainPart.EndnotesPart.Endnotes);
        if (mainPart.WordprocessingCommentsPart?.Comments != null)
            SanitizeRoot(mainPart.WordprocessingCommentsPart.Comments);
    }

    private static void SanitizeRoot(OpenXmlElement root)
    {
        DocxCore.ElementOrder.SanitizeCompatibilityArtifacts(root);
        DocxCore.ElementOrder.RectifyTree(root);
        DocxCore.ElementOrder.FixOrphanBorders(root);
        DocxCore.ElementOrder.RectifyTree(root);
    }

    // ====== 结构分析 ======

    private Regex? _userH1Regex;
    private Regex? _userH2Regex;
    private Regex? _userH3Regex;

    private DocumentStructure Analyze(List<Paragraph> paragraphs, Dictionary<int, string?> prefixes)
    {
        _userH1Regex = MarkerPatternInferrer.Infer(_options.H1MarkerSample);
        _userH2Regex = MarkerPatternInferrer.Infer(_options.H2MarkerSample);
        _userH3Regex = MarkerPatternInferrer.Infer(_options.H3MarkerSample);
        var s = new DocumentStructure();
        for (int i = 0; i < paragraphs.Count; i++)
        {
            s.Paragraphs.Add(new ClassifiedPara
            {
                Element = paragraphs[i],
                Index = i,
                Text = paragraphs[i].InnerText?.Trim() ?? "",
                NumberPrefix = prefixes.TryGetValue(i, out var pf) ? pf : null
            });
        }

        // 1a. 优先读取 Word 既有样式 / OutlineLevel（作者已经手工标注的层级）
        foreach (var p in s.Paragraphs)
        {
            var r = DetectRoleFromExistingStyle(p.Element);
            if (r.HasValue) p.Role = r.Value;
        }

        // 1b. 用户明确指定的 H1/H2/H3 样例 —— 必须在 numPr ilvl 之前，
        //     因为用户的"1"marker 是对**可见渲染文本**的期待；numbering 段原文本不带前缀，
        //     需要先把 NumberPrefix 拼进去再用 regex 测。
        //     顺序：H3 → H2 → H1（最具体的先试，防止 H1 "1." 把 H3 "1.1.1" 错吃）
        foreach (var p in s.Paragraphs)
        {
            if (p.Role != ParaRole.Unclassified) continue;
            var textForMatch = string.IsNullOrEmpty(p.NumberPrefix)
                ? p.Text
                : (p.NumberPrefix + (p.NumberPrefix!.EndsWith(".") || char.IsDigit(p.NumberPrefix[^1]) ? " " : "") + p.Text);
            if (string.IsNullOrEmpty(textForMatch)) continue;
            if (_userH3Regex != null && _userH3Regex.IsMatch(textForMatch)) { p.Role = ParaRole.H3; continue; }
            if (_userH2Regex != null && _userH2Regex.IsMatch(textForMatch)) { p.Role = ParaRole.H2; continue; }
            if (_userH1Regex != null && _userH1Regex.IsMatch(textForMatch)) { p.Role = ParaRole.H1; continue; }
        }

        // 1c. 其次才看自动编号 numPr 的 ilvl（fallback，用户没指定 marker 时）
        //     只对明显像标题的段（短 + 加粗）生效，避免把正文里的带编号列表也误当标题
        foreach (var p in s.Paragraphs)
        {
            if (p.Role != ParaRole.Unclassified) continue;
            var r = DetectRoleFromNumPr(p);
            if (r.HasValue) p.Role = r.Value;
        }

        // 2. 目录
        DetectToc(s);

        // 3. 主标题（TOC 之前的首个短段）
        DetectTitle(s);

        // 4. 编号方案（按正文段落的前缀统计）
        s.Scheme = DetectNumberScheme(s);

        // 5. 对尚未分类的段落进行启发式分级
        ClassifyRemaining(s);

        // 6. 计算正文起点
        if (s.TocEndIndex.HasValue) s.BodyStartIndex = s.TocEndIndex.Value + 1;
        else if (s.TitleIndex.HasValue) s.BodyStartIndex = s.TitleIndex.Value + 1;
        else s.BodyStartIndex = 0;

        // 7. 粗估目录页数（30 段/页）
        if (s.TocStartIndex.HasValue && s.TocEndIndex.HasValue)
        {
            int n = s.TocEndIndex.Value - s.TocStartIndex.Value + 1;
            s.EstimatedTocPageCount = Math.Max(1, (n + 29) / 30);
        }

        return s;
    }

    /// <summary>
    /// 根据段落挂的自动编号 numPr 的 ilvl 推定层级：ilvl=0→H1, 1→H2, 2→H3。
    /// 仅对"看起来像标题"的段生效（非空、长度 ≤50、至少一个 run 加粗），
    /// 避免把正文内的有序列表误判为标题。
    /// </summary>
    private ParaRole? DetectRoleFromNumPr(ClassifiedPara cp)
    {
        var numPr = cp.Element.ParagraphProperties?.NumberingProperties;
        if (numPr == null) return null;

        if (cp.Text.Length == 0 || cp.Text.Length > 50) return null;
        var runs = cp.Element.Elements<Run>().ToList();
        if (runs.Count == 0) return null;
        bool anyBold = runs.Any(r =>
        {
            var b = r.RunProperties?.Bold;
            return b != null && (b.Val?.Value ?? true);
        });
        if (!anyBold) return null;

        int ilvl = numPr.NumberingLevelReference?.Val?.Value ?? 0;
        return ilvl switch
        {
            0 => ParaRole.H1,
            1 => ParaRole.H2,
            2 => ParaRole.H3,
            _ => null
        };
    }

    private ParaRole? DetectRoleFromExistingStyle(Paragraph p)
    {
        var pPr = p.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrEmpty(styleId))
        {
            var sid = styleId.Replace(" ", "").ToLowerInvariant();
            if (sid == "heading1" || sid == "标题1" || sid == StyleIdHeading1.ToLowerInvariant()) return ParaRole.H1;
            if (sid == "heading2" || sid == "标题2" || sid == StyleIdHeading2.ToLowerInvariant()) return ParaRole.H2;
            if (sid == "heading3" || sid == "标题3" || sid == StyleIdHeading3.ToLowerInvariant()) return ParaRole.H3;
            if (sid == "title" || sid == "文档标题" || sid == StyleIdTitle.ToLowerInvariant()) return ParaRole.Title;
        }
        var outline = pPr?.OutlineLevel?.Val?.Value;
        if (outline.HasValue)
        {
            return outline.Value switch
            {
                0 => ParaRole.H1,
                1 => ParaRole.H2,
                2 => ParaRole.H3,
                _ => null
            };
        }
        return null;
    }

    private void DetectToc(DocumentStructure s)
    {
        int limit = Math.Min(s.Paragraphs.Count, 80);
        for (int i = 0; i < limit; i++)
        {
            if (IsTocMarker(s.Paragraphs[i].Text))
            {
                s.TocStartIndex = i;
                s.Paragraphs[i].Role = ParaRole.TocMarker;
                break;
            }
        }
        if (!s.TocStartIndex.HasValue) return;

        // 目录项判定：有前导点引线、省略号、或末尾有页码；对目录而言这些比"像标题"更可靠
        int safetyLimit = Math.Min(s.Paragraphs.Count, s.TocStartIndex.Value + 300);
        int lastTocItemIdx = s.TocStartIndex.Value;
        int consecutiveNonTocItems = 0;

        for (int i = s.TocStartIndex.Value + 1; i < safetyLimit; i++)
        {
            var p = s.Paragraphs[i];

            // 空段 / 分页符段 先跳过但不改变 TOC 结束位置
            if (string.IsNullOrEmpty(p.Text))
            {
                consecutiveNonTocItems++;
                if (consecutiveNonTocItems >= 2)
                {
                    // 连续空段视为 TOC 结束的信号
                    break;
                }
                continue;
            }

            if (IsTocItemLike(p.Text))
            {
                p.Role = ParaRole.TocItem;
                lastTocItemIdx = i;
                consecutiveNonTocItems = 0;
                continue;
            }

            // 明显超长或没有 TOC 特征 → TOC 已经结束
            break;
        }
        s.TocEndIndex = lastTocItemIdx;
    }

    private bool IsTocItemLike(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Length > 120) return false;
        return Regex.IsMatch(text, @"\.{3,}")          // 连续英文点
            || Regex.IsMatch(text, @"…{1,}")             // 省略号
            || Regex.IsMatch(text, @"\t\s*\d+\s*$")    // Tab + 页码
            || Regex.IsMatch(text, @"\s+\d+\s*$")      // 空格 + 页码
            || Regex.IsMatch(text, @"\.{2,}\s*\d+\s*$");
    }

    private void DetectTitle(DocumentStructure s)
    {
        int searchEnd = s.TocStartIndex ?? s.Paragraphs.Count;
        for (int i = 0; i < searchEnd; i++)
        {
            var p = s.Paragraphs[i];
            if (string.IsNullOrEmpty(p.Text)) continue;
            if (p.Role == ParaRole.H1 || p.Role == ParaRole.H2 || p.Role == ParaRole.H3) return;
            if (p.Role == ParaRole.Title) { s.TitleIndex = i; return; }

            if (p.Text.Length < 60 && !LooksLikeAnyHeading(p.Text))
            {
                s.TitleIndex = i;
                p.Role = ParaRole.Title;
            }
            return; // 只看第一个非空段
        }
    }

    private NumberScheme DetectNumberScheme(DocumentStructure s)
    {
        // 扫描正文候选区域（跳过 TOC）
        int start = s.TocEndIndex.HasValue ? s.TocEndIndex.Value + 1 : (s.TitleIndex ?? -1) + 1;
        int chineseTop = 0, chapter = 0, decimalNested = 0, decimalTop = 0;
        for (int i = start; i < s.Paragraphs.Count; i++)
        {
            var t = s.Paragraphs[i].Text;
            if (string.IsNullOrEmpty(t)) continue;
            if (Regex.IsMatch(t, @"^[一二三四五六七八九十百]+[、\.．]")) chineseTop++;
            else if (Regex.IsMatch(t, @"^第[一二三四五六七八九十百]+[章篇部]")) chapter++;
            else if (Regex.IsMatch(t, @"^\d+[\.．]\d+")) decimalNested++;
            else if (Regex.IsMatch(t, @"^\d+[\.．、]\s*[^\d\s]")) decimalTop++;
        }
        if (chineseTop + chapter > 0) return NumberScheme.ChineseTopLevel;
        if (decimalNested > 0 || decimalTop >= 2) return NumberScheme.DecimalMultiLevel;
        return NumberScheme.None;
    }

    private void ClassifyRemaining(DocumentStructure s)
    {
        int start = s.BodyStartIndex;
        if (s.TocEndIndex.HasValue) start = s.TocEndIndex.Value + 1;
        else if (s.TitleIndex.HasValue) start = s.TitleIndex.Value + 1;
        else start = 0;

        for (int i = 0; i < s.Paragraphs.Count; i++)
        {
            var p = s.Paragraphs[i];
            if (p.Role != ParaRole.Unclassified) continue;          // 已被既有样式 / TOC / 标题定过的跳过
            if (string.IsNullOrEmpty(p.Text)) { p.Role = ParaRole.Body; continue; }
            if (i < start) { p.Role = ParaRole.Cover; continue; }   // 封面区（TOC 前、非标题的其它段）

            p.Role = ClassifyHeadingByText(p, s.Scheme);
        }
    }

    private ParaRole ClassifyHeadingByText(ClassifiedPara cp, NumberScheme scheme)
    {
        var text = cp.Text;

        // Markdown
        if (Regex.IsMatch(text, @"^#{3}\s")) return ParaRole.H3;
        if (Regex.IsMatch(text, @"^#{2}\s")) return ParaRole.H2;
        if (Regex.IsMatch(text, @"^#\s"))    return ParaRole.H1;

        if (scheme == NumberScheme.ChineseTopLevel)
        {
            if (Regex.IsMatch(text, @"^[一二三四五六七八九十百]+[、\.．]")) return ParaRole.H1;
            if (Regex.IsMatch(text, @"^第[一二三四五六七八九十百]+[章篇部]")) return ParaRole.H1;
            if (Regex.IsMatch(text, @"^[（(][一二三四五六七八九十]+[）)]")) return ParaRole.H2;
            if (Regex.IsMatch(text, @"^第[一二三四五六七八九十]+节")) return ParaRole.H2;
            if (Regex.IsMatch(text, @"^\d+[\.．、]\s*[^\d\s]")) return ParaRole.H3;
            if (Regex.IsMatch(text, @"^[（(]\d+[）)]")) return ParaRole.H3;
            if (Regex.IsMatch(text, @"^第\d+条")) return ParaRole.H3;
        }
        else if (scheme == NumberScheme.DecimalMultiLevel)
        {
            if (Regex.IsMatch(text, @"^\d+[\.．]\d+[\.．]\d+")) return ParaRole.H3;
            if (Regex.IsMatch(text, @"^\d+[\.．]\d+(?!\d)")) return ParaRole.H2;
            if (Regex.IsMatch(text, @"^\d+[\.．、]\s*[^\d\s]")) return ParaRole.H1;
        }

        // 信号法兜底：短 + 加粗 + 无句末标点 → 疑似标题，降级为 H2
        if (IsShortBoldHeading(cp)) return ParaRole.H2;

        return ParaRole.Body;
    }

    private bool LooksLikeAnyHeading(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Regex.IsMatch(text, @"^[一二三四五六七八九十百]+[、\.．]")
            || Regex.IsMatch(text, @"^第[一二三四五六七八九十百]+[章节篇部条]")
            || Regex.IsMatch(text, @"^[（(][一二三四五六七八九十]+[）)]")
            || Regex.IsMatch(text, @"^[（(]\d+[）)]")
            || Regex.IsMatch(text, @"^\d+[\.．]")
            || Regex.IsMatch(text, @"^#{1,3}\s");
    }

    private bool IsShortBoldHeading(ClassifiedPara cp)
    {
        if (cp.Text.Length == 0 || cp.Text.Length > 30) return false;
        var runs = cp.Element.Elements<Run>().ToList();
        if (runs.Count == 0) return false;
        bool anyBold = runs.Any(r =>
        {
            var b = r.RunProperties?.Bold;
            return b != null && (b.Val?.Value ?? true);
        });
        bool noTerminalPunct = !Regex.IsMatch(cp.Text, @"[。.！!？?；;]$");
        return anyBold && noTerminalPunct;
    }

    private bool IsTocMarker(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string t = Regex.Replace(text.Trim(), @"\s+", "");
        return t == "目录" || t.Equals("contents", StringComparison.OrdinalIgnoreCase)
            || t.Equals("tableofcontents", StringComparison.OrdinalIgnoreCase);
    }

    // ====== 应用格式 ======

    private void ApplyFormatting(DocumentStructure s)
    {
        foreach (var p in s.Paragraphs)
        {
            switch (p.Role)
            {
                case ParaRole.Title: ApplyTitleStyle(p); break;
                case ParaRole.H1:    ApplyHeading1Style(p); break;
                case ParaRole.H2:    ApplyHeading2Style(p); break;
                case ParaRole.H3:    ApplyHeading3Style(p); break;
                case ParaRole.Body:
                    if (!string.IsNullOrEmpty(p.Text)) ApplyBodyTextStyle(p);
                    break;
                // Cover / Toc* 保持原样（numPr 不清，Word 继续按自动编号渲染）
            }
        }
    }

    private void ApplySpecToParagraph(ClassifiedPara cp, string styleId, StyleSpec spec)
    {
        var (line, lineRule) = spec.GetLineSpec();
        RebuildPPr(cp.Element, styleId,
            justify: ParseJustification(spec.Alignment),
            spacingBefore: spec.SpacingBeforePt > 0 ? spec.SpacingBeforeTwips : null,
            spacingAfter:  spec.SpacingAfterPt  > 0 ? spec.SpacingAfterTwips  : null,
            line: line, lineRule: ParseLineRule(lineRule),
            firstLineChars: spec.FirstLineIndentChars > 0 ? spec.FirstLineCharsValue : 0,
            firstLine: spec.FirstLineIndentChars > 0 ? null : "0",
            outlineLevel: OutlineLevelFor(styleId));
        RebuildRPr(cp.Element, spec.Font, spec.SzHalfPoints, bold: spec.Bold, italic: spec.Italic);
        InlineNumberPrefix(cp);
    }

    /// <summary>
    /// 把 styleId 映射到 Word 的 outlineLvl 值：H1=0, H2=1, H3=2。
    /// 决定了 Word / WPS 导航窗格 / 标签栏中段落的层级归属——
    /// 没写 outlineLvl 的段落即便套了"二级标题"样式，Word 仍当正文，误显示到顶层。
    /// </summary>
    private static int? OutlineLevelFor(string styleId)
    {
        return styleId switch
        {
            StyleIdHeading1 => 0,
            StyleIdHeading2 => 1,
            StyleIdHeading3 => 2,
            _ => (int?)null   // Title / Body / Footnote 不在大纲里
        };
    }

    private void ApplyTitleStyle(ClassifiedPara cp)    => ApplySpecToParagraph(cp, StyleIdTitle,    _options.Title);
    private void ApplyHeading1Style(ClassifiedPara cp) => ApplySpecToParagraph(cp, StyleIdHeading1, _options.H1);
    private void ApplyHeading2Style(ClassifiedPara cp) => ApplySpecToParagraph(cp, StyleIdHeading2, _options.H2);
    private void ApplyHeading3Style(ClassifiedPara cp) => ApplySpecToParagraph(cp, StyleIdHeading3, _options.H3);
    private void ApplyBodyTextStyle(ClassifiedPara cp) => ApplySpecToParagraph(cp, StyleIdBody,     _options.Body);

    private static JustificationValues? ParseJustification(string? a)
    {
        if (string.IsNullOrEmpty(a)) return null;
        return a.ToLowerInvariant() switch
        {
            "left" or "start" => JustificationValues.Left,
            "center" => JustificationValues.Center,
            "right" or "end" => JustificationValues.Right,
            "justify" or "both" => JustificationValues.Both,
            _ => null,
        };
    }

    private static LineSpacingRuleValues ParseLineRule(string r) => r switch
    {
        "exact" => LineSpacingRuleValues.Exact,
        "atLeast" => LineSpacingRuleValues.AtLeast,
        _ => LineSpacingRuleValues.Auto,
    };

    /// <summary>
    /// 如果原稿该段挂了 numPr 自动编号，我们在 RebuildPPr 里删掉了 numPr，
    /// 这里把解析出的前缀字符串（如 "1."、"一、"）作为普通文本插回到段落开头，
    /// 保留可见的编号。分隔符：如果前缀末尾是数字 / ASCII 句点，补一个空格；
    /// 是中文标点（、）等直接跟正文，避免多空格。
    /// </summary>
    private void InlineNumberPrefix(ClassifiedPara cp)
    {
        var prefix = cp.NumberPrefix;
        if (string.IsNullOrEmpty(prefix)) return;

        string sep = "";
        char last = prefix[^1];
        if (char.IsDigit(last) || last == '.' || last == ')' || last == ']') sep = " ";

        var para = cp.Element;
        var firstRun = para.Elements<Run>().FirstOrDefault();
        if (firstRun != null)
        {
            var firstText = firstRun.Elements<Text>().FirstOrDefault();
            if (firstText != null)
            {
                firstText.Text = prefix + sep + (firstText.Text ?? "");
                firstText.Space = SpaceProcessingModeValues.Preserve;
                return;
            }
            // first run 存在但没有 Text 子元素：在首个 run 前插一个新 run
        }
        // 造一个新 run，克隆 firstRun 的 rPr（已经是干净的标题 rPr）放到段首
        var newRun = new Run();
        if (firstRun?.RunProperties != null)
            newRun.AppendChild(firstRun.RunProperties.CloneNode(true));
        newRun.AppendChild(new Text(prefix + sep) { Space = SpaceProcessingModeValues.Preserve });

        // 插在 pPr 之后、其它 Run 之前
        var pPr = para.GetFirstChild<ParagraphProperties>();
        if (pPr != null)
            para.InsertAfter(newRun, pPr);
        else
            para.InsertAt(newRun, 0);
    }

    private void ApplyFootnoteFormatting(MainDocumentPart mainPart)
    {
        var footnotesPart = mainPart.FootnotesPart;
        if (footnotesPart?.Footnotes == null) return;
        var spec = _options.Footnote;
        var (line, lineRule) = spec.GetLineSpec();

        foreach (var footnote in footnotesPart.Footnotes.Elements<Footnote>())
        {
            foreach (var para in footnote.Elements<Paragraph>())
            {
                RebuildPPr(para, StyleIdFootnote,
                    justify: ParseJustification(spec.Alignment),
                    spacingBefore: spec.SpacingBeforePt > 0 ? spec.SpacingBeforeTwips : null,
                    spacingAfter:  spec.SpacingAfterPt  > 0 ? spec.SpacingAfterTwips  : null,
                    line: line, lineRule: ParseLineRule(lineRule),
                    firstLineChars: spec.FirstLineIndentChars > 0 ? spec.FirstLineCharsValue : 0,
                    firstLine: spec.FirstLineIndentChars > 0 ? null : "0");
                RebuildRPr(para, spec.Font, spec.SzHalfPoints, bold: spec.Bold, italic: spec.Italic);
            }
        }
    }

    // ====== Section / 页码 ======

    private void SetupSections(MainDocumentPart mainPart, Body body, DocumentStructure s)
    {
        // 清理原有 SectPr（段落级 & Body 级）
        foreach (var p in body.Descendants<Paragraph>())
        {
            p.ParagraphProperties?.RemoveAllChildren<SectionProperties>();
        }
        body.RemoveAllChildren<SectionProperties>();

        // 删除原有 FooterParts
        foreach (var fp in mainPart.FooterParts.ToList())
        {
            mainPart.DeletePart(fp);
        }

        // 创建正文阿拉伯页脚
        var bodyFooterPart = mainPart.AddNewPart<FooterPart>();
        bodyFooterPart.Footer = CreatePageNumberFooter(useRoman: false);
        bodyFooterPart.Footer.Save();
        string bodyFooterId = mainPart.GetIdOfPart(bodyFooterPart);

        // 创建空页脚（封面使用，避免继承）
        var emptyFooterPart = mainPart.AddNewPart<FooterPart>();
        emptyFooterPart.Footer = CreateEmptyFooter();
        emptyFooterPart.Footer.Save();
        string emptyFooterId = mainPart.GetIdOfPart(emptyFooterPart);

        // 目录罗马页脚（仅当 ≥2 页目录）
        bool hasTocSection = s.TocStartIndex.HasValue && s.TocEndIndex.HasValue
                             && s.EstimatedTocPageCount >= 2
                             && s.TocEndIndex.Value >= s.TocStartIndex.Value;
        string? tocFooterId = null;
        if (hasTocSection)
        {
            var tocFooterPart = mainPart.AddNewPart<FooterPart>();
            tocFooterPart.Footer = CreatePageNumberFooter(useRoman: true);
            tocFooterPart.Footer.Save();
            tocFooterId = mainPart.GetIdOfPart(tocFooterPart);
        }

        var paragraphs = s.Paragraphs;

        // section 划分规则：
        //   - 有 TOC 且 ≥2 页：[封面: TOC 前] [TOC: TOC 区] [正文]
        //   - 有 TOC 但 <2 页：[封面∪TOC: 含 TOC 区整体] [正文]（二者均无页码）
        //   - 无 TOC：直接 [正文]，正文首页显示阿拉伯"1"
        int? coverEndIdx = null;
        if (s.TocStartIndex.HasValue)
        {
            if (hasTocSection)
                coverEndIdx = s.TocStartIndex.Value - 1;
            else
                coverEndIdx = s.TocEndIndex ?? (s.TocStartIndex.Value);
        }

        if (coverEndIdx.HasValue && coverEndIdx.Value >= 0 && coverEndIdx.Value < paragraphs.Count)
        {
            var para = paragraphs[coverEndIdx.Value].Element;
            var pPr = EnsureParagraphProperties(para);
            pPr.AppendChild(BuildSectionProperties(emptyFooterId, pageFormat: null, nextPage: true));
        }

        if (hasTocSection)
        {
            var para = paragraphs[s.TocEndIndex!.Value].Element;
            var pPr = EnsureParagraphProperties(para);
            pPr.AppendChild(BuildSectionProperties(tocFooterId!, pageFormat: NumberFormatValues.UpperRoman, nextPage: true));
        }

        // 正文 section（置于 Body 末尾）
        var bodySect = BuildSectionProperties(bodyFooterId, pageFormat: NumberFormatValues.Decimal, nextPage: false);
        body.AppendChild(bodySect);
    }

    private SectionProperties BuildSectionProperties(string footerRefId, NumberFormatValues? pageFormat, bool nextPage)
    {
        var sect = new SectionProperties(
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin { Top = 1984, Right = 1474, Bottom = 1984, Left = 1588, Header = 851, Footer = 992 }
        );
        sect.AppendChild(new FooterReference { Type = HeaderFooterValues.Default, Id = footerRefId });
        if (pageFormat.HasValue)
        {
            sect.AppendChild(new PageNumberType { Start = 1, Format = pageFormat.Value });
        }
        else
        {
            // 封面：Start=1 但不设 Format（封面不显示页码，保险起见也重置计数）
            sect.AppendChild(new PageNumberType { Start = 1 });
        }
        if (nextPage)
        {
            sect.AppendChild(new SectionType { Val = SectionMarkValues.NextPage });
        }
        return sect;
    }

    private Footer CreateEmptyFooter()
    {
        var footer = new Footer();
        footer.AppendChild(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Footer" })));
        return footer;
    }

    private Footer CreatePageNumberFooter(bool useRoman)
    {
        var footer = new Footer();
        var para = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center })
        );
        string fieldCode = useRoman ? " PAGE \\* ROMAN " : " PAGE ";

        para.AppendChild(new Run(
            CreateFooterRunProps(),
            new FieldChar { FieldCharType = FieldCharValues.Begin }
        ));
        para.AppendChild(new Run(
            CreateFooterRunProps(),
            new FieldCode(fieldCode) { Space = SpaceProcessingModeValues.Preserve }
        ));
        para.AppendChild(new Run(
            CreateFooterRunProps(),
            new FieldChar { FieldCharType = FieldCharValues.Separate }
        ));
        para.AppendChild(new Run(CreateFooterRunProps(), new Text("1")));
        para.AppendChild(new Run(
            CreateFooterRunProps(),
            new FieldChar { FieldCharType = FieldCharValues.End }
        ));

        footer.AppendChild(para);
        return footer;
    }

    private RunProperties CreateFooterRunProps()
    {
        return new RunProperties(
            new RunFonts
            {
                EastAsia = FontPageNumber,
                Ascii = FontPageNumber,
                HighAnsi = FontPageNumber,
                ComplexScript = FontPageNumber,
                Hint = FontTypeHintValues.EastAsia
            },
            new FontSize { Val = FontSizeXiaosi },
            new FontSizeComplexScript { Val = FontSizeXiaosi }
        );
    }

    // ====== 辅助 ======

    private ParagraphProperties EnsureParagraphProperties(Paragraph para)
    {
        var pPr = para.GetFirstChild<ParagraphProperties>();
        if (pPr == null)
        {
            pPr = new ParagraphProperties();
            para.InsertAt(pPr, 0);
        }
        return pPr;
    }

    /// <summary>
    /// 重建 pPr：把原 pPr 整个丢掉（清除 numPr / 边框 / keepNext / kinsoku / 残留 rPr / 等等所有 WPS 塞的脏数据），
    /// 按 OpenXML schema 顺序（pStyle 必须为第一个子元素）构造一份干净的 pPr。
    /// </summary>
    private void RebuildPPr(Paragraph para, string styleId,
        JustificationValues? justify,
        string? spacingBefore, string? spacingAfter,
        string? line, LineSpacingRuleValues? lineRule,
        int? firstLineChars, string? firstLine,
        int? outlineLevel = null)
    {
        // 保留 SectionProperties（给 SetupSections 用），其它一律重建
        var preservedSectPrs = para.ParagraphProperties?.Elements<SectionProperties>().ToList() ?? new();
        para.ParagraphProperties?.Remove();

        var pPr = new ParagraphProperties();

        // 按 schema：pStyle 必须是第一个
        pPr.AppendChild(new ParagraphStyleId { Val = styleId });

        if (justify.HasValue)
            pPr.AppendChild(new Justification { Val = justify.Value });

        if (spacingBefore != null || spacingAfter != null || line != null)
        {
            var sp = new SpacingBetweenLines();
            if (spacingBefore != null) sp.Before = spacingBefore;
            if (spacingAfter != null) sp.After = spacingAfter;
            if (line != null) { sp.Line = line; sp.LineRule = lineRule; }
            pPr.AppendChild(sp);
        }

        if (firstLineChars.HasValue || firstLine != null)
        {
            var ind = new Indentation();
            if (firstLineChars.HasValue) ind.FirstLineChars = firstLineChars.Value;
            if (firstLine != null) ind.FirstLine = firstLine;
            pPr.AppendChild(ind);
        }

        // outlineLvl：Word / WPS 导航窗格 / 标签栏依此判标题级别
        // H1=0 / H2=1 / H3=2；其它（Title / Body / Footnote）不设，等于 body text (9)
        if (outlineLevel.HasValue)
            pPr.AppendChild(new OutlineLevel { Val = outlineLevel.Value });

        foreach (var sect in preservedSectPrs)
            pPr.AppendChild(sect.CloneNode(true));

        para.InsertAt(pPr, 0);
    }

    /// <summary>
    /// 重建 rPr：整个 RunProperties 丢掉（含 *Theme 主题字体属性、颜色、高亮、字符样式、kern、bdr、lang 等），
    /// 按 schema 顺序重建一份仅包含字体/粗体/字号的干净 rPr，并带 w:hint="eastAsia" 方便 Word 按 CJK 路径匹配。
    /// </summary>
    private void RebuildRPr(Paragraph para, string fontName, string fontSize, bool bold, bool italic = false)
    {
        foreach (var run in para.Elements<Run>())
        {
            run.RunProperties?.Remove();

            var rPr = new RunProperties();

            // rFonts —— 关键：**不写** *Theme 属性，防止 Word 用主题覆盖
            var fonts = new RunFonts
            {
                Ascii = fontName,
                HighAnsi = fontName,
                EastAsia = fontName,
                ComplexScript = fontName,
                Hint = FontTypeHintValues.EastAsia
            };
            rPr.AppendChild(fonts);

            // Bold（显式 true/false 覆盖样式继承）
            if (bold)
            {
                rPr.AppendChild(new Bold());
                rPr.AppendChild(new BoldComplexScript());
            }
            else
            {
                rPr.AppendChild(new Bold { Val = false });
                rPr.AppendChild(new BoldComplexScript { Val = false });
            }

            // Italic（schema 顺序：i/iCs 在 b/bCs 之后）
            if (italic)
            {
                rPr.AppendChild(new Italic());
                rPr.AppendChild(new ItalicComplexScript());
            }
            else
            {
                rPr.AppendChild(new Italic { Val = false });
                rPr.AppendChild(new ItalicComplexScript { Val = false });
            }

            rPr.AppendChild(new FontSize { Val = fontSize });
            rPr.AppendChild(new FontSizeComplexScript { Val = fontSize });

            run.InsertAt(rPr, 0);
        }
    }

    // 旧方法保留签名以防被其它方法调用；实际内部转调新实现
    private void ApplyRunFont(Paragraph para, string fontName, string fontSize, bool bold)
    {
        foreach (var run in para.Elements<Run>())
        {
            var rPr = run.GetFirstChild<RunProperties>();
            if (rPr == null)
            {
                rPr = new RunProperties();
                run.InsertAt(rPr, 0);
            }

            // 清除干扰属性
            rPr.RemoveAllChildren<RunStyle>();
            rPr.RemoveAllChildren<Color>();
            rPr.RemoveAllChildren<Highlight>();

            // 字体
            var fonts = rPr.GetFirstChild<RunFonts>();
            if (fonts == null) { fonts = new RunFonts(); rPr.InsertAt(fonts, 0); }
            fonts.EastAsia = fontName;
            fonts.Ascii = fontName;
            fonts.HighAnsi = fontName;
            fonts.ComplexScript = fontName;

            // 字号
            var sz = rPr.GetFirstChild<FontSize>();
            if (sz == null) { sz = new FontSize(); rPr.AppendChild(sz); }
            sz.Val = fontSize;

            var szCs = rPr.GetFirstChild<FontSizeComplexScript>();
            if (szCs == null) { szCs = new FontSizeComplexScript(); rPr.AppendChild(szCs); }
            szCs.Val = fontSize;

            // 粗体：显式写 true/false 防止被样式继承覆盖
            rPr.RemoveAllChildren<Bold>();
            rPr.RemoveAllChildren<BoldComplexScript>();
            if (bold)
            {
                rPr.AppendChild(new Bold());
                rPr.AppendChild(new BoldComplexScript());
            }
            else
            {
                rPr.AppendChild(new Bold { Val = false });
                rPr.AppendChild(new BoldComplexScript { Val = false });
            }
        }
    }

    // ====== 样式定义 ======

    private void CreateGongWenStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.StyleDefinitionsPart;
        if (stylesPart == null)
        {
            stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();
        }
        var styles = stylesPart.Styles ??= new Styles();

        RegisterStyleFromSpec(styles, StyleIdTitle,    "公文标题", _options.Title);
        RegisterStyleFromSpec(styles, StyleIdHeading1, "一级标题", _options.H1);
        RegisterStyleFromSpec(styles, StyleIdHeading2, "二级标题", _options.H2);
        RegisterStyleFromSpec(styles, StyleIdHeading3, "三级标题", _options.H3);
        RegisterStyleFromSpec(styles, StyleIdBody,     "公文正文", _options.Body);
        RegisterStyleFromSpec(styles, StyleIdFootnote, "公文脚注", _options.Footnote);

        stylesPart.Styles.Save();
    }

    /// <summary>按 StyleSpec 生成一个 Word 样式定义，插入到 styles.xml。</summary>
    private void RegisterStyleFromSpec(Styles styles, string styleId, string displayName, StyleSpec spec)
    {
        var (line, lineRule) = spec.GetLineSpec();
        var pPr = new StyleParagraphProperties();
        var jc = ParseJustification(spec.Alignment);
        if (jc.HasValue) pPr.AppendChild(new Justification { Val = jc.Value });

        var spacing = new SpacingBetweenLines { Line = line, LineRule = ParseLineRule(lineRule) };
        if (spec.SpacingBeforePt > 0) spacing.Before = spec.SpacingBeforeTwips;
        if (spec.SpacingAfterPt  > 0) spacing.After  = spec.SpacingAfterTwips;
        pPr.AppendChild(spacing);

        if (spec.FirstLineIndentChars > 0)
            pPr.AppendChild(new Indentation { FirstLineChars = spec.FirstLineCharsValue });
        else
            pPr.AppendChild(new Indentation { FirstLineChars = 0, FirstLine = "0" });

        // 大纲级别写进样式定义，让 Word / WPS 导航窗格正确归类
        var outline = OutlineLevelFor(styleId);
        if (outline.HasValue)
            pPr.AppendChild(new OutlineLevel { Val = outline.Value });

        var rPr = new StyleRunProperties(
            new RunFonts { Ascii = spec.Font, HighAnsi = spec.Font, EastAsia = spec.Font, ComplexScript = spec.Font, Hint = FontTypeHintValues.EastAsia },
            new FontSize { Val = spec.SzHalfPoints },
            new FontSizeComplexScript { Val = spec.SzHalfPoints }
        );
        if (spec.Bold)   { rPr.AppendChild(new Bold());   rPr.AppendChild(new BoldComplexScript()); }
        else             { rPr.AppendChild(new Bold { Val = false });   rPr.AppendChild(new BoldComplexScript { Val = false }); }
        if (spec.Italic) { rPr.AppendChild(new Italic()); rPr.AppendChild(new ItalicComplexScript()); }
        else             { rPr.AppendChild(new Italic { Val = false }); rPr.AppendChild(new ItalicComplexScript { Val = false }); }

        var existing = styles.Elements<Style>().FirstOrDefault(st => st.StyleId?.Value == styleId);
        existing?.Remove();

        // 关键 1：<w:name> 用 Word/WPS 内置约定的英文小写"heading N"，让两者的导航窗格按名字识别
        // 关键 2：加 <w:qFormat/>，Word "样式库"会把它当"标题"类快速样式；多数 WPS 版本也据此分类
        string nameForTool = styleId switch
        {
            StyleIdHeading1 => "heading 1",
            StyleIdHeading2 => "heading 2",
            StyleIdHeading3 => "heading 3",
            StyleIdTitle    => "Title",
            _               => displayName
        };

        var style = new Style(
            new StyleName { Val = nameForTool },
            new BasedOn { Val = "Normal" },
            pPr,
            rPr
        )
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId
        };

        // 标题类样式加上 qFormat + UIPriority，Word/WPS 识别为"标题"
        if (outline.HasValue || styleId == StyleIdTitle)
        {
            style.AppendChild(new UIPriority { Val = 9 });
            style.AppendChild(new PrimaryStyle());       // <w:qFormat/>
        }
        // 别名保留中文显示名，便于在 Word 样式面板里看到"一级标题 / 二级标题 / 三级标题"
        if (nameForTool != displayName)
        {
            var aliases = new Aliases { Val = displayName };
            style.InsertAfter(aliases, style.GetFirstChild<StyleName>());
        }

        styles.AppendChild(style);
    }

    private void AddOrReplaceStyle(Styles styles, string styleId, string displayName,
        Func<StyleParagraphProperties> pPrBuilder, Func<StyleRunProperties> rPrBuilder)
    {
        var existing = styles.Elements<Style>().FirstOrDefault(st => st.StyleId?.Value == styleId);
        existing?.Remove();

        var style = new Style(
            new StyleName { Val = displayName },
            new BasedOn { Val = "Normal" },
            pPrBuilder(),
            rPrBuilder()
        )
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId
        };
        styles.AppendChild(style);
    }
}
