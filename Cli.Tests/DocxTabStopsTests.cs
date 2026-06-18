using DocxCore;
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
}
