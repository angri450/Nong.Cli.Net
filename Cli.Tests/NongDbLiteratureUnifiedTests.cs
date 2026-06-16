using Angri450.Nong.Data;
using Angri450.Nong.Literature.Data;
using Angri450.Nong.Literature.Models;
using LiteDB;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Literature storage is now a direct part of NongDb. These regressions lock in two
/// requirements from the unified-nongdb plan: paper imports persist to nong.db without
/// a separate wrapper universe, and opening nong.db retires any legacy literature.db.
/// </summary>
public class NongDbLiteratureUnifiedTests
{
    static string NewDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-litdb-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "nong.db");
    }

    static string DirOf(string dbPath) =>
        Path.GetDirectoryName(dbPath) ?? Path.GetTempPath();

    [Fact]
    public void Import_PersistsToNongDbAndCreatesNoLiteratureDb()
    {
        var nongDb = NewDbPath();
        var dir = DirOf(nongDb);
        try
        {
            using (var db = new NongDb(nongDb))
            {
                var (added, dups) = db.ImportPaperRecords(new[]
                {
                    new PaperRecord { Doi = "10.1/abc", Title = "Humic acid", Year = 2007, Authors = new() { "Qian W" } }
                }, "q1");
                Assert.Equal(1, added);
                Assert.Equal(0, dups);
            }

            using var db = new NongDb(nongDb);
            var paper = Assert.Single(db.Papers.FindAll());
            Assert.Equal("10.1/abc", paper.NormalizedDoi);
            Assert.Equal("Humic acid", paper.Title);

            Assert.False(File.Exists(Path.Combine(dir, "literature.db")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Opening_LegacyLiteratureDb_MigratesPapersIntoNongDbAndRetiresTheFile()
    {
        var nongDb = NewDbPath();
        var dir = DirOf(nongDb);
        var legacyDb = Path.Combine(dir, "literature.db");
        try
        {
            using (var legacy = new LiteDatabase($"Filename={legacyDb};Connection=shared"))
            {
                var papers = legacy.GetCollection<DbPaper>("papers");
                papers.Insert(new DbPaper { NormalizedDoi = "10.2/legacy", Title = "Legacy paper", Year = 2020, ImportedAt = DateTime.UtcNow });
            }

            using (var db = new NongDb(nongDb))
            {
                Assert.Equal(1, db.Papers.Count());
            }

            using var db = new NongDb(nongDb);
            var paper = Assert.Single(db.Papers.FindAll());
            Assert.Equal("10.2/legacy", paper.NormalizedDoi);

            Assert.False(File.Exists(legacyDb));
            Assert.True(File.Exists(legacyDb + ".retired"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
