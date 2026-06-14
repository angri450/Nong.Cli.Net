# Angri450.Nong.Docx

纯 .NET Word 文档引擎。angri450 从零构建 —— 链式写入、JSON 模板驱动样式、模板填充、图片嵌入、论文诊断、OCR 转 Word，一套包搞定。零 COM、零 ImageSharp、跨平台。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Docx)](https://www.nuget.org/packages/Angri450.Nong.Docx)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Docx
```

## Quick Start

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocxCore;

using var doc = WordprocessingDocument.Create("paper.docx",
    WordprocessingDocumentType.Document);
var main = doc.AddMainDocumentPart();
main.Document = new Document(new Body());
var body = main.Document.Body!;

// JSON 模板定义样式
var sp = main.AddNewPart<StyleDefinitionsPart>();
sp.Styles = new Styles();
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");

// 链式写入内容
var w = new DocumentWriter(body);
w.Title("Research on Photosynthesis")
 .Abstract("This study investigates...")
 .Keywords("photosynthesis; chlorophyll; light")
 .Heading("Introduction", 1)
 .Body("Photosynthesis is the process by which plants...")
 .Heading("Methods", 1)
 .Body("Samples were collected from...")
 .Table("Growth Data", 1, new[] { "Day", "Height (cm)", "Leaf Count" },
     new[] { new[] { "1", "2.3", "4" }, new[] { "7", "5.1", "8" } })
 .TableStyle(TableStyles.LightGridAccent1)
 .BibHeading()
 .References("Smith J. Photosynthesis review[J]. Nature, 2025.");

// 图片嵌入
ImageEmbedder.EmbedImages(body, main, new[] { "fig1.png", "fig2.png" });

body.Append(StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build());
main.Document.Save();
```

## Core Capabilities

## Word Core Contract

Word work is split into three deterministic layers:

| Layer | Main Command | Purpose |
|-------|--------------|---------|
| NongMark -> DOCX | `nong word create document.nongmark -o document.docx --json` | New long documents and paper-like deliverables |
| DOCX -> NongPandoc package | `nong word dissect input.docx -o input.slice --json` | Reading, slicing, analysis, and formatting evidence |
| DOCX Repair/Format | `nong word fix-order`, `nong word academic-format` | Existing document repair and academic formatting |

Detailed tracking lives in:

- `WORD_CORE_LAYERS.md`
- `WORD_CAPABILITY_MATRIX.md`

Important: `word validate` passing means schema validity only. Formatted deliverables still require evidence for fonts, line spacing, paragraph layout, table borders, and visible content from `format.json`, `content.jsonl`, `fonts`, `styles`, preview output, or direct OOXML checks. `preview/content.txt` is a lossy view; `content.nongmark` and the package evidence streams are the AI-facing contract.

The slice `manifest.json` uses `schemaVersion: "nong-pandoc/package/v1"` so
Word/PDF/PPT/Excel packages expose the same top-level stream contract.

### DocumentWriter — 链式构建器

angri450 设计的链式 API，覆盖论文写作全流程：

| Method | Description |
|--------|-------------|
| `Title()` / `EnglishTitle()` | 中英文标题 |
| `Abstract()` / `Keywords()` | 摘要和关键词块 |
| `Heading(text, level)` | 章节标题（1-4 级） |
| `Body(text)` | 正文段落 |
| `Footnote(text)` / `Endnote(text)` | 脚注和尾注 |
| `Table(caption, level, headers, rows)` | 数据表格（带标题） |
| `TableStyle(style)` | 90+ 内置表格样式 |
| `Figure(caption, level)` | 图位（带标题） |
| `CrossReference(bookmark, text)` | 内部交叉引用 |
| `Hyperlink(url, text)` | 外部超链接 |
| `BibHeading()` / `References(text)` | 参考文献部分 |
| `Bookmark(name)` | 命名书签供交叉引用 |

### 格式模板 — JSON 驱动样式

angri450 设计：换一个 JSON 文件 = 换一套格式。竞赛论文 → 期刊 → 毕业论文，秒切换。

```csharp
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");
var sectPr = StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build();
```

| Template | 正文字体 | 标题字体 | 用途 |
|----------|---------|---------|------|
| `life-sciences-contest` | 宋体 10.5pt | 黑体 14pt | 竞赛论文（4 页） |
| `journal-paper` | 宋体 10.5pt | 黑体 18pt | 期刊论文（GB/T 7714） |
| `course-paper` | 宋体 10.5pt | 黑体 14pt | 课程论文 |
| `degree-thesis` | 宋体 12pt | 黑体 16pt | 学位论文 |

### 模板引擎 — 占位符填充 / cellReplace 无占位符模填

```csharp
DocxTemplate.Fill("template.docx", "output.docx", new
{
    Name = "Zhang",
    Score = 95,
    Date = "2026-06-01"
});
// 支持: {{tag}} 替换、@if/@foreach 块、表格行数据绑定
```

#### cellReplace — 模板无需事先插占位符 (v4.2.1)

只需一份模板和一份 JSON 键值对。引擎遍历所有表格单元格，用单元格内文字匹配 JSON key，自动判断填法：

- **标签-值表**（右格存在）→ 填右格。例如 `"项目名称"` 匹配到第一列标签格，内容填入第二列的值格
- **单格描述表** → 填本格。例如 `"（描述项目定位"` 匹配到提示文字格，内容填入同一格
- **无匹配的表** → 原封保留。例如承诺书中的署名行和日期行
- **`\n`** → 自动转为 Word 换行符 `<w:br/>`
- **固定行高** → 自动解放为自适应，长文本不会裁切

```json
{
  "cellReplace": {
    "项目名称": "基于xxx技术的xxx装置",
    "项目主申报人": "张三",
    "（描述项目定位": "本项目提供了一种...",
    "姓 名": "张三",
    "年 龄": "35"
  }
}
```

```bash
nong word fill template.docx data.json -o out.docx --json
```

#### tableRows — 填入带表头的多行数据表 (v4.2.1)

对于有固定表头行的表格（如专利列表），用 `tableRows` 追加数据行。引擎匹配 `InnerText` 定位表格，保留第一行（表头），用提供的数组替换剩余行。

```json
{
  "tableRows": {
    "专利号/登记号": [
      ["发明专利", "一种xxx装置", "ZL 202410000000.0", "2024-06-01"],
      ["实用新型专利", "一种xxx方法", "CN 202410000000.0", "2025-03-15"]
    ]
  }
}
```

### 图片嵌入

```csharp
ImageEmbedder.EmbedImages(body, mainPart, new[] { "fig1.png", "fig2.png" });
// PNG/JPEG/GIF/BMP/TIFF — 无需外部图片库
```

### SectionBuilder — 页面布局

```csharp
var sectPr = new SectionBuilder()
    .A4()
    .Margins("3cm", "2.5cm", "2.5cm", "2cm")
    .Build();
```

### 论文分析（16 种类型 + A-E 评级）

angri450 实现的论文诊断管线：

```csharp
var type = PaperTypeClassifier.Classify(text);              // 研究设计类型
var structure = PaperStructureExtractor.BuildPaperStructure(text); // 章节提取
var variables = VariablePlanGenerator.GenerateVariablePlan(text);  // 变量表
var risks = ReferenceAnalyzer.CheckReferenceRisks(text);    // 参考文献质量
var result = PaperDiagnostics.DiagnosePaperQuality(text, ...);     // A-E 评级
// 证据链（10 项）→ 数据需求（9 项）→ 缺口 → 等级
```

### 高级特性

批注、修订模式、内容控件、字体嵌入、文档合并、文档属性管理、只读保护。

### WordPreview — 生成 + 诊断

```csharp
var r = WordPreview.Preview("paper.docx");
// 文本预览 + 7 步诊断 + OOXML 验证
```

## Dependencies

- `Angri450.Nong.ThirdParty` — 合并基础库（DocumentFormat.OpenXml + 全部传递依赖）

无 `System.Drawing.Common`、无 ImageSharp、无 COM。跨平台。

## API Reference

| Class | Description |
|-------|-------------|
| `DocumentWriter` | 链式内容构建器 |
| `StyleBuilder` | JSON 驱动样式和页面布局定义 |
| `DocxTemplate` | 模板引擎（占位符/标签替换） |
| `ImageEmbedder` | 图片插入（自动检测尺寸） |
| `SectionBuilder` | 页面布局（A4、边距、分栏） |
| `PaperDiagnostics` | 论文质量诊断（A-E 评级） |
| `WordPreview` | 内容预览和验证 |
| `AdvancedFeatures` | 批注、修订、内容控件、保护 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0
