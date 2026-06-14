namespace Angri450.Nong.Literature.Models;

public sealed class ProviderSearchResult
{
    public string Provider { get; init; } = "";
    public IReadOnlyList<PaperRecord> Records { get; init; } = Array.Empty<PaperRecord>();
    public IReadOnlyList<LiteratureIssue> Issues { get; init; } = Array.Empty<LiteratureIssue>();
    public bool IsAvailable { get; init; } = true;
    public string? UnavailableReason { get; init; }
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}
