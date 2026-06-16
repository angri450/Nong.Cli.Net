using Angri450.Nong.Data;
using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;

namespace Angri450.Nong.Literature.Data;

public static class NongDbLiteratureExtensions
{
    public static DbPaper ToDbPaper(this PaperRecord record, string queryHash) => new()
    {
        NormalizedDoi = string.IsNullOrWhiteSpace(record.Doi) ? "" : record.Doi.Trim().ToLowerInvariant(),
        QueryHash = queryHash,
        ImportedAt = DateTime.UtcNow,
        Title = record.Title ?? "",
        TitleZh = record.Title ?? "",
        Year = record.Year,
        CitationCount = record.CitationCount ?? 0,
        VenueName = record.Venue ?? record.Journal ?? "",
        Journal = record.Journal ?? "",
        Publisher = record.Publisher ?? "",
        OpenAccess = record.IsOpenAccess == true ? "OA" : (record.OpenAccessStatus ?? ""),
        PdfUrl = record.PdfUrl ?? "",
        LandingPageUrl = record.LandingPageUrl ?? "",
        Authors = string.Join(',', record.Authors),
        Keywords = string.Join(',', record.Keywords),
        KeywordsZh = string.Join(',', record.Keywords),
        Abstract = record.Abstract ?? "",
        AbstractZh = record.Abstract ?? "",
        RetrievedFrom = string.Join(',', record.RetrievedFrom),
        SourceIds = string.Join(',', record.SourceIds.Select(kv => $"{kv.Key}={kv.Value}")),
    };

    public static PaperRecord ToPaperRecord(this DbPaper paper)
    {
        static List<string> Split(string value) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

        return new PaperRecord
        {
            Doi = paper.NormalizedDoi,
            Title = paper.Title,
            Year = paper.Year,
            CitationCount = paper.CitationCount,
            Venue = paper.VenueName,
            Journal = paper.Journal,
            Publisher = paper.Publisher,
            IsOpenAccess = paper.OpenAccess == "OA",
            OpenAccessStatus = paper.OpenAccess,
            PdfUrl = paper.PdfUrl,
            LandingPageUrl = paper.LandingPageUrl,
            Authors = Split(paper.Authors),
            Keywords = Split(paper.Keywords),
            Abstract = string.IsNullOrWhiteSpace(paper.Abstract) ? null : paper.Abstract,
            RetrievedFrom = Split(paper.RetrievedFrom),
        };
    }

    public static (int Added, int Duplicates) ImportPaperRecords(this NongDb db, IEnumerable<PaperRecord> records, string queryHash)
    {
        EnsurePaperIndexes(db);

        var existing = new HashSet<string>(
            db.Papers.FindAll()
                .Where(p => !string.IsNullOrWhiteSpace(p.NormalizedDoi))
                .Select(p => p.NormalizedDoi),
            StringComparer.OrdinalIgnoreCase);

        int added = 0, duplicates = 0;
        foreach (var record in records)
        {
            var paper = record.ToDbPaper(queryHash);
            if (!string.IsNullOrWhiteSpace(paper.NormalizedDoi) && existing.Contains(paper.NormalizedDoi))
            {
                duplicates++;
                continue;
            }

            db.Papers.Insert(paper);
            if (!string.IsNullOrWhiteSpace(paper.NormalizedDoi))
                existing.Add(paper.NormalizedDoi);
            added++;
        }

        return (added, duplicates);
    }

    public static IReadOnlyList<DbPaper> QueryPapers(this NongDb db,
        string? title = null, int? minYear = null, int? maxYear = null,
        int? minCitations = null, string? author = null, string? venue = null,
        string? keyword = null, int limit = 50, int skip = 0)
    {
        EnsurePaperIndexes(db);

        var q = db.Papers.Query();
        if (!string.IsNullOrWhiteSpace(title)) q = q.Where(x => x.Title.Contains(title) || x.TitleZh.Contains(title));
        if (minYear.HasValue) q = q.Where(x => x.Year >= minYear.Value);
        if (maxYear.HasValue) q = q.Where(x => x.Year <= maxYear.Value);
        if (minCitations.HasValue) q = q.Where(x => x.CitationCount >= minCitations.Value);
        if (!string.IsNullOrWhiteSpace(author)) q = q.Where(x => x.Authors.Contains(author));
        if (!string.IsNullOrWhiteSpace(venue)) q = q.Where(x => x.VenueName.Contains(venue) || x.Journal.Contains(venue));
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Keywords.Contains(keyword) || x.KeywordsZh.Contains(keyword));
        return q.OrderByDescending(x => x.CitationCount).Skip(skip).Limit(limit).ToList();
    }

    public static IReadOnlyList<PaperRecord> FilterPapersByDsl(this NongDb db, string dsl, string mode = "strict")
        => new LocalBooleanFilter().Filter(
            db.Papers.FindAll().Select(p => p.ToPaperRecord()).ToList(),
            CnkiParser.Parse(dsl),
            mode,
            out _);

    public static Dictionary<string, object?> AsDocxData(this NongDb db, string? dsl = null, string mode = "strict", int limit = 20)
    {
        var papers = dsl != null ? FilteredPapers(db, dsl, mode, limit) : QueryPapers(db, limit: Math.Min(limit, 1));
        return papers.Count == 0
            ? new Dictionary<string, object?> { ["title"] = "(no papers)" }
            : papers[0].ToDocxData();
    }

    public static Dictionary<string, object?> AsDocxList(this NongDb db, string? dsl = null, string mode = "strict", int limit = 20)
    {
        var papers = dsl != null ? FilteredPapers(db, dsl, mode, limit) : QueryPapers(db, limit: limit);
        return new Dictionary<string, object?> { ["papers"] = papers.Select(p => (object)p.ToDocxData()).ToList() };
    }

    public static string ExportPaperMarkdown(this NongDb db, IEnumerable<DbPaper>? papers = null, int limit = 20, int maxChars = 8000)
    {
        var list = (papers?.ToList() ?? QueryPapers(db, limit: limit).ToList());
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Literature ({list.Count} papers)\n");
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i].CompactEntry();
            if (sb.Length + entry.Length > maxChars)
            {
                sb.AppendLine($"\n... ({list.Count - i} truncated)");
                break;
            }

            sb.AppendLine(entry + "\n");
        }

        return sb.ToString();
    }

    public static CacheStats GetPaperStats(this NongDb db)
    {
        var all = db.Papers.FindAll().ToList();
        return new CacheStats
        {
            TotalRecords = all.Count,
            WithDoi = all.Count(p => !string.IsNullOrWhiteSpace(p.NormalizedDoi)),
            WithAbstract = all.Count(p => !string.IsNullOrWhiteSpace(p.Abstract) || !string.IsNullOrWhiteSpace(p.AbstractZh)),
            WithPdf = all.Count(p => !string.IsNullOrWhiteSpace(p.PdfUrl)),
            YearMin = all.Select(p => p.Year).DefaultIfEmpty().Min(),
            YearMax = all.Select(p => p.Year).DefaultIfEmpty().Max(),
            Sources = all.SelectMany(p => (p.RetrievedFrom ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Where(s => s.Length > 0)
                .GroupBy(s => s.Trim())
                .ToDictionary(g => g.Key, g => g.Count()),
            OldestImport = all.Any() ? all.Min(p => (DateTime?)p.ImportedAt) : null,
            NewestImport = all.Any() ? all.Max(p => (DateTime?)p.ImportedAt) : null,
        };
    }

    static List<DbPaper> FilteredPapers(NongDb db, string dsl, string mode, int limit)
        => db.FilterPapersByDsl(dsl, mode).Take(limit).Select(r => r.ToDbPaper("")).ToList();

    static void EnsurePaperIndexes(NongDb db)
    {
        db.Papers.EnsureIndex(p => p.NormalizedDoi);
        db.Papers.EnsureIndex(p => p.QueryHash);
        db.Papers.EnsureIndex(p => p.ImportedAt);
    }
}
