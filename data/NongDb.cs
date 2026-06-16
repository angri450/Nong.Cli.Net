using LiteDB;

namespace Angri450.Nong.Data;

/// <summary>
/// Unified Nong database. Single LiteDB file for all document data:
/// documents, blocks, formats, assets, papers, outputs.
/// Path: NongWorkplace.Cache/nong.db
/// </summary>
public sealed class NongDb : IDisposable
{
    readonly LiteDatabase _db;

    public NongDb() : this(Path.Combine(NongWorkplace.Cache, "nong.db")) { }

    public NongDb(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _db = new LiteDatabase($"Filename={path};Connection=direct");
    }

    // ═══ Collections ═══

    public ILiteCollection<DbDocument> Documents => _db.GetCollection<DbDocument>("documents");
    public ILiteCollection<DbBlock> Blocks => _db.GetCollection<DbBlock>("blocks");
    public ILiteCollection<DbAsset> Assets => _db.GetCollection<DbAsset>("assets");
    public ILiteCollection<DbFormat> Formats => _db.GetCollection<DbFormat>("formats");
    public ILiteCollection<DbStructure> Structures => _db.GetCollection<DbStructure>("structures");
    public ILiteCollection<DbOutput> Outputs => _db.GetCollection<DbOutput>("outputs");
    public ILiteCollection<DbRelationship> Relationships => _db.GetCollection<DbRelationship>("relationships");
    public ILiteCollection<DbRunProvenance> Runs => _db.GetCollection<DbRunProvenance>("runs");

    /// <summary>Papers collection — unified with LiteratureCache.</summary>
    public ILiteCollection<DbPaper> Papers => _db.GetCollection<DbPaper>("papers");

    /// <summary>Literature lists — search results as first-class objects (execution req #4).</summary>
    public ILiteCollection<DbLiteratureList> LiteratureLists => _db.GetCollection<DbLiteratureList>("literature_lists");

    public LiteDatabase Raw => _db;

    // ═══ Document tracking ═══

    public DbDocument RegisterDocument(string filePath)
    {
        var sha = ComputeSha256(filePath);
        var existing = Documents.FindOne(d => d.Sha256 == sha);
        if (existing != null) return existing;

        var doc = new DbDocument
        {
            FilePath = Path.GetFullPath(filePath),
            FileName = Path.GetFileName(filePath),
            Format = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
            FileSize = new FileInfo(filePath).Length,
            Sha256 = sha,
            RegisteredAt = DateTime.UtcNow
        };
        Documents.Insert(doc);
        Documents.EnsureIndex(d => d.Sha256);
        Documents.EnsureIndex(d => d.Format);
        return doc;
    }

    /// <summary>Import WordSlice output into DB.</summary>
    public DbDocument ImportSlice(string docxPath, string sliceDir)
    {
        var doc = RegisterDocument(docxPath);

        // Read content.jsonl → blocks
        var jsonlPath = Path.Combine(sliceDir, "content.jsonl");
        if (File.Exists(jsonlPath))
        {
            foreach (var line in File.ReadLines(jsonlPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc1 = System.Text.Json.JsonDocument.Parse(line);
                    var root = doc1.RootElement;
                    var block = new DbBlock
                    {
                        DocumentId = doc.Id.ToString(),
                        BlockId = GetStr(root, "blockId") ?? GetStr(root, "id"),
                        BlockType = GetStr(root, "kind") ?? GetStr(root, "blockType") ?? "unknown",
                        Text = GetStr(root, "text"),
                        Index = GetInt(root, "index") ?? 0,
                        Json = line
                    };
                    Blocks.Insert(block);
                }
                catch { /* skip malformed lines */ }
            }
            Blocks.EnsureIndex(b => b.DocumentId);
            Blocks.EnsureIndex(b => b.BlockType);
        }

        // Read format.json
        var fmtPath = Path.Combine(sliceDir, "format.json");
        if (File.Exists(fmtPath))
        {
            var json = File.ReadAllText(fmtPath);
            var fmt = new DbFormat { DocumentId = doc.Id.ToString(), Json = json, ExtractedAt = DateTime.UtcNow };
            Formats.Insert(fmt);
            Formats.EnsureIndex(f => f.DocumentId);
        }

        // Read structure.json
        var structPath = Path.Combine(sliceDir, "structure.json");
        if (File.Exists(structPath))
        {
            var json = File.ReadAllText(structPath);
            var st = new DbStructure { DocumentId = doc.Id.ToString(), Json = json, ExtractedAt = DateTime.UtcNow };
            Structures.Insert(st);
            Structures.EnsureIndex(s => s.DocumentId);
        }

        // Read assets/manifest.json and import images
        var assetsManifest = Path.Combine(sliceDir, "assets", "manifest.json");
        if (File.Exists(assetsManifest))
        {
            var manifestJson = File.ReadAllText(assetsManifest);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<JsonAssetManifest>(manifestJson);
            if (manifest?.Items != null)
            {
                foreach (var item in manifest.Items)
                {
                    var assetPath = Path.Combine(sliceDir, "assets", item.FileName ?? "");
                    var asset = new DbAsset
                    {
                        DocumentId = doc.Id.ToString(),
                        FileName = item.FileName ?? "",
                        MimeType = item.MimeType ?? "application/octet-stream",
                        Width = item.Width,
                        Height = item.Height,
                        Usage = item.Usage ?? "",
                        Data = File.Exists(assetPath) ? File.ReadAllBytes(assetPath) : null,
                        ExtractedAt = DateTime.UtcNow
                    };
                    Assets.Insert(asset);
                }
                Assets.EnsureIndex(a => a.DocumentId);
                Assets.EnsureIndex(a => a.MimeType);
            }
        }

        return doc;
    }

    /// <summary>Track a generated output file.</summary>
    public void TrackOutput(string filePath, string generator, string? sourceDocId = null)
    {
        Outputs.Insert(new DbOutput
        {
            FilePath = Path.GetFullPath(filePath),
            Generator = generator,
            SourceDocumentId = sourceDocId,
            CreatedAt = DateTime.UtcNow,
            FileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0
        });
        Outputs.EnsureIndex(o => o.Generator);
    }

    /// <summary>List all documents of a given format.</summary>
    public IReadOnlyList<DbDocument> FindDocuments(string? format = null)
    {
        if (string.IsNullOrWhiteSpace(format)) return Documents.FindAll().ToList();
        return Documents.Find(d => d.Format == format.ToLowerInvariant()).ToList();
    }

    /// <summary>Get all blocks for a document.</summary>
    public IReadOnlyList<DbBlock> GetBlocks(string documentId)
        => Blocks.Find(b => b.DocumentId == documentId).ToList();

    /// <summary>Get all assets (images, fonts, embedded files) for a document.</summary>
    public IReadOnlyList<DbAsset> GetAssets(string documentId)
        => Assets.Find(a => a.DocumentId == documentId).ToList();

    /// <summary>Get images for a document.</summary>
    public IReadOnlyList<DbAsset> GetImages(string documentId)
        => Assets.Find(a => a.DocumentId == documentId && a.MimeType.StartsWith("image/")).ToList();

    /// <summary>Get format fingerprint for a document.</summary>
    public string? GetFormat(string documentId)
        => Formats.FindOne(f => f.DocumentId == documentId)?.Json;

    /// <summary>Get structure hierarchy for a document.</summary>
    public string? GetStructure(string documentId)
        => Structures.FindOne(s => s.DocumentId == documentId)?.Json;

    // ═══ Relationships ═══

    /// <summary>
    /// Record a directed relationship between two unified objects
    /// (e.g. a document "cites" a paper, a block "embeds" an asset).
    /// Object kinds are the collection/entity names: document, block, asset,
    /// format, structure, output, paper, relationship, run.
    /// </summary>
    public DbRelationship Link(string sourceKind, string sourceId, string kind, string targetKind, string targetId, string? meta = null)
    {
        var rel = new DbRelationship
        {
            SourceKind = sourceKind, SourceId = sourceId,
            Kind = kind,
            TargetKind = targetKind, TargetId = targetId,
            Meta = meta,
            CreatedAt = DateTime.UtcNow
        };
        Relationships.Insert(rel);
        Relationships.EnsureIndex(r => r.Kind);
        Relationships.EnsureIndex(r => r.SourceId);
        Relationships.EnsureIndex(r => r.TargetId);
        return rel;
    }

    /// <summary>Relationships originating from an object.</summary>
    public IReadOnlyList<DbRelationship> GetOutgoing(string sourceId)
        => Relationships.Find(r => r.SourceId == sourceId).ToList();

    /// <summary>Relationships pointing at an object.</summary>
    public IReadOnlyList<DbRelationship> GetIncoming(string targetId)
        => Relationships.Find(r => r.TargetId == targetId).ToList();

    // ═══ Literature lists (unified object model for search results) ═══

    /// <summary>Register a literature list as a first-class object.</summary>
    public DbLiteratureList RegisterLiteratureList(string hash, string query, string provider, int totalPapers)
    {
        var list = new DbLiteratureList
        {
            QueryHash = hash,
            Query = query,
            Provider = provider,
            TotalPapers = totalPapers,
            FetchedAt = DateTime.UtcNow
        };
        LiteratureLists.Insert(list);
        LiteratureLists.EnsureIndex(l => l.QueryHash);
        return list;
    }

    /// <summary>Import papers into the database.</summary>
    public int ImportPapers(IEnumerable<DbPaper> papers)
    {
        int count = 0;
        foreach (var paper in papers)
        {
            Papers.Insert(paper);
            count++;
        }
        Papers.EnsureIndex(p => p.QueryHash);
        return count;
    }

    /// <summary>Find papers by query hash.</summary>
    public IReadOnlyList<DbPaper> FindPapersByHash(string queryHash)
        => Papers.Find(p => p.QueryHash == queryHash).ToList();

    // ═══ Run provenance ═══

    /// <summary>
    /// Begin tracking a command run. Returns the run id (to pass to <see cref="FinishRun"/>).
    /// Inputs are the unified object ids the run consumes (document/paper/asset ids, ...).
    /// </summary>
    public string BeginRun(string command, string? subcommand = null, IEnumerable<string>? inputs = null, string? host = null)
    {
        var run = new DbRunProvenance
        {
            Command = command, Subcommand = subcommand ?? "",
            Inputs = inputs?.ToList() ?? new(),
            Host = host ?? Environment.MachineName,
            StartedAt = DateTime.UtcNow,
            Status = "running"
        };
        Runs.Insert(run);
        Runs.EnsureIndex(r => r.Command);
        Runs.EnsureIndex(r => r.Status);
        return run.Id.ToString()!;
    }

    /// <summary>Finish a run started by <see cref="BeginRun"/>. Outputs are generated object ids.</summary>
    public void FinishRun(string runId, IEnumerable<string>? outputs = null, string status = "ok", string? error = null)
    {
        var run = Runs.FindById(new ObjectId(runId));
        if (run == null) return;
        run.Outputs = outputs?.ToList() ?? new();
        run.FinishedAt = DateTime.UtcNow;
        run.DurationMs = (long)(run.FinishedAt.Value - run.StartedAt).TotalMilliseconds;
        run.Status = status;
        run.Error = error;
        Runs.Update(run);
    }

    public void Dispose() => _db?.Dispose();

    static string ComputeSha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    static string? GetStr(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var n) ? n : null;

    /// <summary>Import literature list from slice directory (one-cut three-stream).</summary>
    public DbLiteratureList ImportLiteratureList(string sliceDir)
    {
        // Read list.json → create DbLiteratureList
        var listPath = Path.Combine(sliceDir, "list.json");
        if (!File.Exists(listPath))
            throw new FileNotFoundException("list.json not found in slice directory", listPath);

        var listJson = File.ReadAllText(listPath);
        using var listDoc = System.Text.Json.JsonDocument.Parse(listJson);
        var listRoot = listDoc.RootElement;

        var list = new DbLiteratureList
        {
            QueryHash = GetStr(listRoot, "queryHash") ?? "",
            Query = GetStr(listRoot, "query") ?? "",
            Provider = GetStr(listRoot, "provider") ?? "",
            FetchedAt = DateTime.UtcNow,
            TotalPapers = 0,
            HasFullText = false,
            HasDoi = false
        };
        LiteratureLists.Insert(list);
        LiteratureLists.EnsureIndex(l => l.QueryHash);
        LiteratureLists.EnsureIndex(l => l.Provider);

        var listId = list.Id.ToString();

        // Read content.jsonl → create DbPaper + DbBlock (paper-item) for each paper
        var contentPath = Path.Combine(sliceDir, "content.jsonl");
        if (File.Exists(contentPath))
        {
            var paperCount = 0;
            foreach (var line in File.ReadLines(contentPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var paperDoc = System.Text.Json.JsonDocument.Parse(line);
                    var paperRoot = paperDoc.RootElement;

                    // Create DbPaper
                    var paper = new DbPaper
                    {
                        NormalizedDoi = GetStr(paperRoot, "doi") ?? "",
                        QueryHash = list.QueryHash,
                        ImportedAt = DateTime.UtcNow,
                        Title = GetStr(paperRoot, "title") ?? "",
                        TitleZh = GetStr(paperRoot, "titleZh") ?? "",
                        Year = GetInt(paperRoot, "year"),
                        CitationCount = GetInt(paperRoot, "citationCount") ?? 0,
                        VenueName = GetStr(paperRoot, "venueName") ?? "",
                        Journal = GetStr(paperRoot, "journal") ?? "",
                        Publisher = GetStr(paperRoot, "publisher") ?? "",
                        OpenAccess = GetStr(paperRoot, "openAccess") ?? "",
                        PdfUrl = GetStr(paperRoot, "pdfUrl") ?? "",
                        LandingPageUrl = GetStr(paperRoot, "landingPageUrl") ?? "",
                        Authors = GetStr(paperRoot, "authors") ?? "",
                        Keywords = GetStr(paperRoot, "keywords") ?? "",
                        KeywordsZh = GetStr(paperRoot, "keywordsZh") ?? "",
                        Abstract = GetStr(paperRoot, "abstract") ?? "",
                        AbstractZh = GetStr(paperRoot, "abstractZh") ?? "",
                        RetrievedFrom = GetStr(paperRoot, "retrievedFrom") ?? "",
                        SourceIds = GetStr(paperRoot, "sourceIds") ?? ""
                    };
                    Papers.Insert(paper);

                    // Create DbBlock (paper-item type) linking to the list
                    var block = new DbBlock
                    {
                        DocumentId = listId,
                        BlockId = paper.Id.ToString(),
                        BlockType = "paper-item",
                        Text = paper.Title,
                        Index = paperCount,
                        Json = line
                    };
                    Blocks.Insert(block);

                    // Create relationship: list → paper
                    Link("literature-list", listId, "contains", "paper", paper.Id.ToString());

                    if (!string.IsNullOrEmpty(paper.NormalizedDoi)) list.HasDoi = true;
                    if (!string.IsNullOrEmpty(paper.PdfUrl)) list.HasFullText = true;

                    paperCount++;
                }
                catch { /* skip malformed lines */ }
            }

            list.TotalPapers = paperCount;
            LiteratureLists.Update(list);

            Blocks.EnsureIndex(b => b.DocumentId);
            Blocks.EnsureIndex(b => b.BlockType);
        }

        // Read structure.json → create DbStructure linked to the list
        var structPath = Path.Combine(sliceDir, "structure.json");
        if (File.Exists(structPath))
        {
            var structJson = File.ReadAllText(structPath);
            var st = new DbStructure { DocumentId = listId, Json = structJson, ExtractedAt = DateTime.UtcNow };
            Structures.Insert(st);
            Structures.EnsureIndex(s => s.DocumentId);
        }

        // Read format.json → create DbFormat linked to the list
        var fmtPath = Path.Combine(sliceDir, "format.json");
        if (File.Exists(fmtPath))
        {
            var fmtJson = File.ReadAllText(fmtPath);
            var fmt = new DbFormat { DocumentId = listId, Json = fmtJson, ExtractedAt = DateTime.UtcNow };
            Formats.Insert(fmt);
            Formats.EnsureIndex(f => f.DocumentId);
        }

        return list;
    }

    /// <summary>Get literature list by query hash.</summary>
    public DbLiteratureList? FindLiteratureList(string queryHash)
        => LiteratureLists.FindOne(l => l.QueryHash == queryHash);

    /// <summary>List all literature lists.</summary>
    public IReadOnlyList<DbLiteratureList> FindLiteratureLists(string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(provider)) return LiteratureLists.FindAll().ToList();
        return LiteratureLists.Find(l => l.Provider == provider).ToList();
    }
}

/// <summary>Registered source document.</summary>
public sealed class DbDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";  // docx, pdf, pptx, xlsx
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
}

/// <summary>Content block from one-cut three-stream dissect.</summary>
public sealed class DbBlock
{
    [BsonId] public ObjectId Id { get; set; }
    public string DocumentId { get; set; } = "";
    public string? BlockId { get; set; }
    public string BlockType { get; set; } = "";  // paragraph, heading, table, image, math, ...
    public string? Text { get; set; }
    public int Index { get; set; }
    public string? Json { get; set; }  // full content.jsonl line
}

/// <summary>Format fingerprint (format.json from WordSlice).</summary>
public sealed class DbFormat
{
    [BsonId] public ObjectId Id { get; set; }
    public string DocumentId { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
}

/// <summary>Document structure (structure.json from WordSlice).</summary>
public sealed class DbStructure
{
    [BsonId] public ObjectId Id { get; set; }
    public string DocumentId { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
}

/// <summary>Extracted asset (image, font, embedded file).</summary>
public sealed class DbAsset
{
    [BsonId] public ObjectId Id { get; set; }
    public string DocumentId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Usage { get; set; }
    public byte[]? Data { get; set; }
    public DateTime ExtractedAt { get; set; }
}

/// <summary>Literature paper — unified with existing CachedPaper.</summary>
public sealed class DbPaper
{
    [BsonId] public ObjectId Id { get; set; }
    public string NormalizedDoi { get; set; } = "";
    public string QueryHash { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public string Title { get; set; } = "";
    public string TitleZh { get; set; } = "";
    public int? Year { get; set; }
    public int CitationCount { get; set; }
    public string VenueName { get; set; } = "";
    public string Journal { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string OpenAccess { get; set; } = "";
    public string PdfUrl { get; set; } = "";
    public string LandingPageUrl { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string KeywordsZh { get; set; } = "";
    public string Abstract { get; set; } = "";
    public string AbstractZh { get; set; } = "";
    public string RetrievedFrom { get; set; } = "";
    public string SourceIds { get; set; } = "";
}

/// <summary>Literature list — search results as first-class object (execution req #4).
/// Supports one-cut three-stream: content (papers as blocks), structure (grouping), format (citation style).</summary>
public sealed class DbLiteratureList
{
    [BsonId] public ObjectId Id { get; set; }
    public string QueryHash { get; set; } = "";
    public string Query { get; set; } = "";
    public string Provider { get; set; } = "";
    public DateTime FetchedAt { get; set; }
    public int TotalPapers { get; set; }
    public bool HasFullText { get; set; }
    public bool HasDoi { get; set; }
}
    /// <summary>Generated output tracking.</summary>
public sealed class DbOutput
{
    [BsonId] public ObjectId Id { get; set; }
    public string FilePath { get; set; } = "";
    public string Generator { get; set; } = "";  // word-fill, pdf-merge, ...
    public string? SourceDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long FileSize { get; set; }
}

/// <summary>
/// Directed relationship between two unified objects. Kind is the predicate
/// (cites, embeds, references, derives-from, ...). Object kinds are the entity
/// names: document, block, asset, format, structure, output, paper, relationship, run.
/// </summary>
public sealed class DbRelationship
{
    [BsonId] public ObjectId Id { get; set; }
    public string SourceKind { get; set; } = "";  // e.g. "document", "paper", "block"
    public string SourceId { get; set; } = "";    // unified object id (string)
    public string Kind { get; set; } = "";        // predicate: cites, embeds, references, ...
    public string TargetKind { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string? Meta { get; set; }             // optional JSON blob for weight/notes
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Run provenance: one record per command/operation execution. Captures what ran,
/// when, on which host, what it consumed (Inputs) and produced (Outputs), and the
/// outcome. Lets any object in nong.db be traced back to the run that made it.
/// </summary>
public sealed class DbRunProvenance
{
    [BsonId] public ObjectId Id { get; set; }
    public string Command { get; set; } = "";       // e.g. "word", "pdf", "lit"
    public string Subcommand { get; set; } = "";    // e.g. "dissect", "fill", "search"
    public string Host { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = "";        // running, ok, error
    public string? Error { get; set; }
    public List<string> Inputs { get; set; } = new();   // consumed unified object ids
    public List<string> Outputs { get; set; } = new();  // produced unified object ids
}

file sealed class JsonAssetManifest
{
    public List<JsonAssetItem>? Items { get; set; }
}

file sealed class JsonAssetItem
{
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Usage { get; set; }
}
