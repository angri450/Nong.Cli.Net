using System.Text.Json.Serialization;

namespace DocxCore;

// ===== Command 1: word outline =====

public sealed record OutlineItem(
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("styleId")] string StyleId,
    [property: JsonPropertyName("blockId")] string BlockId
);

public sealed record OutlineResult(
    [property: JsonPropertyName("items")] List<OutlineItem> Items,
    [property: JsonPropertyName("count")] int Count
);

// ===== Command 2: word images =====

public sealed record ImageInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("internalRelationshipId")] string InternalRelationshipId,
    [property: JsonPropertyName("usedBy")] List<string> UsedBy
);

public sealed record ImageListResult(
    [property: JsonPropertyName("images")] List<ImageInfo> Images,
    [property: JsonPropertyName("summary")] string Summary
);

// ===== Command 3: word comments =====

public sealed record CommentInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("anchorBlockId")] string? AnchorBlockId,
    [property: JsonPropertyName("anchorText")] string? AnchorText
);

public sealed record CommentListResult(
    [property: JsonPropertyName("comments")] List<CommentInfo> Comments,
    [property: JsonPropertyName("summary")] string Summary
);

// ===== Command 4: word revisions =====

public sealed record RevisionSnippet(
    [property: JsonPropertyName("revisionId")] string RevisionId,
    [property: JsonPropertyName("blockId")] string BlockId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("date")] string Date
);

public sealed record RevisionListResult(
    [property: JsonPropertyName("totalRevisions")] int TotalRevisions,
    [property: JsonPropertyName("insertions")] int Insertions,
    [property: JsonPropertyName("deletions")] int Deletions,
    [property: JsonPropertyName("moves")] int Moves,
    [property: JsonPropertyName("snippets")] List<RevisionSnippet> Snippets
);

// ===== Command 5: word infer-format =====

public sealed record InferFormatResult(
    [property: JsonPropertyName("fontFamily")] string? FontFamily,
    [property: JsonPropertyName("fontSize")] string? FontSize,
    [property: JsonPropertyName("alignment")] string? Alignment,
    [property: JsonPropertyName("lineSpacing")] string? LineSpacing,
    [property: JsonPropertyName("lineRule")] string? LineRule,
    [property: JsonPropertyName("firstLineIndent")] string? FirstLineIndent,
    [property: JsonPropertyName("warnings")] List<string> Warnings
);
