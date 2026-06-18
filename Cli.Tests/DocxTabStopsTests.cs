using DocxCore;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class DocxTabStopsTests
{
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
}
