using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

public sealed class UnpaywallProvider : ILiteratureProvider
{
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnvironmentVariable;

    public UnpaywallProvider()
        : this(ProviderHttpClientFactory.Create("unpaywall"))
    {
    }

    public UnpaywallProvider(HttpClient client, Func<string, string?>? getEnvironmentVariable = null)
    {
        _client = client;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public string Name => "unpaywall";

    public LiteratureProviderCapabilities Capabilities { get; } = new()
    {
        DoiLookup = true,
        OpenAccessLookup = true,
        SupportsLocalStrictFilter = true
    };

    public Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken cancellationToken)
    {
        var doi = request.Doi;
        if (string.IsNullOrWhiteSpace(doi))
            doi = request.ParsedQuery?.Terms.FirstOrDefault(t => string.Equals(t.EffectiveField, "DOI", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(doi))
        {
            return Task.FromResult(new ProviderSearchResult
            {
                Provider = Name,
                IsAvailable = true,
                Records = Array.Empty<PaperRecord>(),
                Issues = new[]
                {
                    new LiteratureIssue { Id = "doi_required", Severity = "Warning", Message = "Unpaywall requires a DOI and does not support general search.", Provider = Name }
                },
                Diagnostics = Diagnostics(0)
            });
        }

        return SearchByDoiAsync(doi, cancellationToken);
    }

    async Task<ProviderSearchResult> SearchByDoiAsync(string doi, CancellationToken cancellationToken)
    {
        var record = await GetByDoiAsync(doi, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return string.IsNullOrWhiteSpace(ResolveEmail())
                ? MissingEmailResult()
                : new ProviderSearchResult
                {
                    Provider = Name,
                    Records = Array.Empty<PaperRecord>(),
                    Diagnostics = Diagnostics(0)
                };
        }

        return new ProviderSearchResult
        {
            Provider = Name,
            Records = new[] { record },
            Diagnostics = Diagnostics(1)
        };
    }

    public async Task<PaperRecord?> GetByDoiAsync(string doi, CancellationToken cancellationToken)
    {
        var email = ResolveEmail();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedDoi = NormalizeDoi(doi) ?? doi;
        var url = $"https://api.unpaywall.org/v2/{Uri.EscapeDataString(normalizedDoi)}?email={Uri.EscapeDataString(email)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ProviderHttpClientFactory.SendWithRetryAsync(_client, request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Map(document.RootElement);
    }

    public async Task<PaperRecord?> EnrichAsync(PaperRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Doi))
            return record;

        var oa = await GetByDoiAsync(record.Doi, cancellationToken).ConfigureAwait(false);
        if (oa is null)
            return record;

        record.IsOpenAccess = oa.IsOpenAccess ?? record.IsOpenAccess;
        record.OpenAccessStatus = oa.OpenAccessStatus ?? record.OpenAccessStatus;
        record.PdfUrl = oa.PdfUrl ?? record.PdfUrl;
        record.LandingPageUrl = oa.LandingPageUrl ?? record.LandingPageUrl;
        record.License = oa.License ?? record.License;
        if (!record.RetrievedFrom.Contains("unpaywall", StringComparer.OrdinalIgnoreCase))
            record.RetrievedFrom.Add("unpaywall");
        return record;
    }

    ProviderSearchResult MissingEmailResult()
    {
        return new ProviderSearchResult
        {
            Provider = Name,
            IsAvailable = false,
            UnavailableReason = "email_missing",
            Issues = new[]
            {
                new LiteratureIssue { Id = "provider_credential_missing", Severity = "Warning", Message = "Unpaywall requires NONG_LIT_UNPAYWALL_EMAIL or NONG_LIT_MAILTO.", Provider = Name }
            },
            Diagnostics = Diagnostics(0)
        };
    }

    string? ResolveEmail()
    {
        var email = _getEnvironmentVariable("NONG_LIT_UNPAYWALL_EMAIL");
        if (string.IsNullOrWhiteSpace(email))
            email = _getEnvironmentVariable("NONG_LIT_MAILTO");
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    bool HasEmail() => !string.IsNullOrWhiteSpace(ResolveEmail());

    string EmailEnv()
    {
        if (!string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_UNPAYWALL_EMAIL")))
            return "NONG_LIT_UNPAYWALL_EMAIL";
        return !string.IsNullOrWhiteSpace(_getEnvironmentVariable("NONG_LIT_MAILTO"))
            ? "NONG_LIT_MAILTO"
            : string.Empty;
    }

    Dictionary<string, object> Diagnostics(int records)
    {
        return new Dictionary<string, object>
        {
            ["hasEmail"] = HasEmail(),
            ["has_email"] = HasEmail(),
            ["email_env"] = EmailEnv(),
            ["records"] = records
        };
    }

    static PaperRecord Map(JsonElement item)
    {
        var record = new PaperRecord
        {
            Doi = NormalizeDoi(GetString(item, "doi")),
            Title = GetString(item, "title"),
            IsOpenAccess = GetBool(item, "is_oa"),
            OpenAccessStatus = GetString(item, "oa_status"),
            Year = GetInt(item, "year")
        };
        record.RetrievedFrom.Add("unpaywall");
        if (!string.IsNullOrWhiteSpace(record.Doi))
            record.SourceIds["unpaywall"] = record.Doi;

        if (item.TryGetProperty("best_oa_location", out var best) && best.ValueKind == JsonValueKind.Object)
        {
            record.PdfUrl = GetString(best, "url_for_pdf");
            record.LandingPageUrl = GetString(best, "url");
            record.License = GetString(best, "license");
            var hostType = GetString(best, "host_type");
            if (!string.IsNullOrWhiteSpace(hostType))
                record.MatchReasons.Add("unpaywall_host_type=" + hostType);
        }

        return record;
    }

    static string? GetString(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    static bool? GetBool(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    static int? GetInt(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;
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
