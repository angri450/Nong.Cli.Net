using System.Text.Json.Serialization;
using ScottPlot;

namespace ChartCore;

public class BarChartConfig
{
    public Dictionary<string, List<double>> Groups { get; set; } = new();
    public Dictionary<string, string>? SignificanceLabels { get; set; }
    public string Title { get; set; } = "";
    public string YLabel { get; set; } = "";
    public string OutPath { get; set; } = "chart.png";
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public Color[]? Colors { get; set; }
    public bool ShowErrorBar { get; set; } = true;
    public bool ShowSignificance { get; set; } = false;
    public bool ShowMeanValue { get; set; } = false;
    public int TitleFontSize { get; set; } = 20;
    public int AxisFontSize { get; set; } = 14;
    public float BarWidth { get; set; } = 0.6f;
    public bool ShowGrid { get; set; } = true;

    public static Color[] DefaultColors { get; } = new Color[]
    {
        new(91, 155, 213),   // Blue  #5B9BD5
        new(237, 125, 49),   // Orange #ED7D31
        new(112, 173, 71),   // Green  #70AD47
        new(255, 192, 0),    // Yellow #FFC000
        new(165, 116, 182),  // Purple #A574B6
        new(219, 68, 83),    // Red    #DB4453
    };
}

/// <summary>JSON 反序列化用的简化配置</summary>
public class BarChartConfigJson
{
    [JsonPropertyName("dataFile")]
    public string? DataFile { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("ylabel")]
    public string? YLabel { get; set; }

    [JsonPropertyName("outPath")]
    public string? OutPath { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("showErrorBar")]
    public bool? ShowErrorBar { get; set; }

    [JsonPropertyName("significance")]
    public Dictionary<string, string>? Significance { get; set; }
}
