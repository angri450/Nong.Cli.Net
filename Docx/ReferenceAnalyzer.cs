using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// 参考文献风险分析器。提取、解析、检查参考文献的格式完整性和正文引用对应关系。
/// 移植自 SynthDataDesktop paper_references.py。
/// </summary>
public static class ReferenceAnalyzer
{
    static readonly Regex RefHeadingRe = new(
        @"^\s*(?:[\d一二三四五六七八九十]+[、.．]\s*)?(参考文献|参考资料|引用文献|references|bibliography)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex NextBackMatterRe = new(
        @"^\s*(附录|致谢|作者简介|appendix|acknowledg?ements?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>提取参考文献文本块。</summary>
    public static string ExtractReferenceBlock(string text)
    {
        var m = RefHeadingRe.Match(text);
        if (!m.Success) return "";
        var block = text[(m.Index + m.Length)..];
        var next = NextBackMatterRe.Match(block);
        if (next.Success) block = block[..next.Index];
        return block.Trim();
    }

    /// <summary>从参考文献块中提取条目列表。</summary>
    public static List<ReferenceEntry> ExtractReferences(string text)
    {
        var block = ExtractReferenceBlock(text);
        if (string.IsNullOrEmpty(block)) return new();
        var lines = block.Split('\n');
        var rows = new List<ReferenceEntry>();
        var current = "";
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var startsNew = Regex.IsMatch(line,
                @"^(\[\d+\]|\d+[.、．]|[A-Z][A-Za-z\-]+.+\([12]\d{3}\)|[一-龥A-Za-z]{2,}.+[12]\d{3})");
            if (startsNew && current.Length > 0)
            {
                rows.Add(ParseReference(current, rows.Count + 1));
                current = line;
            }
            else current = $"{current} {line}".Trim();
        }
        if (current.Length > 0) rows.Add(ParseReference(current, rows.Count + 1));
        return rows;
    }

    static ReferenceEntry ParseReference(string line, int index)
    {
        var numberMatch = Regex.Match(line, @"^\s*(?:\[(\d+)\]|(\d+)[.、．])\s*");
        var normalized = Regex.Replace(line, @"^\s*(?:\[\d+\]|\d+[.、．])\s*", "").Trim();
        var yearMatch = Regex.Match(line, @"(19|20)\d{2}");
        var doiMatch = Regex.Match(line, @"10\.\d{4,9}/[-._;()/:A-Za-z0-9]+");
        var authorHint = AuthorHint(normalized);
        var titleHint = TitleHint(normalized);
        var risks = new List<string>();
        if (!yearMatch.Success) risks.Add("年份缺失或难以识别");
        if (normalized.Length < 12) risks.Add("条目过短");
        if (string.IsNullOrEmpty(authorHint)) risks.Add("作者字段不清楚");
        if (string.IsNullOrEmpty(titleHint)) risks.Add("题名字段不清楚");
        return new ReferenceEntry
        {
            序号 = index,
            编号 = numberMatch.Groups[1].Success ? numberMatch.Groups[1].Value : (numberMatch.Groups[2].Success ? numberMatch.Groups[2].Value : ""),
            原文 = line,
            年份 = yearMatch.Success ? yearMatch.Value : "",
            作者线索 = authorHint,
            题名线索 = titleHint,
            DOI = doiMatch.Success ? doiMatch.Value : "",
            格式风险 = string.Join("；", risks),
        };
    }

    static string AuthorHint(string reference)
    {
        var cleaned = reference.Trim();
        if (cleaned.Length == 0) return "";
        if (cleaned.Contains('.')) return cleaned.Split('.')[0].Trim().Truncate(80);
        if (cleaned.Contains('，')) return cleaned.Split('，')[0].Trim().Truncate(80);
        if (cleaned.Contains(',')) return cleaned.Split(',')[0].Trim().Truncate(80);
        var yearPos = Regex.Match(cleaned, @"(19|20)\d{2}");
        if (yearPos.Success) return cleaned[..yearPos.Index].Trim(' ', '.', '，', ',', '(', ')', '（', '）').Truncate(80);
        return cleaned.Length <= 60 ? cleaned[..Math.Min(30, cleaned.Length)] : "";
    }

    static string TitleHint(string reference)
    {
        var cleaned = Regex.Replace(reference, @"^\s*(?:\[\d+\]|\d+[.、．])\s*", "").Trim();
        var parts = cleaned.Split(new[] { '.', '。' }, 2);
        if (parts.Length >= 2 && parts[1].Trim().Length > 0) return parts[1].Trim().Truncate(120);
        var yearRemoved = Regex.Replace(cleaned, @"[\(（]?(19|20)\d{2}[\)）]?[.，, ]*", "");
        yearRemoved = yearRemoved.Replace(AuthorHint(cleaned), "").Trim(' ', '.', '，', ',');
        return yearRemoved.Truncate(120);
    }

    /// <summary>提取正文中的行内引用。</summary>
    public static List<(string Citation, int Line)> ExtractInlineCitations(string text)
    {
        var findings = new List<(string, int)>();
        var patterns = new[]
        {
            @"\[\d+(?:[,-]\d+)*\]",
            @"[一-龥]{2,}(?:等)?（(?:19|20)\d{2}）",
            @"[一-龥]{2,}(?:等)?\s*[，,]\s*(?:19|20)\d{2}",
            @"\([A-Z][A-Za-z\-]+(?: et al\.)?,\s*(?:19|20)\d{2}\)",
        };
        foreach (var pattern in patterns)
            foreach (Match m in Regex.Matches(text, pattern))
                findings.Add((m.Value, text[..m.Index].Count(c => c == '\n') + 1));
        return findings;
    }

    /// <summary>全面参考文献风险检查。</summary>
    public static List<ReferenceRisk> CheckReferenceRisks(string text, List<ReferenceEntry>? refs = null)
    {
        refs ??= ExtractReferences(text);
        var citations = ExtractInlineCitations(text);
        var risks = new List<ReferenceRisk>();

        if (refs.Count == 0)
            risks.Add(Risk("未识别到参考文献列表", "参考文献区域",
                "无法核对正文引用与参考文献是否对应。", "请补充规范参考文献列表；系统只提供格式和对应关系风险，不判断文献真实性。"));

        if (citations.Count == 0)
            risks.Add(Risk("正文引用较少或未识别", "全文",
                "文献综述可能没有与正文论点建立清晰对应关系。",
                "检查每个理论命题、变量选择和方法选择是否有对应文献支持。"));

        foreach (var r in refs)
            if (!string.IsNullOrEmpty(r.格式风险))
                risks.Add(Risk(r.格式风险, $"参考文献第{r.序号}条",
                    "作者、年份、题名或来源字段可能不完整。",
                    "用 CNKI、万方、Web of Science、Google Scholar 或学校数据库核验书目信息。"));

        if (refs.Count > 0 && citations.Count > 0)
        {
            var citedNums = new HashSet<string>();
            foreach (var (c, _) in citations)
                foreach (Match m in Regex.Matches(c, @"\[(\d+)\]"))
                    citedNums.Add(m.Groups[1].Value);

            var refNums = new HashSet<string>(refs.Where(r => !string.IsNullOrEmpty(r.编号)).Select(r => r.编号));

            foreach (var n in citedNums.Except(refNums).OrderBy(int.Parse).Take(8))
                risks.Add(Risk("正文编号引用未在参考文献中匹配", $"正文引用 [{n}]",
                    "可能存在正文引用缺失参考文献，或参考文献编号格式不规范。",
                    "人工核对该编号对应条目，禁止由软件自动补造文献。"));

            foreach (var n in refNums.Except(citedNums).OrderBy(int.Parse).Take(8))
                risks.Add(Risk("参考文献条目未在正文编号引用中出现", $"参考文献 [{n}]",
                    "该条文献可能未被正文引用，或正文采用作者—年份格式导致未匹配。",
                    "检查该条是否服务于正文论点；不需要的条目应删除，必要条目应在正文对应位置引用。"));
        }

        risks.Add(Risk("文献真实性未联网核验", "全体参考文献",
            "当前版本不联网核验 DOI、期刊、作者和题名真实性。",
            "输出检索关键词和数据库建议，由用户自行在正式数据库核验。"));

        return risks;
    }

    /// <summary>构建文献检索策略建议。</summary>
    public static Dictionary<string, object> BuildLiteratureSearchStrategy(List<string> keywords, string paperType)
    {
        var clean = keywords.Where(k => k.Length > 0).Take(8).ToList();
        return new()
        {
            ["检索数据库"] = new[] { "CNKI", "万方", "Web of Science", "Scopus", "Google Scholar" },
            ["检索关键词"] = clean.Count > 0 ? clean.ToArray() : new[] { "核心概念", "研究对象", "方法关键词" },
            ["方法文献建议"] = $"检索 {paperType} 相关方法论文、量表开发论文或识别策略论文。",
            ["理论文献建议"] = "围绕核心概念、解释机制和边界条件检索高被引理论文献。",
            ["经验文献建议"] = "检索同一研究对象、相近变量体系和相近数据来源的经验研究。",
            ["禁止事项"] = "不得由软件凭空生成真实文献条目。",
        };
    }

    static ReferenceRisk Risk(string problem, string location, string description, string suggestion) => new()
    {
        文献问题 = problem, 位置 = location, 风险说明 = description, 修改建议 = suggestion,
    };
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) => s.Length <= max ? s : s[..max];
}
