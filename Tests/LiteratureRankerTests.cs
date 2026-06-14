using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Xunit;

namespace Tests;

public class LiteratureRankerTests
{
    [Theory]
    [InlineData(RankProfile.Balanced)]
    [InlineData(RankProfile.Classic)]
    [InlineData(RankProfile.Recent)]
    public void Rank_ScoresAreFiniteAndClamped(RankProfile profile)
    {
        var query = CnkiParser.Parse("SU=(humic+soil)");
        var records = new[]
        {
            new PaperRecord { Title = "Humic soil", CitationCount = 100, Year = DateTime.UtcNow.Year, RetrievedFrom = { "openalex" } },
            new PaperRecord { Title = "Other", CitationCount = -1, Year = 1900 }
        };

        var ranked = new LiteratureRanker().Rank(records, query, profile);
        Assert.All(ranked, r =>
        {
            Assert.True(double.IsFinite(r.RelevanceScore));
            Assert.InRange(r.RelevanceScore, 0, 1);
        });
    }

    [Fact]
    public void Rank_RecentProfilePrefersRecentRelevantRecord()
    {
        var query = CnkiParser.Parse("SU='humic acid'");
        var older = new PaperRecord
        {
            Title = "Humic acid study",
            Abstract = "humic acid",
            Year = 2000,
            CitationCount = 500,
            MatchReasons = { "SU:humic acid" }
        };
        var newer = new PaperRecord
        {
            Title = "Humic acid recent study",
            Abstract = "humic acid",
            Year = 2025,
            CitationCount = 5,
            MatchReasons = { "SU:humic acid" }
        };

        var ranked = new LiteratureRanker().Rank(new[] { older, newer }, query, RankProfile.Recent);

        Assert.Same(newer, ranked[0]);
    }
}
