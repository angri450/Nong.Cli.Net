using System.Text.Json.Serialization;

namespace PandocCore;

public static class NongPandocArtifactNames
{
    public const string Manifest = "manifest.json";
    public const string Document = "document.json";
    public const string ContentJsonl = "content.jsonl";
    public const string ContentNongMark = "content.nongmark";
    public const string Structure = "structure.json";
    public const string Format = "format.json";
    public const string Diagnostics = "diagnostics.json";
    public const string AssetsManifest = "assets/manifest.json";
    public const string TextPreview = "preview/content.txt";
}

public sealed record NongPandocSliceManifest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "nong-pandoc/package/v1";

    [JsonPropertyName("source")]
    public NongPandocSourceInfo Source { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("streams")]
    public NongPandocStreamPaths Streams { get; init; } = NongPandocStreamPaths.Default;

    [JsonPropertyName("metrics")]
    public NongPandocMetrics Metrics { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();
}

public sealed record NongPandocSourceInfo
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("format")]
    public string Format { get; init; } = "unknown";

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("pageCount")]
    public int? PageCount { get; init; }

    [JsonPropertyName("sheetCount")]
    public int? SheetCount { get; init; }

    [JsonPropertyName("slideCount")]
    public int? SlideCount { get; init; }
}

public sealed record NongPandocStreamPaths
{
    public static NongPandocStreamPaths Default { get; } = new();

    [JsonPropertyName("document")]
    public string Document { get; init; } = NongPandocArtifactNames.Document;

    [JsonPropertyName("contentJsonl")]
    public string ContentJsonl { get; init; } = NongPandocArtifactNames.ContentJsonl;

    [JsonPropertyName("contentNongMark")]
    public string ContentNongMark { get; init; } = NongPandocArtifactNames.ContentNongMark;

    [JsonPropertyName("structure")]
    public string Structure { get; init; } = NongPandocArtifactNames.Structure;

    [JsonPropertyName("format")]
    public string Format { get; init; } = NongPandocArtifactNames.Format;

    [JsonPropertyName("diagnostics")]
    public string Diagnostics { get; init; } = NongPandocArtifactNames.Diagnostics;

    [JsonPropertyName("assets")]
    public string Assets { get; init; } = NongPandocArtifactNames.AssetsManifest;

    [JsonPropertyName("textPreview")]
    public string TextPreview { get; init; } = NongPandocArtifactNames.TextPreview;
}

public sealed record NongPandocMetrics
{
    [JsonPropertyName("blocks")]
    public int Blocks { get; init; }

    [JsonPropertyName("paragraphs")]
    public int Paragraphs { get; init; }

    [JsonPropertyName("headings")]
    public int Headings { get; init; }

    [JsonPropertyName("tables")]
    public int Tables { get; init; }

    [JsonPropertyName("figures")]
    public int Figures { get; init; }

    [JsonPropertyName("images")]
    public int Images { get; init; }

    [JsonPropertyName("references")]
    public int References { get; init; }

    [JsonPropertyName("warnings")]
    public int Warnings { get; init; }

    public static NongPandocMetrics FromDocument(NongPandocDocument document, int warnings = 0)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new NongPandocMetrics
        {
            Blocks = document.Blocks.Count,
            Paragraphs = document.Blocks.OfType<NongParagraphBlock>().Count(),
            Headings = document.Blocks.OfType<NongHeadingBlock>().Count(),
            Tables = document.Blocks.OfType<NongTableBlock>().Count(),
            Figures = document.Blocks.OfType<NongFigureBlock>().Count(),
            Images = document.Blocks.OfType<NongFigureBlock>().Count(f => !string.IsNullOrWhiteSpace(f.Source)),
            References = document.Blocks.OfType<NongReferencesBlock>().Sum(r => r.Entries.Count),
            Warnings = warnings
        };
    }
}

public sealed record NongPandocPackageContract
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "nong-pandoc/contract/v1";

    [JsonPropertyName("requiredStreams")]
    public List<string> RequiredStreams { get; init; } = new()
    {
        NongPandocArtifactNames.Manifest,
        NongPandocArtifactNames.Document,
        NongPandocArtifactNames.ContentJsonl,
        NongPandocArtifactNames.ContentNongMark,
        NongPandocArtifactNames.Structure,
        NongPandocArtifactNames.Format,
        NongPandocArtifactNames.Diagnostics,
        NongPandocArtifactNames.AssetsManifest,
    };

    [JsonPropertyName("optionalStreams")]
    public List<string> OptionalStreams { get; init; } = new()
    {
        NongPandocArtifactNames.TextPreview,
    };

    [JsonPropertyName("aiPrimaryReadOrder")]
    public List<string> AiPrimaryReadOrder { get; init; } = new()
    {
        NongPandocArtifactNames.ContentNongMark,
        NongPandocArtifactNames.Structure,
        NongPandocArtifactNames.Format,
        NongPandocArtifactNames.Diagnostics,
    };
}
