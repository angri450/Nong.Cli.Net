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

            if (term is { IsBetween: true })
            {
                if (!string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(term.EffectiveField, "CF", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN is only supported for YE and CF in Stage19; found '{term.EffectiveField}' at position {term.Position}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                    continue;
                }

                var isYear = string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase);
                var parsedStart = isYear ? TryParseYear(term.BetweenStart, out var ys) : int.TryParse(term.BetweenStart, out ys);
                var parsedEnd = isYear ? TryParseYear(term.BetweenEnd, out var ye) : int.TryParse(term.BetweenEnd, out ye);

                if (!parsedStart)
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN start value '{term.BetweenStart}' must be a valid {(isYear ? "four digit year" : "integer")}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }

                if (!parsedEnd)
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN end value '{term.BetweenEnd}' must be a valid {(isYear ? "four digit year" : "integer")}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }

                if (parsedStart && parsedEnd && ys > ye)
                {
                    issues.Add(new CnkiParseIssue(
                        "E006",
                        "Error",
                        $"BETWEEN start value {ys} must be less than or equal to end value {ye}.",
                        term.Position,
                        Context(query.Text, term.Position)));
                }
            }

            // Validate word frequency
            if (term.MinFrequency.HasValue && term.MinFrequency.Value < 1)
            {
                issues.Add(new CnkiParseIssue(
                    "E006",
                    "Warning",
                    $"Word frequency must be >= 1; got {term.MinFrequency.Value}.",
                    term.Position,
                    Context(query.Text, term.Position)));
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
