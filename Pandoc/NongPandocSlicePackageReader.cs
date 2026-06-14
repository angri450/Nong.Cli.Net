using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandocCore;

public sealed record NongPandocSliceReadOptions
{
    public string ExpectedSchemaVersion { get; init; } = "nong-pandoc/package/v1";

    public JsonSerializerOptions JsonOptions { get; init; } = NongPandocSlicePackageReader.DefaultJsonOptions;

    public IReadOnlyList<string> RequiredStreams { get; init; } = NongPandocSlicePackageReader.DefaultRequiredStreams;

    public bool RequireNonEmptyRequiredStreams { get; init; } = true;

    public bool StrictEvidence { get; init; }
}

public sealed record NongPandocSliceReadResult
{
    public string Directory { get; init; } = "";

    public NongPandocSliceManifest Manifest { get; init; } = new();

    public Dictionary<string, string> Artifacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AiReadOrder { get; init; } = Array.Empty<string>();

    public string ContentNongMark { get; init; } = "";

    public IReadOnlyList<JsonDocument> ContentBlocks { get; init; } = Array.Empty<JsonDocument>();

    public JsonDocument Structure { get; init; } = JsonDocument.Parse("{}");

    public JsonDocument Format { get; init; } = JsonDocument.Parse("{}");

    public JsonDocument Diagnostics { get; init; } = JsonDocument.Parse("{}");

    public JsonDocument Assets { get; init; } = JsonDocument.Parse("""{"items":[]}""");

    public string? TextPreview { get; init; }

    public NongPandocEvidenceValidationResult EvidenceValidation { get; init; } = new();

    public NongPandocSliceSummary Summary { get; init; } = new();
}

public sealed record NongPandocSliceSummary
{
    [JsonPropertyName("source")]
    public NongPandocSourceInfo Source { get; init; } = new();

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "";

    [JsonPropertyName("streams")]
    public NongPandocStreamPaths Streams { get; init; } = NongPandocStreamPaths.Default;

    [JsonPropertyName("metrics")]
    public NongPandocMetrics Metrics { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();

    [JsonPropertyName("aiReadOrder")]
    public IReadOnlyList<string> AiReadOrder { get; init; } = Array.Empty<string>();

    [JsonPropertyName("artifacts")]
    public Dictionary<string, string> Artifacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("previewAvailable")]
    public bool PreviewAvailable { get; init; }

    [JsonPropertyName("evidence")]
    public NongPandocEvidenceValidationResult Evidence { get; init; } = new();
}

public sealed record NongPandocEvidenceValidationResult
{
    [JsonPropertyName("strict")]
    public bool Strict { get; init; }

    [JsonPropertyName("valid")]
    public bool Valid => Errors.Count == 0;

    [JsonPropertyName("checkedBlocks")]
    public int CheckedBlocks { get; init; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();
}

public sealed class NongPandocSliceReadException : Exception
{
    public NongPandocSliceReadException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public static class NongPandocSlicePackageReader
{
    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static readonly IReadOnlyList<string> DefaultRequiredStreams =
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

    public static readonly IReadOnlyList<string> AiPrimaryReadOrder =
    [
        NongPandocArtifactNames.ContentNongMark,
        NongPandocArtifactNames.Structure,
        NongPandocArtifactNames.Format,
        NongPandocArtifactNames.Diagnostics,
    ];

    public static NongPandocSliceReadResult Read(string sliceDirectory) =>
        Read(sliceDirectory, new NongPandocSliceReadOptions());

    public static NongPandocSliceReadResult Read(
        string sliceDirectory,
        NongPandocSliceReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(sliceDirectory))
            throw new NongPandocSliceReadException("Slice directory is required.");

        try
        {
            var root = Path.GetFullPath(sliceDirectory);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Slice directory not found: {sliceDirectory}");

            var manifestPath = Path.Combine(root, NongPandocArtifactNames.Manifest);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Slice manifest was not found.", manifestPath);

            var manifest = ReadJson<NongPandocSliceManifest>(manifestPath, options.JsonOptions)
                ?? throw new NongPandocSliceReadException("Slice manifest is empty or invalid.");

            if (!string.Equals(manifest.SchemaVersion, options.ExpectedSchemaVersion, StringComparison.Ordinal))
            {
                throw new NongPandocSliceReadException(
                    $"Unsupported slice schemaVersion '{manifest.SchemaVersion}'. Expected '{options.ExpectedSchemaVersion}'.");
            }

            var artifacts = BuildArtifactMap(root, manifest.Streams);
            VerifyRequiredStreams(root, artifacts, options.RequiredStreams, options.RequireNonEmptyRequiredStreams);

            var contentNongMark = File.ReadAllText(artifacts[NongPandocArtifactNames.ContentNongMark]);
            var contentBlocks = ReadJsonlDocuments(artifacts[NongPandocArtifactNames.ContentJsonl], options.JsonOptions);
            var structure = ReadJsonDocument(artifacts[NongPandocArtifactNames.Structure], options.JsonOptions);
            var format = ReadJsonDocument(artifacts[NongPandocArtifactNames.Format], options.JsonOptions);
            var diagnostics = ReadJsonDocument(artifacts[NongPandocArtifactNames.Diagnostics], options.JsonOptions);
            var assets = ReadJsonDocument(artifacts[NongPandocArtifactNames.AssetsManifest], options.JsonOptions);

            var evidence = ValidateEvidence(manifest.Source.Format, structure.RootElement, options.StrictEvidence);
            if (options.StrictEvidence && !evidence.Valid)
                throw new NongPandocSliceReadException("Slice evidence contract failed: " + string.Join("; ", evidence.Errors));

            var preview = artifacts.TryGetValue(NongPandocArtifactNames.TextPreview, out var previewPath) && File.Exists(previewPath)
                ? File.ReadAllText(previewPath)
                : null;

            var summary = new NongPandocSliceSummary
            {
                Source = manifest.Source,
                SchemaVersion = manifest.SchemaVersion,
                Streams = manifest.Streams,
                Metrics = manifest.Metrics,
                Warnings = manifest.Warnings,
                AiReadOrder = AiPrimaryReadOrder,
                Artifacts = artifacts,
                PreviewAvailable = preview != null,
                Evidence = evidence,
            };

            return new NongPandocSliceReadResult
            {
                Directory = root,
                Manifest = manifest,
                Artifacts = artifacts,
                AiReadOrder = AiPrimaryReadOrder,
                ContentNongMark = contentNongMark,
                ContentBlocks = contentBlocks,
                Structure = structure,
                Format = format,
                Diagnostics = diagnostics,
                Assets = assets,
                TextPreview = preview,
                EvidenceValidation = evidence,
                Summary = summary,
            };
        }
        catch (NongPandocSliceReadException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            throw new NongPandocSliceReadException($"Failed to read NongPandoc slice package: {ex.Message}", ex);
        }
    }

    public static void VerifyRequiredStreams(
        string sliceDirectory,
        IReadOnlyDictionary<string, string> artifacts,
        IEnumerable<string> requiredStreams,
        bool requireNonEmpty = true)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(requiredStreams);

        _ = Path.GetFullPath(sliceDirectory);

        foreach (var streamName in requiredStreams)
        {
            if (string.IsNullOrWhiteSpace(streamName))
                continue;

            if (!artifacts.TryGetValue(streamName, out var path) || string.IsNullOrWhiteSpace(path))
                throw new NongPandocSliceReadException($"Required slice stream is missing from manifest: {streamName}");

            if (!File.Exists(path))
                throw new NongPandocSliceReadException($"Required slice artifact was not found: {streamName}");

            if (requireNonEmpty && IsEffectivelyEmpty(path))
                throw new NongPandocSliceReadException($"Required slice artifact is empty: {streamName}");
        }
    }

    private static Dictionary<string, string> BuildArtifactMap(string root, NongPandocStreamPaths streams)
    {
        var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NongPandocArtifactNames.Manifest] = Path.Combine(root, NongPandocArtifactNames.Manifest),
            [NongPandocArtifactNames.Document] = Resolve(root, streams.Document),
            [NongPandocArtifactNames.ContentJsonl] = Resolve(root, streams.ContentJsonl),
            [NongPandocArtifactNames.ContentNongMark] = Resolve(root, streams.ContentNongMark),
            [NongPandocArtifactNames.Structure] = Resolve(root, streams.Structure),
            [NongPandocArtifactNames.Format] = Resolve(root, streams.Format),
            [NongPandocArtifactNames.Diagnostics] = Resolve(root, streams.Diagnostics),
            [NongPandocArtifactNames.AssetsManifest] = Resolve(root, streams.Assets),
            [NongPandocArtifactNames.TextPreview] = Resolve(root, streams.TextPreview),
        };

        return artifacts;
    }

    private static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            throw new NongPandocSliceReadException("Slice stream path is empty.");

        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new NongPandocSliceReadException($"Slice stream path escapes package directory: {relative}");
        }

        return fullPath;
    }

    private static T? ReadJson<T>(string path, JsonSerializerOptions options)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, options);
    }

    private static JsonDocument ReadJsonDocument(string path, JsonSerializerOptions options)
    {
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling,
        });
    }

    private static List<JsonDocument> ReadJsonlDocuments(string path, JsonSerializerOptions options)
    {
        var documents = new List<JsonDocument>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            documents.Add(JsonDocument.Parse(line, new JsonDocumentOptions
            {
                AllowTrailingCommas = options.AllowTrailingCommas,
                CommentHandling = options.ReadCommentHandling,
            }));
        }

        return documents;
    }

    public static NongPandocEvidenceValidationResult ValidateEvidence(
        string format,
        JsonElement structureRoot,
        bool strict)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var checkedBlocks = 0;

        if (!structureRoot.TryGetProperty("blockIndex", out var blockIndex)
            || blockIndex.ValueKind != JsonValueKind.Object)
        {
            errors.Add("structure.blockIndex is missing or not an object.");
            return new NongPandocEvidenceValidationResult
            {
                Strict = strict,
                CheckedBlocks = 0,
                Errors = errors,
                Warnings = warnings,
            };
        }

        foreach (var block in blockIndex.EnumerateObject())
        {
            checkedBlocks++;
            if (!block.Value.TryGetProperty("provenance", out var provenance)
                || provenance.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{block.Name}: provenance is missing.");
                continue;
            }

            var actualFormat = GetString(provenance, "format");
            if (string.IsNullOrWhiteSpace(actualFormat))
                errors.Add($"{block.Name}: provenance.format is missing.");
            else if (!string.IsNullOrWhiteSpace(format)
                && !string.Equals(actualFormat, format, StringComparison.OrdinalIgnoreCase))
                errors.Add($"{block.Name}: provenance.format '{actualFormat}' does not match package format '{format}'.");

            if (!HasProperty(provenance, "position"))
                errors.Add($"{block.Name}: provenance.position is missing.");

            if (string.IsNullOrWhiteSpace(GetString(provenance, "source")))
                errors.Add($"{block.Name}: provenance.source is missing.");

            switch (format.ToLowerInvariant())
            {
                case "pdf":
                    if (!HasProperty(provenance, "page"))
                        errors.Add($"{block.Name}: PDF provenance.page is missing.");
                    if (!HasArray(provenance, "bbox", 4))
                        errors.Add($"{block.Name}: PDF provenance.bbox must contain four values.");
                    break;
                case "xlsx":
                    if (string.IsNullOrWhiteSpace(GetString(provenance, "sheet")))
                        errors.Add($"{block.Name}: Excel provenance.sheet is missing.");
                    if (string.IsNullOrWhiteSpace(GetString(provenance, "address")))
                        errors.Add($"{block.Name}: Excel provenance.address is missing.");
                    break;
                case "pptx":
                    if (!HasProperty(provenance, "slide"))
                        errors.Add($"{block.Name}: PPTX provenance.slide is missing.");
                    if (!provenance.TryGetProperty("layout", out var layout) || layout.ValueKind != JsonValueKind.Object)
                        warnings.Add($"{block.Name}: PPTX provenance.layout is missing.");
                    break;
                case "docx":
                    if (!HasProperty(provenance, "position"))
                        errors.Add($"{block.Name}: Word provenance.position is missing.");
                    break;
            }
        }

        if (checkedBlocks == 0)
            errors.Add("structure.blockIndex is empty.");

        return new NongPandocEvidenceValidationResult
        {
            Strict = strict,
            CheckedBlocks = checkedBlocks,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private static bool HasProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static bool HasArray(JsonElement element, string propertyName, int expectedLength) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Array
        && value.GetArrayLength() == expectedLength;

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
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
