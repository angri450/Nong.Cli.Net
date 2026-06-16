using LiteDB;
using Angri450.Nong.Data;          // unified NongDb / DbPaper (single nong.db)
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Data;

/// <summary>
/// Literature paper cache. Stage C of the unified-nongdb plan: papers no longer have a
/// separate literature.db. They live in the single nong.db via NongDb.Papers, alongside
/// documents, blocks, assets, outputs and the other unified model objects.
/// File: NongWorkplace.Cache/nong.db (shared with the rest of Nong).
/// </summary>
public interface ILiteratureCache : IDisposable
{
    (int Added, int Duplicates) Import(IEnumerable<PaperRecord> records, string queryHash);
    IReadOnlyList<DbPaper> Query(string? title = null, int? minYear = null, int? maxYear = null,
        int? minCitations = null, string? author = null, string? venue = null,
        string? keyword = null, int limit = 50, int skip = 0);
    IReadOnlyList<PaperRecord> FilterByDsl(string dsl, string mode = "strict");
    Dictionary<string, object?> AsDocxData(string? dsl = null, string mode = "strict", int limit = 20);
    Dictionary<string, object?> AsDocxList(string? dsl = null, string mode = "strict", int limit = 20);
    string ExportMarkdown(IEnumerable<DbPaper>? papers = null, int limit = 20, int maxChars = 8000);
    int Count();
    CacheStats GetStats();
}

public sealed class LiteratureCache : ILiteratureCache
{
    readonly NongDb _db;
    readonly ILiteCollection<DbPaper> _papers;
    readonly bool _ownsDb;

    /// <summary>Open the unified nong.db at the default workplace cache location.</summary>
    public LiteratureCache() : this(Path.Combine(NongWorkplace.Cache, "nong.db")) { }

    /// <summary>Open the unified nong.db at an explicit path (tests / custom roots).</summary>
    public LiteratureCache(string dbPath)
    {
        var full = Path.GetFullPath(dbPath);
        NongWorkplace.RequireUnderRoot(full);

        _db = new NongDb(full);
        _ownsDb = true;
        _papers = _db.Papers;
        _papers.EnsureIndex(x => x.NormalizedDoi);
        _papers.EnsureIndex(x => x.QueryHash);
        _papers.EnsureIndex(x => x.ImportedAt);

        // One-time legacy migration: papers used to live in a sibling literature.db.
        // If it exists next to the nong.db, fold its papers into the unified store and
        // retire the file. This runs once per install and then self-disables (the file is
        // renamed away so it never matches again).
        MigrateLegacyLiteratureDb(full);
    }

    void MigrateLegacyLiteratureDb(string nongDbPath)
    {
        var legacy = Path.Combine(Path.GetDirectoryName(nongDbPath) ?? "", "literature.db");
        if (!File.Exists(legacy)) return;

        try
        {
            using var old = new LiteDatabase($"Filename={legacy};Connection=shared;ReadOnly=true");
            var oldPapers = old.GetCollection<DbPaper>("papers");
            var existingDois = new HashSet<string>(
                _papers.FindAll()
                    .Where(p => !string.IsNullOrWhiteSpace(p.NormalizedDoi))
                    .Select(p => p.NormalizedDoi),
                StringComparer.OrdinalIgnoreCase);

            foreach (var p in oldPapers.FindAll())
            {
                if (!string.IsNullOrWhiteSpace(p.NormalizedDoi) && existingDois.Contains(p.NormalizedDoi))
                    continue;
                // Strip the ObjectId from the legacy row so LiteDB assigns a fresh id.
                p.Id = ObjectId.Empty;
                _papers.Insert(p);
                if (!string.IsNullOrWhiteSpace(p.NormalizedDoi)) existingDois.Add(p.NormalizedDoi);
            }
        }
        catch
        {
            // Migration is best-effort: never block opening the cache on a corrupt legacy file.
            return;
        }

        // Retire the legacy file so this migration never re-runs.
        try { File.Move(legacy, legacy + ".retired", overwrite: true); } catch { }
    }

    /// <summary>Wrap an existing NongDb (caller owns disposal). Shares the single nong.db.</summary>
    public LiteratureCache(NongDb db)
    {
        _db = db;
        _ownsDb = false;
        _papers = _db.Papers;
        _papers.EnsureIndex(x => x.NormalizedDoi);
        _papers.EnsureIndex(x => x.QueryHash);
        _papers.EnsureIndex(x => x.ImportedAt);
    }

    // ═══ DbPaper <-> PaperRecord mapping ═══
    // These conversions live in the Literature layer (not in Data/NongDb.cs) so the
    // unified Data package stays free of an upward dependency on Literature models.

    public static DbPaper FromRecord(PaperRecord r, string queryHash) => new()
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

    static PaperRecord ToRecord(DbPaper p)
    {
        var list = (string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        return new PaperRecord
        {
            Doi = p.NormalizedDoi, Title = p.Title, Year = p.Year, CitationCount = p.CitationCount,
            Venue = p.VenueName, Journal = p.Journal, Publisher = p.Publisher,
            IsOpenAccess = p.OpenAccess == "OA", OpenAccessStatus = p.OpenAccess,
            PdfUrl = p.PdfUrl, LandingPageUrl = p.LandingPageUrl,
            Authors = list(p.Authors), Keywords = list(p.Keywords),
            Abstract = string.IsNullOrWhiteSpace(p.Abstract) ? null : p.Abstract,
            RetrievedFrom = list(p.RetrievedFrom),
        };
    }

    // ═══ Import ═══

    public (int Added, int Duplicates) Import(IEnumerable<PaperRecord> records, string queryHash)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _papers.FindAll())
            if (!string.IsNullOrWhiteSpace(p.NormalizedDoi)) existing.Add(p.NormalizedDoi);

        int added = 0, dups = 0;
        foreach (var r in records)
        {
            var d = FromRecord(r, queryHash);
            if (!string.IsNullOrWhiteSpace(d.NormalizedDoi) && existing.Contains(d.NormalizedDoi)) { dups++; continue; }
            _papers.Insert(d);
            if (!string.IsNullOrWhiteSpace(d.NormalizedDoi)) existing.Add(d.NormalizedDoi);
            added++;
        }
        return (added, dups);
    }

    // ═══ Query ═══

    public IReadOnlyList<DbPaper> Query(
        string? title = null, int? minYear = null, int? maxYear = null,
        int? minCitations = null, string? author = null, string? venue = null,
        string? keyword = null, int limit = 50, int skip = 0)
    {
        var q = _papers.Query();
        if (!string.IsNullOrWhiteSpace(title))   q = q.Where(x => x.Title.Contains(title) || x.TitleZh.Contains(title));
        if (minYear.HasValue)    q = q.Where(x => x.Year >= minYear.Value);
        if (maxYear.HasValue)    q = q.Where(x => x.Year <= maxYear.Value);
        if (minCitations.HasValue) q = q.Where(x => x.CitationCount >= minCitations.Value);
        if (!string.IsNullOrWhiteSpace(author))  q = q.Where(x => x.Authors.Contains(author));
        if (!string.IsNullOrWhiteSpace(venue))   q = q.Where(x => x.VenueName.Contains(venue) || x.Journal.Contains(venue));
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Keywords.Contains(keyword) || x.KeywordsZh.Contains(keyword));
        return q.OrderByDescending(x => x.CitationCount).Skip(skip).Limit(limit).ToList();
    }

    public IReadOnlyList<PaperRecord> FilterByDsl(string dsl, string mode = "strict")
        => new Pipeline.LocalBooleanFilter().Filter(_papers.FindAll().Select(p => ToRecord(p)).ToList(), Dsl.CnkiParser.Parse(dsl), mode, out _);

    // ═══ DocxTemplate ═══

    public Dictionary<string, object?> AsDocxData(string? dsl = null, string mode = "strict", int limit = 20)
    {
        var papers = dsl != null ? Filtered(dsl, mode, limit) : Query(limit: Math.Min(limit, 1));
        return papers.Count == 0
            ? new Dictionary<string, object?> { ["title"] = "(no papers)" }
            : papers[0].ToDocxData();
    }

    public Dictionary<string, object?> AsDocxList(string? dsl = null, string mode = "strict", int limit = 20)
    {
        var papers = dsl != null ? Filtered(dsl, mode, limit) : Query(limit: limit);
        return new Dictionary<string, object?> { ["papers"] = papers.Select(p => (object)p.ToDocxData()).ToList() };
    }

    List<DbPaper> Filtered(string dsl, string mode, int limit)
    {
        var records = FilterByDsl(dsl, mode).Take(limit).ToList();
        return records.Select(r => FromRecord(r, "")).ToList();
    }

    // ═══ Markdown ═══

    public string ExportMarkdown(IEnumerable<DbPaper>? papers = null, int limit = 20, int maxChars = 8000)
    {
        var list = (papers?.ToList() ?? Query(limit: limit).ToList());
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Literature ({list.Count} papers)\n");
        foreach (var p in list)
        {
            var entry = p.CompactEntry();
            if (sb.Length + entry.Length > maxChars) { sb.AppendLine($"\n... ({list.Count - list.IndexOf(p)} truncated)"); break; }
            sb.AppendLine(entry + "\n");
        }
        return sb.ToString();
    }

    // ═══ Stats ═══

    public int Count() => _papers.Count();

    public CacheStats GetStats()
    {
        var all = _papers.FindAll().ToList();
        return new CacheStats
        {
            TotalRecords = all.Count,
            WithDoi = all.Count(p => !string.IsNullOrWhiteSpace(p.NormalizedDoi)),
            WithAbstract = all.Count(p => !string.IsNullOrWhiteSpace(p.Abstract) || !string.IsNullOrWhiteSpace(p.AbstractZh)),
            WithPdf = all.Count(p => !string.IsNullOrWhiteSpace(p.PdfUrl)),
            YearMin = all.Select(p => p.Year).DefaultIfEmpty().Min(),
            YearMax = all.Select(p => p.Year).DefaultIfEmpty().Max(),
            Sources = all.SelectMany(p => p.RetrievedFrom.Split(',', StringSplitOptions.RemoveEmptyEntries)).Where(s => s.Length > 0).GroupBy(s => s.Trim()).ToDictionary(g => g.Key, g => g.Count()),
            OldestImport = all.Any() ? all.Min(p => (DateTime?)p.ImportedAt) : null,
            NewestImport = all.Any() ? all.Max(p => (DateTime?)p.ImportedAt) : null,
        };
    }

    public void Dispose()
    {
        if (_ownsDb) _db?.Dispose();
    }
}
