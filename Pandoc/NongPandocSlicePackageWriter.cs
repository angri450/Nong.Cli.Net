using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandocCore;

public sealed record NongPandocSliceWritePayload
{
    public string OutputDirectory { get; init; } = "";

    public object Manifest { get; init; } = new NongPandocSliceManifest();

    public object Document { get; init; } = new();

    public IReadOnlyList<object>? ContentJsonlItems { get; init; }

    public IReadOnlyList<string>? ContentJsonlLines { get; init; }

    public string NongMarkText { get; init; } = "";

    public object Structure { get; init; } = new();

    public object Format { get; init; } = new();

    public object Diagnostics { get; init; } = new();

    public object AssetsManifest { get; init; } = new();

    public string? TextPreview { get; init; }
}

public sealed record NongPandocSliceWriteOptions
{
    public JsonSerializerOptions JsonOptions { get; init; } = NongPandocSlicePackageWriter.DefaultJsonOptions;

    public JsonSerializerOptions JsonlOptions { get; init; } = NongPandocSlicePackageWriter.DefaultJsonlOptions;

    public IReadOnlyList<string> RequiredArtifacts { get; init; } = NongPandocSlicePackageWriter.DefaultRequiredArtifacts;

    public bool RequireNonEmptyRequiredArtifacts { get; init; } = true;
}

public sealed record NongPandocSliceWriteResult
{
    public string OutputDirectory { get; init; } = "";

    public string ManifestPath { get; init; } = "";

    public Dictionary<string, string> Artifacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class NongPandocSliceWriteException : Exception
{
    public NongPandocSliceWriteException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public static class NongPandocSlicePackageWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonSerializerOptions DefaultJsonlOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly IReadOnlyList<string> DefaultRequiredArtifacts =
    [
        NongPandocArtifactNames.Manifest,
        NongPandocArtifactNames.Document,
        NongPandocArtifactNames.ContentJsonl,
        NongPandocArtifactNames.ContentNongMark,
        NongPandocArtifactNames.Structure,
        NongPandocArtifactNames.Format,
        NongPandocArtifactNames.Diagnostics,
        NongPandocArtifactNames.AssetsManifest,
    ];

    public static NongPandocSliceWriteResult Write(NongPandocSliceWritePayload payload) =>
        Write(payload, new NongPandocSliceWriteOptions());

    public static NongPandocSliceWriteResult Write(
        NongPandocSliceWritePayload payload,
        NongPandocSliceWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(payload.OutputDirectory))
            throw new NongPandocSliceWriteException("Slice output directory is required.");

        try
        {
            var outputDir = Path.GetFullPath(payload.OutputDirectory);
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "assets"));
            if (payload.TextPreview != null)
                Directory.CreateDirectory(Path.Combine(outputDir, "preview"));

            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.Manifest), payload.Manifest, options.JsonOptions);
            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.Document), payload.Document, options.JsonOptions);
            WriteContentJsonl(Path.Combine(outputDir, NongPandocArtifactNames.ContentJsonl), payload, options.JsonlOptions);
            File.WriteAllText(Path.Combine(outputDir, NongPandocArtifactNames.ContentNongMark), payload.NongMarkText, Utf8NoBom);
            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.Structure), payload.Structure, options.JsonOptions);
            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.Format), payload.Format, options.JsonOptions);
            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.Diagnostics), payload.Diagnostics, options.JsonOptions);
            WriteJson(Path.Combine(outputDir, NongPandocArtifactNames.AssetsManifest), payload.AssetsManifest, options.JsonOptions);

            if (payload.TextPreview != null)
            {
                File.WriteAllText(Path.Combine(outputDir, NongPandocArtifactNames.TextPreview), payload.TextPreview, Utf8NoBom);
            }

            VerifyArtifacts(outputDir, options.RequiredArtifacts, options.RequireNonEmptyRequiredArtifacts);

            var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relative in EnumerateKnownArtifacts(payload.TextPreview != null))
                artifacts[relative] = Path.GetFullPath(Path.Combine(outputDir, relative));

            return new NongPandocSliceWriteResult
            {
                OutputDirectory = outputDir,
                ManifestPath = Path.GetFullPath(Path.Combine(outputDir, NongPandocArtifactNames.Manifest)),
                Artifacts = artifacts,
            };
        }
        catch (NongPandocSliceWriteException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            throw new NongPandocSliceWriteException($"Failed to write NongPandoc slice package: {ex.Message}", ex);
        }
    }

    public static void VerifyArtifacts(
        string outputDirectory,
        IEnumerable<string> requiredArtifacts,
        bool requireNonEmpty = true)
    {
        ArgumentNullException.ThrowIfNull(requiredArtifacts);
        var outputDir = Path.GetFullPath(outputDirectory);

        foreach (var relative in requiredArtifacts)
        {
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var path = Path.Combine(outputDir, relative);
            if (!File.Exists(path))
                throw new NongPandocSliceWriteException($"Required slice artifact was not created: {relative}");

            if (requireNonEmpty && IsEffectivelyEmpty(path))
                throw new NongPandocSliceWriteException($"Required slice artifact is empty: {relative}");
        }
    }

    private static void WriteJson(string path, object value, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, value.GetType(), options), Utf8NoBom);
    }

    private static void WriteContentJsonl(
        string path,
        NongPandocSliceWritePayload payload,
        JsonSerializerOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var writer = new StreamWriter(path, false, Utf8NoBom);
        if (payload.ContentJsonlLines != null)
        {
            foreach (var line in payload.ContentJsonlLines)
            {
                writer.WriteLine(line);
            }

            return;
        }

        if (payload.ContentJsonlItems == null)
            return;

        foreach (var item in payload.ContentJsonlItems)
        {
            writer.WriteLine(JsonSerializer.Serialize(item, item.GetType(), options));
        }
    }

    private static IEnumerable<string> EnumerateKnownArtifacts(bool hasTextPreview)
    {
        yield return NongPandocArtifactNames.Manifest;
        yield return NongPandocArtifactNames.Document;
        yield return NongPandocArtifactNames.ContentJsonl;
        yield return NongPandocArtifactNames.ContentNongMark;
        yield return NongPandocArtifactNames.Structure;
        yield return NongPandocArtifactNames.Format;
        yield return NongPandocArtifactNames.Diagnostics;
        yield return NongPandocArtifactNames.AssetsManifest;

        if (hasTextPreview)
            yield return NongPandocArtifactNames.TextPreview;
    }

    private static bool IsEffectivelyEmpty(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var index = HasUtf8Bom(bytes) ? 3 : 0;
        for (var i = index; i < bytes.Length; i++)
        {
            if (!char.IsWhiteSpace((char)bytes[i]))
                return false;
        }

        return true;
    }

    private static bool HasUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
}
