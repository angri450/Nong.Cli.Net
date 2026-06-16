using System.Globalization;
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
        "pua", "pua-supp", "specials", "control-pics", "ocr", "braille",
    };

    /// <summary>
    /// Score text quality from Poppler-extracted runs. No longer depends on PdfPig Word type.
    /// </summary>
    internal static PdfTextQualitySummary AnalyzeRuns(IEnumerable<PdfRun> runs)
    {
        var byFont = new Dictionary<string, FontStats>(StringComparer.OrdinalIgnoreCase);
        var totalChars = 0;
        var suspiciousChars = 0;

        foreach (var run in runs)
        {
            var font = run.Format?.Font ?? "__unknown__";
            if (!byFont.TryGetValue(font, out var stats))
            {
                stats = new FontStats();
                byFont[font] = stats;
            }

            foreach (var ch in run.Text ?? "")
            {
                stats.Total++;
                totalChars++;
                if (LooksSuspicious(ch))
                {
                    stats.Suspicious++;
                    suspiciousChars++;
                }
            }
        }

        var suspectFonts = byFont
            .Where(kvp => kvp.Value.Suspicious > kvp.Value.Total * 0.15)
            .Select(kvp => kvp.Key)
            .OrderBy(f => f)
            .ToList();

        return new PdfTextQualitySummary
        {
            Characters = totalChars,
            SuspiciousCharacters = suspiciousChars,
            SuspiciousRatio = totalChars == 0 ? 0 : (double)suspiciousChars / totalChars,
            SuspectFonts = suspectFonts,
        };
    }

    /// <summary>
    /// Backward-compatible: analyze PdfPig Words. Kept for Nong.Cli.Net fallback compatibility.
    /// </summary>
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
                stats.Total++;
                totalChars++;
                if (LooksSuspicious(ch))
                {
                    stats.Suspicious++;
                    suspiciousChars++;
                }
            }
        }

        var suspectFonts = byFont
            .Where(kvp => kvp.Value.Suspicious > kvp.Value.Total * 0.15)
            .Select(kvp => kvp.Key)
            .OrderBy(f => f)
            .ToList();

        return new PdfTextQualitySummary
        {
            Characters = totalChars,
            SuspiciousCharacters = suspiciousChars,
            SuspiciousRatio = totalChars == 0 ? 0 : (double)suspiciousChars / totalChars,
            SuspectFonts = suspectFonts,
        };
    }

    /// <summary>
    /// Score a single block's text. Used as a simpler alternative to the full run-based analysis.
    /// </summary>
    internal static double ScoreText(string text, string? font, ICollection<string> suspectFonts)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var suspicious = 0;
        foreach (var ch in text)
            if (LooksSuspicious(ch)) suspicious++;

        var ratio = (double)suspicious / text.Length;
        if (font != null && suspectFonts.Contains(font))
            ratio = Math.Min(1.0, ratio + 0.2);

        return ratio;
    }

    static bool LooksSuspicious(char c)
    {
        if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
            return true;

        var cat = char.GetUnicodeCategory(c);
        return cat switch
        {
            UnicodeCategory.PrivateUse => true,
            UnicodeCategory.OtherNotAssigned => true,
            UnicodeCategory.ModifierLetter => true,
            _ => false,
        };
    }

    sealed class FontStats
    {
        public int Total;
        public int Suspicious;
    }
}
