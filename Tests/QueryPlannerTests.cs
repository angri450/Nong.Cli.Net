using System.Text.Json;
using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Pipeline;
using Xunit;

namespace Tests;

public class QueryPlannerTests
{
    [Fact]
    public void Plan_ProducesProviderQueriesAndFields()
    {
        var query = CnkiParser.Parse("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')");
        var plan = new QueryPlanner().Plan(query, new[] { "openalex", "crossref", "unpaywall" });

        Assert.Contains("SU", plan.ParsedFields);
        Assert.Contains("腐植酸", plan.NormalizedConcepts);
        Assert.Contains(plan.Providers, p => p.Name == "openalex" && p.RoughQueries.Count > 0);
        Assert.Contains(plan.Providers, p => p.Name == "unpaywall" && p.Limitations.Any(l => l.Contains("DOI-only", StringComparison.OrdinalIgnoreCase)));
        Assert.All(plan.Providers, provider => Assert.True(provider.RoughQueries.Count <= QueryPlanner.DefaultMaxRoughQueriesPerProvider));
    }

    [Fact]
    public void Plan_TruncatesOrExplosionDeterministically()
    {
        var query = CnkiParser.Parse("SU=(a+b+c+d+e+f)*(g+h+i+j+k)");
        var first = new QueryPlanner().Plan(query, new[] { "openalex" }, maxQueriesPerProvider: 20);
        var second = new QueryPlanner().Plan(query, new[] { "openalex" }, maxQueriesPerProvider: 20);
        var provider = Assert.Single(first.Providers);

        Assert.Equal(20, provider.RoughQueries.Count);
        Assert.Equal(provider.RoughQueries, second.Providers.Single().RoughQueries);
        Assert.Contains(first.Issues, i => i.Id == "rough_query_truncated" && i.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_ReportsCredentialAvailabilityAsBooleanOnly()
    {
        var oldMailto = Environment.GetEnvironmentVariable("NONG_LIT_MAILTO");
        try
        {
            Environment.SetEnvironmentVariable("NONG_LIT_MAILTO", "person@example.org");

            var plan = new QueryPlanner().Plan(
                CnkiParser.Parse("DOI='10.1016/j.chemgeo.2007.05.018'"),
                new[] { "unpaywall" });
            var json = JsonSerializer.Serialize(plan);

            Assert.True(plan.Providers.Single().HasRequiredCredential);
            Assert.Equal(new[] { "10.1016/j.chemgeo.2007.05.018" }, plan.Providers.Single().RoughQueries);
            Assert.DoesNotContain("person@example.org", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NONG_LIT_MAILTO", oldMailto);
        }
    }
}
