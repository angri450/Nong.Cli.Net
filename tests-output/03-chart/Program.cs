using ChartCore;

// ── 1. Sample data ──
var groups = new Dictionary<string, List<double>>
{
    ["Treatment A"] = new() { 95, 88, 92, 78, 85 },
    ["Treatment B"] = new() { 72, 68, 75, 70, 73 },
    ["Treatment C"] = new() { 55, 58, 52, 60, 57 },
};

string outDir = AppContext.BaseDirectory;
int pass = 0, fail = 0;

void PrintException(Exception ex)
{
    var inner = ex;
    while (inner != null)
    {
        Console.WriteLine($"  [{inner.GetType().Name}] {inner.Message}");
        if (inner.StackTrace != null)
            Console.WriteLine(inner.StackTrace);
        inner = inner.InnerException;
    }
}

// ── 2. BarChart ──
try
{
    string barPath = Path.Combine(outDir, "chart-bar.png");
    ChartBuilder.BarChart(groups, "Treatment Effect Comparison", "Yield (kg/ha)", barPath);
    if (File.Exists(barPath))
    {
        Console.WriteLine($"PASS BarChart -> {barPath} ({new FileInfo(barPath).Length} bytes)");
        pass++;
    }
    else
    {
        Console.WriteLine($"FAIL BarChart -> file not created: {barPath}");
        fail++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL BarChart:");
    PrintException(ex);
    fail++;
}

// ── 3. PieChart ──
try
{
    var pieSlices = groups.ToDictionary(g => g.Key, g => g.Value.Average());
    string piePath = Path.Combine(outDir, "chart-pie.png");
    ChartTypes.PieChart(pieSlices, "Mean Yield Distribution", piePath);
    if (File.Exists(piePath))
    {
        Console.WriteLine($"PASS PieChart -> {piePath} ({new FileInfo(piePath).Length} bytes)");
        pass++;
    }
    else
    {
        Console.WriteLine($"FAIL PieChart -> file not created: {piePath}");
        fail++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL PieChart:");
    PrintException(ex);
    fail++;
}

// ── 4. LineChart ──
try
{
    var lineData = groups.ToDictionary(
        g => g.Key,
        g => g.Value.ToArray());
    double[] xs = Enumerable.Range(1, lineData.First().Value.Length).Select(i => (double)i).ToArray();
    string linePath = Path.Combine(outDir, "chart-line.png");
    ChartTypes.LineChart(lineData, xs, "Replicate Trend", "Replicate", "Yield (kg/ha)", linePath);
    if (File.Exists(linePath))
    {
        Console.WriteLine($"PASS LineChart -> {linePath} ({new FileInfo(linePath).Length} bytes)");
        pass++;
    }
    else
    {
        Console.WriteLine($"FAIL LineChart -> file not created: {linePath}");
        fail++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL LineChart:");
    PrintException(ex);
    fail++;
}

// ── 5. BoxPlot ──
try
{
    string boxPath = Path.Combine(outDir, "chart-boxplot.png");
    ChartTypes.BoxPlot(groups, "Yield Distribution by Treatment", "Yield (kg/ha)", boxPath);
    if (File.Exists(boxPath))
    {
        Console.WriteLine($"PASS BoxPlot -> {boxPath} ({new FileInfo(boxPath).Length} bytes)");
        pass++;
    }
    else
    {
        Console.WriteLine($"FAIL BoxPlot -> file not created: {boxPath}");
        fail++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL BoxPlot:");
    PrintException(ex);
    fail++;
}

// ── 6. BarChart with significance ──
try
{
    var anova = StatsEngine.OneWayAnova(groups);
    var duncan = StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW, 0.05);
    var sigLabels = duncan.Groups.ToDictionary(g => g.Label, g => g.Significance);
    string sigPath = Path.Combine(outDir, "chart-bar-sig.png");
    ChartBuilder.BarChartWithSignificance(groups, sigLabels, "Treatment Effect (Duncan MRT)", "Yield (kg/ha)", sigPath);
    if (File.Exists(sigPath))
    {
        Console.WriteLine($"PASS BarChartWithSignificance -> {sigPath} ({new FileInfo(sigPath).Length} bytes)");
        pass++;
    }
    else
    {
        Console.WriteLine($"FAIL BarChartWithSignificance -> file not created: {sigPath}");
        fail++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL BarChartWithSignificance:");
    PrintException(ex);
    fail++;
}

// ── 7. StatsEngine: ANOVA + Duncan MRT ──
Console.WriteLine();
try
{
    var result = StatsEngine.FullAnalysis(groups);
    result.Print();
    pass++;
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL FullAnalysis:");
    PrintException(ex);
    fail++;
}

// ── Summary ──
Console.WriteLine();
Console.WriteLine($"=== SUMMARY: {pass} PASS, {fail} FAIL ===");
