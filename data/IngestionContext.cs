using LiteDB;

namespace Angri450.Nong.Data;

/// <summary>
/// High-level ingestion context wrapping NongDb with unified semantics.
/// 
/// Provides a consistent API across word/pdf/lit commands for:
/// - Document registration and slice import (one-cut three-flow)
/// - Automatic provenance tracking (BeginRun/FinishRun)
/// - Automatic relationship creation (document → blocks, document → assets)
/// - Unified query patterns
/// 
/// Stage D of unified-nongdb master plan: align command surfaces to this path.
/// </summary>
public sealed class IngestionContext : IDisposable
{
    readonly NongDb _db;
    readonly bool _ownsDb;

    /// <summary>Open the unified nong.db at the default workplace cache location.</summary>
    public IngestionContext() : this(new NongDb(), ownsDb: true) { }

    /// <summary>Open the unified nong.db at an explicit path.</summary>
    public IngestionContext(string dbPath) : this(new NongDb(dbPath), ownsDb: true) { }

    /// <summary>Wrap an existing NongDb (caller owns disposal).</summary>
    public IngestionContext(NongDb db, bool ownsDb = false)
    {
        _db = db;
        _ownsDb = ownsDb;
    }

    /// <summary>Underlying NongDb instance for direct access when needed.</summary>
    public NongDb Db => _db;

    // ═══ Unified ingestion (one-cut three-flow) ═══

    /// <summary>
    /// Ingest a document slice into nong.db with full provenance and relationships.
    /// 
    /// One-cut three-flow pattern:
    /// 1. Register document (if not already present)
    /// 2. Import slice (blocks, format, structure, assets)
    /// 3. Create relationships (document → blocks, document → assets, document → outputs)
    /// 4. Track provenance (BeginRun/FinishRun)
    /// </summary>
    public IngestionResult IngestSlice(string filePath, string sliceDir, string command, string subcommand)
    {
        var runId = _db.BeginRun(command, subcommand, inputs: new[] { filePath });
        try
        {
            // 1. Register document
            var doc = _db.RegisterDocument(filePath);

            // 2. Import slice
            _db.ImportSlice(filePath, sliceDir);

            // 3. Query what was imported
            var docId = doc.Id.ToString();
            var blocks = _db.GetBlocks(docId);
            var assets = _db.GetAssets(docId);
            var format = _db.GetFormat(docId);

            // 4. Create relationships (document → blocks, document → assets)
            foreach (var block in blocks)
            {
                _db.Link("document", docId, "contains", "block", block.Id.ToString());
            }
            foreach (var asset in assets)
            {
                _db.Link("document", docId, "contains", "asset", asset.Id.ToString());
            }

            // 5. Track provenance
            _db.FinishRun(runId, outputs: new[] { docId }, status: "ok");

            return new IngestionResult
            {
                DocumentId = docId,
                FileName = doc.FileName,
                Format = doc.Format,
                Sha256 = doc.Sha256,
                Blocks = blocks.Count,
                Images = assets.Count(a => a.MimeType.StartsWith("image/")),
                HasFormat = format != null,
                RunId = runId
            };
        }
        catch (Exception ex)
        {
            _db.FinishRun(runId, status: "error", error: ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Ingest a literature list (search results) into nong.db with full provenance and relationships.
    /// 
    /// One-cut three-stream pattern:
    /// 1. Import list metadata (from list.json)
    /// 2. Import papers as content blocks (from content.jsonl)
    /// 3. Import structure (grouping, from structure.json)
    /// 4. Import format (citation style, from format.json)
    /// 5. Create relationships (list → papers)
    /// 6. Track provenance (BeginRun/FinishRun)
    /// </summary>
    public LiteratureListIngestionResult IngestLiteratureList(string sliceDir, string command, string subcommand)
    {
        var runId = _db.BeginRun(command, subcommand);
        try
        {
            // 1. Import list with one-cut three-stream
            var list = _db.ImportLiteratureList(sliceDir);

            // 2. Query what was imported
            var listId = list.Id.ToString();
            var paperBlocks = _db.GetBlocks(listId).Where(b => b.BlockType == "paper-item").ToList();
            var structure = _db.GetStructure(listId);
            var format = _db.GetFormat(listId);

            // 3. Track provenance
            _db.FinishRun(runId, outputs: new[] { listId }, status: "ok");

            return new LiteratureListIngestionResult
            {
                ListId = listId,
                QueryHash = list.QueryHash,
                TotalPapers = list.TotalPapers,
                HasFullText = list.HasFullText,
                HasDoi = list.HasDoi,
                HasStructure = structure != null,
                HasFormat = format != null,
                RunId = runId
            };
        }
        catch (Exception ex)
        {
            _db.FinishRun(runId, status: "error", error: ex.Message);
            throw;
        }
    }

    // ═══ Unified query patterns ═══

    /// <summary>List documents, optionally filtered by format.</summary>
    public IReadOnlyList<DbDocument> QueryDocuments(string? format = null)
        => _db.FindDocuments(format);

    /// <summary>Get blocks for a document, optionally filtered by type.</summary>
    public IReadOnlyList<DbBlock> QueryBlocks(string documentId, string? blockType = null, int limit = int.MaxValue)
    {
        var blocks = _db.GetBlocks(documentId);
        if (!string.IsNullOrWhiteSpace(blockType))
            blocks = blocks.Where(b => b.BlockType == blockType).ToList();
        return blocks.Take(limit).ToList();
    }

    /// <summary>Get assets (images, fonts, etc.) for a document, optionally filtered by MIME type.</summary>
    public IReadOnlyList<DbAsset> QueryAssets(string documentId, string? mimeTypePrefix = null)
    {
        var assets = _db.GetAssets(documentId);
        if (!string.IsNullOrWhiteSpace(mimeTypePrefix))
            assets = assets.Where(a => a.MimeType.StartsWith(mimeTypePrefix)).ToList();
        return assets;
    }

    /// <summary>Get format fingerprint for a document.</summary>
    public string? QueryFormat(string documentId)
        => _db.GetFormat(documentId);

    /// <summary>Get relationships originating from an object (outgoing edges).</summary>
    public IReadOnlyList<DbRelationship> QueryOutgoing(string sourceId)
        => _db.GetOutgoing(sourceId);

    /// <summary>Get relationships pointing at an object (incoming edges).</summary>
    public IReadOnlyList<DbRelationship> QueryIncoming(string targetId)
        => _db.GetIncoming(targetId);

    /// <summary>Get run provenance for a specific run.</summary>
    public DbRunProvenance? QueryRun(string runId)
        => _db.Runs.FindById(new ObjectId(runId));

    /// <summary>List all runs, optionally filtered by command or status.</summary>
    public IReadOnlyList<DbRunProvenance> QueryRuns(string? command = null, string? status = null, int limit = 50)
    {
        var query = _db.Runs.Query();
        if (!string.IsNullOrWhiteSpace(command))
            query = query.Where(r => r.Command == command);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);
        return query.OrderByDescending(r => r.StartedAt).Limit(limit).ToList();
    }

    /// <summary>List all literature lists, optionally filtered by provider.</summary>
    public IReadOnlyList<DbLiteratureList> QueryLiteratureLists(string? provider = null)
        => _db.FindLiteratureLists(provider);

    /// <summary>Get a specific literature list by query hash.</summary>
    public DbLiteratureList? QueryLiteratureList(string queryHash)
        => _db.FindLiteratureList(queryHash);

    /// <summary>Get papers in a literature list (via paper-item blocks).</summary>
    public IReadOnlyList<DbPaper> QueryPapersInList(string listId)
    {
        var paperBlocks = _db.GetBlocks(listId).Where(b => b.BlockType == "paper-item").ToList();
        var papers = new List<DbPaper>();
        
        foreach (var block in paperBlocks)
        {
            if (block.BlockId != null)
            {
                try
                {
                    var paperOid = new ObjectId(block.BlockId);
                    var paper = _db.Papers.FindById(paperOid);
                    if (paper != null)
                        papers.Add(paper);
                }
                catch (FormatException)
                {
                    // Invalid ObjectId format, skip this block
                }
            }
        }
        
        return papers;
    }

    public void Dispose()
    {
        if (_ownsDb) _db?.Dispose();
    }
}

/// <summary>Result of a slice ingestion operation.</summary>
public sealed class IngestionResult
{
    public string DocumentId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int Blocks { get; set; }
    public int Images { get; set; }
    public bool HasFormat { get; set; }
    public string RunId { get; set; } = "";
}

/// <summary>Result of a literature list ingestion operation.</summary>
public sealed class LiteratureListIngestionResult
{
    public string ListId { get; set; } = "";
    public string QueryHash { get; set; } = "";
    public int TotalPapers { get; set; }
    public bool HasFullText { get; set; }
    public bool HasDoi { get; set; }
    public bool HasStructure { get; set; }
    public bool HasFormat { get; set; }
    public string RunId { get; set; } = "";
}
