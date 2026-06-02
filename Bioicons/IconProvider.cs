using System.Reflection;

namespace Bioicons;

/// <summary>
/// Provides access to embedded SVG scientific illustration icons organized by category.
/// </summary>
public static class IconProvider
{
    private static readonly Dictionary<string, Dictionary<string, string>> Cache = new();
    private static readonly object Lock = new();
    private static bool _loaded;

    /// <summary>
    /// Gets the SVG string for the specified icon.
    /// </summary>
    /// <param name="category">The icon category (e.g. Biology, Chemistry, Medical, LabEquipment, Arrows, Experimental).</param>
    /// <param name="name">The icon name without .svg extension (e.g. cell, dna, flask).</param>
    /// <returns>The SVG markup string.</returns>
    /// <exception cref="ArgumentException">Thrown when the category or name is not found.</exception>
    public static string GetSvg(string category, string name)
    {
        EnsureLoaded();

        if (!Cache.TryGetValue(category, out var icons))
            throw new ArgumentException($"Category '{category}' not found. Available: {string.Join(", ", Cache.Keys)}");

        if (!icons.TryGetValue(name, out var svg))
            throw new ArgumentException($"Icon '{name}' not found in category '{category}'. Available: {string.Join(", ", icons.Keys)}");

        return svg;
    }

    /// <summary>
    /// Returns all available icon categories.
    /// </summary>
    public static IReadOnlyList<string> GetCategories()
    {
        EnsureLoaded();
        return Cache.Keys.OrderBy(k => k).ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns all icon names within a given category.
    /// </summary>
    /// <param name="category">The category to query.</param>
    /// <returns>List of icon names (without .svg extension).</returns>
    /// <exception cref="ArgumentException">Thrown when the category is not found.</exception>
    public static IReadOnlyList<string> GetIcons(string category)
    {
        EnsureLoaded();

        if (!Cache.TryGetValue(category, out var icons))
            throw new ArgumentException($"Category '{category}' not found. Available: {string.Join(", ", Cache.Keys)}");

        return icons.Keys.OrderBy(k => k).ToList().AsReadOnly();
    }

    /// <summary>
    /// Saves an icon SVG to a file on disk.
    /// </summary>
    /// <param name="category">The icon category.</param>
    /// <param name="name">The icon name without .svg extension.</param>
    /// <param name="outputPath">The file path to write to.</param>
    public static void SaveSvg(string category, string name, string outputPath)
    {
        var svg = GetSvg(category, name);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, svg);
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (Lock)
        {
            if (_loaded) return;
            LoadAll();
            _loaded = true;
        }
    }

    private static void LoadAll()
    {
        var assembly = typeof(IconProvider).Assembly;
        var prefix = "Bioicons.";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix) || !resourceName.EndsWith(".svg"))
                continue;

            // Resource name format: Bioicons.{Category}.{icon-name}.svg
            var relative = resourceName[prefix.Length..];           // e.g. "Biology.cell.svg"
            var svgSuffix = relative[^4..];                         // ".svg"
            var withoutExt = relative[..^4];                        // e.g. "Biology.cell"
            var dotIndex = withoutExt.IndexOf('.');
            if (dotIndex < 0) continue;

            var category = withoutExt[..dotIndex];                  // e.g. "Biology"
            var iconName = withoutExt[(dotIndex + 1)..];            // e.g. "cell"

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            using var reader = new StreamReader(stream);
            var svgContent = reader.ReadToEnd();

            if (!Cache.TryGetValue(category, out var dict))
            {
                dict = new Dictionary<string, string>();
                Cache[category] = dict;
            }

            dict[iconName] = svgContent;
        }
    }
}
