using System.Globalization;
using System.CommandLine;
using System.Text.Json;
using ChartCore;
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
        cmd.AddCommand(CreateBoxplotChart(jsonOpt));
        cmd.AddCommand(CreateHistogramChart(jsonOpt));
        cmd.AddCommand(CreateHeatmapChart(jsonOpt));
        cmd.AddCommand(CreateRadarChart(jsonOpt));

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

                var workerArgs = new List<string> { "chart", "bar", "--file", file, "--output", output, "--error", error };
                NativeRenderWorkerHost.AddOption(workerArgs, "--title", title);
                NativeRenderWorkerHost.AddOption(workerArgs, "--ylabel", ylabel);
                if (noSig)
                    workerArgs.Add("--no-significance");
                NativeRenderWorkerHost.Run("chart bar", json, workerArgs);
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

                NativeRenderWorkerHost.Run("chart line", json,
                    new[] { "chart", "line", "--file", file, "--output", output });
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

                NativeRenderWorkerHost.Run("chart scatter", json,
                    new[] { "chart", "scatter", "--file", file, "--output", output });
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

                NativeRenderWorkerHost.Run("chart pie", json,
                    new[] { "chart", "pie", "--file", file, "--output", output });
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

    // ===== chart boxplot =====

    static Command CreateBoxplotChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json) with groups");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var ylabelOpt = new Option<string>("--ylabel", () => "", "Y-axis label");
        var cmd = new Command("boxplot", "Box plot for treatment group distribution") { fileArg, outOpt, titleOpt, ylabelOpt };

        cmd.SetHandler((string file, string output, string title, string ylabel, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart boxplot", err, json); return; }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr = StatsValidation.Validate(groups, "chart boxplot");
                if (verr != null) { CliHelpers.WriteError("chart boxplot", verr, json); return; }

                var workerArgs = new List<string> { "chart", "boxplot", "--file", file, "--output", output };
                NativeRenderWorkerHost.AddOption(workerArgs, "--title", title);
                NativeRenderWorkerHost.AddOption(workerArgs, "--ylabel", ylabel);
                NativeRenderWorkerHost.Run("chart boxplot", json, workerArgs);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart boxplot",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, titleOpt, ylabelOpt, jsonOpt);

        return cmd;
    }

    // ===== chart histogram =====

    static Command CreateHistogramChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json) with groups");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var xlabelOpt = new Option<string>("--xlabel", () => "", "X-axis label");
        var ylabelOpt = new Option<string>("--ylabel", () => "Frequency", "Y-axis label");
        var binCountOpt = new Option<int>("--bin-count", () => 20, "Number of bins");
        var cmd = new Command("histogram", "Histogram for data distribution") { fileArg, outOpt, titleOpt, xlabelOpt, ylabelOpt, binCountOpt };

        cmd.SetHandler((string file, string output, string title, string xlabel, string ylabel, int binCount, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart histogram", err, json); return; }

            try
            {
                var groups = DataLoader.FromJson(file);
                // Histogram merges all values; single group is valid
                if (groups.Count == 0 || groups.Values.All(v => v.Count == 0))
                {
                    CliHelpers.WriteError("chart histogram",
                        ErrorCodes.ValidationFailed with { Message = "Data file must contain at least one non-empty group." }, json);
                    return;
                }

                var workerArgs = new List<string> { "chart", "histogram", "--file", file, "--output", output, "--bin-count", binCount.ToString() };
                NativeRenderWorkerHost.AddOption(workerArgs, "--title", title);
                NativeRenderWorkerHost.AddOption(workerArgs, "--xlabel", xlabel);
                NativeRenderWorkerHost.AddOption(workerArgs, "--ylabel", ylabel);
                NativeRenderWorkerHost.Run("chart histogram", json, workerArgs);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart histogram",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, titleOpt, xlabelOpt, ylabelOpt, binCountOpt, jsonOpt);

        return cmd;
    }

    // ===== chart heatmap =====

    static Command CreateHeatmapChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json) with 2D array");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var colormapOpt = new Option<string>("--colormap", () => "", "Colormap name (e.g. Viridis, Plasma, Inferno)");
        var cmd = new Command("heatmap", "Heatmap chart from 2D data") { fileArg, outOpt, titleOpt, colormapOpt };

        cmd.SetHandler((string file, string output, string title, string colormap, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart heatmap", err, json); return; }

            try
            {
                var workerArgs = new List<string> { "chart", "heatmap", "--file", file, "--output", output };
                NativeRenderWorkerHost.AddOption(workerArgs, "--title", title);
                NativeRenderWorkerHost.AddOption(workerArgs, "--colormap", colormap);
                NativeRenderWorkerHost.Run("chart heatmap", json, workerArgs);
            }
            catch (Exception ex) { CliHelpers.WriteError("chart heatmap", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, colormapOpt, jsonOpt);
        return cmd;
    }

    // ===== chart radar =====

    static Command CreateRadarChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON with categories and series");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var cmd = new Command("radar", "Radar/spider chart for multi-index comparison") { fileArg, outOpt, titleOpt };

        cmd.SetHandler((string file, string output, string title, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart radar", err, json); return; }

            try
            {
                var workerArgs = new List<string> { "chart", "radar", "--file", file, "--output", output };
                NativeRenderWorkerHost.AddOption(workerArgs, "--title", title);
                NativeRenderWorkerHost.Run("chart radar", json, workerArgs);
            }
            catch (Exception ex) { CliHelpers.WriteError("chart radar", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, titleOpt, jsonOpt);
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

internal class HeatmapSpec
{
    public double[][]? Data { get; set; }
    public int Rows { get; set; }
    public int Cols { get; set; }
    public string? Colormap { get; set; }
}

internal class RadarSpec
{
    public string[]? Categories { get; set; }
    public List<RadarSeriesEntry> Series { get; set; } = new();
}

internal class RadarSeriesEntry
{
    public string? Name { get; set; }
    public double[]? Values { get; set; }
}
