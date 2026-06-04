# Angri450.Nong.Chart

学术图表 + 统计分析一站式工具包。angri450 实现了 ANOVA 方差分析和 Duncan 多重比较（MRC 标准农业统计方法），18 种图表类型直接出图，为农学论文量身打造。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Chart)](https://www.nuget.org/packages/Angri450.Nong.Chart)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Chart
```

## Quick Start

```csharp
using ChartCore;

var data = new Dictionary<string, List<double>>
{
    ["Treatment A"] = new() { 95, 88, 92, 78, 85 },
    ["Treatment B"] = new() { 72, 68, 75, 70, 73 },
    ["Treatment C"] = new() { 55, 58, 52, 60, 57 },
};

// 基础柱状图
ChartBuilder.BarChart(data, "Treatment Effects", "Height (cm)", "chart.png");

// 带 ANOVA + Duncan MRT 显著性标注
var analysis = StatsEngine.FullAnalysis(data);
analysis.Print();
ChartBuilder.BarChartWithSignificance(data, analysis.Duncan.Labels,
    "Effects", "cm", "chart-sig.png");
```

## Chart Types

angri450 实现了 18 种图表类型：柱状图、饼图、环图、折线图、面积图、散点图、箱线图、直方图、雷达图、K 线图、气泡图、热力图、仪表盘、南丁格尔玫瑰图、棒棒糖图、人口金字塔、函数图、误差棒图。

## Statistical Analysis

angri450 按照农业科学惯例实现了完整的统计管线：

### ANOVA

```csharp
var result = StatsEngine.OneWayAnova(data);
Console.WriteLine($"F = {result.FValue:F2}, p = {result.PValue:F4}");
```

### Duncan MRT

```csharp
var duncan = StatsEngine.DuncanMrt(data);
foreach (var g in duncan.Groups)
    Console.WriteLine($"Group {g.Label}: {string.Join(", ", g.Treatments)}");
```

### 完整分析管线

```csharp
var full = StatsEngine.FullAnalysis(data);
// 描述性统计 → 正态性检验 → ANOVA → Duncan MRT → 显著性标注
full.Print();           // 控制台输出
full.ToTable();         // Markdown 表格
```

## Dependencies

- `Angri450.Nong.ThirdParty` — 合并基础库（ScottPlot + SkiaSharp + 全部传递依赖）

## API Reference

| Class | Description |
|-------|-------------|
| `ChartBuilder` | 18 种图表静态方法，输出到文件或流 |
| `StatsEngine` | ANOVA、Duncan MRT、描述性统计、完整分析管线 |
| `DataLoader` | 从 CSV、JSON、Dictionary 导入数据 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0
