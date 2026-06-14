namespace Angri450.Nong.Literature.Dsl;

public static class CnkiDslValidator
{
    public static CnkiValidationResult Validate(string text) => Validate(CnkiParser.Parse(text));

    public static CnkiValidationResult Validate(CnkiQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var issues = query.Issues.ToList();

        foreach (var term in query.Terms)
        {
            if (!CnkiDslFields.SupportedFields.Contains(term.EffectiveField))
            {
                var position = term.FieldPosition ?? term.Position;
                    issues.Add(new CnkiParseIssue(
                    "E006",
                    "Error",
                    $"Unsupported CNKI field '{term.EffectiveField}' at position {position}.",
                    position,
                    Context(query.Text, position)));
            }

            if (term.IsBetween)
            {
                if (!string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN is only supported for YE in Stage19; found '{term.EffectiveField}' at position {term.Position}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                    continue;
                }

                if (!TryParseYear(term.BetweenStart, out var start))
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN start year '{term.BetweenStart}' must be a four digit year.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }

                if (!TryParseYear(term.BetweenEnd, out var end))
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN end year '{term.BetweenEnd}' must be a four digit year.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }

                if (TryParseYear(term.BetweenStart, out start)
                    && TryParseYear(term.BetweenEnd, out end)
                    && start > end)
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN start year {start} must be less than or equal to end year {end}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }
            }
        }

        if (query.Root is null && issues.Count == 0)
        {
            issues.Add(new CnkiParseIssue("E006", "Error", "Query is empty.", 0, string.Empty));
        }

        var distinctIssues = issues
            .GroupBy(issue => new { issue.Id, issue.Message, issue.Position })
            .Select(group => group.First())
            .ToArray();

        return new CnkiValidationResult
        {
            Query = query,
            Issues = distinctIssues
        };
    }

    static bool TryParseYear(string? value, out int year)
    {
        year = 0;
        return value is { Length: 4 }
            && value.All(char.IsDigit)
            && int.TryParse(value, out year);
    }

    static string Context(string text, int position)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var start = Math.Max(0, position - 16);
        var end = Math.Min(text.Length, position + 17);
        return text[start..end];
    }
}
