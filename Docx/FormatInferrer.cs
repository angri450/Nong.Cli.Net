using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// Parses Chinese format descriptions into OpenXML style parameters.
///
/// Input examples:
///   "黑体 四号 居中 固定行距28磅" -> {fontFamily:"黑体", fontSize:"14pt", alignment:"center", lineSpacing:"28pt", lineRule:"exact"}
///   "宋体 小四 首行缩进2字符 1.5倍行距" -> {fontFamily:"宋体", fontSize:"12pt", firstLineIndent:"2char", lineSpacing:"1.5", lineRule:"auto"}
///
/// Conservative: if input does not match known patterns, return what can be identified
/// plus warnings. Empty/unparseable input returns a result with warnings (CLI layer converts
/// to E006).
/// </summary>
public static class FormatInferrer
{
    // Chinese font size mapping: name -> points
    private static readonly Dictionary<string, string> FontSizeMap = new()
    {
        ["初号"] = "42pt",
        ["小初"] = "36pt",
        ["一号"] = "26pt",
        ["小一"] = "24pt",
        ["二号"] = "22pt",
        ["小二"] = "18pt",
        ["三号"] = "16pt",
        ["小三"] = "15pt",
        ["四号"] = "14pt",
        ["小四"] = "12pt",
        ["五号"] = "10.5pt",
        ["小五"] = "9pt",
    };

    // Known Chinese font families
    private static readonly HashSet<string> KnownFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "宋体", "黑体", "仿宋", "楷体", "隶书", "幼圆", "微软雅黑", "等线",
        "Times New Roman", "Arial", "Calibri", "Cambria",
    };

    // Alignment keywords mapping
    private static readonly Dictionary<string, string> AlignmentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["居中"] = "center",
        ["居中对齐"] = "center",
        ["左对齐"] = "left",
        ["右对齐"] = "right",
        ["两端对齐"] = "both",
        ["分散对齐"] = "distributed",
    };

    public static InferFormatResult Infer(string input)
    {
        var warnings = new List<string>();
        string? fontFamily = null;
        string? fontSize = null;
        string? alignment = null;
        string? lineSpacing = null;
        string? lineRule = null;
        string? firstLineIndent = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            warnings.Add("Input is empty or whitespace-only.");
            return new InferFormatResult(fontFamily, fontSize, alignment, lineSpacing, lineRule, firstLineIndent, warnings);
        }

        var trimmed = input.Trim();
        // Track which parts we've consumed (index ranges in the original string)
        var consumedRanges = new List<(int Start, int End)>();

        // --- Font size (号-based) ---
        foreach (var kvp in FontSizeMap.OrderByDescending(kvp => kvp.Key.Length))
        {
            int idx = trimmed.IndexOf(kvp.Key, StringComparison.Ordinal);
            if (idx >= 0)
            {
                fontSize = kvp.Value;
                consumedRanges.Add((idx, idx + kvp.Key.Length));
                break;
            }
        }

        // --- Font family ---
        foreach (var font in KnownFonts.OrderByDescending(f => f.Length))
        {
            int idx = trimmed.IndexOf(font, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                fontFamily = font;
                consumedRanges.Add((idx, idx + font.Length));
                break;
            }
        }

        // --- Alignment ---
        foreach (var akvp in AlignmentMap.OrderByDescending(kvp => kvp.Key.Length))
        {
            int idx = trimmed.IndexOf(akvp.Key, StringComparison.Ordinal);
            while (idx >= 0)
            {
                // Don't re-consume ranges that overlap
                if (!consumedRanges.Any(r => idx >= r.Start && idx < r.End))
                {
                    alignment = akvp.Value;
                    consumedRanges.Add((idx, idx + akvp.Key.Length));
                    break;
                }
                idx = trimmed.IndexOf(akvp.Key, idx + 1, StringComparison.Ordinal);
            }
            if (alignment != null) break;
        }

        // --- Line spacing: 固定行距XX磅 (exact) ---
        var fixedSpacingMatch = Regex.Match(trimmed, @"固定行距\s*([\d.]+)\s*磅?");
        if (fixedSpacingMatch.Success)
        {
            lineSpacing = fixedSpacingMatch.Groups[1].Value + "pt";
            lineRule = "exact";
            consumedRanges.Add((fixedSpacingMatch.Index, fixedSpacingMatch.Index + fixedSpacingMatch.Length));
        }

        // --- Line spacing: XX倍行距 (auto) ---
        if (lineSpacing == null)
        {
            var multipleMatch = Regex.Match(trimmed, @"([\d.]+)\s*倍行距");
            if (multipleMatch.Success)
            {
                lineSpacing = multipleMatch.Groups[1].Value;
                lineRule = "auto";
                consumedRanges.Add((multipleMatch.Index, multipleMatch.Index + multipleMatch.Length));
            }
        }

        // --- Line spacing: 单倍行距 / 双倍行距 ---
        if (lineSpacing == null)
        {
            if (trimmed.Contains("单倍行距"))
            {
                lineSpacing = "1.0";
                lineRule = "auto";
                int idx = trimmed.IndexOf("单倍行距", StringComparison.Ordinal);
                consumedRanges.Add((idx, idx + 4));
            }
            else if (trimmed.Contains("双倍行距"))
            {
                lineSpacing = "2.0";
                lineRule = "auto";
                int idx = trimmed.IndexOf("双倍行距", StringComparison.Ordinal);
                consumedRanges.Add((idx, idx + 4));
            }
        }

        // --- First-line indent: 首行缩进X字符 ---
        var indentMatch = Regex.Match(trimmed, @"首行缩进\s*([\d.]+)\s*字符");
        if (indentMatch.Success)
        {
            firstLineIndent = indentMatch.Groups[1].Value + "char";
            consumedRanges.Add((indentMatch.Index, indentMatch.Index + indentMatch.Length));
        }

        // --- Detect unparseable text ---
        // Merge consumed ranges and check for gaps
        var merged = MergeRanges(consumedRanges);
        var remaining = ExtractRemainingPhrases(trimmed, merged);

        if (remaining.Count > 0)
        {
            foreach (var phrase in remaining)
            {
                if (!string.IsNullOrWhiteSpace(phrase) && phrase.Length > 1)
                    warnings.Add($"Unrecognized: \"{phrase}\"");
            }
        }

        // If nothing was identified, add a general warning
        bool nothingFound = fontFamily == null && fontSize == null && alignment == null
            && lineSpacing == null && firstLineIndent == null;
        if (nothingFound && warnings.Count == 0)
            warnings.Add("No known format patterns found in input.");

        return new InferFormatResult(fontFamily, fontSize, alignment, lineSpacing, lineRule, firstLineIndent, warnings);
    }

    private static List<(int Start, int End)> MergeRanges(List<(int Start, int End)> ranges)
    {
        if (ranges.Count == 0) return ranges;

        var sorted = ranges.OrderBy(r => r.Start).ToList();
        var merged = new List<(int Start, int End)> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var last = merged[^1];
            if (sorted[i].Start <= last.End)
            {
                merged[^1] = (last.Start, Math.Max(last.End, sorted[i].End));
            }
            else
            {
                merged.Add(sorted[i]);
            }
        }
        return merged;
    }

    private static List<string> ExtractRemainingPhrases(string input, List<(int Start, int End)> consumed)
    {
        var phrases = new List<string>();
        int pos = 0;

        // Also split by whitespace for individual tokens outside consumed ranges
        foreach (var range in consumed.OrderBy(r => r.Start))
        {
            if (pos < range.Start)
            {
                var gap = input[pos..range.Start];
                foreach (var token in gap.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    phrases.Add(token.Trim());
            }
            pos = Math.Max(pos, range.End);
        }

        if (pos < input.Length)
        {
            var gap = input[pos..];
            foreach (var token in gap.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                phrases.Add(token.Trim());
        }

        return phrases;
    }
}
