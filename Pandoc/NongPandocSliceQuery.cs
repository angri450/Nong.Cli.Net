using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandocCore;

public sealed record NongPandocSliceBlockView
{
    public string BlockId { get; init; } = "";
    public JsonNode? Content { get; init; }
    public JsonNode? Structure { get; init; }
    public JsonNode? Format { get; init; }
    public JsonNode? Diagnostics { get; init; }
    public List<JsonNode?> Assets { get; init; } = new();
}

public static class NongPandocSliceQuery
{
    public static List<JsonNode?> Blocks(NongPandocSliceReadResult slice) =>
        slice.ContentBlocks
            .Select(d => Clone(d.RootElement))
            .ToList();

    public static NongPandocSliceBlockView Block(NongPandocSliceReadResult slice, string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block id is required.", nameof(blockId));

        var content = slice.ContentBlocks
            .Select(d => d.RootElement)
            .FirstOrDefault(e => MatchesBlockId(e, blockId));

        var structure = TryGetBlockIndexEntry(slice.Structure.RootElement, blockId);
        var assets = AssetsForBlock(slice.Assets.RootElement, blockId);

        return new NongPandocSliceBlockView
        {
            BlockId = blockId,
            Content = content.ValueKind == JsonValueKind.Undefined ? null : Clone(content),
            Structure = structure,
            Format = Clone(slice.Format.RootElement),
            Diagnostics = Clone(slice.Diagnostics.RootElement),
            Assets = assets,
        };
    }

    public static List<JsonNode?> Assets(NongPandocSliceReadResult slice)
    {
        if (!slice.Assets.RootElement.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
            return new List<JsonNode?>();

        return items.EnumerateArray().Select(Clone).ToList();
    }

    private static bool MatchesBlockId(JsonElement element, string blockId)
    {
        if (TryGetString(element, "blockId", out var actual) && string.Equals(actual, blockId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (TryGetString(element, "id", out actual) && string.Equals(actual, blockId, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static JsonNode? TryGetBlockIndexEntry(JsonElement structureRoot, string blockId)
    {
        if (!structureRoot.TryGetProperty("blockIndex", out var blockIndex)
            || blockIndex.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var entry in blockIndex.EnumerateObject())
        {
            if (string.Equals(entry.Name, blockId, StringComparison.OrdinalIgnoreCase))
                return Clone(entry.Value);
        }

        return null;
    }

    private static List<JsonNode?> AssetsForBlock(JsonElement assetsRoot, string blockId)
    {
        if (!assetsRoot.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
            return new List<JsonNode?>();

        var result = new List<JsonNode?>();
        foreach (var item in items.EnumerateArray())
        {
            if (AssetReferencesBlock(item, blockId))
                result.Add(Clone(item));
        }

        return result;
    }

    private static bool AssetReferencesBlock(JsonElement item, string blockId)
    {
        if (TryGetString(item, "id", out var id) && string.Equals(id, blockId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (TryGetString(item, "name", out var name) && string.Equals(name, blockId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (TryGetString(item, "shapeId", out var shapeId) && string.Equals(shapeId, blockId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (item.TryGetProperty("usedBy", out var usedBy) && usedBy.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in usedBy.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String
                    && string.Equals(value.GetString(), blockId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = "";
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? "";
        return value.Length > 0;
    }

    private static JsonNode? Clone(JsonElement element) => JsonNode.Parse(element.GetRawText());
}
