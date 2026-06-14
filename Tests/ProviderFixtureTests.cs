using System.Net;
using System.Text;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Providers;
using Xunit;

namespace Tests;

public sealed class ProviderFixtureTests
{
    [Fact]
    public async Task OpenAlexProvider_RetriesTransientAndMapsFixture()
    {
        var handler = new QueueFixtureHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            request =>
            {
                Assert.Contains("search=humic", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("api_key=secret-key", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
                return JsonResponse(ReadFixture("openalex-search.json"));
            });
        using var client = ProviderHttpClientFactory.Create("openalex", handler, new Uri("https://fixture.openalex.test/"));
        var provider = new OpenAlexProvider(client, name => name == "NONG_LIT_OPENALEX_KEY" ? "secret-key" : null);

        var result = await provider.SearchAsync(new LiteratureSearchRequest { Query = "humic", Limit = 5 }, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(2, handler.Calls);
        Assert.True((bool)result.Diagnostics["has_api_key"]);
        Assert.Equal("NONG_LIT_OPENALEX_KEY", result.Diagnostics["api_key_env"]);
        Assert.DoesNotContain("secret-key", FlattenDiagnostics(result.Diagnostics));

        var record = Assert.Single(result.Records);
        Assert.Equal("10.1016/j.chemgeo.2007.05.018", record.Doi);
        Assert.Equal("Humic acid and rare earth", record.Title);
        Assert.Equal("Qian W", record.FirstAuthor);
        Assert.Equal("This is abstract", record.Abstract);
        Assert.Equal(42, record.CitationCount);
        Assert.True(record.IsOpenAccess);
        Assert.Equal("green", record.OpenAccessStatus);
        Assert.Equal("https://example.test/oa", record.PdfUrl);
        Assert.Contains("openalex", record.RetrievedFrom);
    }

    [Fact]
    public async Task OpenAlexProvider_DoiLookupAcceptsApiKeyAliasAndMapsOaFields()
    {
        var handler = new QueueFixtureHandler(request =>
        {
            Assert.Contains("/works/doi:10.1016%2Fj.chemgeo.2007.05.018", request.RequestUri?.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("api_key=secret-key", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
            return JsonResponse(ReadFixture("openalex-work.json"));
        });
        using var client = ProviderHttpClientFactory.Create("openalex", handler, new Uri("https://fixture.openalex.test/"));
        var provider = new OpenAlexProvider(client, name => name == "NONG_LIT_OPENALEX_API_KEY" ? "secret-key" : null);

        var result = await provider.SearchAsync(
            new LiteratureSearchRequest { Doi = "https://doi.org/10.1016/j.chemgeo.2007.05.018" },
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.True((bool)result.Diagnostics["has_api_key"]);
        Assert.Equal("NONG_LIT_OPENALEX_API_KEY", result.Diagnostics["api_key_env"]);
        Assert.DoesNotContain("secret-key", FlattenDiagnostics(result.Diagnostics));

        var record = Assert.Single(result.Records);
        Assert.Equal("Chemical Geology", record.Venue);
        Assert.Equal("Alice Chen", record.FirstAuthor);
        Assert.Equal("https://example.org/article.pdf", record.PdfUrl);
        Assert.Equal("https://example.org/article", record.LandingPageUrl);
        Assert.Equal("cc-by", record.License);
        Assert.Contains("Nong Research Institute", record.Affiliations);
    }

    [Fact]
    public async Task CrossrefProvider_MapsFixtureAndDoesNotExposeMailto()
    {
        var handler = new QueueFixtureHandler(request =>
        {
            Assert.Contains("query.bibliographic=humic", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("mailto=user%40example.test", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("user@example.test", request.Headers.UserAgent.ToString(), StringComparison.OrdinalIgnoreCase);
            return JsonResponse(ReadFixture("crossref-search.json"));
        });
        using var client = ProviderHttpClientFactory.Create("crossref", handler, new Uri("https://fixture.crossref.test/"));
        var provider = new CrossrefProvider(client, name => name == "NONG_LIT_MAILTO" ? "user@example.test" : null);

        var result = await provider.SearchAsync(new LiteratureSearchRequest { Query = "humic", Limit = 5 }, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.True((bool)result.Diagnostics["has_mailto"]);
        Assert.Equal("NONG_LIT_MAILTO", result.Diagnostics["mailto_env"]);
        Assert.DoesNotContain("user@example.test", FlattenDiagnostics(result.Diagnostics));

        var record = Assert.Single(result.Records);
        Assert.Equal("10.1016/j.chemgeo.2007.05.018", record.Doi);
        Assert.Equal("Humic acid and rare earth", record.Title);
        Assert.Equal("Chem Geol", record.Venue);
        Assert.Equal("Elsevier", record.Publisher);
        Assert.Equal("W Qian", record.FirstAuthor);
        Assert.Equal("10", record.Volume);
        Assert.Equal("2", record.Issue);
        Assert.Equal("1-8", record.Pages);
        Assert.Equal("https://example.test/license", record.License);
        Assert.Contains("Example Fund", record.Funders);
        Assert.Contains("crossref", record.RetrievedFrom);
    }

    [Fact]
    public async Task CrossrefProvider_DoiLookupMapsLicenseFunderAndAffiliation()
    {
        var handler = new QueueFixtureHandler(request =>
        {
            Assert.Contains("/works/10.1016%2Fj.chemgeo.2007.05.018", request.RequestUri?.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            return JsonResponse(ReadFixture("crossref-work.json"));
        });
        using var client = ProviderHttpClientFactory.Create("crossref", handler, new Uri("https://fixture.crossref.test/"));
        var provider = new CrossrefProvider(client);

        var record = await provider.GetByDoiAsync("10.1016/j.chemgeo.2007.05.018", CancellationToken.None);

        Assert.NotNull(record);
        Assert.Equal("Chemical Geology", record.Venue);
        Assert.Equal(2007, record.Year);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", record.License);
        Assert.Contains("National Natural Science Foundation", record.Funders);
        Assert.Contains("Nong Research Institute", record.Affiliations);
    }

    [Fact]
    public async Task UnpaywallProvider_MissingEmailReturnsUnavailableWithoutNetwork()
    {
        var handler = new QueueFixtureHandler(_ => throw new InvalidOperationException("Network should not be called without email."));
        using var client = ProviderHttpClientFactory.Create("unpaywall", handler, new Uri("https://fixture.unpaywall.test/"));
        var provider = new UnpaywallProvider(client, _ => null);

        var result = await provider.SearchAsync(
            new LiteratureSearchRequest { Doi = "10.1016/j.chemgeo.2007.05.018" },
            CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal("email_missing", result.UnavailableReason);
        Assert.False((bool)result.Diagnostics["has_email"]);
        Assert.Equal("", result.Diagnostics["email_env"]);
        Assert.Empty(result.Records);
        Assert.Equal(0, handler.Calls);
        Assert.Contains(result.Issues, issue => issue.Id == "provider_credential_missing");
    }

    [Fact]
    public async Task UnpaywallProvider_FallsBackToMailtoAndMapsLegalOpenAccessDetails()
    {
        var handler = new QueueFixtureHandler(request =>
        {
            Assert.Contains("/10.1016%2Fj.chemgeo.2007.05.018", request.RequestUri?.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("email=user%40example.test", request.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
            return JsonResponse(ReadFixture("unpaywall-work.json"));
        });
        using var client = ProviderHttpClientFactory.Create("unpaywall", handler, new Uri("https://fixture.unpaywall.test/"));
        var provider = new UnpaywallProvider(client, name => name == "NONG_LIT_MAILTO" ? "user@example.test" : null);

        var result = await provider.SearchAsync(
            new LiteratureSearchRequest { Doi = "10.1016/j.chemgeo.2007.05.018" },
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.True((bool)result.Diagnostics["has_email"]);
        Assert.Equal("NONG_LIT_MAILTO", result.Diagnostics["email_env"]);
        Assert.DoesNotContain("user@example.test", FlattenDiagnostics(result.Diagnostics));

        var record = Assert.Single(result.Records);
        Assert.Equal("10.1016/j.chemgeo.2007.05.018", record.Doi);
        Assert.True(record.IsOpenAccess);
        Assert.Equal("gold", record.OpenAccessStatus);
        Assert.Equal("https://example.org/legal.pdf", record.PdfUrl);
        Assert.Equal("https://example.org/legal", record.LandingPageUrl);
        Assert.Equal("cc-by", record.License);
        Assert.Contains("unpaywall_host_type=publisher", record.MatchReasons);
        Assert.Contains("unpaywall", record.RetrievedFrom);
    }

    static string ReadFixture(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var path = Path.Combine(current.FullName, "Tests", "Fixtures", "Literature", name);
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate literature fixture.", name);
    }

    static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    static string FlattenDiagnostics(IReadOnlyDictionary<string, object> diagnostics)
    {
        return string.Join(" ", diagnostics.Select(pair => pair.Key + "=" + pair.Value));
    }

    sealed class QueueFixtureHandler : HttpMessageHandler
    {
        readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;

        public QueueFixtureHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
        {
            _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (_responders.Count == 0)
            {
                throw new InvalidOperationException("No fixture response was queued for " + request.RequestUri);
            }

            return Task.FromResult(_responders.Dequeue()(request));
        }
    }
}
