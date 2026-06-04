using ScottPlot;

namespace ChartCore;

public static class ChartBuilder
{
    /// <summary>简单柱形图（含误差棒）</summary>
    public static void BarChart(Dictionary<string, List<double>> groups,
        string title, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600,
        bool showErrorBar = true, bool showGrid = true)
    {
        var config = new BarChartConfig
        {
            Groups = groups,
            Title = title,
            YLabel = ylabel,
            OutPath = outPath,
            Colors = colors ?? BarChartConfig.DefaultColors,
            Width = width,
            Height = height,
            ShowErrorBar = showErrorBar,
            ShowGrid = showGrid,
            ShowSignificance = false,
        };
        BarChart(config);
    }

    /// <summary>带显著性标注的柱形图</summary>
    public static void BarChartWithSignificance(Dictionary<string, List<double>> groups,
        Dictionary<string, string> significanceLabels,
        string title, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var config = new BarChartConfig
        {
            Groups = groups,
            SignificanceLabels = significanceLabels,
            Title = title,
            YLabel = ylabel,
            OutPath = outPath,
            Colors = colors ?? BarChartConfig.DefaultColors,
            Width = width,
            Height = height,
            ShowErrorBar = true,
            ShowSignificance = true,
        };
        BarChart(config);
    }

    /// <summary>完整配置的柱形图</summary>
    public static void BarChart(BarChartConfig config)
    {
        var plt = new Plot();
        plt.Title(config.Title, config.TitleFontSize);
        plt.YLabel(config.YLabel, config.AxisFontSize);

        int barCount = config.Groups.Count;
        double[] positions = Enumerable.Range(0, barCount).Select(i => (double)i).ToArray();
        double[] values = new double[barCount];
        double[] errors = new double[barCount];
        string[] labels = new string[barCount];

        int idx = 0;
        foreach (var kv in config.Groups)
        {
            labels[idx] = kv.Key;
            values[idx] = kv.Value.Average();
            var stats = GroupStats.Compute(kv.Value);
            errors[idx] = config.ShowErrorBar ? stats.SEM : 0;
            idx++;
        }

        var colors = config.Colors ?? BarChartConfig.DefaultColors;

        for (int i = 0; i < barCount; i++)
        {
            var bar = plt.Add.Bar(new Bar
            {
                Position = positions[i],
                Value = values[i],
                ValueBase = 0,
                FillColor = colors[i % colors.Length],
            });
        }

        // 显著性标注
        if (config.ShowSignificance && config.SignificanceLabels != null)
        {
            double maxVal = values.Max() + errors.Max();
            double offset = (maxVal * 0.05);
            for (int i = 0; i < barCount; i++)
            {
                if (config.SignificanceLabels.TryGetValue(labels[i], out var sig) && !string.IsNullOrEmpty(sig))
                {
                    double labelY = values[i] + errors[i] + offset;
                    if (config.ShowMeanValue)
                        labelY += maxVal * 0.05;
                    var txt = plt.Add.Text(sig, positions[i], labelY);
                    txt.Alignment = ScottPlot.Alignment.UpperCenter;
                    txt.LabelFontSize = config.AxisFontSize;
                }
            }
        }

        // 均值标注
        if (config.ShowMeanValue)
        {
            double maxVal = values.Max() + errors.Max();
            for (int i = 0; i < barCount; i++)
            {
                double labelY = values[i] + errors[i] + maxVal * 0.02;
                var txt = plt.Add.Text(values[i].ToString("F2"), positions[i], labelY);
                txt.Alignment = ScottPlot.Alignment.UpperCenter;
                txt.LabelFontSize = config.AxisFontSize - 2;
            }
        }

        // X 轴
        plt.Axes.SetLimitsX(-0.8, barCount - 0.2);

        // Y 轴
        double maxY = values.Max() + errors.Max();
        double yPad = maxY * 0.25;
        plt.Axes.SetLimitsY(0, maxY + yPad);

        // Tick labels
        Tick[] ticks = positions.Select((p, i) => new Tick(p, labels[i])).ToArray();
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
        plt.Axes.Bottom.TickLabelStyle.FontSize = config.AxisFontSize;

        if (config.ShowGrid) plt.Grid.IsVisible = true;

        Directory.CreateDirectory(Path.GetDirectoryName(config.OutPath) ?? ".");
        plt.SavePng(config.OutPath, config.Width, config.Height);
    }

    /// <summary>Get a CJK-capable font family name for chart rendering.</summary>
    public static string GetCjkFontFamily() => FontHelper.GetCjkFamilyName();
}
