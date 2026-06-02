namespace Nong.Genre;

/// <summary>
/// 模板加载器。从程序集嵌入资源中读取 JSON 格式模板。
/// </summary>
public static class GenreTemplate
{
    /// <summary>列出所有可用模板名称（不含 .json 后缀）。</summary>
    public static string[] List()
    {
        var asm = typeof(GenreTemplate).Assembly;
        var prefix = "Nong.Genre.templates.";
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix) && n.EndsWith(".json"))
            .Select(n => n[prefix.Length..^5])
            .ToArray();
    }

    /// <summary>按名称加载模板内容。</summary>
    public static string Load(string name)
    {
        var asm = typeof(GenreTemplate).Assembly;
        var resourceName = $"Nong.Genre.templates.{name}.json";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Template '{name}' not found. Available: {string.Join(", ", List())}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
