namespace ChartCore;

public static class StatsEngine
{
    /// <summary>单因素方差分析 (One-Way ANOVA)</summary>
    public static AnovaResult OneWayAnova(Dictionary<string, List<double>> groups)
    {
        if (groups.Count < 2)
            throw new InvalidOperationException("ANOVA requires at least 2 groups.");

        // 聚合所有数据
        var allValues = groups.Values.SelectMany(v => v).ToList();
        if (allValues.Count == 0)
            throw new InvalidOperationException("No data.");

        int k = groups.Count;
        int N = groups.Values.Sum(v => v.Count);
        double grandMean = allValues.Average();

        // 组间 (Between)
        double SSB = 0;
        foreach (var (label, vals) in groups)
        {
            double groupMean = vals.Average();
            SSB += vals.Count * (groupMean - grandMean) * (groupMean - grandMean);
        }

        // 组内 (Within)
        double SSW = 0;
        foreach (var (label, vals) in groups)
        {
            double groupMean = vals.Average();
            SSW += vals.Sum(v => (v - groupMean) * (v - groupMean));
        }

        double SST = SSB + SSW;
        int dfB = k - 1;
        int dfW = N - k;
        double MSB = SSB / dfB;
        double MSW = SSW / dfW;
        double F = MSB / MSW;

        // F 分布 P 值近似 (Abramowitz & Stegun 26.6.2)
        double P = FDistributionPValue(F, dfB, dfW);

        // 各组描述性统计
        var groupStats = groups.ToDictionary(
            kv => kv.Key,
            kv => GroupStats.Compute(kv.Value));

        return new AnovaResult
        {
            F = F, P = P,
            SSB = SSB, SSW = SSW, SST = SST,
            MSB = MSB, MSW = MSW,
            dfB = dfB, dfW = dfW, N = N,
            GroupStats = groupStats
        };
    }

    /// <summary>Duncan 多重极差检验</summary>
    public static DuncanResult DuncanMRT(Dictionary<string, List<double>> groups,
        double MSW, int dfW, double alpha = 0.05)
    {
        var items = groups.Select(kv => new
        {
            Label = kv.Key,
            Mean = kv.Value.Average(),
            SD = Math.Sqrt(kv.Value.Sum(x => Math.Pow(x - kv.Value.Average(), 2)) / (kv.Value.Count - 1)),
            N = kv.Value.Count
        }).ToList();

        // 按均值排序
        items.Sort((a, b) => b.Mean.CompareTo(a.Mean));

        // 标准误
        double se = Math.Sqrt(MSW / items.Average(x => (double)x.N));

        // Duncan 极差表 (alpha = 0.05)
        // 对于 dfW <= ∞，取临界值 q(alpha, p, dfW)
        int[] ranks = Enumerable.Range(1, items.Count).ToArray();
        double[] qValues = ranks.Select(p => GetDuncanQ(alpha, p, dfW)).ToArray();

        // 比较矩阵
        var sigLabels = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var assigned = new HashSet<char>();
            for (int j = 0; j <= i; j++)
            {
                int p = Math.Abs(i - j) + 1;
                double diff = Math.Abs(items[i].Mean - items[j].Mean);
                double critical = qValues[p - 1] * se;

                if (diff < critical)
                    assigned.Add((char)('a' + j));
            }

            // 简化显著性标注：相同字母标在一起
            var labels = new bool[items.Count];
            for (int j = 0; j < items.Count; j++)
            {
                int p = Math.Abs(i - j) + 1;
                double diff = Math.Abs(items[i].Mean - items[j].Mean);
                double critical = qValues[p - 1] * se;
                if (diff < critical)
                {
                    // 找出这两个位置共享的字母
                    labels[j] = true;
                }
            }

            // 用连通分量标注
            // 简化：按差异分组分配字母
        }

        // 实际标注逻辑
        var result = new List<DuncanGroup>();
        for (int i = 0; i < items.Count; i++)
        {
            var sb = new System.Text.StringBuilder();
            bool[] connected = new bool[items.Count];
            for (int j = 0; j < items.Count; j++)
            {
                int p = Math.Abs(i - j) + 1;
                double diff = Math.Abs(items[i].Mean - items[j].Mean);
                double critical = qValues[p - 1] * se;
                if (diff < critical) connected[j] = true;
            }

            // 贪心给字母
            char[] letterForGroup = new char[items.Count];
            Array.Fill(letterForGroup, (char)0);
            for (int g = 0; g < items.Count; g++)
            {
                if (letterForGroup[g] != 0) continue;
                char assign = (char)('a' + g);
                letterForGroup[g] = assign;
                for (int h = g + 1; h < items.Count; h++)
                {
                    if (connected[g] && connected[h])
                    {
                        // check if they share connectivity
                        bool allConnected = true;
                        for (int k = g; k <= h; k++)
                        {
                            int pp = Math.Abs(k - g) + 1;
                            double dd = Math.Abs(items[g].Mean - items[k].Mean);
                            double cc = qValues[pp - 1] * se;
                            if (dd >= cc) { allConnected = false; break; }
                        }
                        if (allConnected) letterForGroup[h] = assign;
                    }
                }
            }
            string sig = letterForGroup[i].ToString();
            result.Add(new DuncanGroup
            {
                Label = items[i].Label,
                Mean = items[i].Mean,
                SD = items[i].SD,
                Significance = sig
            });
        }

        // 修复显著性标注：用标准 Duncan 分组逻辑
        // 对排序后均值，递减比较相邻差值
        for (int i = 0; i < result.Count; i++)
        {
            char best = 'a';
            for (int j = 0; j < i; j++)
            {
                int p = i - j + 1;
                double diff = result[j].Mean - result[i].Mean;
                double critical = qValues[p - 1] * se;
                if (diff < critical)
                {
                    best = (char)(Math.Max(best, result[j].Significance[0]));
                }
            }
            if (i > 0)
            {
                double diff = result[i - 1].Mean - result[i].Mean;
                double critical = qValues[1] * se;
                if (diff >= critical)
                    best = (char)(result[i - 1].Significance[0] + 1);
                if (best > 'z') best = 'z';
            }
            result[i].Significance = best.ToString();
        }

        return new DuncanResult
        {
            Groups = result,
            Alpha = alpha,
            MSE = MSW,
            dfError = dfW
        };
    }

    /// <summary>一次性运行 ANOVA + Duncan MRT</summary>
    public static FullAnalysisResult FullAnalysis(Dictionary<string, List<double>> groups, double alpha = 0.05)
    {
        var anova = OneWayAnova(groups);
        var duncan = DuncanMRT(groups, anova.MSW, anova.dfW, alpha);
        return new FullAnalysisResult { Anova = anova, Duncan = duncan };
    }

    // === 内部实现 ===

    private static double FDistributionPValue(double F, int df1, int df2)
    {
        // 正则化不完全 Beta 函数近似 P(F|df1,df2)
        double x = df2 / (df2 + df1 * F);
        return RegularizedIncompleteBeta(0.5 * df2, 0.5 * df1, x);
    }

    private static double RegularizedIncompleteBeta(double a, double b, double x)
    {
        // 连分数展开
        if (x < 0 || x > 1) return double.NaN;
        if (x == 0 || x == 1) return x;

        double front = Math.Exp(GammaLog(a + b) - GammaLog(a) - GammaLog(b) + a * Math.Log(x) + b * Math.Log(1 - x));

        // Lentz 连分数
        double c = 1, d = 1 - (a + b) * x / (a + 1);
        if (Math.Abs(d) < 1e-30) d = 1e-30;
        d = 1 / d;
        double h = d;

        int maxIter = 200;
        for (int m = 1; m <= maxIter; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((a + m2 - 1) * (a + m2));
            d = 1 + aa * d;
            if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = 1 + aa / c;
            if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1 / d;
            h *= d * c;

            aa = -(a + m) * (a + b + m) * x / ((a + m2) * (a + m2 + 1));
            d = 1 + aa * d;
            if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = 1 + aa / c;
            if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1 / d;
            double del = d * c;
            h *= del;

            if (Math.Abs(del - 1) < 1e-12) break;
        }

        return front * h / a;
    }

    private static double GammaLog(double x)
    {
        // Stirling + Lanczos 近似
        double[] coef = { 76.18009172947146, -86.50532032941677, 24.01409824083091,
                          -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;
        for (int j = 0; j < 6; j++) ser += coef[j] / ++y;
        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    // Duncan 临界值表 (alpha=0.05, dfW -> 每组重复数均衡时的近似)
    // 表值来自 Harter (1960) 对无穷自由度的近似
    private static readonly double[] QInf = { 0, 2.77, 2.92, 3.02, 3.09, 3.15, 3.19, 3.23, 3.26, 3.29,
                                              3.31, 3.33, 3.35, 3.37, 3.39, 3.40, 3.41, 3.43, 3.44, 3.45 };

    private static double GetDuncanQ(double alpha, int p, int df)
    {
        if (p <= 1) return 0;
        if (p > QInf.Length) p = QInf.Length;
        double qInf = QInf[p - 1];
        if (df >= 120) return qInf;
        // 小样本修正
        double correction = 1 + 1.0 / df;
        return qInf * correction;
    }
}
