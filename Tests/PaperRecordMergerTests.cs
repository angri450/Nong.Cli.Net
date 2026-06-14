using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Xunit;

namespace Tests;

public class PaperRecordMergerTests
{
    [Fact]
    public void Merge_DoiDedupePreservesSources()
    {
        var records = new[]
        {
            new PaperRecord
            {
                Doi = "https://doi.org/10.1/abc",
                Title = "A",
                CitationCount = 1,
                RetrievedFrom = { "openalex" },
                SourceIds = { ["openalex"] = "W1" }
            },
            new PaperRecord
            {
                Doi = "doi:10.1/ABC.",
                Abstract = "long abstract",
                CitationCount = 5,
                PdfUrl = "https://example.test/a.pdf",
                RetrievedFrom = { "unpaywall" },
                SourceIds = { ["unpaywall"] = "10.1/abc" }
            }
        };

        var merged = new PaperRecordMerger().Merge(records);
        var record = Assert.Single(merged);
        Assert.Equal(5, record.CitationCount);
        Assert.Equal("long abstract", record.Abstract);
        Assert.Equal("https://example.test/a.pdf", record.PdfUrl);
        Assert.Contains("openalex", record.RetrievedFrom);
        Assert.Contains("unpaywall", record.RetrievedFrom);
        Assert.Equal("W1", record.SourceIds["openalex"]);
        Assert.Equal("10.1/abc", record.SourceIds["unpaywall"]);
    }

    [Fact]
    public void Merge_MissingDoiApproximateTitleYearFirstAuthor()
    {
        var records = new[]
        {
            new PaperRecord
            {
                Title = "Humic acid rare earth fertilizer",
                Year = 2010,
                Authors = { "Wang Li" },
                RetrievedFrom = { "openalex" }
            },
            new PaperRecord
            {
                Title = "Humic acid rare earth fertilizer",
                Year = 2010,
                Authors = { "Wang Li" },
                Abstract = "Richer metadata",
                RetrievedFrom = { "crossref" }
            }
        };

        var record = Assert.Single(new PaperRecordMerger().Merge(records));

        Assert.Equal("Richer metadata", record.Abstract);
        Assert.Contains("openalex", record.RetrievedFrom);
        Assert.Contains("crossref", record.RetrievedFrom);
    }

    [Fact]
    public void Merge_DoesNotMergeChineseEnglishTitlesWithoutDoiButDoesWithDoi()
    {
        var chinese = new PaperRecord { Title = "腐植酸研究", Year = 2020, Authors = { "张三" }, FirstAuthor = "张三" };
        var english = new PaperRecord { Title = "Humic acid study", Year = 2020, Authors = { "张三" }, FirstAuthor = "张三" };

        var merged = new PaperRecordMerger().Merge(new[] { chinese, english });
        Assert.Equal(2, merged.Count);

        chinese.Doi = "10.1234/example";
        english.Doi = "https://doi.org/10.1234/example";

        Assert.Single(new PaperRecordMerger().Merge(new[] { chinese, english }));
    }
}
