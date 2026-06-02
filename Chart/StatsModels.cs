namespace ChartCore;

public class GroupStats
{
    public int N { get; set; }
    public double Mean { get; set; }
    public double SD { get; set; }
    public double SEM { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }

    public static GroupStats Compute(List<double> data)
    {
        var n = data.Count;
        if (n == 0) return new GroupStats { N = 0 };
        var mean = data.Average();
        var sd = n > 1 ? Math.Sqrt(data.Sum(x => (x - mean) * (x - mean)) / (n - 1)) : 0;
        return new GroupStats
        {
            N = n,
            Mean = mean,
            SD = sd,
            SEM = sd / Math.Sqrt(n),
            Min = data.Min(),
            Max = data.Max()
        };
    }
}

public class AnovaResult
{
    public double F { get; set; }
    public double P { get; set; }
    public double SSB { get; set; }
    public double SSW { get; set; }
    public double SST { get; set; }
    public double MSB { get; set; }
    public double MSW { get; set; }
    public int dfB { get; set; }
    public int dfW { get; set; }
    public int N { get; set; }
    public Dictionary<string, GroupStats> GroupStats { get; set; } = new();
}

public class DuncanGroup
{
    public string Label { get; set; } = "";
    public double Mean { get; set; }
    public double SD { get; set; }
    public string Significance { get; set; } = "";
}

public class DuncanResult
{
    public List<DuncanGroup> Groups { get; set; } = new();
    public double Alpha { get; set; }
    public double MSE { get; set; }
    public int dfError { get; set; }

    public string GetSignificance(string label)
    {
        return Groups.FirstOrDefault(g => g.Label == label)?.Significance ?? "";
    }
}

public class FullAnalysisResult
{
    public AnovaResult Anova { get; set; } = new();
    public DuncanResult Duncan { get; set; } = new();

    public void Print(TextWriter? writer = null)
    {
        var w = writer ?? Console.Out;
        w.WriteLine("=== ANOVA ===");
        w.WriteLine($"F({Anova.dfB},{Anova.dfW}) = {Anova.F:F4}, P = {Anova.P:F4}");
        w.WriteLine($"SSB = {Anova.SSB:F4}, SSW = {Anova.SSW:F4}, SST = {Anova.SST:F4}");
        w.WriteLine($"MSB = {Anova.MSB:F4}, MSW = {Anova.MSW:F4}");
        w.WriteLine();
        w.WriteLine("=== Group Statistics ===");
        w.WriteLine($"{"Group",-12} {"N",4} {"Mean",10} {"SD",10} {"SEM",10} {"Min",10} {"Max",10}");
        foreach (var (label, stats) in Anova.GroupStats)
            w.WriteLine($"{label,-12} {stats.N,4} {stats.Mean,10:F4} {stats.SD,10:F4} {stats.SEM,10:F4} {stats.Min,10:F4} {stats.Max,10:F4}");
        w.WriteLine();
        w.WriteLine($"=== Duncan MRT (alpha={Duncan.Alpha}) ===");
        foreach (var g in Duncan.Groups)
            w.WriteLine($"  {g.Label}: mean={g.Mean:F4}, sd={g.SD:F4}, sig={g.Significance}");
    }
}
