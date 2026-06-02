using ScottPlot;
using ScottPlot.Plottables;

namespace ChartCore;

/// <summary>
/// Wraps ScottPlot 5.1.58 plottable types into clean builder APIs.
/// Each method creates a Plot, configures it, saves to PNG, and returns.
/// </summary>
public static class ChartTypes
{
    private static readonly Color[] DefaultColors = BarChartConfig.DefaultColors;

    // ────────────────────────────────────────────────────────────
    //  Helper methods
    // ────────────────────────────────────────────────────────────

    private static void SaveChart(Plot plt, string outPath)
    {
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        plt.SavePng(outPath, 800, 600);
    }

    private static void SaveChart(Plot plt, string outPath, int width, int height)
    {
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        plt.SavePng(outPath, width, height);
    }

    private static void ConfigureAxes(Plot plt, string title, string xlabel, string ylabel)
    {
        plt.Title(title);
        plt.XLabel(xlabel);
        plt.YLabel(ylabel);
    }

    private static void AddLegend(Plot plt, bool show)
    {
        if (show)
            plt.ShowLegend();
        else
            plt.HideLegend();
    }

    private static Color PickColor(Color[]? colors, int index)
    {
        var palette = colors ?? DefaultColors;
        return palette[index % palette.Length];
    }

    // ────────────────────────────────────────────────────────────
    //  1. PieChart
    // ────────────────────────────────────────────────────────────

    public static void PieChart(Dictionary<string, double> slices, string title, string outPath,
        Color[]? colors = null, int width = 800, int height = 600,
        bool showLabels = true, bool showValues = false, bool showPercent = true)
    {
        var plt = new Plot();
        plt.Title(title);

        var pieSlices = new List<PieSlice>();
        double total = slices.Values.Sum();
        int i = 0;
        foreach (var kv in slices)
        {
            var slice = new PieSlice(kv.Value, PickColor(colors, i), kv.Key);
            if (showLabels)
            {
                string label = kv.Key;
                if (showValues)
                    label += $"\n{kv.Value:F1}";
                if (showPercent)
                    label += $"\n{kv.Value / total * 100:F1}%";
                slice.LabelText = label;
            }
            else
            {
                slice.LabelText = "";
            }
            pieSlices.Add(slice);
            i++;
        }

        plt.Add.Pie(pieSlices);

        plt.HideAxesAndGrid();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  2. DonutChart
    // ────────────────────────────────────────────────────────────

    public static void DonutChart(Dictionary<string, double> slices, string title, string outPath,
        Color[]? colors = null, int width = 800, int height = 600, double innerRadius = 0.5)
    {
        var plt = new Plot();
        plt.Title(title);

        var pieSlices = new List<PieSlice>();
        int i = 0;
        foreach (var kv in slices)
        {
            pieSlices.Add(new PieSlice(kv.Value, PickColor(colors, i), kv.Key));
            i++;
        }

        var pie = plt.Add.Pie(pieSlices);
        pie.DonutFraction = innerRadius;

        plt.HideAxesAndGrid();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  3. LineChart (multi-series)
    // ────────────────────────────────────────────────────────────

    public static void LineChart(Dictionary<string, double[]> series, double[] xs,
        string title, string xlabel, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600,
        bool showMarkers = true, bool showLegend = true)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        int i = 0;
        foreach (var kv in series)
        {
            var scatter = plt.Add.Scatter(xs, kv.Value, PickColor(colors, i));
            scatter.LegendText = kv.Key;
            scatter.MarkerSize = showMarkers ? 6 : 0;
            scatter.LineWidth = 2;
            i++;
        }

        AddLegend(plt, showLegend && series.Count > 1);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  4. AreaChart
    // ────────────────────────────────────────────────────────────

    public static void AreaChart(Dictionary<string, double[]> series, double[] xs,
        string title, string xlabel, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600, bool showLegend = true)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        int i = 0;
        foreach (var kv in series)
        {
            var scatter = plt.Add.Scatter(xs, kv.Value, PickColor(colors, i));
            scatter.LegendText = kv.Key;
            scatter.FillY = true;
            scatter.FillYColor = PickColor(colors, i).WithAlpha(0.3);
            scatter.MarkerSize = 0;
            scatter.LineWidth = 2;
            i++;
        }

        AddLegend(plt, showLegend && series.Count > 1);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  5. ScatterChart (with optional regression line)
    // ────────────────────────────────────────────────────────────

    public static void ScatterChart(double[] xs, double[] ys,
        string title, string xlabel, string ylabel, string outPath,
        Color? color = null, int width = 800, int height = 600,
        MarkerShape markerShape = MarkerShape.FilledCircle, float markerSize = 8,
        bool showRegression = false)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        var c = color ?? DefaultColors[0];
        var scatter = plt.Add.ScatterPoints(xs, ys, c);
        scatter.MarkerShape = markerShape;
        scatter.MarkerSize = markerSize;

        if (showRegression && xs.Length >= 2)
        {
            // Simple linear regression
            double sumX = xs.Sum(), sumY = ys.Sum();
            double sumXY = xs.Zip(ys, (a, b) => a * b).Sum();
            double sumX2 = xs.Select(a => a * a).Sum();
            int n = xs.Length;
            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            double xMin = xs.Min(), xMax = xs.Max();
            double[] regX = [xMin, xMax];
            double[] regY = [slope * xMin + intercept, slope * xMax + intercept];

            var regLine = plt.Add.Scatter(regX, regY, c);
            regLine.MarkerSize = 0;
            regLine.LineWidth = 2;
            regLine.LegendText = $"y = {slope:F3}x + {intercept:F3}";
            plt.ShowLegend();
        }

        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  6. MultiScatterChart
    // ────────────────────────────────────────────────────────────

    public static void MultiScatterChart(
        Dictionary<string, (double[] xs, double[] ys)> series,
        string title, string xlabel, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600, bool showLegend = true)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        int i = 0;
        foreach (var kv in series)
        {
            var scatter = plt.Add.ScatterPoints(kv.Value.xs, kv.Value.ys, PickColor(colors, i));
            scatter.LegendText = kv.Key;
            scatter.MarkerSize = 8;
            i++;
        }

        AddLegend(plt, showLegend && series.Count > 1);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  7. BoxPlot
    // ────────────────────────────────────────────────────────────

    public static void BoxPlot(Dictionary<string, List<double>> groups,
        string title, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);
        plt.YLabel(ylabel);

        var boxes = new List<Box>();
        var labels = new List<string>();
        int i = 0;
        foreach (var kv in groups)
        {
            var sorted = kv.Value.OrderBy(v => v).ToArray();
            int n = sorted.Length;
            double q1 = sorted[n / 4];
            double median = sorted[n / 2];
            double q3 = sorted[3 * n / 4];
            double whiskerMin = sorted[0];
            double whiskerMax = sorted[n - 1];

            boxes.Add(new Box
            {
                Position = i,
                BoxMin = q1,
                BoxMax = q3,
                BoxMiddle = median,
                WhiskerMin = whiskerMin,
                WhiskerMax = whiskerMax,
                FillColor = PickColor(colors, i),
            });
            labels.Add(kv.Key);
            i++;
        }

        plt.Add.Boxes(boxes);

        // X-axis tick labels
        var positions = Enumerable.Range(0, labels.Count).Select(p => (double)p).ToArray();
        Tick[] ticks = positions.Select((p, idx) => new Tick(p, labels[idx])).ToArray();
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        plt.Axes.SetLimitsX(-0.8, labels.Count - 0.2);

        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  8. Histogram
    // ────────────────────────────────────────────────────────────

    public static void Histogram(double[] values, string title, string xlabel, string ylabel, string outPath,
        int binCount = 20, Color? color = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        var histogram = ScottPlot.Statistics.Histogram.WithBinCount(binCount, values);
        var bars = plt.Add.Histogram(histogram, color ?? DefaultColors[0]);

        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  9. RadarChart (spider chart)
    // ────────────────────────────────────────────────────────────

    public static void RadarChart(string[] categories, Dictionary<string, double[]> series,
        string title, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);

        // Build double[,] where rows=series, cols=categories
        int seriesCount = series.Count;
        int catCount = categories.Length;
        double[,] values = new double[seriesCount, catCount];

        int row = 0;
        foreach (var kv in series)
        {
            for (int col = 0; col < catCount; col++)
                values[row, col] = kv.Value[col];
            row++;
        }

        var radar = plt.Add.Radar(values);

        // Configure series colors and labels
        row = 0;
        foreach (var kv in series)
        {
            if (row < radar.Series.Count)
            {
                radar.Series[row].FillColor = PickColor(colors, row).WithAlpha(0.3);
                radar.Series[row].LineColor = PickColor(colors, row);
                radar.Series[row].LegendText = kv.Key;
            }
            row++;
        }

        // Configure category labels on the polar axis
        radar.PolarAxis.SetSpokes(categories, 1.0);

        plt.ShowLegend();
        plt.HideAxesAndGrid();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  10. StockChart (candlestick)
    // ────────────────────────────────────────────────────────────

    public static void StockChart(OHLC[] ohlcs, string title, string outPath,
        Color? upColor = null, Color? downColor = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);

        var candlestick = plt.Add.Candlestick(ohlcs);
        candlestick.RisingColor = upColor ?? new Color(38, 166, 91);    // green
        candlestick.FallingColor = downColor ?? new Color(226, 57, 57); // red

        plt.Axes.DateTimeTicksBottom();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  11. BubbleChart
    // ────────────────────────────────────────────────────────────

    public static void BubbleChart(double[] xs, double[] ys, double[] sizes,
        string title, string xlabel, string ylabel, string outPath,
        Color? color = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        var c = color ?? DefaultColors[0];
        for (int i = 0; i < xs.Length; i++)
        {
            float size = (float)sizes[i];
            plt.Add.Marker(xs[i], ys[i], MarkerShape.FilledCircle, size, c.WithAlpha(0.6));
        }

        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  12. HeatmapChart
    // ────────────────────────────────────────────────────────────

    public static void HeatmapChart(double[,] intensities, string title, string outPath,
        string? colormap = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);

        var heatmap = plt.Add.Heatmap(intensities);

        // Apply colormap by name if provided
        if (!string.IsNullOrEmpty(colormap))
        {
            var colormaps = ScottPlot.Colormap.GetColormaps();
            var matched = colormaps.FirstOrDefault(cm =>
                string.Equals(cm.Name, colormap, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                heatmap.Colormap = matched;
        }

        plt.Add.ColorBar(heatmap, Edge.Right);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  13. GaugeChart (radial gauge)
    // ────────────────────────────────────────────────────────────

    public static void GaugeChart(double[] values, string[] labels, string title, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);

        var gauge = plt.Add.RadialGaugePlot(values);
        gauge.Labels = labels;

        var gaugeColors = new Color[values.Length];
        for (int i = 0; i < values.Length; i++)
            gaugeColors[i] = PickColor(colors, i);
        gauge.Colors = gaugeColors;

        plt.HideAxesAndGrid();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  14. CoxcombChart (nightingale)
    // ────────────────────────────────────────────────────────────

    public static void CoxcombChart(Dictionary<string, double> slices, string title, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);

        var pieSlices = new List<PieSlice>();
        int i = 0;
        foreach (var kv in slices)
        {
            pieSlices.Add(new PieSlice(kv.Value, PickColor(colors, i), kv.Key));
            i++;
        }

        plt.Add.Coxcomb(pieSlices);
        plt.HideAxesAndGrid();
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  15. LollipopChart
    // ────────────────────────────────────────────────────────────

    public static void LollipopChart(Dictionary<string, double> groups,
        string title, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);
        plt.YLabel(ylabel);

        var values = groups.Values.ToArray();
        var positions = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();

        var lollipop = plt.Add.Lollipop(values, positions);
        lollipop.Color = PickColor(colors, 0);

        // X-axis tick labels
        var labels = groups.Keys.ToArray();
        Tick[] ticks = positions.Select((p, idx) => new Tick(p, labels[idx])).ToArray();
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        plt.Axes.SetLimitsX(-0.8, values.Length - 0.2);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  16. PopulationChart
    // ────────────────────────────────────────────────────────────

    public static void PopulationChart(Dictionary<string, double[]> groups,
        string title, string ylabel, string outPath,
        Color[]? colors = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        plt.Title(title);
        plt.YLabel(ylabel);

        int i = 0;
        foreach (var kv in groups)
        {
            var pop = plt.Add.Population(kv.Value, i);
            pop.Bar.FillColor = PickColor(colors, i);
            pop.Box.FillColor = PickColor(colors, i);
            i++;
        }

        // X-axis tick labels
        var labels = groups.Keys.ToArray();
        var positions = Enumerable.Range(0, labels.Length).Select(p => (double)p).ToArray();
        Tick[] ticks = positions.Select((p, idx) => new Tick(p, labels[idx])).ToArray();
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

        plt.Axes.SetLimitsX(-0.8, labels.Length - 0.2);
        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  17. FunctionChart
    // ────────────────────────────────────────────────────────────

    public static void FunctionChart(Func<double, double> func,
        double xMin, double xMax, string title, string xlabel, string ylabel, string outPath,
        Color? color = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        var funcPlot = plt.Add.Function(func);
        funcPlot.MinX = xMin;
        funcPlot.MaxX = xMax;
        funcPlot.LineColor = color ?? DefaultColors[0];
        funcPlot.LineWidth = 2;

        SaveChart(plt, outPath, width, height);
    }

    // ────────────────────────────────────────────────────────────
    //  18. ErrorBarChart
    // ────────────────────────────────────────────────────────────

    public static void ErrorBarChart(double[] xs, double[] ys,
        double[] xErrors, double[] yErrors,
        string title, string xlabel, string ylabel, string outPath,
        Color? color = null, int width = 800, int height = 600)
    {
        var plt = new Plot();
        ConfigureAxes(plt, title, xlabel, ylabel);

        var c = color ?? DefaultColors[0];

        // Plot the data points
        var scatter = plt.Add.ScatterPoints(xs, ys, c);
        scatter.MarkerSize = 6;

        // Add error bars with both X and Y errors via direct constructor
        var errorBar = new ErrorBar(
            xs, ys,
            xErrors, xErrors,  // xErrorsPositive, xErrorsNegative
            yErrors, yErrors   // yErrorsPositive, yErrorsNegative
        );
        errorBar.Color = c;
        plt.Add.Plottable(errorBar);

        SaveChart(plt, outPath, width, height);
    }
}
