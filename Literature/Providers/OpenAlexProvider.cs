using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

public sealed class OpenAlexProvider : ILiteratureProvider
{
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnvironmentVariable;

    public OpenAlexProvider()
        : this(ProviderHttpClientFactory.Create("openalex"))
    {
    }

    public OpenAlexProvider(HttpClient client, Func<string, string?>? getEnvironmentVariable = null)
    {
        _client = client;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public string Name => "openalex";

    public LiteratureProviderCapabilities Capabilities { get; } = new()
    {
        Search = true,
        DoiLookup = true,
        CitationLookup = true,
        SupportsLocalStrictFilter = true
    };

    public async Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Doi))
        {
            var record = await GetByDoiAsync(request.Doi, cancellationToken).ConfigureAwait(false);
            return new ProviderSearchResult
            {
                Provider = Name,
                Records = record is null ? Array.Empty<PaperRecord>() : new[] { record },
                Diagnostics = Diagnostics(record is null ? 0 : 1)
            };
        }

        var query = request.ProviderQuery
            ?? request.RoughQueries.FirstOrDefault()
            ?? request.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ProviderSearchResult
            {
                Provider = Name,
                Records = Array.Empty<PaperRecord>(),
                Diagnostics = Diagnostics(0)
            };
        }

        var url = BuildWorksUrl("works", query, request.Limit);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ProviderHttpClientFactory.SendWithRetryAsync(_client, httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Unavailable((int)response.StatusCode, response.ReasonPhrase);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var records = new List<PaperRecord>();
        if (document.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                records.Add(MapWork(item));
            }
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

        var encoded = "doi:" + Uri.EscapeDataString(NormalizeDoi(doi) ?? doi);
        var url = AppendAuth($"https://api.openalex.org/works/{encoded}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ProviderHttpClientFactory.SendWithRetryAsync(_client, request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapWork(document.RootElement);
    }

    public Task<PaperRecord?> EnrichAsync(PaperRecord record, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(record.Doi)
            ? Task.FromResult<PaperRecord?>(record)
            : GetByDoiAsync(record.Doi, cancellationToken);
    }

    string BuildWorksUrl(string path, string query, int limit)
    {
        var perPage = Math.Clamp(limit <= 0 ? 25 : limit, 1, 200);
        var url = $"https://api.openalex.org/{path}?search={Uri.EscapeDataString(query)}&per-page={perPage}";
        return AppendAuth(url);
    }

    string AppendAuth(string url)
    {
        var apiKey = _getEnvironmentVariable("NONG_LIT_OPENALEX_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = _getEnvironmentVariable("NONG_LIT_OPENALEX_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            return url;

        return url + (url.Contains('?') ? '&' : '?') + "api_key=" + Uri.EscapeDataString(apiKey);
    }

    bool HasApiKey()
    {
        return !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_OPENALEX_API_KEY"))
            || !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_OPENALEX_KEY"));
    }

    ProviderSearchResult Unavailable(int status, string? reason)
    {
        return new ProviderSearchResult
        {
            Provider = Name,
            IsAvailable = false,
            UnavailableReason = $"HTTP {status}: {reason}",
            Issues = new[]
            {
                new LiteratureIssue { Id = "provider_unavailable", Severity = "Warning", Message = $"OpenAlex unavailable: HTTP {status}.", Provider = Name }
            },
            Diagnostics = Diagnostics(0, status)
        };
    }

    Dictionary<string, object> Diagnostics(int records, int? status = null)
    {
        var env = !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_OPENALEX_API_KEY"))
            ? "NONG_LIT_OPENALEX_API_KEY"
            : !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_OPENALEX_KEY"))
                ? "NONG_LIT_OPENALEX_KEY"
                : string.Empty;
        var diagnostics = new Dictionary<string, object>
        {
            ["hasApiKey"] = HasApiKey(),
            ["has_api_key"] = HasApiKey(),
            ["api_key_env"] = env,
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
            Id = GetString(item, "id"),
            Title = GetString(item, "display_name") ?? GetString(item, "title"),
            Doi = NormalizeDoi(GetString(item, "doi")),
            Year = GetInt(item, "publication_year"),
            Venue = GetNestedString(item, "primary_location", "source", "display_name"),
            Journal = GetNestedString(item, "primary_location", "source", "display_name"),
            CitationCount = GetInt(item, "cited_by_count"),
            Abstract = RestoreAbstract(item),
            IsOpenAccess = GetNestedBool(item, "open_access", "is_oa"),
            OpenAccessStatus = GetNestedString(item, "open_access", "oa_status"),
            LandingPageUrl = GetNestedString(item, "open_access", "oa_url"),
            PdfUrl = GetNestedString(item, "best_oa_location", "pdf_url"),
            License = GetNestedString(item, "best_oa_location", "license")
        };

        record.LandingPageUrl ??= GetNestedString(item, "best_oa_location", "landing_page_url");
        record.PdfUrl ??= GetNestedString(item, "open_access", "oa_url");

        AddSource(record, "openalex", record.Id);

        if (item.TryGetProperty("authorships", out var authorships) && authorships.ValueKind == JsonValueKind.Array)
        {
            foreach (var authorship in authorships.EnumerateArray())
            {
                var name = GetNestedString(authorship, "author", "display_name");
                if (!string.IsNullOrWhiteSpace(name))
                    record.Authors.Add(name);

                if (authorship.TryGetProperty("institutions", out var institutions) && institutions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var institution in institutions.EnumerateArray())
                    {
                        var inst = GetString(institution, "display_name");
                        if (!string.IsNullOrWhiteSpace(inst) && !record.Affiliations.Contains(inst))
                            record.Affiliations.Add(inst);
                    }
                }
            }
        }

        record.FirstAuthor = record.Authors.FirstOrDefault();

        if (item.TryGetProperty("concepts", out var concepts) && concepts.ValueKind == JsonValueKind.Array)
        {
            foreach (var concept in concepts.EnumerateArray())
            {
                var name = GetString(concept, "display_name");
                if (!string.IsNullOrWhiteSpace(name))
                    record.Concepts.Add(name);
            }
        }

        if (item.TryGetProperty("topics", out var topics) && topics.ValueKind == JsonValueKind.Array)
        {
            foreach (var topic in topics.EnumerateArray())
            {
                var name = GetString(topic, "display_name");
                if (!string.IsNullOrWhiteSpace(name))
                    record.Topics.Add(name);
            }
        }

        return record;
    }

    static string? RestoreAbstract(JsonElement item)
    {
        if (!item.TryGetProperty("abstract_inverted_index", out var index) || index.ValueKind != JsonValueKind.Object)
            return null;

        var positions = new SortedDictionary<int, string>();
        foreach (var wordProperty in index.EnumerateObject())
        {
            if (wordProperty.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var pos in wordProperty.Value.EnumerateArray())
            {
                if (pos.TryGetInt32(out var position))
                    positions[position] = wordProperty.Name;
            }
        }

        return positions.Count == 0 ? null : string.Join(' ', positions.Values);
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

    static bool? GetNestedBool(JsonElement item, params string[] path)
    {
        if (!TryGetNested(item, out var value, path))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    static string? GetNestedString(JsonElement item, params string[] path)
    {
        return TryGetNested(item, out var value, path) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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

    static void AddSource(PaperRecord record, string provider, string? id)
    {
        if (!record.RetrievedFrom.Contains(provider, StringComparer.OrdinalIgnoreCase))
            record.RetrievedFrom.Add(provider);
        if (!string.IsNullOrWhiteSpace(id))
            record.SourceIds[provider] = id;
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
