using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

public enum TabAlignment { Left, Center, Right, Decimal, Bar, Num }
public enum TabLeader { None, Dot, Hyphen, Underscore, Heavy, MiddleDot }

public sealed record TabStopSpec(double PositionCm, TabAlignment Alignment, TabLeader Leader = TabLeader.None);

public sealed class DocxTabStops
{
    private const double TwipsPerCm = 567.0;

    private readonly List<TabStopSpec> _stops = new();
    public IReadOnlyList<TabStopSpec> Stops => _stops;

    public DocxTabStops Add(TabStopSpec stop) { _stops.Add(stop); return this; }
    public DocxTabStops Clear() { _stops.Clear(); return this; }

    public void ApplyTo(ParagraphProperties pPr)
    {
        if (_stops.Count == 0) return;
        var existing = pPr.GetFirstChild<Tabs>();
        if (existing != null) existing.Remove();
        var tabsEl = new Tabs();
        foreach (var s in _stops)
        {
            var tab = new TabStop
            {
                Position = (int)(s.PositionCm * TwipsPerCm)
            };
            tab.Val = MapAlignment(s.Alignment);
            if (s.Leader != TabLeader.None)
                tab.Leader = MapLeader(s.Leader);
            tabsEl.Append(tab);
        }
        pPr.Append(tabsEl);
    }

    public static DocxTabStops ReadFrom(ParagraphProperties? pPr)
    {
        var result = new DocxTabStops();
        var tabsEl = pPr?.GetFirstChild<Tabs>();
        if (tabsEl == null) return result;
        foreach (var tab in tabsEl.Elements<TabStop>())
        {
            var posTwips = tab.Position?.Value ?? 0;
            if (posTwips == 0) continue;
            var align = UnmapAlignment(tab.Val?.Value);
            var leader = UnmapLeader(tab.Leader?.Value);
            result.Add(new TabStopSpec(posTwips / TwipsPerCm, align, leader));
        }
        return result;
    }

    private static TabStopValues MapAlignment(TabAlignment a)
    {
        if (a == TabAlignment.Center) return TabStopValues.Center;
        if (a == TabAlignment.Right) return TabStopValues.Right;
        if (a == TabAlignment.Decimal) return TabStopValues.Decimal;
        if (a == TabAlignment.Bar) return TabStopValues.Bar;
        if (a == TabAlignment.Num) return TabStopValues.Decimal; // num not in OOXML, fallback to decimal
        return TabStopValues.Left;
    }

    private static TabStopLeaderCharValues MapLeader(TabLeader l)
    {
        if (l == TabLeader.Dot) return TabStopLeaderCharValues.Dot;
        if (l == TabLeader.Hyphen) return TabStopLeaderCharValues.Hyphen;
        if (l == TabLeader.Underscore) return TabStopLeaderCharValues.Underscore;
        if (l == TabLeader.Heavy) return TabStopLeaderCharValues.Heavy;
        if (l == TabLeader.MiddleDot) return TabStopLeaderCharValues.MiddleDot;
        return TabStopLeaderCharValues.None;
    }

    private static TabAlignment UnmapAlignment(TabStopValues? v)
    {
        if (v == TabStopValues.Center) return TabAlignment.Center;
        if (v == TabStopValues.Right) return TabAlignment.Right;
        if (v == TabStopValues.Decimal) return TabAlignment.Decimal;
        if (v == TabStopValues.Bar) return TabAlignment.Bar;
        return TabAlignment.Left;
    }

    private static TabLeader UnmapLeader(TabStopLeaderCharValues? l)
    {
        if (l == TabStopLeaderCharValues.Dot) return TabLeader.Dot;
        if (l == TabStopLeaderCharValues.Hyphen) return TabLeader.Hyphen;
        if (l == TabStopLeaderCharValues.Underscore) return TabLeader.Underscore;
        if (l == TabStopLeaderCharValues.Heavy) return TabLeader.Heavy;
        if (l == TabStopLeaderCharValues.MiddleDot) return TabLeader.MiddleDot;
        return TabLeader.None;
    }
}
