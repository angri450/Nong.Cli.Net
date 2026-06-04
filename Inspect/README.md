# Angri450.Nong.Inspect

AI 生成内容检测工具包。angri450 基于 DocxCore 构建 —— 16 种论文分类、结构提取、证据链诊断、数据需求评估、缺口评级（A-E）、参考文献风险分析、变量操作化规划。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Inspect)](https://www.nuget.org/packages/Angri450.Nong.Inspect)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## 功能

**论文检测**: 16 种分类、结构提取、证据链诊断、数据需求评估、缺口评级（A-E）、参考文献风险分析、变量操作化规划。

**论文写作**: 链式 API 生成完整论文（中英文标题、摘要、关键词、自动编号标题、引用、参考文献、变量表）。

**公文和信函**: 标准格式的基本生成器（开发中）。

## Dependencies

- `Angri450.Nong.ThirdParty` (基础)
- `Angri450.Nong.Docx` (Word 引擎)

## Quick Start

```csharp
using Nong.Inspect;
using DocxCore;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// 写论文
using var doc = WordprocessingDocument.Create("paper.docx", WordprocessingDocumentType.Document);
var main = doc.AddMainDocumentPart();
main.Document = new Document(new Body());
var body = main.Document.Body!;

Gbt7714Style.BuildAll(sp.Styles);
Gbt7714Style.BuildNumbering(np.Numbering);

var w = new PaperWriter(body, doc);
w.Title("标题").Abstract("摘要").Heading("引言", 1).Body("正文[1]").BibHeading("参考文献").References("...");

// 检测论文
var text = File.ReadAllText("paper.txt");
var types = PaperTypeClassifier.Classify(text);
var structure = PaperStructureExtractor.BuildPaperStructure(text);
var diagnosis = PaperDiagnostics.DiagnosePaperQuality(text, ...);
```

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0
