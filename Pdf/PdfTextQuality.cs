using UglyToad.PdfPig.Content;

namespace PdfCore;

internal sealed record PdfTextQualitySummary
{
    public int Characters { get; set; }
    public int SuspiciousCharacters { get; set; }
    public double SuspiciousRatio { get; set; }
    public List<string> SuspectFonts { get; set; } = new();
}

internal static class PdfTextQuality
{
    static readonly HashSet<string> SuspiciousBuckets = new(StringComparer.Ordinal)
    {
        "pua",
        "pua-supp",
        "specials",
        "control-pics",
        "ocr",
        "braille",
    };

    internal static PdfTextQualitySummary AnalyzeWords(IEnumerable<Word> words)
    {
        var byFont = new Dictionary<string, FontStats>(StringComparer.OrdinalIgnoreCase);
        var totalChars = 0;
        var suspiciousChars = 0;

        foreach (var word in words)
        {
            var font = string.IsNullOrWhiteSpace(word.FontName) ? "__unknown__" : word.FontName;
            if (!byFont.TryGetValue(font, out var stats))
            {
                stats = new FontStats();
                byFont[font] = stats;
            }

            foreach (var ch in word.Text ?? "")
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                    continue;

                var bucket = ScriptBucket(ch);
                stats.Characters++;
                stats.Buckets.Add(bucket);
                totalChars++;

                if (SuspiciousBuckets.Contains(bucket))
                {
                    stats.SuspiciousCharacters++;
                    suspiciousChars++;
                }
            }
        }

        var suspectFonts = new List<string>();
        foreach (var (font, stats) in byFont)
        {
            if (stats.Characters < 3)
                continue;

            var ratio = stats.SuspiciousCharacters / (double)stats.Characters;
            if (ratio > 0.30)
            {
                suspectFonts.Add(font);
                continue;
            }

            if (LooksLikeSubsetFont(font) && ratio > 0.05)
            {
                suspectFonts.Add(font);
            }
        }

        return new PdfTextQualitySummary
        {
            Characters = totalChars,
            SuspiciousCharacters = suspiciousChars,
            SuspiciousRatio = totalChars == 0 ? 0 : suspiciousChars / (double)totalChars,
            SuspectFonts = suspectFonts.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToList(),
        };
    }

    internal static double ScoreText(string? text, string? fontName, IReadOnlyCollection<string> suspectFonts)
    {
        if (!string.IsNullOrWhiteSpace(fontName) &&
            suspectFonts.Contains(fontName, StringComparer.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var scored = 0;
        var suspicious = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                continue;

            scored++;
            if (SuspiciousBuckets.Contains(ScriptBucket(ch)))
                suspicious++;
        }

        return scored == 0 ? 0 : Math.Min(1, suspicious / (double)scored * 1.5);
    }

    static bool LooksLikeSubsetFont(string font)
    {
        if (font.Length < 8 || font[6] != '+')
            return false;

        for (var i = 0; i < 6; i++)
        {
            if (font[i] is < 'A' or > 'Z')
                return false;
        }

        return true;
    }

    static string ScriptBucket(char ch)
    {
        var cp = ch;
        if (cp <= 0x007F) return "latin-basic";
        if (cp <= 0x024F) return "latin-ext";
        if (cp is >= '\uE000' and <= '\uF8FF') return "pua";
        if (cp is >= '\uFFF0' and <= '\uFFFF') return "specials";
        if (cp is >= '\u2400' and <= '\u243F') return "control-pics";
        if (cp is >= '\u2440' and <= '\u245F') return "ocr";
        if (cp is >= '\u2800' and <= '\u28FF') return "braille";
        return "other";
    }

    sealed class FontStats
    {
        public int Characters { get; set; }
        public int SuspiciousCharacters { get; set; }
        public HashSet<string> Buckets { get; } = new(StringComparer.Ordinal);
    }
}
