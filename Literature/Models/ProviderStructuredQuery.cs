namespace Angri450.Nong.Literature.Models;

/// <summary>
/// Structured query parameters extracted from the DSL AST.
/// Each provider maps these to its native API syntax (OpenAlex filter=, Crossref query.author=, etc.).
/// </summary>
public sealed class ProviderStructuredQuery
{
    /// <summary>Free-text keywords for the main search query.</summary>
    public string? FreeTextQuery { get; init; }

    /// <summary>Year range (inclusive).</summary>
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }

    /// <summary>Minimum citation count.</summary>
    public int? MinCitations { get; init; }

    /// <summary>Author names to filter by.</summary>
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();

    /// <summary>Institution names to filter by.</summary>
    public IReadOnlyList<string> Institutions { get; init; } = Array.Empty<string>();

    /// <summary>Title-specific search terms.</summary>
    public IReadOnlyList<string> TitleTerms { get; init; } = Array.Empty<string>();

    /// <summary>Abstract-specific search terms.</summary>
    public IReadOnlyList<string> AbstractTerms { get; init; } = Array.Empty<string>();

    /// <summary>Venue/journal names.</summary>
    public IReadOnlyList<string> Venues { get; init; } = Array.Empty<string>();

    public bool HasFilters =>
        YearFrom.HasValue || YearTo.HasValue || MinCitations.HasValue
        || Authors.Count > 0 || Institutions.Count > 0
        || TitleTerms.Count > 0 || AbstractTerms.Count > 0 || Venues.Count > 0;

    public static ProviderStructuredQuery Empty { get; } = new();
}
