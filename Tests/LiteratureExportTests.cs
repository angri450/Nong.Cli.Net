using Angri450.Nong.Literature.Export;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Xunit;

namespace Tests;

public class LiteratureExportTests
{
    [Fact]
    public void Export_WritesNonEmptyArtifactsAndJsonRoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-lit-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = new LiteratureSearchResult
            {
                Records = new[]
                {
                    new PaperRecord
                    {
                        Title = "Humic acid and rare earth",
                        Authors = { "Qian W" },
                        Year = 2007,
                        Venue = "Chem Geol",
                        Doi = "10.1016/j.chemgeo.2007.05.018"
                    }
                }
            };

            var json = Path.Combine(dir, "refs.json");
            var md = Path.Combine(dir, "refs.md");
            var bib = Path.Combine(dir, "refs.bib");
            JsonLiteratureExporter.Write(json, result);
            MarkdownLiteratureExporter.Write(md, result.Records);
            BibTeXExporter.Write(bib, result.Records);

            Assert.True(new FileInfo(json).Length > 0);
            Assert.True(new FileInfo(md).Length > 0);
            Assert.True(new FileInfo(bib).Length > 0);
            var loaded = JsonLiteratureExporter.ReadResultOrRecords(json);
            Assert.Single(loaded.Records);
            Assert.Contains("@article", File.ReadAllText(bib));
            Assert.Contains("DOI: 10.1016/j.chemgeo.2007.05.018", File.ReadAllText(md));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CitationFormatter_EscapesBibTexAndUsesStableKeys()
    {
        var first = new PaperRecord
        {
            Title = "A title with {braces} and \"quotes\" \\ slash",
            Authors = { "Qian, Wei" },
            FirstAuthor = "Qian",
            Year = 2020,
            Venue = "Journal of Export Tests",
            Doi = "10.1234/example",
            Abstract = "Abstract with {braces} and \"quotes\" \\ slash"
        };
        var second = new PaperRecord
        {
            Title = first.Title,
            Authors = { "Qian, Wei" },
            FirstAuthor = "Qian",
            Year = 2020,
            Venue = first.Venue
        };

        var bib = BibTeXExporter.Export(new[] { first, second });

        Assert.Contains("@article{qian2020_1", bib);
        Assert.Contains("@article{qian2020_2", bib);
        Assert.Contains("\\{braces\\}", bib);
        Assert.Contains("\\\"", bib);
        Assert.Contains("\\\\", bib);
    }
}
