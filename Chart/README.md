# Angri450.Nong.Chart

Statistical chart generation for academic papers. 18 chart types with built-in ANOVA and Duncan MRT significance testing.

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

// Basic bar chart
ChartBuilder.BarChart(data, "Treatment Effects", "Height (cm)", "chart.png");

// With ANOVA + Duncan MRT
var analysis = StatsEngine.FullAnalysis(data);
analysis.Print();
ChartBuilder.BarChartWithSignificance(data, analysis.Duncan.Labels,
    "Effects", "cm", "chart-sig.png");
```

## Chart Types

Bar, pie, donut, line, area, scatter, box plot, histogram, radar, candlestick, bubble, heatmap, gauge, coxcomb, lollipop, population, function plot, error bar.

## Statistical Analysis

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

### Full Analysis Pipeline

```csharp
var full = StatsEngine.FullAnalysis(data);
// Descriptive stats → normality test → ANOVA → Duncan MRT → significance labels
full.Print();           // Console output
full.ToTable();         // Markdown table
```

## Dependencies

- `Angri450.Nong.ThirdParty` — merged foundation (ScottPlot + SkiaSharp + all transitive deps)

## API Reference

| Class | Description |
|-------|-------------|
| `ChartBuilder` | Static methods for all 18 chart types, output to file or stream |
| `StatsEngine` | ANOVA, Duncan MRT, descriptive statistics, full analysis pipeline |
| `DataLoader` | Import data from CSV, JSON, dictionary |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
