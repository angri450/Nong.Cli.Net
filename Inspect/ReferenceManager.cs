using System.Text.RegularExpressions;

namespace Nong.Inspect;

/// <summary>
/// 参考文献管理器。负责引用键解析、自动编号、条目格式化（GB/T 7714）。
///
/// 用法：
///   var db = new Dictionary&lt;string, RefEntry&gt; { ["smith2024"] = new RefEntry { ... } };
///   var (body, refs) = ReferenceManager.Process(text, db);
///   // body: 正文中 [@smith2024] → [1]
///   // refs: 格式化好的参考文献列表 ["[1] Smith J. ...", "[2] ..."]
/// </summary>
public static class ReferenceManager
{
    static readonly Regex CitePattern = new(@"\[@([a-zA-Z0-9_\-]+(?:\s*;\s*@[a-zA-Z0-9_\-]+)*)\]", RegexOptions.Compiled);

    /// <summary>
    /// 解析正文中的 [@key] 标记，替换为 [1][2]... 顺序编号。
    /// </summary>
    public static string Resolve(string bodyText, Dictionary<string, RefEntry> database)
    {
        var order = BuildOrder(bodyText);
        return CitePattern.Replace(bodyText, m =>
        {
            var keys = ParseKeys(m.Groups[1].Value);
            var nums = keys.Select(k =>
            {
                var idx = order.IndexOf(k);
                return idx >= 0 ? (idx + 1).ToString() : "??";
            });
            return "[" + string.Join(",", nums) + "]";
        });
    }

    /// <summary>返回正文中按出现顺序排列的引用键列表。</summary>
    public static List<string> CitedKeys(string bodyText)
    {
        return BuildOrder(bodyText);
    }

    /// <summary>根据引用键列表生成格式化参考文献列表（GB/T 7714）。</summary>
    public static List<string> GenerateRefs(List<string> keys, Dictionary<string, RefEntry> database, bool numbering = true)
    {
        var refs = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (!database.TryGetValue(key, out var entry))
            {
                refs.Add($"[?] 缺失引用: {key}");
                continue;
            }
            var num = numbering ? $"[{i + 1}] " : "";
            refs.Add(num + Format(entry));
        }
        return refs;
    }

    /// <summary>检查正文中引用但数据库缺失的键。</summary>
    public static List<string> MissingKeys(string bodyText, Dictionary<string, RefEntry> database)
    {
        var cited = new HashSet<string>();
        foreach (Match m in CitePattern.Matches(bodyText))
        {
            foreach (var k in ParseKeys(m.Groups[1].Value))
                cited.Add(k);
        }
        return cited.Where(k => !database.ContainsKey(k)).ToList();
    }

    /// <summary>检查数据库中未被引用的键。</summary>
    public static List<string> UncitedKeys(string bodyText, Dictionary<string, RefEntry> database)
    {
        var cited = new HashSet<string>();
        foreach (Match m in CitePattern.Matches(bodyText))
        {
            foreach (var k in ParseKeys(m.Groups[1].Value))
                cited.Add(k);
        }
        return database.Keys.Where(k => !cited.Contains(k)).ToList();
    }

    // ===== 格式化（GB/T 7714） =====

    static string Format(RefEntry e)
    {
        if (e.Raw != null) return e.Raw;

        return e.Type switch
        {
            "article" => FmtArticle(e),
            "book" => FmtBook(e),
            "thesis" => FmtThesis(e),
            "conference" => FmtConference(e),
            "patent" => FmtPatent(e),
            "report" => FmtReport(e),
            "standard" => FmtStandard(e),
            "online" => FmtOnline(e),
            _ => FmtArticle(e), // fallback
        };
    }

    static string JoinAuthors(List<string> authors)
    {
        if (authors.Count == 0) return "";
        if (authors.Count <= 3) return string.Join(", ", authors);
        return $"{authors[0]}, {authors[1]}, {authors[2]}, 等";
    }

    static string FmtArticle(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var vol = e.Volume != null ? $", {e.Volume}" : "";
        var iss = e.Issue != null ? $"({e.Issue})" : "";
        var pages = e.Pages != null ? $": {e.Pages}" : "";
        var doi = e.Doi != null ? $". DOI: {e.Doi}" : "";
        return $"{authors}. {e.Title}[J]. {e.Journal}, {e.Year}{vol}{iss}{pages}.{doi}";
    }

    static string FmtBook(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var pub = e.Publisher != null ? $"{e.Publisher}, " : "";
        var pages = e.Pages != null ? $": {e.Pages}" : "";
        return $"{authors}. {e.Title}[M]. {pub}{e.Year}{pages}.";
    }

    static string FmtThesis(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var degree = e.Degree != null ? $"[{e.Degree}]" : "[D]";
        var pub = e.Publisher != null ? $". {e.Publisher}" : "";
        return $"{authors}. {e.Title}{degree}. {e.Journal}{pub}, {e.Year}.";
    }

    static string FmtConference(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var pages = e.Pages != null ? $": {e.Pages}" : "";
        return $"{authors}. {e.Title}[C]. {e.Journal}, {e.Year}{pages}.";
    }

    static string FmtPatent(RefEntry e)
    {
        var holder = e.Author.Count > 0 ? e.Author[0] : "";
        var num = e.Number != null ? $": {e.Number}" : "";
        return $"{holder}. {e.Title}[P]{num}. {e.Year}.";
    }

    static string FmtReport(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var pub = e.Publisher != null ? $". {e.Publisher}" : "";
        return $"{authors}. {e.Title}[R]{pub}, {e.Year}.";
    }

    static string FmtStandard(RefEntry e)
    {
        var num = e.Number != null ? $"{e.Number}, " : "";
        return $"{num}{e.Title}[S]. {e.Publisher}, {e.Year}.";
    }

    static string FmtOnline(RefEntry e)
    {
        var authors = JoinAuthors(e.Author);
        var access = e.AccessDate != null ? $"[引用日期 {e.AccessDate}]" : "";
        return $"{authors}. {e.Title}[EB/OL]. {e.Url}{access}.";
    }

    // ===== helpers =====

    static List<string> BuildOrder(string bodyText)
    {
        var order = new List<string>();
        var seen = new HashSet<string>();
        foreach (Match m in CitePattern.Matches(bodyText))
        {
            foreach (var k in ParseKeys(m.Groups[1].Value))
            {
                if (seen.Add(k))
                    order.Add(k);
            }
        }
        return order;
    }

    static List<string> ParseKeys(string group)
    {
        return group.Split(';')
            .Select(s => s.Trim().TrimStart('@'))
            .Where(s => s.Length > 0)
            .ToList();
    }
}
