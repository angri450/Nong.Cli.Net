using LiteDB;

namespace Angri450.Nong.Data;

/// <summary>
/// Unified Nong database interface. All document-related data:
/// documents, blocks, formats, assets, structures, outputs.
/// Literature papers have their own storage contract — see ILiteratureCache.
/// </summary>
public interface INongDb : IDisposable
{
    ILiteCollection<DbDocument> Documents { get; }
    ILiteCollection<DbBlock> Blocks { get; }
    ILiteCollection<DbAsset> Assets { get; }
    ILiteCollection<DbFormat> Formats { get; }
    ILiteCollection<DbStructure> Structures { get; }
    ILiteCollection<DbOutput> Outputs { get; }

    LiteDatabase Raw { get; }

    DbDocument RegisterDocument(string filePath);
    DbDocument ImportSlice(string docxPath, string sliceDir);
    void TrackOutput(string filePath, string generator, string? sourceDocId = null);
    IReadOnlyList<DbDocument> FindDocuments(string? format = null);
    IReadOnlyList<DbBlock> GetBlocks(string documentId);
    IReadOnlyList<DbAsset> GetImages(string documentId);
    string? GetFormat(string documentId);
}
