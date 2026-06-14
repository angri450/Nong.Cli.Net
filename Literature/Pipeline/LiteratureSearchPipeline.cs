using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Providers;

namespace Angri450.Nong.Literature.Pipeline;

public sealed class LiteratureSearchPipeline
{
    readonly ProviderRegistry _registry;
    readonly QueryPlanner _planner;
    readonly LocalBooleanFilter _filter;
    readonly PaperRecordMerger _merger;
    readonly LiteratureRanker _ranker;

    public LiteratureSearchPipeline()
        : this(ProviderRegistry.CreateDefault())
    {
    }

    public LiteratureSearchPipeline(ProviderRegistry registry)
    {
        _registry = registry;
        _planner = new QueryPlanner();
        _filter = new LocalBooleanFilter();
        _merger = new PaperRecordMerger();
        _ranker = new LiteratureRanker();
    }

    public async Task<LiteratureSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken cancellationToken)
    {
        var query = request.ParsedQuery ?? CnkiParser.Parse(request.Query);
        var validation = CnkiDslValidator.Validate(query);
        if (!validation.IsValid)
        {
            return new LiteratureSearchResult
            {
                Issues = validation.Issues.Select(i => new LiteratureIssue
                {
                    Id = i.Id,
                    Severity = i.Severity,
                    Message = i.Message,
                    Position = i.Position
                }).ToArray()
            };
        }

        var sources = request.Sources.Count == 0 ? new[] { "openalex", "crossref" } : request.Sources;
        var plan = _planner.Plan(query, sources);
        var issues = new List<LiteratureIssue>(plan.Issues);
        if (plan.Issues.Any(i => string.Equals(i.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
        {
            return new LiteratureSearchResult { Issues = issues, Plan = plan };
        }

        var records = new List<PaperRecord>();
        foreach (var providerPlan in plan.Providers)
        {
            var provider = _registry.Create(providerPlan.Name);
            var providerRequest = new LiteratureSearchRequest
            {
                Query = request.Query,
                ParsedQuery = query,
                Sources = request.Sources,
                Limit = request.Limit,
                Profile = request.Profile,
                FilterMode = request.FilterMode,
                RoughQueries = providerPlan.RoughQueries,
                ProviderQuery = providerPlan.RoughQueries.FirstOrDefault(),
                Doi = query.Terms.FirstOrDefault(t => string.Equals(t.EffectiveField, "DOI", StringComparison.OrdinalIgnoreCase))?.Value
            };

            if (string.Equals(providerPlan.Name, "unpaywall", StringComparison.OrdinalIgnoreCase))
            {
                var result = await provider.SearchAsync(providerRequest, cancellationToken).ConfigureAwait(false);
                records.AddRange(result.Records);
                issues.AddRange(result.Issues);
                continue;
            }

            foreach (var rough in providerPlan.RoughQueries.DefaultIfEmpty(""))
            {
                var roughRequest = new LiteratureSearchRequest
                {
                    Query = request.Query,
                    ParsedQuery = query,
                    Sources = request.Sources,
                    Limit = request.Limit,
                    Profile = request.Profile,
                    FilterMode = request.FilterMode,
                    RoughQueries = new[] { rough },
                    ProviderQuery = rough
                };
                var result = await provider.SearchAsync(roughRequest, cancellationToken).ConfigureAwait(false);
                records.AddRange(result.Records);
                issues.AddRange(result.Issues);
            }
        }

        var filtered = _filter.Filter(records, query, request.FilterMode, out var filterIssues);
        issues.AddRange(filterIssues);
        var merged = _merger.Merge(filtered);
        var ranked = _ranker.Rank(merged, query, request.Profile)
            .Take(Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 500))
            .ToArray();

        return new LiteratureSearchResult
        {
            Records = ranked,
            Issues = issues,
            Plan = plan,
            Metrics = new Dictionary<string, object>
            {
                ["candidates"] = records.Count,
                ["filtered"] = filtered.Count,
                ["merged"] = merged.Count,
                ["returned"] = ranked.Length
            }
        };
    }
}
