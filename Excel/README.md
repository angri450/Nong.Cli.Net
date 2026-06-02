# Angri450.Nong.Excel

Chainable Excel generation API over ClosedXML. Build professional spreadsheets with a fluent builder pattern.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Excel)](https://www.nuget.org/packages/Angri450.Nong.Excel)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Excel
```

> `ClosedXML.Excel` 等类型由 `Angri450.Nong.ThirdParty` 提供（已作为传递依赖自动安装）。**不要**额外安装 `ClosedXML` NuGet 包，否则会出现类型冲突（CS0433）。

## Quick Start

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
wb.SaveAs("output.xlsx");
```

## Features

### SheetBuilder (Chainable API)

| Method | Description |
|--------|-------------|
| `Headers(params string[])` | Set column headers |
| `Data(IEnumerable<string[]>)` | Populate data rows |
| `ColumnWidths(params double[])` | Set column widths |
| `HeaderStyle(bg, fg)` | Header row background and foreground color |
| `AlternatingRows(startRow, color)` | Alternating row background starting from row N |
| `FreezePanes(row, col)` | Freeze panes at given position |
| `MergeCells(range)` | Merge a cell range |
| `AutoFilter()` | Enable auto-filter on header row |

### AdvancedBuilder

Pivot tables, sparklines, auto-filters, comments, hyperlinks, rich text, sheet protection, named ranges, sorting, and print setup.

### StylePresets

Built-in themes: `Mono`, `Finance`, `Academic`.

```csharp
StylePresets.Apply("Academic", wb);
```

### FormulaValidator

Pre-save formula validation with evaluation feedback.

```csharp
var result = FormulaValidator.Validate(wb);
if (result.HasErrors)
    Console.WriteLine(string.Join("\n", result.Errors));
```

## Dependencies

- `Angri450.Nong.ThirdParty` — merged foundation (ClosedXML + OpenXml + all transitive deps)
- `System.IO.Packaging` — NuGet, OPC container support

## API Reference

| Class | Description |
|-------|-------------|
| `ExcelBuilder` | Entry point for sheet and workbook building |
| `AdvancedBuilder` | Pivot tables, sparklines, comments, hyperlinks, protection |
| `StylePresets` | Pre-built theme application |
| `FormulaValidator` | Formula syntax and reference validation |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
