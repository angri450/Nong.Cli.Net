using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Providers;

namespace Angri450.Nong.Literature.Pipeline;

public sealed class QueryPlanner
{
    public const int DefaultMaxRoughQueriesPerProvider = 20;
    static readonly string[] DefaultProviders = { "openalex", "crossref", "unpaywall" };

    public SearchPlan CreatePlan(string queryText, IEnumerable<string>? providerNames = null, int maxQueriesPerProvider = DefaultMaxRoughQueriesPerProvider)
    {
        return Plan(CnkiParser.Parse(queryText), providerNames ?? DefaultProviders, maxQueriesPerProvider);
    }

    public SearchPlan CreatePlan(CnkiQuery query, IEnumerable<string>? providerNames = null, int maxQueriesPerProvider = DefaultMaxRoughQueriesPerProvider)
    {
        return Plan(query, providerNames ?? DefaultProviders, maxQueriesPerProvider);
    }

    public SearchPlan Plan(CnkiQuery query, IEnumerable<string> providerNames, int maxQueriesPerProvider = DefaultMaxRoughQueriesPerProvider)
    {
        var normalized = CnkiQueryNormalizer.Normalize(query);
        var issues = new List<LiteratureIssue>();
        var providers = new List<ProviderPlan>();
        var registry = ProviderRegistry.CreateDefault();
        var conjunctions = BuildPositiveConjunctions(query.Root).ToArray();

        foreach (var providerName in providerNames.Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var implemented = registry.IsImplemented(providerName);
            var roughQueries = implemented
                ? BuildRoughQueries(providerName, conjunctions, maxQueriesPerProvider, issues)
                : Array.Empty<string>();

            providers.Add(new ProviderPlan
            {
                Name = providerName.ToLowerInvariant(),
                IsImplemented = implemented,
                HasRequiredCredential = HasCredential(providerName),
                RoughQueries = roughQueries,
                Limitations = Limitations(providerName)
            });

            if (!implemented)
            {
                issues.Add(new LiteratureIssue
                {
                    Id = "provider_unsupported",
                    Severity = "Error",
                    Provider = providerName,
                    Message = $"Literature provider '{providerName}' is not implemented in Stage19."
                });
            }
        }

        return new SearchPlan
        {
            ParsedFields = normalized.Fields,
            NormalizedConcepts = normalized.Concepts,
            Providers = providers,
            Issues = issues
        };
    }

    static IReadOnlyList<string> BuildRoughQueries(
        string providerName,
        IReadOnlyList<IReadOnlyList<CnkiTermNode>> conjunctions,
        int maxQueries,
        List<LiteratureIssue> issues)
    {
        if (string.Equals(providerName, "unpaywall", StringComparison.OrdinalIgnoreCase))
        {
            var doiQueries = conjunctions
                .SelectMany(group => group)
                .Where(term => string.Equals(term.EffectiveField, "DOI", StringComparison.OrdinalIgnoreCase))
                .Select(term => PaperRecordMerger.NormalizeDoi(term.Value))
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (doiQueries.Length > maxQueries)
            {
                issues.Add(new LiteratureIssue
                {
                    Id = "rough_query_truncated",
                    Severity = "Warning",
                    Provider = providerName,
                    Message = $"Rough query combinations exceeded {maxQueries}; truncated deterministically."
                });
                doiQueries = doiQueries.Take(maxQueries).ToArray();
            }

            return doiQueries;
        }

        var queries = conjunctions
            .Select(group => string.Join(' ', group
                .Where(t => !t.IsBetween && !string.Equals(t.EffectiveField, "CF", StringComparison.OrdinalIgnoreCase))
                .Select(t => t.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))))
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(q => q, StringComparer.Ordinal)
            .ToArray();

        if (queries.Length > maxQueries)
        {
            issues.Add(new LiteratureIssue
            {
                Id = "rough_query_truncated",
                Severity = "Warning",
                Provider = providerName,
                Message = $"Rough query combinations exceeded {maxQueries}; truncated deterministically."
            });
            queries = queries.Take(maxQueries).ToArray();
        }

        return queries.Length == 0 ? new[] { "" } : queries;
    }

    public static IReadOnlyList<IReadOnlyList<CnkiTermNode>> BuildPositiveConjunctions(CnkiAstNode? node)
    {
        if (node is null)
            return new[] { Array.Empty<CnkiTermNode>() };

        var dnf = ToDnf(node)
            .Select(group => (IReadOnlyList<CnkiTermNode>)group
                .Where(term => term.IsBetween || IsPositiveRoughTerm(term))
                .ToArray())
            .ToArray();

        return dnf.Length == 0 ? new[] { Array.Empty<CnkiTermNode>() } : dnf;
    }

    static bool IsPositiveRoughTerm(CnkiTermNode term)
    {
        return !string.IsNullOrWhiteSpace(term.Value)
            && !string.Equals(term.EffectiveField, "YE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(term.EffectiveField, "CF", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<List<CnkiTermNode>> ToDnf(CnkiAstNode node)
    {
        switch (node)
        {
            case CnkiTermNode term:
                return new[] { new List<CnkiTermNode> { term } };
            case CnkiNotNode:
                return new[] { new List<CnkiTermNode>() };
            case CnkiBinaryNode { Operator: CnkiBooleanOperator.Or } binary:
                return ToDnf(binary.Left).Concat(ToDnf(binary.Right)).Select(g => g.ToList()).ToArray();
            case CnkiBinaryNode { Operator: CnkiBooleanOperator.And } binary:
                var left = ToDnf(binary.Left);
                var right = ToDnf(binary.Right);
                var combined = new List<List<CnkiTermNode>>();
                foreach (var l in left)
                foreach (var r in right)
                    combined.Add(l.Concat(r).ToList());
                return combined;
            default:
                return new[] { new List<CnkiTermNode>() };
        }
    }

    static bool HasCredential(string providerName)
    {
        if (string.Equals(providerName, "unpaywall", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONG_LIT_UNPAYWALL_EMAIL"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONG_LIT_MAILTO"));
        }

        if (string.Equals(providerName, "openalex", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONG_LIT_OPENALEX_API_KEY"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONG_LIT_OPENALEX_KEY"));
        }

        return true;
    }

    static IReadOnlyList<string> Limitations(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openalex" => new[] { "No full-text search; CNKI DSL is filtered locally." },
            "crossref" => new[] { "Rough metadata search; DOI lookup/enrichment is primary; CNKI DSL is filtered locally." },
            "unpaywall" => new[] { "DOI-only OA lookup; no general metadata search." },
            _ => new[] { "Provider is not implemented in Stage19." }
        };
    }
}
