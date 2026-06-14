# Angri450.Nong.Pptx

面向 AI Agent 的 PPT 生成器。angri450 设计了 10 套主题、6 种布局系统、CJK 字体自动注入的流畅 API —— 适合 Agent 批量出片，也适合人类直接调用。

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

## Themes (10 套内置)

angri450 精选的 10 套配色方案：`Professional`、`Academic`、`Modern`、`Minimal`、`Warm`、`Cool`、`midnight-executive`、`coral-energy`、`teal-trust`、`cherry-bold`。

```csharp
// 也支持 JSON 自定义主题
var theme = ThemePreset.BuildFromJson("formats/cherry-bold.json");
SlideBuilder.Create().Theme(theme).AddTitleSlide(...).Save("output.pptx");
```

## Layout System

angri450 基于视觉重心分布设计的六种布局：

| Layout | Description | Best For |
|--------|-------------|----------|
| `SingleFocus` | 1-2 元素，大量留白 | 标题页、关键信息 |
| `Symmetric` | 50/50 双栏 | 对比、前后效果 |
| `Asymmetric` | 2/3 + 1/3 分割 | 主要发现 + 辅助信息 |
| `ThreeColumn` | 三等分 | 特性列表、指标 |
| `PrimarySecondary` | 1 主 + 2-3 辅 | 关键洞察 + 证据 |
| `HeroTop` | 顶部大横幅 + 底部卡片 | 摘要仪表盘 |

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

## Card 布局

`TwoColumns`、`Cards`、`BigNumber`、`Quote` — 预设的卡片式布局辅助方法。

## CJK 字体支持

angri450 实现：每个文本运行自动注入 `<a:ea>` 元素。中/日/韩字体无需手动编辑 XML 即可正确渲染。

## 检查和验证

```csharp
var map = SlidePreview.ShapeMap("output.pptx");
Console.WriteLine(map.Json);   // 所有形状的结构化 JSON

SlideValidator.ValidateAndReport("output.pptx");  // 结构完整性检查
```

## NongPandoc Slice

```bash
nong pptx dissect slides.pptx -o slides.slice --json
```

The command writes the shared `nong-pandoc/package/v1` package:

```text
manifest.json
document.json
content.jsonl
content.nongmark
structure.json
format.json
diagnostics.json
assets/manifest.json
preview/content.txt
```

Use `content.nongmark` as the primary AI-readable stream, followed by
`structure.json`, `format.json`, and `diagnostics.json`.

## Dependencies

- `ShapeCrawler` — OpenXML SDK 封装，用于 PPTX 操作

## API Reference

| Class | Description |
|-------|-------------|
| `SlideBuilder` | 链式幻灯片创建入口 |
| `PresentationBuilder` | 多页演示文稿构建器 |
| `ThemePreset` | 10 套主题 + JSON 主题加载 |
| `SlideHelper` | 布局辅助（SingleFocus、Symmetric 等） |
| `SlidePreview` | 形状检查和 JSON 导出 |
| `SlideValidator` | 结构验证 |
| `RawAccessor` | 底层 OOXML 访问 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0
