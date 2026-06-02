# Angri450.Nong.Docx

OpenXML Word generation library. Pure .NET, zero COM, single merged dependency. One-stop paper writing: create, fill, analyze, and diagnose.

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

// Styles from JSON template
var sp = main.AddNewPart<StyleDefinitionsPart>();
sp.Styles = new Styles();
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");

// Chainable content
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

// Images
ImageEmbedder.EmbedImages(body, main, new[] { "fig1.png", "fig2.png" });

body.Append(StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build());
main.Document.Save();
```

## Core Capabilities

### DocumentWriter — Chainable Builder

| Method | Description |
|--------|-------------|
| `Title()` / `EnglishTitle()` | Chinese and English titles |
| `Abstract()` / `Keywords()` | Abstract and keywords block |
| `Heading(text, level)` | Section heading (1-4) |
| `Body(text)` | Body paragraph |
| `Footnote(text)` / `Endnote(text)` | Footnotes and endnotes |
| `Table(caption, level, headers, rows)` | Data table with caption |
| `TableStyle(style)` | 90+ built-in table styles |
| `Figure(caption, level)` | Figure placeholder with caption |
| `CrossReference(bookmark, text)` | Internal cross-reference |
| `Hyperlink(url, text)` | External hyperlink |
| `BibHeading()` / `References(text)` | Bibliography section |
| `Bookmark(name)` | Named bookmark for cross-references |

### Format Templates — JSON-Driven Styling

```csharp
// Swap JSON = swap format. Contest → journal → thesis in seconds.
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");
var sectPr = StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build();
```

| Template | Body Font | Heading Font | Use Case |
|----------|-----------|-------------|----------|
| `life-sciences-contest` | 宋体 10.5pt | 黑体 14pt | Contest (4 pages) |
| `journal-paper` | 宋体 10.5pt | 黑体 18pt | Journal (GB/T 7714) |
| `course-paper` | 宋体 10.5pt | 黑体 14pt | Course paper |
| `degree-thesis` | 宋体 12pt | 黑体 16pt | Degree thesis |

### Template Engine — Fill Placeholders

```csharp
DocxTemplate.Fill("template.docx", "output.docx", new
{
    Name = "Zhang",
    Score = 95,
    Date = "2026-06-01"
});
// Supports: {{tag}} replacement, @if/@foreach blocks, table row data binding
```

### Image Embedding

```csharp
ImageEmbedder.EmbedImages(body, mainPart, new[] { "fig1.png", "fig2.png" });
// PNG/JPEG/GIF/BMP/TIFF — no external image library
```

### SectionBuilder — Page Layout

```csharp
var sectPr = new SectionBuilder()
    .A4()
    .Margins("3cm", "2.5cm", "2.5cm", "2cm")
    .Build();
```

### Paper Analysis (16 Types)

```csharp
var type = PaperTypeClassifier.Classify(text);              // Research design type
var structure = PaperStructureExtractor.BuildPaperStructure(text); // Section extraction
var variables = VariablePlanGenerator.GenerateVariablePlan(text);  // Variable table
var risks = ReferenceAnalyzer.CheckReferenceRisks(text);    // Reference quality
var result = PaperDiagnostics.DiagnosePaperQuality(text, ...);     // A-E grade
// Evidence chain (10 items) → data requirements (9 items) → gap → grade
```

### Advanced Features

Comments, track changes, content controls, font embedding, document merge, document property management, read-only protection.

### WordPreview — Generate + Diagnose

```csharp
var r = WordPreview.Preview("paper.docx");
// Text preview + 7-step diagnostics + OOXML validation
```

## Dependencies

- `Angri450.Nong.ThirdParty` — merged foundation (DocumentFormat.OpenXml + all transitive deps)

No `System.Drawing.Common`, no ImageSharp, no COM. Cross-platform.

## API Reference

| Class | Description |
|-------|-------------|
| `DocumentWriter` | Chainable content builder |
| `StyleBuilder` | JSON-driven style and layout definition |
| `DocxTemplate` | Template engine with placeholder/tag replacement |
| `ImageEmbedder` | Image insertion with dimension auto-detection |
| `SectionBuilder` | Page layout (A4, margins, columns) |
| `PaperDiagnostics` | Quality diagnosis with A-E grading |
| `WordPreview` | Content preview and validation |
| `AdvancedFeatures` | Comments, track changes, content controls, protection |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
