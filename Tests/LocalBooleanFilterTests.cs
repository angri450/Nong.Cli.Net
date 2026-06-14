using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Xunit;

namespace Tests;

public class LocalBooleanFilterTests
{
    [Fact]
    public void Filter_StrictMatchesTitleAndYear()
    {
        var query = CnkiParser.Parse("TI=humic AND YE BETWEEN ('2000','2013')");
        var records = new[]
        {
            new PaperRecord { Title = "Humic acid and rare earth", Year = 2007 },
            new PaperRecord { Title = "Humic acid", Year = 2020 }
        };

        var filtered = new LocalBooleanFilter().Filter(records, query, "strict", out _);
        var record = Assert.Single(filtered);
        Assert.Equal(2007, record.Year);
    }

    [Fact]
    public void Filter_MapsCnkiFieldsToPaperRecordFields()
    {
        var record = new PaperRecord
        {
            Title = "Humic acid rare earth complex",
            Authors = { "Qian Weichang", "Li Ming" },
            Venue = "Journal of Soil Chemistry",
            Year = 2007,
            Doi = "https://doi.org/10.1016/J.CHEMGEO.2007.05.018",
            CitationCount = 45,
            Abstract = "The abstract mentions chelation and fertilizer.",
            Keywords = { "micronutrient" },
            Concepts = { "稀土" },
            Topics = { "complexation" }
        };
        var filter = new LocalBooleanFilter();

        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("SU='稀土'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("TI='rare earth'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("KY='micronutrient'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("AB='chelation'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("AU='Qian'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("JN='Soil Chemistry'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("YE BETWEEN ('2000','2013')"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("DOI='10.1016/j.chemgeo.2007.05.018'"), "strict", out _));
        Assert.Single(filter.Filter(new[] { record }, CnkiParser.Parse("CF='40'"), "strict", out _));
    }

    [Fact]
    public void Filter_AppliesBooleanStrictly()
    {
        var query = CnkiParser.Parse("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')*('络合'+'螯合')");
        var included = new PaperRecord { Title = "腐植酸稀土微肥", Abstract = "络合" };
        var excluded = new PaperRecord { Title = "腐植酸微肥", Abstract = "普通肥料" };

        var filtered = new LocalBooleanFilter().Filter(new[] { included, excluded }, query, "strict", out _);

        Assert.Same(included, Assert.Single(filtered));
    }

    [Fact]
    public void Filter_FullTextUnavailableStrictRejectsRecallKeepsCandidate()
    {
        var query = CnkiParser.Parse("FT=humic");
        var records = new[] { new PaperRecord { Title = "Metadata only" } };
        var filter = new LocalBooleanFilter();

        var strict = filter.Filter(records, query, "strict", out _);
        var recall = filter.Filter(records, query, "recall", out var issues);

        Assert.Empty(strict);
        Assert.Single(recall);
        Assert.Contains(issues, i => i.Id == "full_text_unavailable");
        Assert.Contains("FT unavailable", records[0].MatchReasons.Single());
    }
}
