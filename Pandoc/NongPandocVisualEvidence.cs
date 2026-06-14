using System.Text.Json.Serialization;

namespace PandocCore;

public sealed record NongPandocVisualEvidence
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("fonts")]
    public List<string> Fonts { get; init; } = new();

    [JsonPropertyName("headings")]
    public List<string> Headings { get; init; } = new();

    [JsonPropertyName("body")]
    public List<string> Body { get; init; } = new();

    [JsonPropertyName("lineSpacing")]
    public List<string> LineSpacing { get; init; } = new();

    [JsonPropertyName("tables")]
    public List<string> Tables { get; init; } = new();

    [JsonPropertyName("latinNames")]
    public List<string> LatinNames { get; init; } = new();

    [JsonPropertyName("chemistry")]
    public List<string> Chemistry { get; init; } = new();

    [JsonPropertyName("audit")]
    public Dictionary<string, string> Audit { get; init; } = new();

    [JsonPropertyName("layout")]
    public List<string> Layout { get; init; } = new();

    [JsonPropertyName("assets")]
    public List<string> Assets { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();
}
