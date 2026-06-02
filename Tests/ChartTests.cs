using ChartCore;
using Xunit;

namespace Tests;

/// <summary>Helper to detect whether SkiaSharp native library is available.</summary>
internal static class SkiaCheck
{
    private static bool? _available;

    /// <summary>Returns true if SkiaSharp native library is functional.</summary>
    public static bool IsAvailable()
    {
        if (!_available.HasValue)
        {
            try
            {
                // Attempt a simple SkiaSharp operation
                using var bmp = new SkiaSharp.SKBitmap(1, 1);
                _available = true;
            }
            catch
            {
                _available = false;
            }
        }
        return _available.Value;
    }
}

public class ChartTests : IDisposable
{
    private readonly string _tempDir;

    public ChartTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "chart-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ---- GroupStats ----

    [Fact]
    public void GroupStats_Compute_KnownValues()
    {
        var data = new List<double> { 2, 4, 6, 8, 10 };
        var stats = GroupStats.Compute(data);

        Assert.Equal(5, stats.N);
        Assert.Equal(6.0, stats.Mean, 4);
        Assert.True(stats.SD > 0);
        Assert.Equal(2.0, stats.Min);
        Assert.Equal(10.0, stats.Max);
        Assert.True(stats.SEM > 0);
        Assert.True(stats.SEM < stats.SD);
    }

    [Fact]
    public void GroupStats_Compute_EmptyList()
    {
        var stats = GroupStats.Compute(new List<double>());
        Assert.Equal(0, stats.N);
    }

    [Fact]
    public void GroupStats_Compute_SingleValue()
    {
        var stats = GroupStats.Compute(new List<double> { 42 });
        Assert.Equal(1, stats.N);
        Assert.Equal(42.0, stats.Mean, 4);
        Assert.Equal(0, stats.SD);
    }

    // ---- OneWayAnova ----

    [Fact]
    public void OneWayAnova_ThreeGroups_ProducesValidResult()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["Control"] = new() { 10.1, 10.5, 9.8, 10.3, 10.0 },
            ["TreatA"] = new() { 12.2, 12.8, 11.9, 12.5, 12.1 },
            ["TreatB"] = new() { 15.0, 15.5, 14.8, 15.2, 14.9 }
        };

        var result = StatsEngine.OneWayAnova(groups);

        Assert.True(result.F > 0, "F-value should be positive");
        Assert.True(result.P >= 0 && result.P <= 1, "P-value should be in [0,1]");
        Assert.True(result.SSB > 0, "SSB should be positive");
        Assert.True(result.SSW >= 0, "SSW should be non-negative");
        Assert.Equal(2, result.dfB);  // k-1 = 3-1 = 2
        Assert.Equal(12, result.dfW); // N-k = 15-3 = 12
        Assert.Equal(15, result.N);
        Assert.Equal(3, result.GroupStats.Count);
    }

    [Fact]
    public void OneWayAnova_SignificantDifference_LowPValue()
    {
        // Groups with clearly different means
        var groups = new Dictionary<string, List<double>>
        {
            ["Low"] = new() { 1, 2, 3, 2, 1 },
            ["Mid"] = new() { 10, 11, 10, 9, 10 },
            ["High"] = new() { 50, 51, 49, 50, 52 }
        };

        var result = StatsEngine.OneWayAnova(groups);

        Assert.True(result.F > 10, "F should be large for well-separated groups");
        Assert.True(result.P < 0.05, "P should be significant for well-separated groups");
    }

    [Fact]
    public void OneWayAnova_TooFewGroups_Throws()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["Only"] = new() { 1, 2, 3 }
        };

        Assert.Throws<InvalidOperationException>(() => StatsEngine.OneWayAnova(groups));
    }

    // ---- DuncanMRT ----

    [Fact]
    public void DuncanMRT_ProducesSignificanceLabels()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["A"] = new() { 10, 11, 10, 9, 10 },
            ["B"] = new() { 20, 21, 19, 20, 20 },
            ["C"] = new() { 30, 31, 29, 30, 30 }
        };

        var anova = StatsEngine.OneWayAnova(groups);
        var duncan = StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW);

        Assert.Equal(3, duncan.Groups.Count);
        Assert.True(duncan.Alpha > 0);

        foreach (var g in duncan.Groups)
        {
            Assert.False(string.IsNullOrEmpty(g.Significance),
                $"Group '{g.Label}' should have a significance label");
        }
    }

    [Fact]
    public void DuncanMRT_WellseparatedGroups_DifferentLabels()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["Low"] = new() { 1, 2, 1, 2, 1 },
            ["Mid"] = new() { 50, 51, 50, 49, 50 },
            ["High"] = new() { 100, 101, 99, 100, 100 }
        };

        var anova = StatsEngine.OneWayAnova(groups);
        var duncan = StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW);

        var labels = duncan.Groups.Select(g => g.Significance).Distinct().ToList();
        Assert.True(labels.Count >= 2, "Well-separated groups should have different significance labels");
    }

    // ---- FullAnalysis ----

    [Fact]
    public void FullAnalysis_CombinesAnovaAndDuncan()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["T1"] = new() { 5.1, 5.3, 5.0, 5.2, 5.1 },
            ["T2"] = new() { 7.2, 7.5, 7.1, 7.3, 7.4 },
            ["T3"] = new() { 9.8, 9.5, 10.0, 9.7, 9.6 }
        };

        var result = StatsEngine.FullAnalysis(groups);

        Assert.NotNull(result.Anova);
        Assert.NotNull(result.Duncan);
        Assert.True(result.Anova.F > 0);
        Assert.Equal(3, result.Duncan.Groups.Count);
    }

    [Fact]
    public void FullAnalysis_Print_DoesNotThrow()
    {
        var groups = new Dictionary<string, List<double>>
        {
            ["A"] = new() { 1, 2, 3 },
            ["B"] = new() { 4, 5, 6 },
            ["C"] = new() { 7, 8, 9 }
        };

        var result = StatsEngine.FullAnalysis(groups);
        var sw = new StringWriter();
        result.Print(sw);

        var output = sw.ToString();
        Assert.Contains("ANOVA", output);
        Assert.Contains("Duncan", output);
    }

    // ---- DataLoader ----

    [Fact]
    public void DataLoader_FromJson_ReadsGroups()
    {
        var jsonPath = Path.Combine(_tempDir, "data.json");
        File.WriteAllText(jsonPath, """{"G1":[1.0,2.0,3.0],"G2":[4.0,5.0,6.0]}""");

        var groups = DataLoader.FromJson(jsonPath);

        Assert.Equal(2, groups.Count);
        Assert.True(groups.ContainsKey("G1"));
        Assert.True(groups.ContainsKey("G2"));
        Assert.Equal(3, groups["G1"].Count);
        Assert.Equal(1.0, groups["G1"][0]);
    }

    [Fact]
    public void DataLoader_FromCsv_ReadsGroups()
    {
        var csvPath = Path.Combine(_tempDir, "data.csv");
        File.WriteAllText(csvPath, "A,1.5\nA,2.5\nB,3.5\nB,4.5\n");

        var groups = DataLoader.FromCsv(csvPath);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["A"].Count);
        Assert.Equal(1.5, groups["A"][0]);
        Assert.Equal(2.5, groups["A"][1]);
    }

    // ---- ChartBuilder ----

    [Fact]
    public void ChartBuilder_BarChart_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var groups = new Dictionary<string, List<double>>
        {
            ["A"] = new() { 10, 12, 11 },
            ["B"] = new() { 20, 22, 21 },
            ["C"] = new() { 15, 14, 16 }
        };

        var outPath = Path.Combine(_tempDir, "bar.png");
        ChartBuilder.BarChart(groups, "Test Bar", "Value", outPath);

        Assert.True(File.Exists(outPath), "PNG file should exist");
        Assert.True(new FileInfo(outPath).Length > 0, "PNG file should be non-empty");
    }

    [Fact]
    public void ChartBuilder_BarChartWithSignificance_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var groups = new Dictionary<string, List<double>>
        {
            ["A"] = new() { 10, 12, 11 },
            ["B"] = new() { 20, 22, 21 }
        };
        var sig = new Dictionary<string, string>
        {
            ["A"] = "a",
            ["B"] = "b"
        };

        var outPath = Path.Combine(_tempDir, "bar-sig.png");
        ChartBuilder.BarChartWithSignificance(groups, sig, "Sig Test", "Value", outPath);

        Assert.True(File.Exists(outPath), "PNG file should exist");
        Assert.True(new FileInfo(outPath).Length > 0, "PNG file should be non-empty");
    }

    // ---- ChartTypes ----

    [Fact]
    public void ChartTypes_PieChart_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var slices = new Dictionary<string, double>
        {
            ["Category A"] = 30,
            ["Category B"] = 45,
            ["Category C"] = 25
        };

        var outPath = Path.Combine(_tempDir, "pie.png");
        ChartTypes.PieChart(slices, "Pie Test", outPath);

        Assert.True(File.Exists(outPath), "PNG file should exist");
        Assert.True(new FileInfo(outPath).Length > 0, "PNG file should be non-empty");
    }

    [Fact]
    public void ChartTypes_LineChart_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var xs = new double[] { 1, 2, 3, 4, 5 };
        var series = new Dictionary<string, double[]>
        {
            ["Series1"] = new double[] { 2, 4, 6, 8, 10 },
            ["Series2"] = new double[] { 1, 3, 5, 7, 9 }
        };

        var outPath = Path.Combine(_tempDir, "line.png");
        ChartTypes.LineChart(series, xs, "Line Test", "X", "Y", outPath);

        Assert.True(File.Exists(outPath), "PNG file should exist");
        Assert.True(new FileInfo(outPath).Length > 0, "PNG file should be non-empty");
    }

    [Fact]
    public void ChartTypes_BoxPlot_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var groups = new Dictionary<string, List<double>>
        {
            ["G1"] = new() { 1, 2, 3, 4, 5, 6, 7, 8 },
            ["G2"] = new() { 3, 4, 5, 6, 7, 8, 9, 10 }
        };

        var outPath = Path.Combine(_tempDir, "box.png");
        ChartTypes.BoxPlot(groups, "Box Test", "Value", outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    // ---- ChartCombine ----

    [Fact]
    public void ChartCombine_MergeHorizontal_CombinesImages()
    {
        if (!SkiaCheck.IsAvailable()) return;
        // Create two small chart images first
        var groups1 = new Dictionary<string, List<double>>
        {
            ["A"] = new() { 10, 11, 12 },
            ["B"] = new() { 20, 21, 22 }
        };
        var groups2 = new Dictionary<string, List<double>>
        {
            ["C"] = new() { 30, 31, 32 },
            ["D"] = new() { 40, 41, 42 }
        };

        var path1 = Path.Combine(_tempDir, "c1.png");
        var path2 = Path.Combine(_tempDir, "c2.png");
        ChartBuilder.BarChart(groups1, "Chart 1", "Y", path1, width: 400, height: 300);
        ChartBuilder.BarChart(groups2, "Chart 2", "Y", path2, width: 400, height: 300);

        var mergedPath = Path.Combine(_tempDir, "merged.png");
        ChartCombine.MergeHorizontal(
            new[] { path1, path2 },
            new[] { "A", "B" },
            mergedPath);

        Assert.True(File.Exists(mergedPath), "Merged file should exist");
        Assert.True(new FileInfo(mergedPath).Length > 0, "Merged file should be non-empty");
    }

    [Fact]
    public void ChartCombine_MergeHorizontal_NoLabels_Works()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var groups = new Dictionary<string, List<double>>
        {
            ["X"] = new() { 5, 6, 7 }
        };
        var path1 = Path.Combine(_tempDir, "nolabel1.png");
        ChartBuilder.BarChart(groups, "T", "Y", path1, width: 300, height: 200);

        var mergedPath = Path.Combine(_tempDir, "merged-nolabel.png");
        ChartCombine.MergeHorizontal(new[] { path1 }, null, mergedPath);

        Assert.True(File.Exists(mergedPath));
    }
}
