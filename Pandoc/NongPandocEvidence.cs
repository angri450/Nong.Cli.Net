using System.Text.Json.Serialization;

namespace PandocCore;

public sealed record NongPandocBlockProvenance
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("page")]
    public int? Page { get; init; }

    [JsonPropertyName("sheet")]
    public string? Sheet { get; init; }

    [JsonPropertyName("slide")]
    public int? Slide { get; init; }

    [JsonPropertyName("position")]
    public int? Position { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("bbox")]
    public double[]? Bbox { get; init; }

    [JsonPropertyName("layout")]
    public NongPandocLayoutEvidence? Layout { get; init; }

    [JsonPropertyName("assetId")]
    public string? AssetId { get; init; }

    [JsonPropertyName("relationshipId")]
    public string? RelationshipId { get; init; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }

    [JsonPropertyName("notes")]
    public List<string>? Notes { get; init; }
}

public sealed record NongPandocLayoutEvidence
{
    [JsonPropertyName("x")]
    public double? X { get; init; }

    [JsonPropertyName("y")]
    public double? Y { get; init; }

    [JsonPropertyName("width")]
    public double? Width { get; init; }

    [JsonPropertyName("height")]
    public double? Height { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "";
}
