using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

public interface ILiteratureProvider
{
    string Name { get; }
    LiteratureProviderCapabilities Capabilities { get; }

    Task<ProviderSearchResult> SearchAsync(
        LiteratureSearchRequest request,
        CancellationToken cancellationToken);

    Task<PaperRecord?> GetByDoiAsync(
        string doi,
        CancellationToken cancellationToken);

    Task<PaperRecord?> EnrichAsync(
        PaperRecord record,
        CancellationToken cancellationToken);
}
