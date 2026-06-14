using System.Text.Json.Serialization;

namespace PandocCore;

/// <summary>
/// Nong's Pandoc-style canonical document model. It is semantic structure,
/// not a rich-text or OOXML mirror.
/// </summary>
public sealed record NongPandocDocument
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "nong-pandoc/v1";

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("blocks")]
    public List<NongPandocBlock> Blocks { get; init; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NongParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(NongHeadingBlock), "heading")]
[JsonDerivedType(typeof(NongBlockQuoteBlock), "blockQuote")]
[JsonDerivedType(typeof(NongBulletListBlock), "bulletList")]
[JsonDerivedType(typeof(NongOrderedListBlock), "orderedList")]
[JsonDerivedType(typeof(NongTableBlock), "table")]
[JsonDerivedType(typeof(NongFigureBlock), "figure")]
[JsonDerivedType(typeof(NongReferencesBlock), "references")]
[JsonDerivedType(typeof(NongRawBlock), "raw")]
public abstract record NongPandocBlock
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record NongParagraphBlock : NongPandocBlock
{
    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();

    public static NongParagraphBlock FromText(string text) => new()
    {
        Inlines = new List<NongPandocInline> { NongPandocInline.TextRun(text) }
    };
}

public sealed record NongHeadingBlock : NongPandocBlock
{
    [JsonPropertyName("level")]
    public int Level { get; init; } = 1;

    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();

    public static NongHeadingBlock FromText(int level, string text, string? id = null) => new()
    {
        Level = Math.Clamp(level, 1, 6),
        Id = id,
        Inlines = new List<NongPandocInline> { NongPandocInline.TextRun(text) }
    };
}

public sealed record NongBlockQuoteBlock : NongPandocBlock
{
    [JsonPropertyName("blocks")]
    public List<NongPandocBlock> Blocks { get; init; } = new();
}

public sealed record NongBulletListBlock : NongPandocBlock
{
    [JsonPropertyName("items")]
    public List<List<NongPandocBlock>> Items { get; init; } = new();
}

public sealed record NongOrderedListBlock : NongPandocBlock
{
    [JsonPropertyName("start")]
    public int Start { get; init; } = 1;

    [JsonPropertyName("items")]
    public List<List<NongPandocBlock>> Items { get; init; } = new();
}

public sealed record NongTableBlock : NongPandocBlock
{
    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("style")]
    public string? Style { get; init; }

    [JsonPropertyName("headers")]
    public List<string> Headers { get; init; } = new();

    [JsonPropertyName("rows")]
    public List<List<string>> Rows { get; init; } = new();
}

public sealed record NongFigureBlock : NongPandocBlock
{
    [JsonPropertyName("src")]
    public string Source { get; init; } = "";

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("alt")]
    public string? AltText { get; init; }
}

public sealed record NongReferencesBlock : NongPandocBlock
{
    [JsonPropertyName("entries")]
    public List<string> Entries { get; init; } = new();
}

public sealed record NongRawBlock : NongPandocBlock
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NongTextInline), "text")]
[JsonDerivedType(typeof(NongEmphasisInline), "emphasis")]
[JsonDerivedType(typeof(NongStrongInline), "strong")]
[JsonDerivedType(typeof(NongCodeInline), "code")]
[JsonDerivedType(typeof(NongSuperscriptInline), "superscript")]
[JsonDerivedType(typeof(NongSubscriptInline), "subscript")]
[JsonDerivedType(typeof(NongLinkInline), "link")]
[JsonDerivedType(typeof(NongRawInline), "raw")]
public abstract record NongPandocInline
{
    public static NongTextInline TextRun(string text) => new() { Text = text };
}

public sealed record NongTextInline : NongPandocInline
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed record NongEmphasisInline : NongPandocInline
{
    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();
}

public sealed record NongStrongInline : NongPandocInline
{
    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();
}

public sealed record NongCodeInline : NongPandocInline
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "";
}

public sealed record NongSuperscriptInline : NongPandocInline
{
    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();
}

public sealed record NongSubscriptInline : NongPandocInline
{
    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();
}

public sealed record NongLinkInline : NongPandocInline
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("inlines")]
    public List<NongPandocInline> Inlines { get; init; } = new();
}

public sealed record NongRawInline : NongPandocInline
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public static class NongPandocRuntimePolicy
{
    public const bool UsesBundledPandoc = false;
    public const bool RequiresExternalPandocExecutable = false;
    public const string LicenseBoundary = "Apache-2.0 pure .NET core; Pandoc CLI interop must live in an optional bridge.";
}
