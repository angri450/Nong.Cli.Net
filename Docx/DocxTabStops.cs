namespace DocxCore;

public enum TabAlignment { Left, Center, Right, Decimal, Bar, Num }
public enum TabLeader { None, Dot, Hyphen, Underscore, Heavy, MiddleDot }

public sealed record TabStopSpec(double PositionCm, TabAlignment Alignment, TabLeader Leader = TabLeader.None);

public sealed class DocxTabStops
{
    private readonly List<TabStopSpec> _stops = new();
    public IReadOnlyList<TabStopSpec> Stops => _stops;

    public DocxTabStops Add(TabStopSpec stop) { _stops.Add(stop); return this; }
    public DocxTabStops Clear() { _stops.Clear(); return this; }
}
