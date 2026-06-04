# Angri450.Nong.Excel

链式 Excel 生成 API，基于 ClosedXML。angri450 设计的流畅构建器模式 —— 一行链式调用搞定表头、数据、样式、冻结、筛选，无需手写 OpenXML。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Excel)](https://www.nuget.org/packages/Angri450.Nong.Excel)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Excel
```

> `ClosedXML.Excel` 等类型由 `Angri450.Nong.ThirdParty` 提供（已作为传递依赖自动安装）。不要额外安装 `ClosedXML` NuGet 包，否则会出现类型冲突（CS0433）。

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

### SheetBuilder (链式 API)

angri450 设计的链式构建器，每一步返回自身，IDE 自动补全所有可用操作：

| Method | Description |
|--------|-------------|
| `Headers(params string[])` | 设置列标题 |
| `Data(IEnumerable<string[]>)` | 填充数据行 |
| `ColumnWidths(params double[])` | 设置列宽 |
| `HeaderStyle(bg, fg)` | 标题行背景色和前景色 |
| `AlternatingRows(startRow, color)` | 从第 N 行开始交替行背景色 |
| `FreezePanes(row, col)` | 冻结窗格 |
| `MergeCells(range)` | 合并单元格 |
| `AutoFilter()` | 启用自动筛选 |

### AdvancedBuilder

数据透视表、迷你图、自动筛选、批注、超链接、富文本、工作表保护、命名区域、排序、打印设置。

### StylePresets

内置三套主题：`Mono`、`Finance`、`Academic`。

```csharp
StylePresets.Apply("Academic", wb);
```

### FormulaValidator

保存前公式验证，返回评估反馈：

```csharp
var result = FormulaValidator.Validate(wb);
if (result.HasErrors)
    Console.WriteLine(string.Join("\n", result.Errors));
```

## Dependencies

- `Angri450.Nong.ThirdParty` — 合并基础库（ClosedXML + OpenXml + 全部传递依赖）
- `System.IO.Packaging` — NuGet, OPC 容器支持

## API Reference

| Class | Description |
|-------|-------------|
| `ExcelBuilder` | 工作表和文档构建入口 |
| `AdvancedBuilder` | 数据透视表、迷你图、批注、超链接、保护 |
| `StylePresets` | 预设主题应用 |
| `FormulaValidator` | 公式语法和引用验证 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0
