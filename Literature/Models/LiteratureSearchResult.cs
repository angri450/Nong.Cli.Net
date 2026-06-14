namespace Angri450.Nong.Literature.Models;

public sealed class LiteratureSearchResult
{
    public IReadOnlyList<PaperRecord> Records { get; init; } = Array.Empty<PaperRecord>();
    public IReadOnlyList<LiteratureIssue> Issues { get; init; } = Array.Empty<LiteratureIssue>();
    public IReadOnlyDictionary<string, object> Metrics { get; init; } = new Dictionary<string, object>();
    public SearchPlan? Plan { get; init; }
}

public sealed class LiteratureIssue
{
    public string Id { get; init; } = "";
    public string Severity { get; init; } = "Warning";
    public string Message { get; init; } = "";
    public int? Position { get; init; }
    public string? Provider { get; init; }
}
