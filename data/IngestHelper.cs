namespace Angri450.Nong.Data;

/// <summary>
/// Shared ingestion helper for --ingest flag across all command groups.
/// Converts unstructured command output into searchable NongDb blocks.
/// </summary>
public static class IngestHelper
{
    /// <summary>
    /// Ingest one piece of text as a searchable block.
    /// Source is the source document/file name, category is the command group.
    /// </summary>
    public static string IngestText(string text, string source, string category, string? subcategory = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        using var ctx = new IngestionContext();
        var doc = ctx.Db.RegisterDocument(source);
        var docId = doc.Id.ToString();

        var block = new DbBlock
        {
            DocumentId = docId,
            BlockId = Guid.NewGuid().ToString("N")[..12],
            BlockType = subcategory ?? category,
            Text = text,
            Index = 0,
            Json = System.Text.Json.JsonSerializer.Serialize(new { text, source, category, ingestedAt = DateTime.UtcNow })
        };
        ctx.Db.Blocks.Insert(block);
        ctx.Db.Blocks.EnsureIndex(b => b.DocumentId);
        ctx.Db.Blocks.EnsureIndex(b => b.BlockType);

        return block.Id.ToString();
    }

    /// <summary>
    /// Ingest multiple text items as searchable blocks.
    /// </summary>
    public static int IngestTexts(IEnumerable<string> texts, string source, string category, string? subcategory = null)
    {
        if (texts == null) return 0;
        var items = texts.ToList();
        if (items.Count == 0) return 0;

        using var ctx = new IngestionContext();
        var doc = ctx.Db.RegisterDocument(source);
        var docId = doc.Id.ToString();

        int count = 0;
        foreach (var text in items)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            ctx.Db.Blocks.Insert(new DbBlock
            {
                DocumentId = docId,
                BlockId = Guid.NewGuid().ToString("N")[..12],
                BlockType = subcategory ?? category,
                Text = text,
                Index = count,
                Json = System.Text.Json.JsonSerializer.Serialize(new { text, source, category, ingestedAt = DateTime.UtcNow })
            });
            count++;
        }

        ctx.Db.Blocks.EnsureIndex(b => b.DocumentId);
        ctx.Db.Blocks.EnsureIndex(b => b.BlockType);
        return count;
    }

    /// <summary>
    /// Ingest literature search results as Papers (delegates to existing cache-import pipeline).
    /// </summary>
    public static int IngestPapers(IEnumerable<DbPaper> papers, string query, string provider)
    {
        if (papers == null) return 0;
        var list = papers.ToList();
        if (list.Count == 0) return 0;

        using var ctx = new IngestionContext();
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(query)))[..12];

        ctx.Db.RegisterLiteratureList(hash, query, provider, list.Count);
        ctx.Db.ImportPapers(list);

        foreach (var paper in ctx.Db.FindPapersByHash(hash))
            ctx.Db.Link("literature-list", hash, "contains", "paper", paper.Id.ToString());

        return list.Count;
    }
}
