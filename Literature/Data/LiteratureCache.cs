using LiteDB;
using Angri450.Nong.Data;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Data;

/// <summary>
/// Literature paper cache. Thin wrapper over NongDb.Papers collection.
/// For backwards compatibility with existing LitCommands callers.
/// All data goes to NongWorkplace.Cache/nong.db
/// </summary>
public sealed class LiteratureCache : IDisposable
{
    readonly NongDb _db;
    readonly ILiteCollection<DbPaper> _papers;

    public LiteratureCache() : this(Path.Combine(NongWorkplace.Cache, "nong.db")) { }

    public LiteratureCache(string dbPath)
    {
        _db = new NongDb(dbPath);
        _papers = _db.Papers;
        _papers.EnsureIndex(x => x.NormalizedDoi);
        _papers.EnsureIndex(x => x.QueryHash);
        _papers.EnsureIndex(x => x.ImportedAt);
    }

    public NongDb Db => _db;

    // ═══ Import ═══

    public (int Added, int Duplicates) Import(IEnumerable<PaperRecord> records, string queryHash)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _papers.FindAll())
            if (!string.IsNullOrWhiteSpace(p.NormalizedDoi)) existing.Add(p.NormalizedDoi);

        int added = 0, dups = 0;
        foreach (var r in records)
        {
            var d = DbPaper.Create(r, queryHash);
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
        => new Pipeline.LocalBooleanFilter().Filter(_papers.FindAll().Select(p => p.ToRecord()).ToList(), Dsl.CnkiParser.Parse(dsl), mode, out _);

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
        return records.Select(r => DbPaper.Create(r, "")).ToList();
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
            YearMin = all.Where(p => p.Year.HasValue).Min(p => p.Year),
            YearMax = all.Where(p => p.Year.HasValue).Max(p => p.Year),
            Sources = all.SelectMany(p => p.RetrievedFrom.Split(',', StringSplitOptions.RemoveEmptyEntries)).Where(s => s.Length > 0).GroupBy(s => s.Trim()).ToDictionary(g => g.Key, g => g.Count()),
            OldestImport = all.Min(p => p.ImportedAt),
            NewestImport = all.Max(p => p.ImportedAt),
        };
    }

    public void Dispose() => _db?.Dispose();
}

public sealed class CacheStats
{
    public int TotalRecords { get; set; } public int WithDoi { get; set; } public int WithAbstract { get; set; } public int WithPdf { get; set; }
    public int? YearMin { get; set; } public int? YearMax { get; set; }
    public Dictionary<string, int> Sources { get; set; } = new();
    public DateTime? OldestImport { get; set; } public DateTime? NewestImport { get; set; }
}

/// <summary>DocxTemplate extensions on DbPaper — lives here so DbPaper stays a clean DB model.</summary>
public static class DbPaperExt
{
    public static Dictionary<string, object?> ToDocxData(this DbPaper p) => new()
    {
        ["cellReplace"] = new Dictionary<string, object?>
        {
            ["title"] = p.TitleZh ?? p.Title, ["title_en"] = p.Title, ["title_zh"] = p.TitleZh,
            ["year"] = p.Year?.ToString() ?? "", ["doi"] = p.NormalizedDoi,
            ["venue"] = p.VenueName, ["journal"] = p.Journal, ["publisher"] = p.Publisher,
            ["abstract"] = p.AbstractZh ?? p.Abstract,
            ["abstract_en"] = p.Abstract, ["abstract_zh"] = p.AbstractZh,
            ["keywords"] = p.Keywords, ["keywords_zh"] = p.KeywordsZh,
            ["citations"] = p.CitationCount.ToString(), ["oa"] = p.OpenAccess,
        },
        ["tableRows"] = new Dictionary<string, object?>
        {
            ["name"] = S(p.Authors).Select(a => (object)new List<string> { a, "" }).ToList()
        }
    };

    public static string CompactEntry(this DbPaper p)
    {
        var title = (p.TitleZh ?? p.Title ?? "?").Trunc(80);
        var author = S(p.Authors).FirstOrDefault() + (p.Authors.Contains(',') ? " et al." : "");
        var venue = (p.VenueName ?? p.Journal ?? "").Trunc(40);
        var doi = !string.IsNullOrWhiteSpace(p.NormalizedDoi) ? $"doi:{p.NormalizedDoi}" : "";
        return $"**{title}**\n{author} ({p.Year}). {venue}. {doi}\nCitations: {p.CitationCount} | OA: {p.OpenAccess}";
    }

    static List<string> S(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}

file static class Str { public static string Trunc(this string s, int m) => s.Length <= m ? s : s[..(m - 3)] + "..."; }
