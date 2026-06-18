using System.Diagnostics;
using System.Text.Json;
using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class DocxTabStopsTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, int exitCode) Run(params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: true,
            environment: null,
            args);
        return (result.StdOut, result.ExitCode);
    }

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    static string CreateMinimalDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-tabs-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart!.Document = new Document(new Body(
            new Paragraph(new Run(new Text("标题\t页码")))
        ));
        return path;
    }

    // ===== Unit tests (no CLI) =====
    [Fact]
    public void Add_RecordsStop_InStopsCollection()
    {
        var tabs = new DocxTabStops();
        var stop = new TabStopSpec(4.0, TabAlignment.Left, TabLeader.Dot);

        tabs.Add(stop);

        Assert.Single(tabs.Stops);
        Assert.Equal(4.0, tabs.Stops[0].PositionCm);
        Assert.Equal(TabAlignment.Left, tabs.Stops[0].Alignment);
        Assert.Equal(TabLeader.Dot, tabs.Stops[0].Leader);
    }

    [Fact]
    public void Clear_RemovesAllStops()
    {
        var tabs = new DocxTabStops();
        tabs.Add(new TabStopSpec(4.0, TabAlignment.Left));

        tabs.Clear();

        Assert.Empty(tabs.Stops);
    }

    [Fact]
    public void ApplyTo_WritesTabsElement_WithCorrectValPosLeader()
    {
        var tabs = new DocxTabStops();
        tabs.Add(new TabStopSpec(4.0, TabAlignment.Right, TabLeader.Dot));

        var pPr = new ParagraphProperties();
        tabs.ApplyTo(pPr);

        var tabsEl = pPr.GetFirstChild<Tabs>();
        Assert.NotNull(tabsEl);
        var tab = tabsEl!.GetFirstChild<TabStop>();
        Assert.NotNull(tab);
        // 4cm = 4 * 567 twips = 2268 twips (1cm = 567 twips, OOXML uses twips)
        Assert.Equal(2268, tab!.Position?.Value);
        Assert.Equal(TabStopValues.Right, tab.Val?.Value);
        Assert.Equal(TabStopLeaderCharValues.Dot, tab.Leader?.Value);
    }

    [Fact]
    public void ReadFrom_RoundTrips_StopsMatch()
    {
        var pPr = new ParagraphProperties();
        var original = new DocxTabStops();
        original.Add(new TabStopSpec(8.0, TabAlignment.Center, TabLeader.None));
        original.ApplyTo(pPr);

        var read = DocxTabStops.ReadFrom(pPr);
        Assert.Single(read.Stops);
        Assert.Equal(8.0, read.Stops[0].PositionCm);
        Assert.Equal(TabAlignment.Center, read.Stops[0].Alignment);
    }

    [Fact]
    public void ParagraphBuilder_TabStop_AddsToParagraphProperties()
    {
        var p = new ParagraphBuilder()
            .Text("标题\t页码")
            .TabStop(15.0, TabAlignment.Right, TabLeader.Dot)
            .Build();

        var pPr = p.ParagraphProperties;
        Assert.NotNull(pPr);
        var tabs = DocxTabStops.ReadFrom(pPr!);
        Assert.Single(tabs.Stops);
        Assert.Equal(15.0, tabs.Stops[0].PositionCm);
        Assert.Equal(TabAlignment.Right, tabs.Stops[0].Alignment);
        Assert.Equal(TabLeader.Dot, tabs.Stops[0].Leader);
    }

    // ===== CLI tests =====

    [Fact]
    public void WordTabStopsCommand_SetAndRead_RoundTrips()
    {
        RequireCli();
        var docxPath = CreateMinimalDocx();
        var outPath = docxPath + ".out.docx";

        try
        {
            // Set tab stops on first paragraph (index 0)
            var (setOut, setCode) = Run("word", "tab-stops", "--input", docxPath, "--paragraph", "0", "--set", "4cm dot,15cm right", "--output", outPath);
            Assert.Equal(0, setCode);

            // Read back with --json
            var (readOut, readCode) = Run("word", "tab-stops", "--input", outPath, "--paragraph", "0", "--json");
            Assert.Equal(0, readCode);

            // Verify JSON contains expected tab stops
            Assert.Contains("\"positionCm\": 4", readOut);
            Assert.Contains("\"positionCm\": 15", readOut);
            Assert.Contains("\"alignment\": \"Right\"", readOut);
            Assert.Contains("\"leader\": \"Dot\"", readOut);
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }
}
