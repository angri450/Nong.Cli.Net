namespace DocxCore;

/// <summary>Stable ID allocator and OpenXML anchor map for Word documents.</summary>
/// <remarks>
/// Public ID prefixes:
///   p0001+ (paragraph), h0001+ (heading), t0001+ (table),
///   img0001+ (image), m0001+ (math), f0001+ (footnote),
///   e0001+ (endnote), c0001+ (comment), rev0001+ (revision),
///   bm0001+ (bookmark), link0001+ (hyperlink)
/// Table rows/cols: tr0001+ / tc0001+ (independent counters).
/// OpenXML rId is NEVER exposed as a public id.
/// Same docx sliced twice produces the same IDs.
/// </remarks>
public class WordBlockIdMap
{
    // Counters per prefix
    readonly Dictionary<string, int> _counters = new();

    /// <summary>Generate next stable ID for a prefix using a local counter reference.</summary>
    public static string NextId(string prefix, ref int counter) => $"{prefix}{++counter:D4}";

    // Public ID -> OpenXML anchor info
    readonly Dictionary<string, AnchorInfo> _anchors = new();

    /// <summary>Record anchor metadata for a public block ID.</summary>
    public void Track(string publicId, string elementType, int position, string? internalRId = null)
    {
        _anchors[publicId] = new AnchorInfo(publicId, elementType, position, internalRId);
    }

    /// <summary>Allocate the next sequential ID for a given block-type prefix.</summary>
    public string AllocateId(string prefix)
    {
        if (!_counters.ContainsKey(prefix))
            _counters[prefix] = 0;

        _counters[prefix]++;
        return $"{prefix}{_counters[prefix]:D4}";
    }

    /// <summary>Check whether a public ID has been tracked.</summary>
    public bool IsValidId(string id) => _anchors.ContainsKey(id);

    /// <summary>Retrieve anchor metadata for a tracked public ID.</summary>
    public AnchorInfo? GetAnchor(string id) =>
        _anchors.TryGetValue(id, out var a) ? a : null;
}

/// <summary>Maps a public block ID back to its OpenXML element origin.</summary>
public sealed record AnchorInfo(
    string PublicId,
    string ElementType,
    int Position,
    string? InternalRId
);
