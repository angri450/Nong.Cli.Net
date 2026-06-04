using System.Text;
using System.Text.RegularExpressions;

namespace Nong.Inspect;

/// <summary>
/// 容错地把用户随手输入的"标题编号样例"扩成一个 regex。
/// 设计原则：
///   1. 括号宽度：中英文自动互通，开闭不必一致（"（一)" 也认）
///   2. 后缀标点：用户可省略；推测时允许常见后缀（、/./．）
///   3. 无法识别的输入返回 null，调用方回退到自动检测
/// </summary>
public static class MarkerPatternInferrer
{
    private const string CnDigits = "一二三四五六七八九十百千";
    private const string CnNumClass = @"[一二三四五六七八九十百千两]+";
    private const string DecimalClass = @"\d+";
    private const string LetterClass = @"[A-Za-z]";

    private const string OpenBracketClass = @"[（(]";
    private const string CloseBracketClass = @"[）)]";

    private const string CommonSuffixClass = @"[、．.]";     // 数字 / 汉字数字常见后缀
    private const string NumSuffixClass = @"[．.、]";         // 同上

    public static Regex? Infer(string? userSample)
    {
        if (string.IsNullOrWhiteSpace(userSample)) return null;
        string s = userSample.Trim();
        if (s.Length == 0) return null;

        // ——— 括号包裹的（一）/（1）/(1) ———
        var mBracket = Regex.Match(s, @"^[（(](.+?)[）)]$");
        if (mBracket.Success)
        {
            string inside = mBracket.Groups[1].Value.Trim();
            string? inner = InferInnerNumberClass(inside);
            if (inner == null) return null;
            return new Regex("^" + OpenBracketClass + inner + CloseBracketClass);
        }

        // ——— "第X章/节/篇/条/部" ———
        var mChapter = Regex.Match(s, @"^第(.+?)(章|节|篇|部|条|卷|回)$");
        if (mChapter.Success)
        {
            string numPart = mChapter.Groups[1].Value.Trim();
            string type = mChapter.Groups[2].Value;
            string? numClass = InferInnerNumberClass(numPart);
            if (numClass == null) numClass = CnNumClass;
            return new Regex("^第" + numClass + type);
        }

        // ——— 嵌套十进制 1.1 / 1.1.1 ———
        // 关键：用 negative lookahead 锁死层级数，防止 H2 "1.1" 的 regex 把 H3 "1.1.1" 也吃进去
        var mNested = Regex.Match(s, @"^\d+(?:[．.]\d+)+$");
        if (mNested.Success)
        {
            int dots = s.Count(c => c == '.' || c == '．');
            var sb = new StringBuilder("^" + DecimalClass);
            for (int i = 0; i < dots; i++) sb.Append(@"[．.]" + DecimalClass);
            sb.Append(@"(?![．.]\d)");   // 绝不再跟 ".数字"
            return new Regex(sb.ToString());
        }

        // ——— 末尾带句末标点的情况：先拆 suffix，再看主体 ———
        string body = s;
        string? explicitSuffix = null;
        if (body.Length >= 2)
        {
            char last = body[^1];
            if (last == '、' || last == '.' || last == '．' || last == '。' || last == ',' || last == '，')
            {
                explicitSuffix = last.ToString();
                body = body.Substring(0, body.Length - 1);
            }
        }

        string? mainClass = InferInnerNumberClass(body);
        if (mainClass == null) return null;

        string suffix;
        if (explicitSuffix != null)
        {
            suffix = explicitSuffix switch
            {
                "、" => "、",
                "." or "．" => @"[．.]",
                "。" => "。",
                "," or "，" => @"[,，]",
                _ => explicitSuffix
            };
        }
        else
        {
            if (mainClass == DecimalClass) suffix = NumSuffixClass;
            else if (mainClass == CnNumClass) suffix = CommonSuffixClass;
            else if (mainClass == LetterClass) suffix = NumSuffixClass;
            else suffix = NumSuffixClass;
        }

        // 对十进制前缀（"1." 之类）加 negative lookahead：后缀被消费后下一字符不能是数字
        // 否则 H1 "1." 会错吃 "1.1"（消费 "1."，然后遇见 "1"）
        string tail = mainClass == DecimalClass ? @"(?!\d)" : "";

        return new Regex("^" + mainClass + suffix + tail);
    }

    private static string? InferInnerNumberClass(string text)
    {
        string t = text.Trim();
        if (t.Length == 0) return null;

        if (Regex.IsMatch(t, @"^\d+$")) return DecimalClass;

        bool allCn = t.All(c => CnDigits.Contains(c));
        if (allCn) return CnNumClass;

        if (t.Length == 1 && char.IsLetter(t[0])) return LetterClass;

        return null;
    }
}
