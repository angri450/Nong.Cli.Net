using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Nong.Inspect;

/// <summary>
/// 把 Word 的"自动编号列表"(w:numPr) 渲染文本化：
/// 段落原本挂着 &lt;w:numPr numId=X ilvl=Y/&gt;，Word 会按 numbering.xml 里
/// abstractNum 的 lvlText ("%1." 之类) 和 numFmt (decimal/chineseCounting/…) 自动画出
/// "1.", "2.", "一、" 等前缀。公文排版时我们要删掉 numPr 避免列表缩进污染，
/// 但又不想把这些可见的数字丢掉，所以按文档顺序"跑一遍"列表计数器，
/// 把每段的前缀固化成字符串返回给调用者。
/// </summary>
public class NumberingResolver
{
    private sealed class LvlDef
    {
        public string LvlText = "";
        public NumberFormatValues Fmt = NumberFormatValues.Decimal;
        public int Start = 1;
    }

    // abstractNumId -> 各 ilvl 的定义
    private readonly Dictionary<string, List<LvlDef>> _abstracts = new();
    // numId -> abstractNumId
    private readonly Dictionary<string, string> _numIdToAbstract = new();
    // numId -> 每个 ilvl 的当前计数（运行时状态）
    private readonly Dictionary<string, int[]> _counters = new();

    private const int MaxLevels = 9;

    public void Load(NumberingDefinitionsPart? part)
    {
        var numbering = part?.Numbering;
        if (numbering == null) return;

        foreach (var abstractNum in numbering.Elements<AbstractNum>())
        {
            var aid = abstractNum.AbstractNumberId?.Value.ToString();
            if (aid == null) continue;
            var levels = new List<LvlDef>();
            for (int i = 0; i < MaxLevels; i++) levels.Add(new LvlDef());
            foreach (var lvl in abstractNum.Elements<Level>())
            {
                int ilvl = lvl.LevelIndex?.Value ?? 0;
                if (ilvl < 0 || ilvl >= MaxLevels) continue;
                var def = levels[ilvl];
                var fmt = lvl.NumberingFormat?.Val;
                if (fmt != null && fmt.HasValue) def.Fmt = fmt.Value;
                def.LvlText = lvl.LevelText?.Val?.Value ?? "";
                var start = lvl.StartNumberingValue?.Val?.Value;
                if (start.HasValue) def.Start = start.Value;
            }
            _abstracts[aid] = levels;
        }

        foreach (var ni in numbering.Elements<NumberingInstance>())
        {
            var nid = ni.NumberID?.Value.ToString();
            var aid = ni.AbstractNumId?.Val?.Value.ToString();
            if (nid != null && aid != null)
                _numIdToAbstract[nid] = aid;
        }
    }

    /// <summary>按文档顺序调用。有 numPr 的段落返回其前缀文本（如"1."、"一、"、"1.1"）；没有则返回 null。</summary>
    public string? AdvanceAndFormat(Paragraph p)
    {
        var numPr = p.ParagraphProperties?.NumberingProperties;
        if (numPr == null) return null;

        var numIdEl = numPr.NumberingId;
        if (numIdEl?.Val == null) return null;
        string numId = numIdEl.Val!.Value.ToString();
        int ilvl = numPr.NumberingLevelReference?.Val?.Value ?? 0;
        if (ilvl < 0 || ilvl >= MaxLevels) return null;

        if (!_numIdToAbstract.TryGetValue(numId, out var aid)) return null;
        if (!_abstracts.TryGetValue(aid, out var levels)) return null;

        if (!_counters.TryGetValue(numId, out var counters))
        {
            counters = new int[MaxLevels];
            _counters[numId] = counters;
        }

        // 推进计数：第一次见到该 ilvl 用 start，否则 +1；比 ilvl 更深的全部归零
        if (counters[ilvl] == 0)
            counters[ilvl] = levels[ilvl].Start;
        else
            counters[ilvl]++;
        for (int i = ilvl + 1; i < MaxLevels; i++) counters[i] = 0;

        var lvlDef = levels[ilvl];
        string text = Regex.Replace(lvlDef.LvlText, @"%(\d+)", m =>
        {
            int lvlIdx = int.Parse(m.Groups[1].Value) - 1;
            if (lvlIdx < 0 || lvlIdx >= MaxLevels) return "";
            int c = counters[lvlIdx];
            if (c == 0) c = levels[Math.Min(lvlIdx, levels.Count - 1)].Start;
            var fmt = (lvlIdx < levels.Count ? levels[lvlIdx] : lvlDef).Fmt;
            return FormatNumber(c, fmt);
        });
        return text;
    }

    private static string FormatNumber(int n, NumberFormatValues fmt)
    {
        if (fmt == NumberFormatValues.Decimal) return n.ToString();
        if (fmt == NumberFormatValues.DecimalZero) return n.ToString("D2");
        if (fmt == NumberFormatValues.UpperLetter) return ToLetters(n, upper: true);
        if (fmt == NumberFormatValues.LowerLetter) return ToLetters(n, upper: false);
        if (fmt == NumberFormatValues.UpperRoman) return ToRoman(n).ToUpperInvariant();
        if (fmt == NumberFormatValues.LowerRoman) return ToRoman(n).ToLowerInvariant();
        if (fmt == NumberFormatValues.ChineseCounting
         || fmt == NumberFormatValues.ChineseCountingThousand
         || fmt == NumberFormatValues.IdeographTraditional
         || fmt == NumberFormatValues.IdeographZodiacTraditional
         || fmt == NumberFormatValues.JapaneseCounting
         || fmt == NumberFormatValues.TaiwaneseCounting)
            return ToChineseCounting(n);
        if (fmt == NumberFormatValues.IdeographDigital
         || fmt == NumberFormatValues.JapaneseDigitalTenThousand)
            return ToChineseDigits(n);
        return n.ToString();
    }

    private static string ToLetters(int n, bool upper)
    {
        if (n < 1) return "";
        var sb = new StringBuilder();
        while (n > 0)
        {
            int r = (n - 1) % 26;
            sb.Insert(0, (char)((upper ? 'A' : 'a') + r));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    private static string ToRoman(int n)
    {
        if (n < 1) return "";
        int[] vals = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        string[] syms = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        var sb = new StringBuilder();
        for (int i = 0; i < vals.Length; i++)
            while (n >= vals[i]) { sb.Append(syms[i]); n -= vals[i]; }
        return sb.ToString();
    }

    private static readonly string CnDigits = "零一二三四五六七八九";

    // 中式计数 "一、二、…、九、十、十一、十二、…、二十、二十一、…"
    private static string ToChineseCounting(int n)
    {
        if (n <= 0) return "零";
        if (n < 10) return CnDigits[n].ToString();
        if (n == 10) return "十";
        if (n < 20) return "十" + CnDigits[n - 10];
        if (n < 100) return CnDigits[n / 10] + "十" + (n % 10 == 0 ? "" : CnDigits[n % 10].ToString());
        // 100+: 简单退化，不追求完美
        return n.ToString();
    }

    // 一个数字一个字："12" → "一二"
    private static string ToChineseDigits(int n)
    {
        var s = n.ToString();
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(CnDigits[c - '0']);
        return sb.ToString();
    }
}
