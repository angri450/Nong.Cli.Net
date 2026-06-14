using System.Text;

namespace Angri450.Nong.Literature.Dsl;

public static class CnkiQueryNormalizer
{
    public static CnkiNormalizedQuery Normalize(string text) => Normalize(CnkiParser.Parse(text));

    public static CnkiNormalizedQuery Normalize(CnkiQuery query)
    {
        var terms = query.Terms
            .Where(t => !string.IsNullOrWhiteSpace(t.Value))
            .ToArray();
        var parsedFields = terms
            .GroupBy(t => t.EffectiveField, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CnkiParsedField(
                group.Key,
                CnkiDslFields.Normalize(group.Key),
                group
                    .Where(t => !t.IsBetween)
                    .Select(t => t.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                group.Min(t => t.Position)))
            .OrderBy(field => field.Position)
            .ToArray();

        var conceptGroups = BuildConceptGroups(query.Root).ToArray();
        var yearRanges = terms
            .Where(t => t.IsBetween && string.Equals(t.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase))
            .Select(t => new CnkiYearRange(
                int.TryParse(t.BetweenStart, out var start) ? start : null,
                int.TryParse(t.BetweenEnd, out var end) ? end : null,
                t.Position))
            .ToArray();

        return new CnkiNormalizedQuery
        {
            Text = NormalizeText(query.Text),
            Terms = terms,
            Fields = terms
                .Select(t => t.EffectiveField.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray(),
            ParsedFields = parsedFields,
            Concepts = terms
                .Where(IsConcept)
                .Select(t => NormalizeText(t.Value))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray(),
            ConceptGroups = conceptGroups,
            YearRanges = yearRanges
        };
    }

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsControl(ch))
                continue;
            builder.Append(char.IsWhiteSpace(ch) ? ' ' : char.ToLowerInvariant(ch));
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    static bool IsConcept(CnkiTermNode term)
    {
        if (term.IsBetween)
            return false;
        return !string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(term.EffectiveField, "CF", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(term.EffectiveField, "DOI", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(term.Value);
    }

    static IEnumerable<CnkiConceptGroup> BuildConceptGroups(CnkiAstNode? root)
    {
        if (root is null)
        {
            yield break;
        }

        foreach (var node in SplitAnd(root))
        {
            var terms = ExtractTerms(node)
                .Where(IsConcept)
                .ToArray();
            if (terms.Length == 0)
            {
                continue;
            }

            yield return new CnkiConceptGroup(
                terms[0].EffectiveField,
                terms.Select(t => t.Value).Distinct(StringComparer.Ordinal).ToArray(),
                terms.Min(t => t.Position));
        }
    }

    static IEnumerable<CnkiAstNode> SplitAnd(CnkiAstNode node)
    {
        if (node is CnkiBinaryNode { Operator: CnkiBooleanOperator.And } binary)
        {
            foreach (var left in SplitAnd(binary.Left))
            {
                yield return left;
            }

            foreach (var right in SplitAnd(binary.Right))
            {
                yield return right;
            }

            yield break;
        }

        yield return node;
    }

    static IEnumerable<CnkiTermNode> ExtractTerms(CnkiAstNode node)
    {
        switch (node)
        {
            case CnkiTermNode term:
                yield return term;
                break;
            case CnkiBinaryNode binary:
                foreach (var term in ExtractTerms(binary.Left))
                {
                    yield return term;
                }

                foreach (var term in ExtractTerms(binary.Right))
                {
                    yield return term;
                }

                break;
            case CnkiNotNode:
                break;
        }
    }
}
