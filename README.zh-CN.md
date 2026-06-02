# Angri450.Nong

纯 .NET 科研文档生成工具集，零 JavaScript 依赖。

## 项目解决什么问题

`Angri450.Nong` 面向科研与数据报告场景，提供一套可在 .NET 环境中直接使用的文档与图形能力，避免 COM 自动化和复杂前端/脚本链路，帮助你更稳定地生成 Excel、图表、流程图、Word、PowerPoint 以及 OCR 结果文档。

## 核心特性与技术栈

- **纯 .NET**：核心能力使用 C# 实现，跨平台可运行。
- **低依赖链**：通过内联关键开源库源码，减少外部 NuGet 依赖数量。
- **面向科研表达**：内置统计图、流程图/网络图/系统发育树等常见科研可视化。
- **OpenXML 体系**：围绕 DocumentFormat.OpenXml 构建文档处理能力。

## 能力概览（文档 / 办公）

| 能力 | 模块 | 说明 |
|---|---|---|
| Excel 生成 | `Angri450.Nong.Excel` | 基于 ClosedXML，支持数据写入、样式、透视表、条件格式等高级能力。 |
| 统计图表 | `Angri450.Nong.Chart` | 基于 ScottPlot，支持柱状图、折线图、箱线图、热力图等多种图表。 |
| 科学图形 | `Angri450.Nong.Diagram` | 基于 MSAGL + SkiaSharp，支持流程图、网络图、系统发育树。 |
| Word 文档 | `Angri450.Nong.Docx` | 基于 OpenXML 的 Word 生成/模板填充/格式化与诊断。 |
| PowerPoint | `Angri450.Nong.Pptx` | 基于 ShapeCrawler 的 PPTX 构建，支持主题、布局、图表与形状操作。 |
| OCR 与多模态 | `Angri450.Nong.MultiModal` | 支持云端/本地 OCR，并可输出 Markdown 或 Word。 |

## 快速开始

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

### Diagram

```powershell
dotnet new console -n DiagramWriter -o MyDiagram --force
cd MyDiagram
dotnet add package Angri450.Nong.Diagram --version 1.0.0
```

### 其他模块安装

```powershell
dotnet add package Angri450.Nong.Docx
dotnet add package Angri450.Nong.Pptx
dotnet add package Angri450.Nong.MultiModal
```

更多模块用法请参考：
- `Docx/README.md`
- `Pptx/README.md`
- `MultiModal/README.md`

## 架构与依赖（摘录）

```text
Angri450.Nong/
├── Excel/、Chart/、Diagram/    三大核心能力
├── ClosedXML/                  内联 ClosedXML
├── DocumentFormat.OpenXml*/    内联 OpenXML SDK 与框架
├── ScottPlot/                  内联图表能力
├── SkiaSharp/、HarfBuzzSharp/  图形与文字渲染
├── MSAGL/                      图布局
└── Tests/                      xUnit 测试
```

## 运行要求

- .NET SDK 11.0（preview）
- SkiaSharp / HarfBuzzSharp 原生二进制（NuGet 自动安装）

## 许可协议

本项目采用 **Apache-2.0** 许可证。

## 贡献与反馈

- 欢迎通过 Issue 反馈问题或需求：<https://github.com/angri450/Nong.NET/issues>
- 欢迎通过 Pull Request 贡献改进：<https://github.com/angri450/Nong.NET/pulls>

