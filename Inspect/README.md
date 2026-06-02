# Angri450.Nong.Inspect

AI-generated content inspection toolkit. Built on `Angri450.Nong.Docx`.

## What it does

**Paper inspection**: 16-type classification, structure extraction, evidence chain diagnosis, data requirements assessment, gap grading (A-E), reference risk analysis, variable operationalization planning.

**Paper writing**: Chainable API generating complete papers (Chinese/English title, abstract, keywords, auto-numbered headings, citations, bibliography, variable tables).

**Official documents & letters**: Basic writers with standard formatting (under development).

## Dependencies

- `Angri450.Nong.ThirdParty` (foundation)
- `Angri450.Nong.Docx` (Word engine)

## Quick Start

```csharp
using Nong.Inspect;
using DocxCore;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// Write a paper
using var doc = WordprocessingDocument.Create("paper.docx", WordprocessingDocumentType.Document);
var main = doc.AddMainDocumentPart();
main.Document = new Document(new Body());
var body = main.Document.Body!;

Gbt7714Style.BuildAll(sp.Styles);
Gbt7714Style.BuildNumbering(np.Numbering);

var w = new PaperWriter(body, doc);
w.Title("标题").Abstract("摘要").Heading("引言", 1).Body("正文[1]").BibHeading("参考文献").References("...");

// Inspect a paper
var text = File.ReadAllText("paper.txt");
var types = PaperTypeClassifier.Classify(text);
var structure = PaperStructureExtractor.BuildPaperStructure(text);
var diagnosis = PaperDiagnostics.DiagnosePaperQuality(text, ...);
```

## License

MIT
