using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Pipeline;

public enum LocalBooleanFilterMode
{
    Strict,
    Recall
}

public sealed class LocalBooleanFilterResult
{
    public IReadOnlyList<PaperRecord> Records { get; init; } = Array.Empty<PaperRecord>();
    public IReadOnlyList<LiteratureIssue> Issues { get; init; } = Array.Empty<LiteratureIssue>();
    public IReadOnlyDictionary<PaperRecord, IReadOnlyList<string>> MatchReasons { get; init; } =
        new Dictionary<PaperRecord, IReadOnlyList<string>>();
}

public sealed class LocalBooleanFilter
{
    public IReadOnlyList<PaperRecord> Filter(string queryText, IEnumerable<PaperRecord> records, LocalBooleanFilterMode mode = LocalBooleanFilterMode.Strict)
    {
        return FilterWithDiagnostics(queryText, records, mode).Records;
    }

    public IReadOnlyList<PaperRecord> Filter(CnkiQuery query, IEnumerable<PaperRecord> records, LocalBooleanFilterMode mode = LocalBooleanFilterMode.Strict)
    {
        return FilterWithDiagnostics(query, records, mode).Records;
    }

    public LocalBooleanFilterResult FilterWithDiagnostics(string queryText, IEnumerable<PaperRecord> records, LocalBooleanFilterMode mode = LocalBooleanFilterMode.Strict)
    {
        return FilterWithDiagnostics(CnkiParser.Parse(queryText), records, mode);
    }

    public LocalBooleanFilterResult FilterWithDiagnostics(CnkiQuery query, IEnumerable<PaperRecord> records, LocalBooleanFilterMode mode = LocalBooleanFilterMode.Strict)
    {
        var filtered = Filter(records, query, mode == LocalBooleanFilterMode.Recall ? "recall" : "strict", out var issues);
        return new LocalBooleanFilterResult
        {
            Records = filtered,
            Issues = issues,
            MatchReasons = filtered.ToDictionary(record => record, record => (IReadOnlyList<string>)record.MatchReasons.ToArray())
        };
    }

    public IReadOnlyList<PaperRecord> Filter(IEnumerable<PaperRecord> records, CnkiQuery query, string mode, out IReadOnlyList<LiteratureIssue> issues)
    {
        var recall = string.Equals(mode, "recall", StringComparison.OrdinalIgnoreCase);
        var output = new List<PaperRecord>();
        var localIssues = new List<LiteratureIssue>();

        foreach (var record in records)
        {
            var matched = Evaluate(query.Root, record, recall, localIssues);
            if (matched)
                output.Add(record);
        }

        issues = localIssues;
        return output;
    }

    static bool Evaluate(CnkiAstNode? node, PaperRecord record, bool recall, List<LiteratureIssue> issues)
    {
        return node switch
        {
            null => true,
            CnkiTermNode term => MatchTerm(term, record, recall, issues),
            CnkiProximityNode prox => MatchProximity(prox, record, recall, issues),
            CnkiNotNode not => !Evaluate(not.Operand, record, recall, issues),
            CnkiBinaryNode { Operator: CnkiBooleanOperator.And } binary =>
                Evaluate(binary.Left, record, recall, issues) && Evaluate(binary.Right, record, recall, issues),
            CnkiBinaryNode { Operator: CnkiBooleanOperator.Or } binary =>
                Evaluate(binary.Left, record, recall, issues) || Evaluate(binary.Right, record, recall, issues),
            _ => true
        };
    }

    static bool MatchTerm(CnkiTermNode term, PaperRecord record, bool recall, List<LiteratureIssue> issues)
    {
        if (term.IsBetween)
            return MatchBetween(term, record);

        var field = term.EffectiveField.ToUpperInvariant();
        if (field == "FT" && string.IsNullOrWhiteSpace(record.FullText))
        {
            if (recall)
            {
                record.MatchReasons.Add($"FT unavailable; kept in recall mode for term '{term.Value}'.");
                issues.Add(new LiteratureIssue
                {
                    Id = "full_text_unavailable",
                    Severity = "Warning",
                    Message = "Full text is unavailable for remote metadata candidate; kept by recall mode."
                });
                return true;
            }
            return false;
        }

        if (field == "CF")
        {
            return int.TryParse(term.Value, out var required) && record.CitationCount.GetValueOrDefault() >= required;
        }

        if (field == "YE")
        {
            return int.TryParse(term.Value, out var required) && record.Year.GetValueOrDefault() == required;
        }

        if (field == "DOI")
        {
            var expected = PaperRecordMerger.NormalizeDoi(term.Value);
            var actual = PaperRecordMerger.NormalizeDoi(record.Doi);
            var doiMatched = !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            if (doiMatched)
                record.MatchReasons.Add("DOI");
            return doiMatched;
        }

        var haystack = FieldText(record, field);
        var needle = CnkiQueryNormalizer.NormalizeText(term.Value);

        // Word frequency check: term must appear at least MinFrequency times
        if (term.MinFrequency.HasValue && term.MinFrequency.Value > 1)
        {
            var count = CountOccurrences(haystack, needle);
            if (count < term.MinFrequency.Value)
            {
                record.MatchReasons.Add($"!{field}:{term.Value}(freq={count}<{term.MinFrequency})");
                return false;
            }
            record.MatchReasons.Add($"{field}:{term.Value}(freq={count}>={term.MinFrequency})");
            return true;
        }

        bool matched;

        if (term.IsFuzzy)
        {
            // Fuzzy match: all characters of needle must appear in haystack (any order, not necessarily contiguous)
            matched = FuzzyContains(haystack, needle);
        }
        else
        {
            matched = !string.IsNullOrWhiteSpace(needle) &&
                CnkiQueryNormalizer.NormalizeText(haystack).Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        // In recall mode, keep papers where the provider returned them but the
        // local filter can't match (e.g. Chinese DSL vs English abstracts).
        if (!matched && recall && HasCjk(needle) && !HasCjk(haystack))
        {
            record.MatchReasons.Add($"{field}:{term.Value}(recall-cjk-mismatch)");
            return true;
        }

        if (matched)
            record.MatchReasons.Add($"{field}:{term.Value}" + (term.IsFuzzy ? "(fuzzy)" : ""));
        return matched;
    }

    static bool MatchProximity(CnkiProximityNode prox, PaperRecord record, bool recall, List<LiteratureIssue> issues)
    {
        var field = prox.EffectiveField.ToUpperInvariant();
        var text = FieldText(record, field);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (recall) { record.MatchReasons.Add($"{field}:prox({prox.Kind},{prox.Distance})(recall-empty)"); return true; }
            return false;
        }

        var left = CnkiQueryNormalizer.NormalizeText(prox.Left.Value);
        var right = CnkiQueryNormalizer.NormalizeText(prox.Right.Value);
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var normalized = CnkiQueryNormalizer.NormalizeText(text);

        var ok = prox.Kind switch
        {
            CnkiProximityKind.Near => MatchNear(normalized, left, right, prox.Distance, ordered: false),
            CnkiProximityKind.Prev => MatchNear(normalized, left, right, prox.Distance, ordered: true),
            CnkiProximityKind.Aft  => MatchAft(normalized, left, right, prox.Distance),
            CnkiProximityKind.Sen  => MatchSen(normalized, left, right, prox.Distance),
            CnkiProximityKind.Prg  => MatchPrg(normalized, left, right, prox.Distance),
            CnkiProximityKind.SameSentence => MatchSameSentence(normalized, left, right, ordered: false),
            CnkiProximityKind.SameSentenceOrdered => MatchSameSentence(normalized, left, right, ordered: true),
            _ => MatchNear(normalized, left, right, prox.Distance, ordered: false)
        };

        if (ok)
            record.MatchReasons.Add($"{field}:{prox.Left.Value}/{prox.Kind}/{prox.Distance}/{prox.Right.Value}");
        else if (recall && HasCjk(left) && !HasCjk(normalized))
        {
            record.MatchReasons.Add($"{field}:prox({prox.Kind},{prox.Distance})(recall-cjk-mismatch)");
            return true;
        }

        return ok;
    }

    // ── proximity matchers ───────────────────────────────────

    /// <summary>Same sentence, within N words. If ordered, left must appear before right.</summary>
    static bool MatchNear(string text, string left, string right, int distance, bool ordered)
    {
        foreach (var sentence in SplitSentences(text))
        {
            var words = SplitWords(sentence);
            var leftPositions = FindAll(words, left);
            var rightPositions = FindAll(words, right);
            foreach (var lp in leftPositions)
            foreach (var rp in rightPositions)
            {
                if (ordered && lp > rp) continue;
                var gap = Math.Abs(rp - lp) - 1;
                if (gap >= 0 && gap < distance) return true;
            }
        }
        return false;
    }

    /// <summary>Same sentence, ordered, str1 after str2, &gt;N words apart.</summary>
    static bool MatchAft(string text, string left, string right, int distance)
    {
        foreach (var sentence in SplitSentences(text))
        {
            var words = SplitWords(sentence);
            var leftPositions = FindAll(words, left);
            var rightPositions = FindAll(words, right);
            foreach (var lp in leftPositions)
            foreach (var rp in rightPositions)
            {
                if (rp > lp) continue; // right must come before left (left is AFTER right)
                var gap = Math.Abs(lp - rp) - 1;
                if (gap > distance) return true;
            }
        }
        return false;
    }

    /// <summary>Same paragraph, ordered, within N sentences.</summary>
    static bool MatchSen(string text, string left, string right, int distance)
    {
        foreach (var paragraph in SplitParagraphs(text))
        {
            var sentences = SplitSentences(paragraph);
            for (var i = 0; i < sentences.Length; i++)
            {
                if (!sentences[i].Contains(left, StringComparison.OrdinalIgnoreCase))
                    continue;
                var limit = Math.Min(i + distance + 1, sentences.Length);
                for (var j = i; j < limit; j++)
                {
                    if (sentences[j].Contains(right, StringComparison.OrdinalIgnoreCase)
                        && (j - i) <= distance)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>Full text, within N paragraphs.</summary>
    static bool MatchPrg(string text, string left, string right, int distance)
    {
        var paragraphs = SplitParagraphs(text);
        for (var i = 0; i < paragraphs.Length; i++)
        {
            if (!paragraphs[i].Contains(left, StringComparison.OrdinalIgnoreCase))
                continue;
            var start = Math.Max(0, i - distance);
            var end = Math.Min(i + distance + 1, paragraphs.Length);
            for (var j = start; j < end; j++)
            {
                if (j != i && paragraphs[j].Contains(right, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Same sentence. If ordered, left must appear before right.</summary>
    static bool MatchSameSentence(string text, string left, string right, bool ordered)
    {
        foreach (var sentence in SplitSentences(text))
        {
            var li = sentence.IndexOf(left, StringComparison.OrdinalIgnoreCase);
            var ri = sentence.IndexOf(right, StringComparison.OrdinalIgnoreCase);
            if (li >= 0 && ri >= 0)
            {
                if (!ordered || li <= ri) return true;
            }
        }
        return false;
    }

    static bool MatchBetween(CnkiTermNode term, PaperRecord record)
    {
        if (string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase))
        {
            if (!record.Year.HasValue || !int.TryParse(term.BetweenStart, out var start) || !int.TryParse(term.BetweenEnd, out var end))
                return false;
            return record.Year.Value >= start && record.Year.Value <= end;
        }

        if (string.Equals(term.EffectiveField, "CF", StringComparison.OrdinalIgnoreCase))
        {
            if (!record.CitationCount.HasValue || !int.TryParse(term.BetweenStart, out var start) || !int.TryParse(term.BetweenEnd, out var end))
                return false;
            return record.CitationCount.Value >= start && record.CitationCount.Value <= end;
        }

        return false;
    }

    // ── text helpers ─────────────────────────────────────────

    static string FieldText(PaperRecord record, string field)
    {
        return field switch
        {
            "SU" => Join(record.Title, record.Abstract, record.Keywords, record.Concepts, record.Topics),
            "TI" => record.Title ?? "",
            "KY" => string.Join(' ', record.Keywords),
            "AB" => record.Abstract ?? "",
            "AU" => string.Join(' ', record.Authors),
            "FI" or "F" => record.FirstAuthor ?? record.Authors.FirstOrDefault() ?? "",
            "AF" => string.Join(' ', record.Affiliations),
            "JN" => Join(record.Venue, record.Journal),
            "RF" => string.Join(' ', record.References),
            "FU" => string.Join(' ', record.Funders),
            "CLC" => record.Clc ?? "",
            "SN" => record.Issn ?? "",
            "CN" => record.Cn ?? "",
            "IB" => record.Isbn ?? "",
            "DOI" => record.Doi ?? "",
            "FT" => record.FullText ?? "",
            _ => Join(record.Title, record.Abstract, record.Keywords)
        };
    }

    static string Join(params object?[] values)
    {
        var parts = new List<string>();
        foreach (var value in values)
        {
            switch (value)
            {
                case null:
                    break;
                case string text when !string.IsNullOrWhiteSpace(text):
                    parts.Add(text);
                    break;
                case IEnumerable<string> strings:
                    parts.AddRange(strings.Where(s => !string.IsNullOrWhiteSpace(s)));
                    break;
            }
        }
        return string.Join(' ', parts);
    }

    /// <summary>Split text into sentences. Handles ., !, ?, ;, and CJK punctuation.</summary>
    static string[] SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Split(new[] { '.', '!', '?', ';', '\n', '\r', '．', '！', '？', '；', '。', '，' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Split text into paragraphs by double newlines.</summary>
    internal static string[] SplitParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Split a sentence into words for proximity counting.</summary>
    internal static string[] SplitWords(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return Array.Empty<string>();
        return sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Find all word indices where the given term appears.</summary>
    internal static List<int> FindAll(string[] words, string term)
    {
        var positions = new List<int>();
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                positions.Add(i);
        }
        return positions;
    }

    /// <summary>Count non-overlapping occurrences of needle in haystack.</summary>
    static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle)) return 0;
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    /// <summary>
    /// Fuzzy match: every character of needle must appear in haystack (order-independent, not necessarily contiguous).
    /// CNKI % operator: "包含str或str切分的词".
    /// </summary>
    static bool FuzzyContains(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        if (string.IsNullOrWhiteSpace(haystack)) return false;

        // First try exact substring — fastest path
        if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;

        // Then try word-segmented: each word of needle appears independently
        var needleWords = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (needleWords.Length > 1)
        {
            if (needleWords.All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // For CJK: check if all characters of needle appear somewhere in haystack
        // Strip whitespace before character-level check
        var compact = needle.Replace(" ", "");
        if (compact.Length > 0 && compact.All(c => haystack.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>True if the string contains any CJK character (U+4E00–U+9FFF).</summary>
    static bool HasCjk(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        foreach (var c in s)
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
        return false;
    }
}
