using Angri450.Nong;
using Angri450.Nong.Data;
using Angri450.Nong.Literature.Data;
using LiteDB;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Stage D of the unified-nongdb master plan: literature lists are first-class objects
/// in nong.db with one-cut three-stream support (content/structure/format). These tests
/// verify the DbLiteratureList model and the relationships that link lists to papers.
/// Execution requirement #4.
/// </summary>
public class LiteratureListModelTests
{
    static string NewDbPath()
    {
        var dir = Path.Combine(NongWorkplace.Cache, "litlist-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "nong.db");
    }

    [Fact]
    public void RegisterLiteratureList_CreatesFirstClassObject()
    {
        using var db = new NongDb(NewDbPath());
        var list = db.RegisterLiteratureList(
            hash: "abc123",
            query: "humic acid rare earth",
            provider: "openalex,crossref,unpaywall",
            totalPapers: 10);

        Assert.NotNull(list);
        Assert.NotEqual(ObjectId.Empty, list.Id);
        Assert.Equal("abc123", list.QueryHash);
        Assert.Equal("humic acid rare earth", list.Query);
        Assert.Equal("openalex,crossref,unpaywall", list.Provider);
        Assert.Equal(10, list.TotalPapers);
        Assert.True(list.FetchedAt > DateTime.UtcNow.AddMinutes(-1));

        // Verify it persisted
        var found = db.FindLiteratureList("abc123");
        Assert.NotNull(found);
        Assert.Equal(list.Id, found!.Id);
    }

    [Fact]
    public void FindLiteratureLists_FiltersByProvider()
    {
        using var db = new NongDb(NewDbPath());
        db.RegisterLiteratureList("h1", "query1", "openalex", 5);
        db.RegisterLiteratureList("h2", "query2", "crossref", 3);
        db.RegisterLiteratureList("h3", "query3", "openalex", 7);

        var all = db.FindLiteratureLists();
        Assert.Equal(3, all.Count);

        var openalexOnly = db.FindLiteratureLists("openalex");
        Assert.Equal(2, openalexOnly.Count);
        Assert.All(openalexOnly, l => Assert.Equal("openalex", l.Provider));

        var crossrefOnly = db.FindLiteratureLists("crossref");
        Assert.Single(crossrefOnly);
        Assert.Equal("crossref", crossrefOnly[0].Provider);
    }

    [Fact]
    public void LiteratureList_ToPaperRelationship_UsesContainsEdge()
    {
        using var db = new NongDb(NewDbPath());

        // Create a literature list
        var list = db.RegisterLiteratureList("h1", "test query", "openalex", 2);
        var listId = list.Id.ToString();

        // Create two papers
        var paper1Id = db.Papers.Insert(new DbPaper
        {
            NormalizedDoi = "10.1/paper1",
            Title = "Paper 1",
            QueryHash = "h1",
            ImportedAt = DateTime.UtcNow
        }).AsObjectId.ToString();

        var paper2Id = db.Papers.Insert(new DbPaper
        {
            NormalizedDoi = "10.1/paper2",
            Title = "Paper 2",
            QueryHash = "h1",
            ImportedAt = DateTime.UtcNow
        }).AsObjectId.ToString();

        // Create relationships: list contains paper
        db.Link("literature-list", listId, "contains", "paper", paper1Id);
        db.Link("literature-list", listId, "contains", "paper", paper2Id);

        // Verify relationships
        var outgoing = db.GetOutgoing(listId);
        Assert.Equal(2, outgoing.Count);
        Assert.All(outgoing, rel =>
        {
            Assert.Equal("literature-list", rel.SourceKind);
            Assert.Equal("contains", rel.Kind);
            Assert.Equal("paper", rel.TargetKind);
            Assert.Equal(listId, rel.SourceId);
        });

        // Verify papers are linked
        var papers = db.FindPapersByHash("h1");
        Assert.Equal(2, papers.Count);
        Assert.Contains(papers, p => p.NormalizedDoi == "10.1/paper1");
        Assert.Contains(papers, p => p.NormalizedDoi == "10.1/paper2");
    }

    [Fact]
    public void IngestionContext_QueryLiteratureListAndPapers_Integration()
    {
        var dbPath = NewDbPath();
        using var ctx = new IngestionContext(dbPath);

        // Directly insert test data into the underlying NongDb
        var list = new DbLiteratureList
        {
            QueryHash = "integration-test",
            Query = "test query",
            Provider = "openalex",
            FetchedAt = DateTime.UtcNow,
            TotalPapers = 3,
            HasDoi = true,
            HasFullText = false
        };
        ctx.Db.LiteratureLists.Insert(list);

        // Insert papers
        var paper1 = new DbPaper { NormalizedDoi = "10.1/p1", Title = "Paper 1", QueryHash = "integration-test", ImportedAt = DateTime.UtcNow };
        var paper2 = new DbPaper { NormalizedDoi = "10.1/p2", Title = "Paper 2", QueryHash = "integration-test", ImportedAt = DateTime.UtcNow };
        var paper3 = new DbPaper { NormalizedDoi = "10.1/p3", Title = "Paper 3", QueryHash = "integration-test", ImportedAt = DateTime.UtcNow };
        
        ctx.Db.Papers.Insert(paper1);
        ctx.Db.Papers.Insert(paper2);
        ctx.Db.Papers.Insert(paper3);

        // Create paper-item blocks linking papers to the list
        var listId = list.Id.ToString();
        ctx.Db.Blocks.Insert(new DbBlock { DocumentId = listId, BlockType = "paper-item", BlockId = paper1.Id.ToString(), Text = "Paper 1" });
        ctx.Db.Blocks.Insert(new DbBlock { DocumentId = listId, BlockType = "paper-item", BlockId = paper2.Id.ToString(), Text = "Paper 2" });
        ctx.Db.Blocks.Insert(new DbBlock { DocumentId = listId, BlockType = "paper-item", BlockId = paper3.Id.ToString(), Text = "Paper 3" });

        // Query papers in the list using IngestionContext
        var queried = ctx.QueryPapersInList(listId);
        Assert.Equal(3, queried.Count);
        Assert.Contains(queried, p => p.NormalizedDoi == "10.1/p1");
        Assert.Contains(queried, p => p.NormalizedDoi == "10.1/p2");
        Assert.Contains(queried, p => p.NormalizedDoi == "10.1/p3");

        // Verify the list itself is queryable
        var foundList = ctx.QueryLiteratureList("integration-test");
        Assert.NotNull(foundList);
        Assert.Equal(list.Id, foundList!.Id);
        Assert.Equal("test query", foundList.Query);
        Assert.Equal(3, foundList.TotalPapers);
    }

    [Fact]
    public void MultipleLiteratureLists_CanSharePapers()
    {
        using var db = new NongDb(NewDbPath());

        // Create two literature lists
        var list1 = db.RegisterLiteratureList("h1", "query1", "openalex", 2);
        var list2 = db.RegisterLiteratureList("h2", "query2", "crossref", 1);

        // Create a shared paper
        var sharedPaperId = db.Papers.Insert(new DbPaper
        {
            NormalizedDoi = "10.1/shared",
            Title = "Shared Paper",
            QueryHash = "h1",
            ImportedAt = DateTime.UtcNow
        }).AsObjectId.ToString();

        // Link the paper to both lists
        db.Link("literature-list", list1.Id.ToString(), "contains", "paper", sharedPaperId);
        db.Link("literature-list", list2.Id.ToString(), "contains", "paper", sharedPaperId);

        // Verify both lists contain the paper
        var list1Papers = db.GetOutgoing(list1.Id.ToString());
        var list2Papers = db.GetOutgoing(list2.Id.ToString());

        Assert.Single(list1Papers);
        Assert.Single(list2Papers);
        Assert.Equal(sharedPaperId, list1Papers[0].TargetId);
        Assert.Equal(sharedPaperId, list2Papers[0].TargetId);

        // Verify the paper has two incoming relationships
        var incoming = db.GetIncoming(sharedPaperId);
        Assert.Equal(2, incoming.Count);
        Assert.Contains(incoming, rel => rel.SourceId == list1.Id.ToString());
        Assert.Contains(incoming, rel => rel.SourceId == list2.Id.ToString());
    }
}
