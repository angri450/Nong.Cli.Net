# Angri450.Nong.Pptx

Fluent PowerPoint builder for AI agents. 10 built-in themes, 6 layout types, CJK font support.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Pptx)](https://www.nuget.org/packages/Angri450.Nong.Pptx)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Pptx
```

## Quick Start

```csharp
using PptxCore;

SlideBuilder.Create()
    .Theme(ThemePreset.Professional)
    .AddTitleSlide(opt => opt
        .Title("Q1 Report")
        .Subtitle("Business Review")
        .Author("Alice"))
    .AddContentSlide(opt => opt
        .Title("Key Results")
        .Bullets("Revenue +30%", "Users doubled", "NPS score 72"))
    .AddTableSlide(opt => opt
        .Title("KPIs")
        .Data(new[] {
            new[] { "Metric", "Target", "Actual" },
            new[] { "Revenue", "100M", "120M" },
            new[] { "Users", "2M", "2.5M" }
        }))
    .AddChartSlide(opt => opt
        .Title("Trend")
        .ChartTitle("Revenue")
        .BarChart(new Dictionary<string, double> {
            { "Q1", 10 }, { "Q2", 12 }, { "Q3", 14 }, { "Q4", 18 }
        }))
    .Save("output.pptx");
```

## Themes (10 Built-in)

`Professional`, `Academic`, `Modern`, `Minimal`, `Warm`, `Cool`, `midnight-executive`, `coral-energy`, `teal-trust`, `cherry-bold`.

```csharp
// JSON custom theme
var theme = ThemePreset.BuildFromJson("formats/cherry-bold.json");
SlideBuilder.Create().Theme(theme).AddTitleSlide(...).Save("output.pptx");
```

## Layout System

Six layout types based on visual gravity distribution:

| Layout | Description | Best For |
|--------|-------------|----------|
| `SingleFocus` | 1-2 elements, large whitespace | Title slides, key messages |
| `Symmetric` | 50/50 dual columns | Comparisons, before/after |
| `Asymmetric` | 2/3 + 1/3 split | Main finding + supporting info |
| `ThreeColumn` | 3 balanced columns | Feature lists, metrics |
| `PrimarySecondary` | 1 main + 2-3 supporting | Key insight + evidence |
| `HeroTop` | Large banner + bottom cards | Summary dashboards |

```csharp
.AddSlide(slide => slide.Symmetric(
    leftTitle: "Before",
    leftContent: "Manual process, 8 hours",
    rightTitle: "After",
    rightContent: "Automated, 15 minutes"
))
.AddSlide(slide => slide.ThreeColumn(
    col1Title: "Speed", col1Content: "2x faster",
    col2Title: "Quality", col2Content: "95% accuracy",
    col3Title: "Cost", col3Content: "50% reduction"
))
```

## Card Layouts

`TwoColumns`, `Cards`, `BigNumber`, `Quote` — pre-designed card helpers for common presentation patterns.

## CJK Font Support

Automatic `<a:ea>` element injection into every text run. Chinese/Japanese/Korean fonts render correctly without manual XML editing.

## Inspection and Validation

```csharp
var map = SlidePreview.ShapeMap("output.pptx");
Console.WriteLine(map.Json);   // Structured JSON of all shapes

SlideValidator.ValidateAndReport("output.pptx");  // Structural integrity check
```

## Dependencies

- `ShapeCrawler` — OpenXML SDK wrapper for PPTX manipulation

## API Reference

| Class | Description |
|-------|-------------|
| `SlideBuilder` | Chainable slide creation entry point |
| `PresentationBuilder` | Multi-slide presentation builder |
| `ThemePreset` | 10 themes, JSON theme loading |
| `SlideHelper` | Layout helpers (SingleFocus, Symmetric, etc.) |
| `SlidePreview` | Shape inspection and JSON export |
| `SlideValidator` | Structural validation |
| `RawAccessor` | Low-level OOXML access |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
