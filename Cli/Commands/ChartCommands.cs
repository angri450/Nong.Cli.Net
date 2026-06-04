using System.Globalization;
using System.CommandLine;
using System.Text.Json;
using ChartCore;
using ScottPlot;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Chart command group: stats anova + duncan (phase 3), others stubbed.
/// </summary>
public static class ChartCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("chart", "Statistical charts and analysis");

        cmd.AddCommand(CreateAnalyze(jsonOpt));
        cmd.AddCommand(CreateAnova(jsonOpt));
        cmd.AddCommand(CreateDuncan(jsonOpt));
        cmd.AddCommand(CreateBarChart(jsonOpt));
        cmd.AddCommand(CreateLineChart(jsonOpt));
        cmd.AddCommand(CreateScatterChart(jsonOpt));
        cmd.AddCommand(CreatePieChart(jsonOpt));

        return cmd;
    }

    // ===== stats anova =====

    static Command CreateAnova(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var cmd = new Command("anova", "One-way ANOVA") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("chart anova", err, json);
                return;
            }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr = StatsValidation.Validate(groups, "chart anova");
                if (verr != null) { CliHelpers.WriteError("chart anova", verr, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    return StatsEngine.OneWayAnova(groups);
                });

                if (json)
                {
                    var data = new
                    {
                        f = result.F, p = result.P,
                        ssb = result.SSB, ssw = result.SSW, sst = result.SST,
                        msb = result.MSB, msw = result.MSW,
                        dfb = result.dfB, dfw = result.dfW,
                        groups = result.GroupStats.Select(g => new
                        {
                            name = g.Key, n = g.Value.N, mean = g.Value.Mean,
                            sd = g.Value.SD, sem = g.Value.SEM,
                            min = g.Value.Min, max = g.Value.Max
                        })
                    };
                    var output = JsonOutput.Ok("chart anova",
                        $"F={result.F:F2}, P={result.P:F4}, df=({result.dfB},{result.dfW})",
                        data);
                    output.Metrics["groups"] = result.GroupStats.Count;
                    output.Metrics["totalN"] = result.N;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"=== One-Way ANOVA ===");
                    Console.WriteLine($"F = {result.F:F4}, P = {result.P:F6}");
                    Console.WriteLine($"SSB = {result.SSB:F2}, SSW = {result.SSW:F2}, SST = {result.SST:F2}");
                    Console.WriteLine($"dfB = {result.dfB}, dfW = {result.dfW}");
                    Console.WriteLine($"MSB = {result.MSB:F4}, MSW = {result.MSW:F4}");
                    Console.WriteLine();
                    Console.WriteLine($"{"Group",-16} {"N",4} {"Mean",10} {"SD",10} {"SEM",10} {"Min",8} {"Max",8}");
                    foreach (var g in result.GroupStats)
                        Console.WriteLine($"{g.Key,-16} {g.Value.N,4} {g.Value.Mean,10:F2} {g.Value.SD,10:F2} {g.Value.SEM,10:F2} {g.Value.Min,8:F2} {g.Value.Max,8:F2}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart anova",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== stats duncan =====

    static Command CreateDuncan(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var alphaOpt = new Option<double>("--alpha", () => 0.05, "Significance level");
        var cmd = new Command("duncan", "Duncan MRT post-hoc test") { fileArg, alphaOpt };

        cmd.SetHandler((string file, double alpha, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("chart duncan", err, json);
                return;
            }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr2 = StatsValidation.Validate(groups, "chart duncan");
                if (verr2 != null) { CliHelpers.WriteError("chart duncan", verr2, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var anova = StatsEngine.OneWayAnova(groups);
                    return StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW, alpha);
                });

                if (json)
                {
                    var data = new
                    {
                        alpha = result.Alpha,
                        mse = result.MSE,
                        dfError = result.dfError,
                        groups = result.Groups.Select(g => new
                        {
                            label = g.Label, mean = g.Mean, sd = g.SD,
                            significance = g.Significance
                        })
                    };
                    var output = JsonOutput.Ok("chart duncan",
                        $"Duncan MRT: {result.Groups.Count} groups, alpha={alpha}",
                        data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"=== Duncan MRT (alpha={alpha}) ===");
                    Console.WriteLine($"MSE = {result.MSE:F4}, dfError = {result.dfError}");
                    Console.WriteLine();
                    Console.WriteLine($"{"Group",-16} {"Mean",10} {"SD",8}  Significance");
                    foreach (var g in result.Groups)
                        Console.WriteLine($"{g.Label,-16} {g.Mean,10:F2} {g.SD,8:F2}  {g.Significance}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart duncan",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }


        }, fileArg, alphaOpt, jsonOpt);

        return cmd;
    }

    // ===== chart analyze (P0: one-shot ANOVA + Duncan + stats) =====

    static Command CreateAnalyze(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var alphaOpt = new Option<double>("--alpha", () => 0.05, "Significance level");
        var cmd = new Command("analyze", "Full statistical analysis (ANOVA + Duncan + descriptive stats)") { fileArg, alphaOpt };

        cmd.SetHandler((string file, double alpha, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart analyze", err, json); return; }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr3 = StatsValidation.Validate(groups, "chart analyze");
                if (verr3 != null) { CliHelpers.WriteError("chart analyze", verr3, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    return StatsEngine.FullAnalysis(groups, alpha);
                });

                if (json)
                {
                    var data = new
                    {
                        anova = new
                        {
                            f = result.Anova.F, p = result.Anova.P,
                            ssb = result.Anova.SSB, ssw = result.Anova.SSW,
                            msb = result.Anova.MSB, msw = result.Anova.MSW,
                            dfb = result.Anova.dfB, dfw = result.Anova.dfW
                        },
                        duncan = new
                        {
                            alpha = result.Duncan.Alpha,
                            mse = result.Duncan.MSE,
                            dfError = result.Duncan.dfError,
                            groups = result.Duncan.Groups.Select(g => new
                            {
                                label = g.Label, mean = g.Mean, sd = g.SD,
                                significance = g.Significance
                            })
                        },
                        groups = result.Anova.GroupStats.Select(g => new
                        {
                            name = g.Key, n = g.Value.N, mean = g.Value.Mean,
                            sd = g.Value.SD, sem = g.Value.SEM, min = g.Value.Min, max = g.Value.Max
                        })
                    };

                    var sigGroups = string.Join(", ", result.Duncan.Groups.Select(g => $"{g.Label}{g.Significance}"));
                    var output = JsonOutput.Ok("chart analyze",
                        $"F={result.Anova.F:F2}, P={result.Anova.P:F4}, Duncan groups: {sigGroups}", data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    result.Print();
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart analyze",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, fileArg, alphaOpt, jsonOpt);

        return cmd;
    }

    // ===== chart bar (P0: bar chart with error bars and significance) =====

    static Command CreateBarChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var ylabelOpt = new Option<string>("--ylabel", () => "", "Y-axis label");
        var errorOpt = new Option<string>("--error", () => "sem", "Error bar type: sem or none");
        var noSigOpt = new Option<bool>("--no-significance", () => false, "Disable Duncan significance letters");
        var cmd = new Command("bar", "Bar chart with error bars and significance letters") { fileArg, outOpt, titleOpt, ylabelOpt, errorOpt, noSigOpt };

        cmd.SetHandler((string file, string output, string title, string ylabel, string error, bool noSig, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart bar", err, json); return; }

            try
            {
                var showError = error != "none";

                var groups = DataLoader.FromJson(file);
                var verr4 = StatsValidation.Validate(groups, "chart bar");
                if (verr4 != null) { CliHelpers.WriteError("chart bar", verr4, json); return; }

                CliHelpers.EnsureParentDir(output);
                var (result, elapsed) = CliHelpers.Time(() =>
                {

                    // Run Duncan for significance letters if enabled
                    Dictionary<string, string>? sigLabels = null;
                    if (!noSig)
                    {
                        var anova = StatsEngine.OneWayAnova(groups);
                        if (anova.P < 0.05)
                        {
                            var duncan = StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW, 0.05);
                            sigLabels = duncan.Groups.ToDictionary(g => g.Label, g => g.Significance);
                        }
                    }

                    if (sigLabels != null)
                        ChartBuilder.BarChartWithSignificance(groups, sigLabels, title, ylabel, output, colors: null, width: 800, height: 600);
                    else
                        ChartBuilder.BarChart(groups, title, ylabel, output, colors: null, width: 800, height: 600, showErrorBar: showError, showGrid: true);

                    return (groups.Count, sigLabels);
                });

                if (json)
                {
                    var aerr = CliHelpers.CheckArtifact(output, "PNG");
                    if (aerr != null) { CliHelpers.WriteError("chart bar", aerr, json); return; }

                    var outputJson = JsonOutput.Ok("chart bar",
                        $"Bar chart saved: {output}",
                        new { groups = result.Count, hasSignificance = result.sigLabels != null });
                    outputJson.Artifacts["png"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Bar chart saved: {Path.GetFullPath(output)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart bar",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, fileArg, outOpt, titleOpt, ylabelOpt, errorOpt, noSigOpt, jsonOpt);

        return cmd;
    }

    // ===== chart line =====

    static Command CreateLineChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("line", "Line chart") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart line", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<LineSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Series == null || spec.Series.Count == 0)
                {
                    CliHelpers.WriteError("chart line",
                        ErrorCodes.ValidationFailed with { Message = "series array must be non-empty." }, json);
                    return;
                }

                for (int i = 0; i < spec.Series.Count; i++)
                {
                    var s = spec.Series[i];
                    if (string.IsNullOrWhiteSpace(s.Name))
                    {
                        CliHelpers.WriteError("chart line",
                            ErrorCodes.ValidationFailed with { Message = $"Series [{i}]: name is required." }, json);
                        return;
                    }
                    if (s.X == null || s.Y == null || s.X.Length != s.Y.Length)
                    {
                        CliHelpers.WriteError("chart line",
                            ErrorCodes.ValidationFailed with { Message = $"Series '{s.Name}': x and y arrays must have the same length." }, json);
                        return;
                    }
                    if (s.X.Length == 0)
                    {
                        CliHelpers.WriteError("chart line",
                            ErrorCodes.ValidationFailed with { Message = $"Series '{s.Name}': x and y arrays must be non-empty." }, json);
                        return;
                    }
                    if (s.X.Any(double.IsNaN) || s.X.Any(double.IsInfinity) ||
                        s.Y.Any(double.IsNaN) || s.Y.Any(double.IsInfinity))
                    {
                        CliHelpers.WriteError("chart line",
                            ErrorCodes.ValidationFailed with { Message = $"Series '{s.Name}': values must not be NaN or Infinity." }, json);
                        return;
                    }
                }

                int seriesCount = spec.Series.Count;
                int totalPoints = spec.Series.Sum(s => s.X!.Length);

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var plt = new Plot();
                    plt.Font.Set(ChartBuilder.GetCjkFontFamily());
                    plt.Title(spec.Title ?? "");
                    plt.XLabel(spec.XLabel ?? "");
                    plt.YLabel(spec.YLabel ?? "");

                    var colors = BarChartConfig.DefaultColors;
                    for (int i = 0; i < spec.Series.Count; i++)
                    {
                        var s = spec.Series[i];
                        var scatter = plt.Add.Scatter(s.X!, s.Y!, colors[i % colors.Length]);
                        scatter.LegendText = s.Name;
                        scatter.MarkerSize = 6;
                        scatter.LineWidth = 2;
                    }

                    if (spec.Series.Count > 1) plt.ShowLegend();
                    plt.SavePng(output, 800, 600);
                });

                var aerr = CliHelpers.CheckArtifact(output, "PNG");
                if (aerr != null) { CliHelpers.WriteError("chart line", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("chart line",
                        $"Line chart saved: {output}",
                        new { series = seriesCount, points = totalPoints });
                    outputJson.Artifacts["png"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Line chart saved: {Path.GetFullPath(output)}");
                }
            }
            catch (JsonException jex)
            {
                CliHelpers.WriteError("chart line",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {jex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart line",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== chart scatter =====

    static Command CreateScatterChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("scatter", "Scatter plot") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart scatter", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ScatterSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Points == null || spec.Points.Count == 0)
                {
                    CliHelpers.WriteError("chart scatter",
                        ErrorCodes.ValidationFailed with { Message = "points array must be non-empty." }, json);
                    return;
                }

                foreach (var p in spec.Points)
                {
                    if (double.IsNaN(p.X) || double.IsInfinity(p.X) ||
                        double.IsNaN(p.Y) || double.IsInfinity(p.Y))
                    {
                        CliHelpers.WriteError("chart scatter",
                            ErrorCodes.ValidationFailed with { Message = "Point coordinates must not be NaN or Infinity." }, json);
                        return;
                    }
                }

                // Group points by group name
                var groups = new Dictionary<string, (List<double> Xs, List<double> Ys)>();
                foreach (var p in spec.Points)
                {
                    var key = string.IsNullOrWhiteSpace(p.Group) ? "default" : p.Group!;
                    if (!groups.ContainsKey(key))
                        groups[key] = (new List<double>(), new List<double>());
                    groups[key].Xs.Add(p.X);
                    groups[key].Ys.Add(p.Y);
                }

                int pointCount = spec.Points.Count;
                bool hasTrendline = spec.Trendline && spec.Points.Count >= 2;

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var plt = new Plot();
                    plt.Font.Set(ChartBuilder.GetCjkFontFamily());
                    plt.Title(spec.Title ?? "");
                    plt.XLabel(spec.XLabel ?? "");
                    plt.YLabel(spec.YLabel ?? "");

                    var colors = BarChartConfig.DefaultColors;
                    int gi = 0;
                    bool showLegend = groups.Count > 1 || (groups.Count == 1 && !groups.ContainsKey("default"));

                    foreach (var kv in groups)
                    {
                        var xs = kv.Value.Xs.ToArray();
                        var ys = kv.Value.Ys.ToArray();
                        var scatter = plt.Add.ScatterPoints(xs, ys, colors[gi % colors.Length]);
                        scatter.MarkerSize = 8;
                        if (showLegend)
                            scatter.LegendText = kv.Key;
                        gi++;
                    }

                    // Trendline: overall linear regression across all points
                    if (hasTrendline)
                    {
                        var allX = spec.Points.Select(p => p.X).ToArray();
                        var allY = spec.Points.Select(p => p.Y).ToArray();
                        double sumX = allX.Sum(), sumY = allY.Sum();
                        double sumXY = allX.Zip(allY, (a, b) => a * b).Sum();
                        double sumX2 = allX.Select(a => a * a).Sum();
                        int n = allX.Length;
                        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                        double intercept = (sumY - slope * sumX) / n;

                        double xMin = allX.Min(), xMax = allX.Max();
                        var regLine = plt.Add.Scatter(
                            new double[] { xMin, xMax },
                            new double[] { slope * xMin + intercept, slope * xMax + intercept });
                        regLine.MarkerSize = 0;
                        regLine.LineWidth = 2;
                        regLine.LineColor = new Color(200, 50, 50);
                        regLine.LegendText = $"y={slope:F3}x+{intercept:F3}";
                    }

                    if (showLegend || hasTrendline) plt.ShowLegend();
                    plt.SavePng(output, 800, 600);
                });

                var aerr = CliHelpers.CheckArtifact(output, "PNG");
                if (aerr != null) { CliHelpers.WriteError("chart scatter", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("chart scatter",
                        $"Scatter plot saved: {output}",
                        new { points = pointCount, groups = groups.Count, trendline = spec.Trendline });
                    outputJson.Artifacts["png"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Scatter plot saved: {Path.GetFullPath(output)}");
                }
            }
            catch (JsonException jex)
            {
                CliHelpers.WriteError("chart scatter",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {jex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart scatter",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== chart pie =====

    static Command CreatePieChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("pie", "Pie chart") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart pie", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<PieSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Values == null || spec.Values.Count < 2)
                {
                    CliHelpers.WriteError("chart pie",
                        ErrorCodes.ValidationFailed with { Message = "values array must have at least 2 entries." }, json);
                    return;
                }

                foreach (var v in spec.Values)
                {
                    if (string.IsNullOrWhiteSpace(v.Label))
                    {
                        CliHelpers.WriteError("chart pie",
                            ErrorCodes.ValidationFailed with { Message = "Each value must have a label." }, json);
                        return;
                    }
                    if (v.Value <= 0)
                    {
                        CliHelpers.WriteError("chart pie",
                            ErrorCodes.ValidationFailed with { Message = $"Value '{v.Label}' must be > 0, got {v.Value}." }, json);
                        return;
                    }
                }

                var slices = new Dictionary<string, double>();
                foreach (var v in spec.Values)
                    slices[v.Label!] = v.Value;

                int sliceCount = spec.Values.Count;
                double total = spec.Values.Sum(v => v.Value);

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    ChartTypes.PieChart(slices, spec.Title ?? "", output,
                        colors: BarChartConfig.DefaultColors, width: 800, height: 600,
                        showLabels: true, showValues: true, showPercent: true);
                });

                var aerr = CliHelpers.CheckArtifact(output, "PNG");
                if (aerr != null) { CliHelpers.WriteError("chart pie", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("chart pie",
                        $"Pie chart saved: {output}",
                        new { slices = sliceCount, total = Math.Round(total, 2) });
                    outputJson.Artifacts["png"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Pie chart saved: {Path.GetFullPath(output)}");
                }
            }
            catch (JsonException jex)
            {
                CliHelpers.WriteError("chart pie",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {jex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart pie",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }
}

// === JSON spec models for chart commands ===

internal class LineSpec
{
    public string? Title { get; set; }
    public string? XLabel { get; set; }
    public string? YLabel { get; set; }
    public List<LineSeriesEntry> Series { get; set; } = new();
}

internal class LineSeriesEntry
{
    public string? Name { get; set; }
    public double[]? X { get; set; }
    public double[]? Y { get; set; }
}

internal class ScatterSpec
{
    public string? Title { get; set; }
    public string? XLabel { get; set; }
    public string? YLabel { get; set; }
    public List<ScatterPointEntry> Points { get; set; } = new();
    public bool Trendline { get; set; }
}

internal class ScatterPointEntry
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Group { get; set; }
}

internal class PieSpec
{
    public string? Title { get; set; }
    public List<PieValueEntry> Values { get; set; } = new();
}

internal class PieValueEntry
{
    public string? Label { get; set; }
    public double Value { get; set; }
}
