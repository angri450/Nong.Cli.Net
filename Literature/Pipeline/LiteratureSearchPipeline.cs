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
        var raw = request.ProviderQuery ?? request.Query;

        // Auto-detect plain text: no DSL operators → pass directly to providers
        if (request.ParsedQuery == null && (!raw.Contains('=') || (!raw.Contains('*') && !raw.Contains('+') && !raw.Contains('-'))))
        {
            return await SearchPlainAsync(raw, request, cancellationToken).ConfigureAwait(false);
        }

        var query = request.ParsedQuery ?? CnkiParser.Parse(raw);
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

        var sources = request.Sources.Count == 0 ? new[] { "openalex", "crossref", "unpaywall" } : request.Sources;

        // Plain mode: no DSL, just pass query directly to providers
        if (string.Equals(request.FilterMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            var records2 = new List<PaperRecord>();
            var issues2 = new List<LiteratureIssue>();
            var plainQuery = request.ProviderQuery ?? request.Query;
            foreach (var providerName in sources)
            {
                var p = _registry.Create(providerName);
                var pr = new LiteratureSearchRequest
                {
                    Query = plainQuery,
                    ProviderQuery = plainQuery,
                    Sources = new[] { providerName },
                    Limit = request.Limit,
                };
                var r = await p.SearchAsync(pr, cancellationToken).ConfigureAwait(false);
                records2.AddRange(r.Records);
                issues2.AddRange(r.Issues);
            }
            var deduped = _merger.Merge(records2);
            deduped = deduped.Take(Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 500)).ToList();
            return new LiteratureSearchResult
            {
                Records = deduped,
                Issues = issues2,
                Metrics = new Dictionary<string, object>
                {
                    ["candidates"] = records2.Count,
                    ["returned"] = deduped.Count
                }
            };
        }

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
                StructuredQuery = providerPlan.StructuredQuery,
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
                    ProviderQuery = rough,
                    StructuredQuery = providerPlan.StructuredQuery
                };
                var result = await provider.SearchAsync(roughRequest, cancellationToken).ConfigureAwait(false);
                records.AddRange(result.Records);
                issues.AddRange(result.Issues);
            }
        }

        var filtered = _filter.Filter(records, query, request.FilterMode, out var filterIssues);
        issues.AddRange(filterIssues);

        // V5.0.1: fallback — if strict filtering returned 0 but candidates exist, retry with recall
        var usedFallback = false;
        if (filtered.Count == 0 && records.Count > 0
            && string.Equals(request.FilterMode, "strict", StringComparison.OrdinalIgnoreCase))
        {
            var recallFiltered = _filter.Filter(records, query, "recall", out var recallIssues);
            issues.AddRange(recallIssues);
            if (recallFiltered.Count > 0)
            {
                issues.Add(new LiteratureIssue
                {
                    Id = "fallback_recall",
                    Severity = "Warning",
                    Message = $"Strict filter returned 0 of {records.Count} candidates; auto-fallback to recall mode returned {recallFiltered.Count}."
                });
                filtered = recallFiltered;
                usedFallback = true;
            }
        }

        var merged = _merger.Merge(filtered);
        var ranked = _ranker.Rank(merged, query, request.Profile)
            .Take(Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, 500))
            .ToArray();

        if (usedFallback)
        {
            issues.Add(new LiteratureIssue
            {
                Id = "fallback_applied",
                Severity = "Warning",
                Message = "Results may include less relevant papers due to recall-mode fallback."
            });
        }

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
                ["returned"] = ranked.Length,
                ["fallback"] = usedFallback ? "recall" : "none"
            }
        };
    }

    async Task<LiteratureSearchResult> SearchPlainAsync(string queryText, LiteratureSearchRequest request, CancellationToken ct)
    {
        var srcs = request.Sources.Count == 0
            ? new[] { "openalex", "crossref", "unpaywall" }
            : request.Sources;
        var recs = new List<PaperRecord>();
        var iss = new List<LiteratureIssue>();
        foreach (var pn in srcs)
        {
            var p = _registry.Create(pn);
            var pr = new LiteratureSearchRequest { Query = queryText, ProviderQuery = queryText, Sources = new[] { pn }, Limit = request.Limit };
            var sr = await p.SearchAsync(pr, ct).ConfigureAwait(false);
            recs.AddRange(sr.Records);
            iss.AddRange(sr.Issues);
        }
        var deduped = _merger.Merge(recs);
        deduped = deduped.Take(Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 500)).ToList();
        return new LiteratureSearchResult
        {
            Records = deduped,
            Issues = iss,
            Metrics = new Dictionary<string, object> { ["candidates"] = recs.Count, ["returned"] = deduped.Count }
        };
    }
}
