using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace Nong.Inspect;

/// <summary>
/// 论文写作器。组合 DocumentWriter，提供论文专用的链式 API。
/// 正文中可用 [@key] 引用文献数据库中的条目，自动编号为 [1][2]...
/// </summary>
public class PaperWriter
{
    readonly Body _body;
    readonly DocumentWriter _w;
    Dictionary<string, RefEntry>? _refDb;

    public PaperWriter(Body body) { _body = body; _w = new DocumentWriter(body); }
    public PaperWriter(Body body, WordprocessingDocument doc) { _body = body; _w = new DocumentWriter(body, doc); }

    /// <summary>设置参考文献数据库。正文中 [@key] 将自动替换为 [1][2]... 编号。</summary>
    public PaperWriter SetReferenceDatabase(Dictionary<string, RefEntry> db) { _refDb = db; return this; }

    // ===== 中文标题/摘要 =====

    public PaperWriter Title(string text) { P(text, "Title"); return this; }
    public PaperWriter SubTitle(string text) { P(text, "SubTitle"); return this; }
    public PaperWriter AbstractTitle(string text = "摘  要") { P(text, "AbstractTitle"); return this; }
    public PaperWriter Abstract(string text) { P(text, "Abstract"); return this; }
    public PaperWriter Keywords(string kw)
    {
        var p = NewP("Abstract");
        p.Append(RunBold("关键词："));
        p.Append(Run(" " + kw));
        _body.Append(p);
        return this;
    }

    // ===== 英文标题/摘要 =====

    public PaperWriter EnglishTitle(string text) { P(text, "EnglishTitle"); return this; }
    public PaperWriter EnglishAbstractTitle(string text = "Abstract") { P(text, "AbstractTitle"); return this; }
    public PaperWriter EnglishAbstract(string text) { P(text, "Abstract"); return this; }
    public PaperWriter EnglishKeywords(string kw)
    {
        var p = NewP("Abstract");
        p.Append(RunBold("Key words: "));
        p.Append(Run(kw));
        _body.Append(p);
        return this;
    }

    // ===== 标题（自动编号） =====

    int _h1, _h2;

    public PaperWriter Heading(string text, int level = 1)
    {
        string sid = level switch { 1 => "Heading1", 2 => "Heading2", _ => "Heading3" };
        string prefix = level switch { 1 => $"{++_h1}  ", 2 => $"{_h1}.{++_h2}  ", _ => "" };
        if (level == 1) _h2 = 0;
        P(prefix + text, sid);
        return this;
    }

    public PaperWriter BibHeading(string text = "参考文献") { P(text, "BibHeading"); return this; }

    // ===== 内联格式标记（Pandoc 语法子集） =====
    //
    // 支持的标记：
    //   ***text***  加粗+斜体
    //   **text**    加粗
    //   *text*      斜体（不跟 * 时才生效，避免与 ** 冲突）
    //   ==text==    荧光高亮
    //   ~~text~~    删除线
    //   ^text^      上标（手动上标，如 ^注1^）
    //   ~text~      下标（如 H~2~O）
    //
    // 自动检测（无需标记）：
    //   [N] [N,M] [N-M]  引文上标
    //   (Latin name) 或 （Latin name）  拉丁文学名斜体
    //
    // ===== 正文 =====

    public PaperWriter Body(string text)
    {
        // 如果设置了参考文献数据库，先解析 [@key] → [N]
        if (_refDb != null)
            text = ReferenceManager.Resolve(text, _refDb);

        var p = NewP("Normal");
        foreach (var seg in ParseInline(text))
        {
            var rpr = new RunProperties();
            if (seg.Format.HasFlag(InlineFmt.Bold)) rpr.Append(new Bold());
            if (seg.Format.HasFlag(InlineFmt.Italic)) rpr.Append(new Italic());
            if (seg.Format.HasFlag(InlineFmt.Superscript))
            {
                rpr.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
                rpr.Append(new FontSize { Val = "18" });
            }
            if (seg.Format.HasFlag(InlineFmt.Subscript))
                rpr.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });
            if (seg.Format.HasFlag(InlineFmt.Highlight))
                rpr.Append(new Highlight { Val = HighlightColorValues.Yellow });
            if (seg.Format.HasFlag(InlineFmt.Strikethrough))
                rpr.Append(new Strike());
            p.Append(new Run(rpr, new Text(seg.Text)));
        }
        _body.Append(p);
        return this;
    }

    [Flags]
    enum InlineFmt { None = 0, Bold = 1, Italic = 2, Superscript = 4, Subscript = 8, Highlight = 16, Strikethrough = 32 }

    record struct InlineSeg(string Text, InlineFmt Format);

    static List<InlineSeg> ParseInline(string text)
    {
        // 统一匹配所有标记（长标记优先避免歧义）
        var pattern = @"(\[\d+(?:[,-]\d+)*\])|" +        // 引文上标 [1] [2,3] [1-3]
                       @"([（\(][A-Za-z][A-Za-z\s\.\-]+[）\)])|" +  // 拉丁名斜体 (E. coli)
                       @"(\*\*\*[^*]+\*\*\*)|" +          // 加粗斜体
                       @"(\*\*[^*]+\*\*)|" +              // 加粗
                       @"(\*(?!\*)[^*]+\*(?!\*))|" +      // 斜体（单星不跟星）
                       @"(==[^=]+==)|" +                  // 高亮
                       @"(~~[^~]+~~)|" +                  // 删除线
                       @"(\^[^^]+\^)|" +                  // 上标
                       @"(~[^~]+~)";                      // 下标

        var result = new List<InlineSeg>();
        var matches = Regex.Matches(text, pattern);
        int pos = 0;

        foreach (Match m in matches)
        {
            // 匹配之前的普通文本
            if (m.Index > pos)
                result.Add(new InlineSeg(text[pos..m.Index], InlineFmt.None));

            var raw = m.Value;
            if (raw.StartsWith('[') && raw.EndsWith(']'))
            {
                // 引文上标
                result.Add(new InlineSeg(raw, InlineFmt.Superscript));
            }
            else if ((raw.StartsWith('（') || raw.StartsWith('(')) && raw.Length > 2)
            {
                // 拉丁名斜体（包含括号）
                result.Add(new InlineSeg(raw, InlineFmt.Italic));
            }
            else if (raw.StartsWith("***") && raw.EndsWith("***"))
            {
                result.Add(new InlineSeg(raw[3..^3], InlineFmt.Bold | InlineFmt.Italic));
            }
            else if (raw.StartsWith("**") && raw.EndsWith("**"))
            {
                result.Add(new InlineSeg(raw[2..^2], InlineFmt.Bold));
            }
            else if (raw.StartsWith('*') && raw.EndsWith('*'))
            {
                result.Add(new InlineSeg(raw[1..^1], InlineFmt.Italic));
            }
            else if (raw.StartsWith("==") && raw.EndsWith("=="))
            {
                result.Add(new InlineSeg(raw[2..^2], InlineFmt.Highlight));
            }
            else if (raw.StartsWith("~~") && raw.EndsWith("~~"))
            {
                result.Add(new InlineSeg(raw[2..^2], InlineFmt.Strikethrough));
            }
            else if (raw.StartsWith('^') && raw.EndsWith('^'))
            {
                result.Add(new InlineSeg(raw[1..^1], InlineFmt.Superscript));
            }
            else if (raw.StartsWith('~') && raw.EndsWith('~'))
            {
                result.Add(new InlineSeg(raw[1..^1], InlineFmt.Subscript));
            }

            pos = m.Index + m.Length;
        }

        // 末尾剩余普通文本
        if (pos < text.Length)
            result.Add(new InlineSeg(text[pos..], InlineFmt.None));

        return result;
    }

    // ===== 图 =====

    public PaperWriter Figure(string caption, int? num = null)
    {
        int n = num ?? 1;
        var p = NewP("BodyTextNoIndent", JustificationValues.Center);
        p.Append(Run("[在此处插入图片]", "18"));
        _body.Append(p);
        var c = NewP("BodyTextNoIndent", JustificationValues.Center);
        c.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "16" }), new Text($"图{n}  {caption}")));
        _body.Append(c);
        _body.Append(new Paragraph());
        return this;
    }

    // ===== 表格（委托给 DocumentWriter） =====

    public PaperWriter Table(string caption, int num, string[] headers, string[][] rows)
    {
        _w.Table(caption, num, headers, rows);
        return this;
    }

    public PaperWriter VariableTable(string caption, int num, List<VariablePlanRow> variables)
    {
        var headers = VariablePlanGenerator.Columns;
        var rows = variables.Select(v => new[] {
            v.变量名称, v.中文标签, v.变量角色, v.理论含义, v.操作化方式,
            v.数据类型, v.测量题项指标, v.取值范围, v.数据来源, v.是否必须,
            v.分析用途, v.缺失风险,
        }).ToArray();
        return Table(caption, num, headers, rows);
    }

    // ===== 参考文献 =====

    /// <summary>
    /// 参考文献列表。Word 自动添加 [1] [2] [3]... 编号，请勿在文本中手写编号。
    /// 如已手写，会自动剥离。
    /// </summary>
    public PaperWriter References(params string[] refs)
    {
        foreach (var txt in refs)
        {
            var p = NewP("ReferenceText");
            // 剥离用户手写的 [N] / [N,M] / [N-M] 前缀，编号由 Word 自动生成
            var cleaned = Regex.Replace(txt.TrimStart(), @"^\[\d+(?:[,-]\d+)*\]\s*", "");
            p.Append(Run(cleaned));
            _body.Append(p);
        }
        return this;
    }

    /// <summary>
    /// 自动生成参考文献列表。从之前 Body() 调用中收集的引用键，
    /// 在数据库中查找对应条目，按 GB/T 7714 格式化输出。
    /// 使用前需先调用 SetReferenceDatabase() 和 Body()（含 [@key] 引用）。
    /// </summary>
    public PaperWriter AutoReferences()
    {
        if (_refDb == null)
        {
            _body.Append(NewP("Normal"));
            return this; // 无数据库，跳过
        }

        // 从之前的 Body 调用中收集引用键
        var allKeys = new List<string>();
        var allSeen = new HashSet<string>();
        foreach (var para in _body.Elements<Paragraph>())
        {
            var pt = para.InnerText;
            foreach (var k in ReferenceManager.CitedKeys(pt))
            {
                if (allSeen.Add(k))
                    allKeys.Add(k);
            }
        }

        if (allKeys.Count == 0) return this;

        var refs = ReferenceManager.GenerateRefs(allKeys, _refDb);
        foreach (var txt in refs)
        {
            var p = NewP("ReferenceText");
            var cleaned = Regex.Replace(txt.TrimStart(), @"^\[\d+(?:[,-]\d+)*\]\s*", "");
            p.Append(Run(cleaned));
            _body.Append(p);
        }
        return this;
    }

    // ===== 委托给 DocumentWriter 的引擎方法 =====

    public PaperWriter Footnote(string text) { _w.Footnote(text); return this; }
    public PaperWriter Endnote(string text) { _w.Endnote(text); return this; }
    public PaperWriter CrossReference(string bookmarkName, string displayText) { _w.CrossReference(bookmarkName, displayText); return this; }
    public PaperWriter Hyperlink(string url, string displayText) { _w.Hyperlink(url, displayText); return this; }
    public PaperWriter Bookmark(string name) { _w.Bookmark(name); return this; }
    public PaperWriter TableOfContents(string title = "目录") { _w.TableOfContents(title); return this; }
    public PaperWriter BarChart(string title, string[] categories, double[] values, string seriesName = "系列 1") { _w.BarChart(title, categories, values, seriesName); return this; }
    public PaperWriter TableStyle(string styleId) { _w.TableStyle(styleId); return this; }

    // === helpers ===

    void P(string t, string sid)
    {
        var p = NewP(sid);
        p.Append(Run(t));
        _body.Append(p);
    }

    static Paragraph NewP(string sid, JustificationValues? align = null)
    {
        var ppr = new ParagraphProperties(new ParagraphStyleId { Val = sid });
        if (align != null) ppr.Justification = new Justification { Val = align.Value };
        return new Paragraph(ppr);
    }

    static Run Run(string t, string? fs = null) => fs != null
        ? new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = fs }), new Text(t))
        : new Run(new Text(t));

    static Run RunBold(string t) => new Run(new RunProperties(new Bold()), new Text(t));
}
