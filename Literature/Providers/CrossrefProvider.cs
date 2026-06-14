using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

public sealed class CrossrefProvider : ILiteratureProvider
{
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnvironmentVariable;

    public CrossrefProvider()
        : this(ProviderHttpClientFactory.Create("crossref"))
    {
    }

    public CrossrefProvider(HttpClient client, Func<string, string?>? getEnvironmentVariable = null)
    {
        _client = client;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public string Name => "crossref";

    public LiteratureProviderCapabilities Capabilities { get; } = new()
    {
        Search = true,
        DoiLookup = true,
        SupportsLocalStrictFilter = true
    };

    public async Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken cancellationToken)
    {
        var query = request.ProviderQuery
            ?? request.RoughQueries.FirstOrDefault()
            ?? request.Query;
        var rows = Math.Clamp(request.Limit <= 0 ? 25 : request.Limit, 1, 100);
        var url = AppendMailto($"https://api.crossref.org/works?query.bibliographic={Uri.EscapeDataString(query)}&rows={rows}");
        using var httpRequest = CreateRequest(url);
        using var response = await ProviderHttpClientFactory.SendWithRetryAsync(_client, httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Unavailable((int)response.StatusCode, response.ReasonPhrase);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var records = new List<PaperRecord>();
        if (TryGetNested(document.RootElement, out var items, "message", "items") && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
                records.Add(MapWork(item));
        }

        return new ProviderSearchResult
        {
            Provider = Name,
            Records = records,
            Diagnostics = Diagnostics(records.Count, (int)response.StatusCode)
        };
    }

    public async Task<PaperRecord?> GetByDoiAsync(string doi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doi))
            return null;

        var url = AppendMailto($"https://api.crossref.org/works/{Uri.EscapeDataString(NormalizeDoi(doi) ?? doi)}");
        using var request = CreateRequest(url);
        using var response = await ProviderHttpClientFactory.SendWithRetryAsync(_client, request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return TryGetNested(document.RootElement, out var item, "message") ? MapWork(item) : null;
    }

    public async Task<PaperRecord?> EnrichAsync(PaperRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Doi))
            return record;
        return await GetByDoiAsync(record.Doi, cancellationToken).ConfigureAwait(false) ?? record;
    }

    HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var mailto = _getEnvironmentVariable("NONG_LIT_MAILTO");
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd($"Nong-Literature/{ProviderHttpClientFactory.LiteratureVersion}");
        if (!string.IsNullOrWhiteSpace(mailto))
            request.Headers.UserAgent.ParseAdd("(mailto-configured)");
        return request;
    }

    string AppendMailto(string url)
    {
        var mailto = _getEnvironmentVariable("NONG_LIT_MAILTO");
        if (string.IsNullOrWhiteSpace(mailto))
            return url;
        return url + (url.Contains('?') ? '&' : '?') + "mailto=" + Uri.EscapeDataString(mailto);
    }

    bool HasMailto() => !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_MAILTO"));

    ProviderSearchResult Unavailable(int status, string? reason)
    {
        return new ProviderSearchResult
        {
            Provider = Name,
            IsAvailable = false,
            UnavailableReason = $"HTTP {status}: {reason}",
            Issues = new[]
            {
                new LiteratureIssue { Id = "provider_unavailable", Severity = "Warning", Message = $"Crossref unavailable: HTTP {status}.", Provider = Name }
            },
            Diagnostics = Diagnostics(0, status)
        };
    }

    Dictionary<string, object> Diagnostics(int records, int? status = null)
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["hasMailto"] = HasMailto(),
            ["has_mailto"] = HasMailto(),
            ["mailto_env"] = HasMailto() ? "NONG_LIT_MAILTO" : string.Empty,
            ["records"] = records
        };
        if (status is not null)
        {
            diagnostics["httpStatus"] = status.Value;
            diagnostics["http_status"] = status.Value;
        }
        return diagnostics;
    }

    static PaperRecord MapWork(JsonElement item)
    {
        var record = new PaperRecord
        {
            Doi = NormalizeDoi(GetString(item, "DOI")),
            Title = GetFirstString(item, "title"),
            Venue = GetFirstString(item, "container-title"),
            Journal = GetFirstString(item, "container-title"),
            Publisher = GetString(item, "publisher"),
            Volume = GetString(item, "volume"),
            Issue = GetString(item, "issue"),
            Pages = GetString(item, "page"),
            Year = GetYear(item),
            CitationCount = GetInt(item, "is-referenced-by-count")
        };

        if (!string.IsNullOrWhiteSpace(record.Doi))
            record.SourceIds["crossref"] = record.Doi;
        record.RetrievedFrom.Add("crossref");

        if (item.TryGetProperty("author", out var authors) && authors.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authors.EnumerateArray())
            {
                var given = GetString(author, "given");
                var family = GetString(author, "family");
                var name = string.Join(' ', new[] { given, family }.Where(v => !string.IsNullOrWhiteSpace(v)));
                if (!string.IsNullOrWhiteSpace(name))
                    record.Authors.Add(name);

                if (author.TryGetProperty("affiliation", out var affiliations) && affiliations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var affiliation in affiliations.EnumerateArray())
                    {
                        var affiliationName = GetString(affiliation, "name");
                        if (!string.IsNullOrWhiteSpace(affiliationName) &&
                            !record.Affiliations.Contains(affiliationName, StringComparer.OrdinalIgnoreCase))
                        {
                            record.Affiliations.Add(affiliationName);
                        }
                    }
                }
            }
        }

        record.FirstAuthor = record.Authors.FirstOrDefault();

        if (item.TryGetProperty("license", out var licenses) && licenses.ValueKind == JsonValueKind.Array)
        {
            record.License = licenses.EnumerateArray()
                .Select(l => GetString(l, "URL"))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        if (item.TryGetProperty("funder", out var funders) && funders.ValueKind == JsonValueKind.Array)
        {
            foreach (var funder in funders.EnumerateArray())
            {
                var name = GetString(funder, "name");
                if (!string.IsNullOrWhiteSpace(name))
                    record.Funders.Add(name);
            }
        }

        return record;
    }

    static int? GetYear(JsonElement item)
    {
        foreach (var property in new[] { "published-print", "published-online", "published", "issued" })
        {
            if (TryGetNested(item, out var parts, property, "date-parts") &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0 &&
                parts[0].ValueKind == JsonValueKind.Array &&
                parts[0].GetArrayLength() > 0 &&
                parts[0][0].TryGetInt32(out var year))
            {
                return year;
            }
        }

        return null;
    }

    static string? GetFirstString(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : null).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    static string? GetString(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    static int? GetInt(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    static bool TryGetNested(JsonElement item, out JsonElement value, params string[] path)
    {
        value = item;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }

        return true;
    }

    static string? NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
            return null;
        var normalized = doi.Trim();
        if (normalized.StartsWith("https://doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["https://doi.org/".Length..];
        if (normalized.StartsWith("http://doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["http://doi.org/".Length..];
        if (normalized.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];
        return normalized.Trim().ToLowerInvariant();
    }
}
