using Angri450.Nong.Data;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Unit tests for the unified NongDb object model — the single nong.db that
/// hosts all seven model categories: documents, blocks, formats, structures,
/// assets, outputs, papers (objects) plus relationships and run provenance.
/// Stage B of the unified-nongdb master plan.
/// </summary>
public class NongDbUnifiedModelTests
{
    static string NewDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nongdb-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "nong.db");
    }

    [Fact]
    public void AllSevenCollections_AreReachable()
    {
        using var db = new NongDb(NewDbPath());
        // The master plan requires seven model categories in one DB.
        Assert.NotNull(db.Documents);
        Assert.NotNull(db.Blocks);
        Assert.NotNull(db.Formats);
        Assert.NotNull(db.Structures);
        Assert.NotNull(db.Assets);
        Assert.NotNull(db.Outputs);
        Assert.NotNull(db.Papers);
        // The two added in stage B: relationships + run provenance.
        Assert.NotNull(db.Relationships);
        Assert.NotNull(db.Runs);
    }

    [Fact]
    public void Relationship_LinkRoundtrip_OutgoingAndIncoming()
    {
        using var db = new NongDb(NewDbPath());
        var paperId = db.Papers.Insert(new DbPaper { Title = "Humic acid", NormalizedDoi = "10.x/y" }).AsObjectId.ToString();
        var docId = db.Documents.Insert(new DbDocument { FileName = "paper.docx", Format = "docx", Sha256 = "abc" }).AsObjectId.ToString();

        // A document cites a paper — the cross-object relationship the unified model exists for.
        db.Link("document", docId, "cites", "paper", paperId);

        var outgoing = db.GetOutgoing(docId);
        var incoming = db.GetIncoming(paperId);

        Assert.Single(outgoing);
        Assert.Single(incoming);
        Assert.Equal("cites", outgoing[0].Kind);
        Assert.Equal("paper", outgoing[0].TargetKind);
        Assert.Equal(outgoing[0].Id, incoming[0].Id); // same edge, two viewpoints
    }

    [Fact]
    public void RunProvenance_BeginFinish_CapturesInputsOutputsAndTiming()
    {
        using var db = new NongDb(NewDbPath());
        var inDoc = db.Documents.Insert(new DbDocument { FileName = "in.docx", Format = "docx", Sha256 = "i" }).AsObjectId.ToString();
        var outDoc = db.Documents.Insert(new DbDocument { FileName = "out.docx", Format = "docx", Sha256 = "o" }).AsObjectId.ToString();

        var runId = db.BeginRun("word", "dissect", new[] { inDoc });
        Thread.Sleep(15); // ensure measurable duration
        db.FinishRun(runId, new[] { outDoc }, status: "ok");

        var run = db.Runs.FindById(new LiteDB.ObjectId(runId));
        Assert.NotNull(run);
        Assert.Equal("word", run!.Command);
        Assert.Equal("dissect", run.Subcommand);
        Assert.Equal("ok", run.Status);
        Assert.Contains(inDoc, run.Inputs);
        Assert.Contains(outDoc, run.Outputs);
        Assert.NotNull(run.FinishedAt);
        // FinishedAt must be at or after StartedAt (allow for LiteDB DateTime round-trip drift).
        Assert.True(run.FinishedAt!.Value.CompareTo(run.StartedAt) >= 0,
            $"FinishedAt {run.FinishedAt} before StartedAt {run.StartedAt}");
        Assert.NotEmpty(run.Host);
    }
}
