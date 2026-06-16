using LiteDB;
using Angri450.Nong.Data;       // unified DbPaper lives in the Data package (nong.db)
using Angri450.Nong.Literature.Models;

// NOTE: The DbPaper storage class used to live here as a duplicate of Data/NongDb.cs.
// Stage C of the unified-nongdb plan removed it: papers now persist in the single
// nong.db via Data.DbPaper. Only the cache-stats DTO and the DocxTemplate projection
// extensions remain in this file.

namespace Angri450.Nong.Literature.Data;

/// <summary>Cache statistics for literature papers.</summary>
public sealed class CacheStats
{
    public int TotalRecords { get; set; } public int WithDoi { get; set; } public int WithAbstract { get; set; } public int WithPdf { get; set; }
    public int? YearMin { get; set; } public int? YearMax { get; set; }
    public Dictionary<string, int> Sources { get; set; } = new();
    public System.DateTime? OldestImport { get; set; } public System.DateTime? NewestImport { get; set; }
}

/// <summary>DocxTemplate extensions on DbPaper.</summary>
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
