# Angri450.Nong

Pure .NET scientific document generation toolkit. Zero JS dependencies.

[中文说明（README.zh-CN.md）](./README.zh-CN.md)

## Packages

| Package | Version | Description |
|---------|---------|-------------|
| `Angri450.Nong.Excel` | 1.0.4 | Excel generation with ClosedXML. 13 advanced features: pivot tables, sparklines, auto filters, comments, hyperlinks, rich text, sheet protection, named ranges, conditional formatting, sorting, print setup. |
| `Angri450.Nong.Chart` | 1.0.3 | Statistical charts with ScottPlot. 18 chart types: bar, pie, donut, line, area, scatter, box plot, histogram, radar, stock/candlestick, bubble, heatmap, gauge, coxcomb, lollipop, population, function plot, error bar. ANOVA + Duncan MRT statistical analysis. |
| `Angri450.Nong.Diagram` | 1.0.0 | Scientific diagrams with MSAGL + SkiaSharp. Flowcharts (Sugiyama layout), network graphs (force-directed layout), phylogenetic trees (Newick parser), 40 bioicons SVG icons. |

## Quick Start

### Excel

```powershell
dotnet new console -n ExcelWriter -o MyExcel --force
cd MyExcel
dotnet add package Angri450.Nong.Excel --version 1.0.4
```

```csharp
using ClosedXML.Excel;
using ExcelCore;

var wb = new XLWorkbook();
ExcelBuilder.Sheet(wb, "Data")
    .Headers("Name", "Score", "Grade")
    .Data(new[] {
        new[] { "Alice", "95", "A" },
        new[] { "Bob", "87", "B+" }
    })
    .ColumnWidths(15, 10, 10)
    .HeaderStyle("#1F4E79", "#FFFFFF")
    .AlternatingRows(2, "#F5F5F5");

FormulaValidator.SaveWithEvaluation(wb, "output.xlsx");
```

### Chart

```powershell
dotnet new console -n ChartWriter -o MyChart --force
cd MyChart
dotnet add package Angri450.Nong.Chart --version 1.0.3
```

```csharp
using ChartCore;

var data = DataLoader.FromJson("data.json");
var analysis = StatsEngine.FullAnalysis(data);
analysis.Print();

var sigLabels = analysis.Duncan.Groups
    .ToDictionary(g => g.Label, g => g.Significance);

ChartBuilder.BarChartWithSignificance(
    data, sigLabels,
    "Treatment Effects", "Height (cm)",
    "chart.png", 800, 600);
```

### Diagram

```powershell
dotnet new console -n DiagramWriter -o MyDiagram --force
cd MyDiagram
dotnet add package Angri450.Nong.Diagram --version 1.0.0
```

```csharp
using DiagramCore;
using DiagramCore.Models;

// Flowchart
var graph = new Graph();
graph.AddNode("A", "Sample Collection");
graph.AddNode("B", "DNA Extraction");
graph.AddNode("C", "PCR Amplification");
graph.AddEdge("A", "B");
graph.AddEdge("B", "C");
DiagramBuilder.Flowchart(graph, "workflow.png", 800, 600);

// Phylogenetic tree
var newick = "((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);";
DiagramBuilder.PhylogeneticTree(newick, "tree.png", radial: false, 800, 600);
```

## Architecture

```
Angri450.Nong/
├── Excel/          Angri450.Nong.Excel (ExcelCore)
├── Chart/          Angri450.Nong.Chart (ChartCore)
├── Diagram/        Angri450.Nong.Diagram (DiagramCore)
├── ClosedXML/      Inlined ClosedXML source (585 files)
├── ClosedXML.IO/   Inlined ClosedXML.IO (5 files)
├── ClosedXML.Parser/ Inlined formula parser (44 files)
├── ExcelNumberFormat/ Inlined number format (16 files)
├── RBush/          Inlined spatial index (9 files)
├── SixLabors.Fonts/ Inlined font library (416 files)
├── DocumentFormat.OpenXml/ Inlined OpenXml SDK (78 files)
├── DocumentFormat.OpenXml.Framework/ Inlined framework (305 files)
├── ScottPlot/      Inlined ScottPlot (575 files)
├── SkiaSharp/      Inlined SkiaSharp binding (90 files)
├── HarfBuzzSharp/  Inlined HarfBuzzSharp binding (21 files)
├── SkiaSharp.HarfBuzz/ Inlined text shaping (6 files)
├── MSAGL/          Inlined graph layout (492 files)
├── MSAGL.Drawing/  Inlined graph drawing (59 files)
├── Bioicons/       40 SVG scientific icons
└── Tests/          Unit tests (xUnit)
```

## Dependency Chain

**Before (NuGet packages):**
- Excel: 8 packages
- Chart: 18 packages
- Diagram: Required JS ecosystem (Mermaid, Graphviz, Vega-Lite)

**After (source inlined):**
- Excel: 1 package (System.IO.Packaging)
- Chart: 7 packages (SkiaSharp/HarfBuzzSharp native assets)
- Diagram: 7 packages (same native assets)

## Requirements

- .NET SDK 11.0 (preview)
- Native SkiaSharp/HarfBuzzSharp binaries (auto-installed via NuGet)

## License

Apache-2.0

## Inlined Libraries

| Library | License | Files |
|---------|---------|-------|
| ClosedXML | MIT | 585 |
| DocumentFormat.OpenXml | MIT | 456 |
| ScottPlot | MIT | 575 |
| MSAGL | MIT | 551 |
| SixLabors.Fonts | Apache-2.0 | 416 |
| SkiaSharp | MIT | 90 |
| HarfBuzzSharp | MIT | 21 |
| ClosedXML.Parser | MIT | 44 |
| ExcelNumberFormat | MIT | 16 |
| RBush | MIT | 9 |
