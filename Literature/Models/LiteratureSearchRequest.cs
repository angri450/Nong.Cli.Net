using Angri450.Nong.Literature.Dsl;

namespace Angri450.Nong.Literature.Models;

public sealed class LiteratureSearchRequest
{
    public string Query { get; init; } = "";
    public CnkiQuery? ParsedQuery { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();
    public int Limit { get; init; } = 50;
    public RankProfile Profile { get; init; } = RankProfile.Balanced;
    public string FilterMode { get; init; } = "strict";
    public IReadOnlyList<string> RoughQueries { get; init; } = Array.Empty<string>();
    public string? ProviderQuery { get; init; }
    public string? Doi { get; init; }
    public bool AllowNetwork { get; init; } = true;
}
