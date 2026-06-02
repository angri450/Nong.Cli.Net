using System.Text.RegularExpressions;

namespace Nong.Inspect;

/// <summary>
/// 论文结构抽取器。从纯文本中识别标题层级、章节、摘要、关键词、表格。
/// 移植自 SynthDataDesktop paper_structure.py。
/// </summary>
public static class PaperStructureExtractor
{
    static readonly Dictionary<string, List<string>> SectionAliases = new()
    {
        ["abstract"] = new() { "摘要", "abstract" },
        ["keywords"] = new() { "关键词", "key words", "keywords" },
        ["introduction"] = new() { "引言", "绪论", "introduction" },
        ["literature_review"] = new() { "文献综述", "研究综述", "literature review" },
        ["theory"] = new() { "理论基础", "理论框架", "核心概念", "概念界定", "theory" },
        ["research_question"] = new() { "研究问题", "研究假设", "研究目的", "hypothesis", "research question" },
        ["method"] = new() { "研究方法", "方法", "method", "methodology" },
        ["data"] = new() { "数据来源", "资料来源", "样本说明", "数据与变量", "研究对象与数据", "data", "sample" },
        ["variables"] = new() { "变量说明", "变量定义", "变量测量", "变量操作化", "measurement" },
        ["results"] = new() { "结果", "实证结果", "分析结果", "results" },
        ["discussion"] = new() { "讨论", "discussion" },
        ["conclusion"] = new() { "结论", "conclusion" },
        ["references"] = new() { "参考文献", "参考资料", "引用文献", "references", "bibliography" },
        ["appendix"] = new() { "附录", "appendix" },
    };

    static readonly Regex HeadingRe = new(
        @"^\s*(?:(#{1,6})|第[一二三四五六七八九十百\d]+[章节部分]|" +
        @"[一二三四五六七八九十]+[、.．]|\d+(?:\.\d+){0,3}[、.．\s])\s*(.+?)\s*$",
        RegexOptions.Compiled);

    /// <summary>从纯文本构建论文结构。</summary>
    public static PaperStructure BuildPaperStructure(string text)
    {
        var normalized = NormalizeText(text);
        var sections = SplitSections(normalized);
        var (title, authors) = GuessTitleAndAuthors(normalized);
        var keywords = ExtractKeywords(normalized);
        var abstract_ = ExtractAbstract(normalized, sections);
        var tables = ExtractTablesFromText(normalized);
        int? refLine = null, appendixLine = null;
        foreach (var s in sections)
        {
            if (s.Canonical == "references" && refLine == null) refLine = s.StartLine;
            if (s.Canonical == "appendix" && appendixLine == null) appendixLine = s.StartLine;
        }
        return new PaperStructure
        {
            Title = title,
            Authors = authors,
            Abstract = abstract_,
            Keywords = keywords,
            Sections = sections,
            ReferenceStartLine = refLine,
            AppendixStartLine = appendixLine,
        };
    }

    static string NormalizeText(string text)
    {
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        text = Regex.Replace(text, "　", " ");
        text = Regex.Replace(text, @"[ \t]+", " ");
        return text.Trim();
    }

    static string CanonicalSection(string title)
    {
        var lowered = title.Trim().ToLowerInvariant();
        foreach (var (canonical, aliases) in SectionAliases)
            if (aliases.Any(a => lowered.Contains(a.ToLowerInvariant())))
                return canonical;
        return "other";
    }

    static int HeadingLevel(string line)
    {
        var s = line.Trim();
        if (s.StartsWith('#')) return Math.Min(s.Length - s.TrimStart('#').Length, 3);
        if (Regex.IsMatch(s, @"^\s*\d+\.\d+\.\d+")) return 3;
        if (Regex.IsMatch(s, @"^\s*\d+\.\d+")) return 2;
        if (Regex.IsMatch(s, @"^\s*第.+[章节]")) return 1;
        return 1;
    }

    static bool IsHeading(string line)
    {
        var s = line.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > 80) return false;
        if (HeadingRe.IsMatch(s)) return true;
        var canonical = CanonicalSection(s);
        return canonical != "other" && s.Length <= 24;
    }

    static string CleanHeadingTitle(string line)
    {
        var s = line.Trim().TrimStart('#').Trim();
        var m = HeadingRe.Match(line);
        if (m.Success) return m.Groups[2].Value.Trim(' ', '：', ':');
        return Regex.Replace(s, @"^\s*[\d一二三四五六七八九十]+[、.．]\s*", "").Trim(' ', '：', ':');
    }

    static List<PaperSection> SplitSections(string text)
    {
        var lines = NormalizeText(text).Split('\n');
        var positions = new List<(int lineNo, string title, int level)>();
        for (int i = 0; i < lines.Length; i++)
            if (IsHeading(lines[i]))
                positions.Add((i, CleanHeadingTitle(lines[i]), HeadingLevel(lines[i])));

        if (positions.Count == 0)
        {
            var body = string.Join('\n', lines).Trim();
            if (body.Length > 0)
                return new() { new PaperSection { Title = "全文", Level = 1, StartLine = 1, EndLine = lines.Length, Text = body, Canonical = "other" } };
            return new();
        }

        var sections = new List<PaperSection>();
        for (int i = 0; i < positions.Count; i++)
        {
            var (lineNo, title, level) = positions[i];
            var nextLine = i + 1 < positions.Count ? positions[i + 1].lineNo : lines.Length;
            var body = string.Join('\n', lines[(lineNo + 1)..nextLine]).Trim();
            sections.Add(new PaperSection
            {
                Title = title,
                Level = level,
                StartLine = lineNo + 1,
                EndLine = nextLine,
                Text = body,
                Canonical = CanonicalSection(title),
            });
        }
        return sections;
    }

    static (string title, List<string> authors) GuessTitleAndAuthors(string text)
    {
        var lines = NormalizeText(text).Split('\n')
            .Select(l => l.Trim(' ', '#')).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return ("未识别标题", new());

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "摘要", "关键词", "关键字", "abstract", "keywords" };
        var title = lines.Take(8).FirstOrDefault(l => !skip.Contains(l) && l.Length <= 80) ?? lines[0];
        var authors = lines.Skip(1).Take(7)
            .Where(l => Regex.IsMatch(l, @"(作者|姓名|学院|学校|指导教师|author)", RegexOptions.IgnoreCase))
            .ToList();
        return (title, authors);
    }

    static string ExtractAbstract(string text, List<PaperSection> sections)
    {
        var abs = sections.FirstOrDefault(s => s.Canonical == "abstract");
        if (abs != null) return abs.Text.Length > 1500 ? abs.Text[..1500] : abs.Text;
        var m = Regex.Match(text, @"摘要[:：]?\s*(?<body>.+?)(关键词|关键字|Abstract|Keywords)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups["body"].Value.Trim().Length > 1500 ? m.Groups["body"].Value.Trim()[..1500] : m.Groups["body"].Value.Trim();
        return "";
    }

    static List<string> ExtractKeywords(string text)
    {
        var m = Regex.Match(text, @"(关键词|关键字|Keywords)[:：]?\s*(?<body>[^\n]{1,300})", RegexOptions.IgnoreCase);
        if (!m.Success) return new();
        var parts = Regex.Split(m.Groups["body"].Value, @"[;；,，、\s]+");
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).Take(12).ToList();
    }

    static List<PaperTable> ExtractTablesFromText(string text)
    {
        var lines = NormalizeText(text).Split('\n');
        var tables = new List<PaperTable>();
        string? currentTitle = null;
        var currentRows = new List<List<string>>();
        int startLine = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var titleMatch = Regex.Match(line, @"^(?:表|Table)\s*[\d一二三四五六七八九十\-\.]*\s*[:：.]?\s*(.+)$", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                if (currentTitle != null || currentRows.Count > 0)
                    tables.Add(new PaperTable { Index = tables.Count + 1, Title = currentTitle ?? $"表{tables.Count + 1}", Rows = currentRows, SourceLine = startLine });
                currentTitle = line;
                currentRows = new();
                startLine = i + 1;
                continue;
            }
            if (line.Contains('|') && line.Count(c => c == '|') >= 2)
            {
                var row = line.Trim('|').Split('|').Select(c => c.Trim()).ToList();
                if (!row.All(c => Regex.IsMatch(c, @"^:?-{2,}:?$")))
                    currentRows.Add(row);
            }
        }
        if (currentTitle != null || currentRows.Count > 0)
            tables.Add(new PaperTable { Index = tables.Count + 1, Title = currentTitle ?? $"表{tables.Count + 1}", Rows = currentRows, SourceLine = startLine });
        return tables;
    }
}

/// <summary>
/// 论文中的表格（来自文本解析）。
/// </summary>
public sealed class PaperTable
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public List<List<string>> Rows { get; init; } = new();
    public int SourceLine { get; init; }
}
