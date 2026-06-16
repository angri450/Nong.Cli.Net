using Angri450.Nong;
using Angri450.Nong.Data;
using Angri450.Nong.Literature.Data;
using Angri450.Nong.Literature.Models;
using LiteDB;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Stage C of the unified-nongdb master plan: literature papers live in the single
/// nong.db via NongDb.Papers, not a separate literature.db. These tests lock that in:
/// the LiteratureCache persists to nong.db, never creates literature.db, and folds any
/// legacy literature.db into the unified store on open.
/// </summary>
public class LiteratureCacheUnifiedTests
{
    static string NewDbPath()
    {
        // LiteratureCache enforces that the db sits under the NongWorkplace root, so build
        // the test db under the real Cache dir with a unique name rather than in %TEMP%.
        var dir = Path.Combine(NongWorkplace.Cache, "litc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "nong.db");
    }

    static string DirOf(string dbPath) =>
        Path.GetDirectoryName(dbPath) ?? NongWorkplace.Cache;

    [Fact]
    public void Import_PersistsToNongDbAndCreatesNoLiteratureDb()
    {
        var nongDb = NewDbPath();
        var dir = DirOf(nongDb);
        try
        {
            using (var cache = new LiteratureCache(nongDb))
            {
                var (added, dups) = cache.Import(new[]
                {
                    new PaperRecord { Doi = "10.1/abc", Title = "Humic acid", Year = 2007, Authors = new() { "Qian W" } }
                }, "q1");
                Assert.Equal(1, added);
                Assert.Equal(0, dups);
            }

            // Open nong.db directly (bypassing LiteratureCache) and confirm the paper landed there.
            using var db = new NongDb(nongDb);
            var paper = Assert.Single(db.Papers.FindAll());
            Assert.Equal("10.1/abc", paper.NormalizedDoi);
            Assert.Equal("Humic acid", paper.Title);

            // No separate literature.db must ever be created.
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
            // Seed a legacy literature.db with one paper, the way the pre-stage-C cache did.
            using (var legacy = new LiteDatabase($"Filename={legacyDb};Connection=shared"))
            {
                var papers = legacy.GetCollection<DbPaper>("papers");
                papers.Insert(new DbPaper { NormalizedDoi = "10.2/legacy", Title = "Legacy paper", Year = 2020, ImportedAt = DateTime.UtcNow });
            }

            // Opening the unified cache must fold the legacy paper into nong.db.
            using (var cache = new LiteratureCache(nongDb))
            {
                Assert.Equal(1, cache.Count());
            }

            using var db = new NongDb(nongDb);
            var paper = Assert.Single(db.Papers.FindAll());
            Assert.Equal("10.2/legacy", paper.NormalizedDoi);

            // The legacy file is retired (renamed) so the migration never re-runs,
            // and no active literature.db remains.
            Assert.False(File.Exists(legacyDb));
            Assert.True(File.Exists(legacyDb + ".retired"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
