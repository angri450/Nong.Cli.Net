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
            CnkiNotNode not => !Evaluate(not.Operand, record, recall, issues),
            CnkiBinaryNode { Operator: CnkiBooleanOperator.And } binary => Evaluate(binary.Left, record, recall, issues) && Evaluate(binary.Right, record, recall, issues),
            CnkiBinaryNode { Operator: CnkiBooleanOperator.Or } binary => Evaluate(binary.Left, record, recall, issues) || Evaluate(binary.Right, record, recall, issues),
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
        var matched = !string.IsNullOrWhiteSpace(needle) &&
            CnkiQueryNormalizer.NormalizeText(haystack).Contains(needle, StringComparison.OrdinalIgnoreCase);
        if (matched)
            record.MatchReasons.Add($"{field}:{term.Value}");
        return matched;
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
}
