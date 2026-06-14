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

    /// <summary>Papers collection — unified with LiteratureCache.</summary>
    public ILiteCollection<DbPaper> Papers => _db.GetCollection<DbPaper>("papers");

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

    /// <summary>Get images for a document.</summary>
    public IReadOnlyList<DbAsset> GetImages(string documentId)
        => Assets.Find(a => a.DocumentId == documentId && a.MimeType.StartsWith("image/")).ToList();

    /// <summary>Get format fingerprint for a document.</summary>
    public string? GetFormat(string documentId)
        => Formats.FindOne(f => f.DocumentId == documentId)?.Json;

    public void Dispose() => _db?.Dispose();

    static string ComputeSha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    static string? GetStr(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var n) ? n : null;
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

    // ═══ Conversion ═══

    public static DbPaper Create(Angri450.Nong.Literature.Models.PaperRecord r, string queryHash) => new()
    {
        NormalizedDoi = string.IsNullOrWhiteSpace(r.Doi) ? "" : r.Doi.Trim().ToLowerInvariant(),
        QueryHash = queryHash, ImportedAt = DateTime.UtcNow,
        Title = r.Title ?? "", TitleZh = r.Title ?? "", Year = r.Year,
        CitationCount = r.CitationCount ?? 0,
        VenueName = r.Venue ?? r.Journal ?? "", Journal = r.Journal ?? "", Publisher = r.Publisher ?? "",
        OpenAccess = r.IsOpenAccess == true ? "OA" : (r.OpenAccessStatus ?? ""),
        PdfUrl = r.PdfUrl ?? "", LandingPageUrl = r.LandingPageUrl ?? "",
        Authors = string.Join(',', r.Authors),
        Keywords = string.Join(',', r.Keywords), KeywordsZh = string.Join(',', r.Keywords),
        Abstract = r.Abstract ?? "", AbstractZh = r.Abstract ?? "",
        RetrievedFrom = string.Join(',', r.RetrievedFrom),
        SourceIds = string.Join(',', r.SourceIds.Select(kv => $"{kv.Key}={kv.Value}")),
    };

    public Angri450.Nong.Literature.Models.PaperRecord ToRecord()
    {
        var list = (string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        return new Angri450.Nong.Literature.Models.PaperRecord
        {
            Doi = NormalizedDoi, Title = Title, Year = Year, CitationCount = CitationCount,
            Venue = VenueName, Journal = Journal, Publisher = Publisher,
            IsOpenAccess = OpenAccess == "OA", OpenAccessStatus = OpenAccess,
            PdfUrl = PdfUrl, LandingPageUrl = LandingPageUrl,
            Authors = list(Authors), Keywords = list(Keywords),
            Abstract = string.IsNullOrWhiteSpace(Abstract) ? null : Abstract,
            RetrievedFrom = list(RetrievedFrom),
        };
    }
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
