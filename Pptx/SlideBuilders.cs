namespace PptxCore;

public sealed class TitleSlideBuilder
{
    internal string TitleText { get; private set; } = "";
    internal string SubtitleText { get; private set; } = "";
    internal string AuthorText { get; private set; } = "";
    internal int TitleFontSize { get; private set; } = 36;
    internal string TitleColorHex { get; private set; } = "";
    internal string BackgroundHex { get; private set; } = "";

    public TitleSlideBuilder Title(string t) { TitleText = t; return this; }
    public TitleSlideBuilder Subtitle(string s) { SubtitleText = s; return this; }
    public TitleSlideBuilder Author(string a) { AuthorText = a; return this; }
    public TitleSlideBuilder TitleSize(int s) { TitleFontSize = s; return this; }
    public TitleSlideBuilder TitleColor(string c) { TitleColorHex = c; return this; }
    public TitleSlideBuilder Background(string c) { BackgroundHex = c; return this; }
}

public sealed class ContentSlideBuilder
{
    internal string TitleText { get; private set; } = "";
    internal List<string> BulletItems { get; } = new();
    internal string BackgroundHex { get; private set; } = "";

    public ContentSlideBuilder Title(string t) { TitleText = t; return this; }
    public ContentSlideBuilder Bullet(string b) { BulletItems.Add(b); return this; }
    public ContentSlideBuilder Bullets(params string[] items) { BulletItems.AddRange(items); return this; }
    public ContentSlideBuilder Background(string c) { BackgroundHex = c; return this; }
}

public sealed class TableSlideBuilder
{
    internal string TitleText { get; private set; } = "";
    internal string[][]? TableData { get; private set; }
    internal string BackgroundHex { get; private set; } = "";

    public TableSlideBuilder Title(string t) { TitleText = t; return this; }
    public TableSlideBuilder Data(string[][] data) { TableData = data; return this; }
    public TableSlideBuilder Background(string c) { BackgroundHex = c; return this; }
}

public sealed class ChartSlideBuilder
{
    internal string TitleText { get; private set; } = "";
    internal string ChartText { get; private set; } = "";
    internal Dictionary<string, double>? PieData { get; private set; }
    internal Dictionary<string, double>? BarData { get; private set; }
    internal string BarSeriesName { get; private set; } = "";
    internal string BackgroundHex { get; private set; } = "";

    public ChartSlideBuilder Title(string t) { TitleText = t; return this; }
    public ChartSlideBuilder ChartTitle(string t) { ChartText = t; return this; }
    public ChartSlideBuilder Background(string c) { BackgroundHex = c; return this; }
    public ChartSlideBuilder PieChart(Dictionary<string, double> data) { PieData = data; return this; }
    public ChartSlideBuilder BarChart(Dictionary<string, double> data, string seriesName = "") { BarData = data; BarSeriesName = seriesName; return this; }
}
