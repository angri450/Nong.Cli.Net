using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

public sealed class ProviderRegistry
{
    readonly Dictionary<string, Func<ILiteratureProvider>> _factories;

    public ProviderRegistry(Dictionary<string, Func<ILiteratureProvider>> factories)
    {
        _factories = new Dictionary<string, Func<ILiteratureProvider>>(factories, StringComparer.OrdinalIgnoreCase);
    }

    public static ProviderRegistry CreateDefault()
    {
        return new ProviderRegistry(new Dictionary<string, Func<ILiteratureProvider>>(StringComparer.OrdinalIgnoreCase)
        {
            ["openalex"] = () => new OpenAlexProvider(),
            ["crossref"] = () => new CrossrefProvider(),
            ["unpaywall"] = () => new UnpaywallProvider()
        });
    }

    public IReadOnlyList<string> ImplementedNames => _factories.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool IsImplemented(string name) => _factories.ContainsKey(name);

    public ILiteratureProvider Create(string name)
    {
        if (_factories.TryGetValue(name, out var factory))
            return factory();
        throw new ArgumentException($"Unsupported literature provider: {name}", nameof(name));
    }

    public IReadOnlyList<ILiteratureProvider> CreateMany(IEnumerable<string> names)
    {
        return names.Select(Create).ToArray();
    }
}
