namespace Angri450.Nong.Literature.Models;

public sealed class LiteratureProviderCapabilities
{
    public bool Search { get; init; }
    public bool DoiLookup { get; init; }
    public bool OpenAccessLookup { get; init; }
    public bool FullTextLookup { get; init; }
    public bool CitationLookup { get; init; }
    public bool ReferenceLookup { get; init; }
    public bool WebSearch { get; init; }
    public bool RequiresApiKey { get; init; }
    public bool SupportsCnkiDslNative { get; init; }
    public bool SupportsLocalStrictFilter { get; init; }
}
