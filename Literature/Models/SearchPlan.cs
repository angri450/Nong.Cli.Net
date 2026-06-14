namespace Angri450.Nong.Literature.Models;

public sealed class SearchPlan
{
    public IReadOnlyList<string> ParsedFields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NormalizedConcepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProviderPlan> Providers { get; init; } = Array.Empty<ProviderPlan>();
    public IReadOnlyList<LiteratureIssue> Issues { get; init; } = Array.Empty<LiteratureIssue>();
}

public sealed class ProviderPlan
{
    public string Name { get; init; } = "";
    public bool IsImplemented { get; init; }
    public bool HasRequiredCredential { get; init; } = true;
    public IReadOnlyList<string> RoughQueries { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}
