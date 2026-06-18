namespace Angri450.Nong.Data;

/// <summary>
/// Shared ingestion helper for --ingest flag across all command groups.
/// Writes searchable text blocks directly to NongDb.Blocks so nong search can find them.
/// Uses a virtual document ID (hash prefix) instead of RegisterDocument (which requires a real file).
/// </summary>
public static class IngestHelper
{
    /// <summary>
    /// Ingest one piece of text as a searchable block.
    /// </summary>
    public static void IngestText(string text, string source, string category, string? subcategory = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        using var ctx = new IngestionContext();
        var docId = DocId(source);
        ctx.Db.Blocks.Insert(new DbBlock
        {
            DocumentId = docId,
            BlockId = RandomId(),
            BlockType = subcategory ?? category,
            Text = text,
            Index = 0,
            Json = Serialize(new { text, source, category, ingestedAt = DateTime.UtcNow })
        });
        ctx.Db.Blocks.EnsureIndex(b => b.DocumentId);
        ctx.Db.Blocks.EnsureIndex(b => b.BlockType);
    }

    /// <summary>
    /// Ingest multiple text items as searchable blocks.
    /// </summary>
    public static int IngestTexts(IEnumerable<string> texts, string source, string category, string? subcategory = null)
    {
        var items = (texts ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (items.Count == 0) return 0;

        using var ctx = new IngestionContext();
        var docId = DocId(source);
        int idx = 0;
        foreach (var text in items)
        {
            ctx.Db.Blocks.Insert(new DbBlock
            {
                DocumentId = docId,
                BlockId = RandomId(),
                BlockType = subcategory ?? category,
                Text = text,
                Index = idx++,
                Json = Serialize(new { text, source, category, ingestedAt = DateTime.UtcNow })
            });
        }
        ctx.Db.Blocks.EnsureIndex(b => b.DocumentId);
        ctx.Db.Blocks.EnsureIndex(b => b.BlockType);
        return items.Count;
    }

    /// <summary>
    /// Ingest literature search results as text blocks (title + abstract) for nong search.
    /// Also writes Papers for cache-query/cache-export.
    /// </summary>
    public static int IngestPapers(IEnumerable<DbPaper> papers, string query, string provider)
    {
        var list = (papers ?? Array.Empty<DbPaper>()).ToList();
        if (list.Count == 0) return 0;

        using var ctx = new IngestionContext();
        var hash = Hash(query);
        var docId = $"lit-{hash}";

        ctx.Db.RegisterLiteratureList(hash, query, provider, list.Count);
        ctx.Db.ImportPapers(list);

        var listObj = ctx.Db.FindLiteratureList(hash);
        var listId = listObj?.Id.ToString() ?? hash;
        foreach (var paper in ctx.Db.FindPapersByHash(hash))
            ctx.Db.Link("literature-list", listId, "contains", "paper", paper.Id.ToString());

        // Write searchable text blocks
        int idx = 0;
        foreach (var paper in list)
        {
            var text = new[] { paper.Title, paper.Abstract }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .DefaultIfEmpty(paper.Title)
                .Aggregate((a, b) => $"{a}\n{b}")
                .Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            ctx.Db.Blocks.Insert(new DbBlock
            {
                DocumentId = docId,
                BlockId = RandomId(),
                BlockType = "paper",
                Text = text,
                Index = idx++,
                Json = Serialize(new { title = paper.Title, doi = paper.NormalizedDoi, authors = paper.Authors, year = paper.Year, source = provider, ingestedAt = DateTime.UtcNow })
            });
        }
        ctx.Db.Blocks.EnsureIndex(b => b.DocumentId);
        ctx.Db.Blocks.EnsureIndex(b => b.BlockType);
        return list.Count;
    }

    static string Hash(string s) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(s)))[..12];

    static string DocId(string source) => $"ingest-{Hash(source)}";
    static string RandomId() => Guid.NewGuid().ToString("N")[..12];
    static string Serialize(object o) => System.Text.Json.JsonSerializer.Serialize(o);
}
