# 2026-06-06 Stage18 Guidance: Local PDF One-Cut Three-Stream

## Mission

Build Nong's local PDF document parser so users without a cloud OCR key can still turn PDFs into a machine-readable, page-aware, asset-aware, AI-readable document package.

This is not a minimal repair. The target is a working local pipeline:

```text
PDF -> classify -> extract/render -> local OCR where needed -> reading-order blocks
    -> NongMark/Pandoc-enhanced text projection
    -> canonical JSON streams
    -> assets and provenance
```

The user-facing result must feel like MinerU's useful path, but implemented inside Nong's pure .NET ecosystem. Do not require Python, pip, MinerU, Pandoc executable, or manual model builds on the client machine.

## Product Decision

Critical correction: the final readable output is not plain Markdown.

Stage18 must align with the Word-side enhanced Pandoc/NongMark language used by Nong for document work: a richer text projection with block IDs, page/bbox evidence, formatting markers, assets, tables, math, captions, and provenance. Plain `.md` may exist only as a lossy compatibility preview.

Use this naming unless a later implementation finds an existing stronger project convention:

```text
content.nongmark    # primary AI-readable enhanced text projection
preview/content.md  # optional lossy preview, never the source of truth
```

The source of truth is still JSON-first `nongmark/v1` / `nongpdf/v1`; `content.nongmark` is the required human/AI text projection generated from that canonical model.

Cloud OCR remains the strongest route when `PADDLEOCR_ACCESS_TOKEN` exists. It can parse full PDFs in one step.

But Nong must not make cloud key availability a hard dependency. Local PDF parsing must cover three cases:

1. **Text PDF**
   - Usually exported from Word, browser, LaTeX, WPS, or reporting systems.
   - Has selectable text and often font/position data.
   - Primary route: extract text, coordinates, fonts, images, and reading order.
   - Local OCR is only used on embedded images, figures, formulas, stamps, or cropped regions.

2. **Hybrid PDF**
   - Has some text layer and some scanned/image-only regions.
   - Primary route: extract real text where available; crop image-only regions; run local OCR on those regions.
   - Preserve both native text and OCR evidence.

3. **Scan PDF**
   - Page is mostly bitmap image.
   - Primary route: render pages to images; detect regions; run local OCR; build content streams from OCR blocks.
   - Layout is approximate in early versions, but must be page-aware and provenance-safe.

## What to Learn From MinerU

MinerU's important idea is not "use OCR everywhere". Its useful architecture is:

- Input collection supports PDF/images/office files.
- `parse_method=auto|txt|ocr` chooses native text extraction or OCR.
- Pipeline emits multiple outputs: readable text projection, content list JSON, middle JSON, model JSON, images.
- It treats document parsing as orchestration: layout, OCR, formula, table, image analysis, reading order, and assets are separate stages.
- It supports CPU pipeline mode and stronger VLM/hybrid modes.

Nong should copy the architecture pattern, not the Python implementation.

## What to Learn From Pandoc

Pandoc is not the solution for reading PDF. Pandoc primarily converts between document formats through an AST; it is not a high-quality PDF parser and will not solve scanned PDFs.

Use Pandoc as design inspiration for the enhanced text projection layer:

- Blocks have types.
- Inline text has formatting.
- Images carry captions/alt text.
- Tables and math have structured representations.
- Attribute-bearing block syntax can preserve IDs, page coordinates, and formatting hints.
- Plain Markdown is an interchange/preview format.

Do not make Markdown the canonical edit model. Nong's canonical model remains JSON-first, aligned with `nongmark/v1`.
Do not make `.md` the final user-facing extraction artifact for Stage18.

## New Command Surface

Add a first-class `pdf` command group.

```powershell
nong pdf check <file.pdf> --json
nong pdf dissect <file.pdf> --output <slice-dir> --mode auto --json
nong pdf render <file.pdf> --output <pages-dir> --dpi 200 --json
nong pdf images <file.pdf> --output <assets-dir> --json
```

Optional follow-up commands after the core pipeline works:

```powershell
nong pdf to-nongmark <file.pdf> -o out.nongmark --mode auto --json
nong pdf to-markdown <file.pdf> -o out.md --mode auto --json
nong pdf to-word <file.pdf> -o out.docx --mode auto --json
```

`to-markdown` is a compatibility/export command only. It must not be used as the main PDF extraction route in skills or completion criteria.

Do not expose a command as implemented until it performs real work and has tests. Planned commands can be added only if `commands --all` distinguishes them correctly.

## Error Contract

Use existing Nong error codes:

- `E001 file_not_found`: PDF path missing.
- `E002 unsupported_format`: not `.pdf` or cannot be parsed as PDF.
- `E005 dependency_missing`: required native PDF render runtime or OCR runtime missing.
- `E006 validation_failed`: invalid page range, invalid mode, invalid output path.
- `E007 read_failed`: PDF exists but text/image/page extraction failed.
- `E008 write_failed`: output directory/artifact creation failed.
- `E004 internal_error`: only for unexpected bugs after more specific errors are ruled out.

Never return success with empty critical artifacts.

## Output Layout

`pdf dissect` must create a deterministic slice directory.

```text
<slice-dir>/
  manifest.json
  document.json
  content.jsonl
  structure.json
  format.json
  content.nongmark
  preview/
    content.md
  pages/
    page-0001.png
    page-0002.png
  assets/
    manifest.json
    img0001.png
    crop0001.png
  ocr/
    pages.jsonl
    blocks.jsonl
  diagnostics/
    check.json
    reading-order.json
    warnings.json
```

The command is not done unless `manifest.json`, `document.json`, `content.jsonl`, `structure.json`, `format.json`, and `content.nongmark` exist and are non-empty for a non-empty PDF.

`preview/content.md` can be generated for compatibility, but it is not required to prove the Stage18 contract. If it exists, it must clearly be described as lossy preview.

## Canonical Schema Direction

Create a PDF-specific bridge model, but keep it compatible with Word `nongmark/v1`.

Recommended schema names:

```text
nongpdf/v1       # PDF extraction and page evidence
nongmark/v1      # shared canonical document blocks
```

`document.json` should include:

```json
{
  "schemaVersion": "nongpdf/v1",
  "source": {
    "path": "input.pdf",
    "sha256": "...",
    "pageCount": 0,
    "classification": "text|hybrid|scan|unknown"
  },
  "streams": {
    "contentJsonl": "content.jsonl",
    "structureJson": "structure.json",
    "formatJson": "format.json",
    "nongmarkText": "content.nongmark",
    "markdownPreview": "preview/content.md",
    "assetsManifest": "assets/manifest.json"
  }
}
```

Every content block must have stable IDs:

```json
{
  "id": "p0001",
  "blockId": "p0001",
  "index": 1,
  "kind": "paragraph|heading|table|image|formula|pageBreak|ocrText",
  "page": 1,
  "bbox": [72.0, 108.0, 480.0, 140.0],
  "source": "pdfText|pdfImage|localOcr|cloudOcr|inferred",
  "text": "..."
}
```

For images:

```json
{
  "id": "img0001",
  "kind": "image",
  "page": 2,
  "bbox": [60, 180, 520, 420],
  "assetId": "img0001",
  "captionBlockId": "p0018",
  "ocrBlockIds": ["ocr0021"]
}
```

For tables, early versions may be approximate:

```json
{
  "id": "tbl0001",
  "kind": "table",
  "page": 3,
  "bbox": [60, 120, 520, 360],
  "source": "pdfText|localOcr|inferred",
  "html": "<table>...</table>",
  "confidence": "high|medium|low"
}
```

## PDF Classification Heuristics

`pdf check` must report enough evidence for routing.

Required metrics:

- `pageCount`
- `hasTextLayer`
- `textCharCount`
- `textCharsPerPage`
- `imageCount`
- `imageCoverageRatio`
- `renderRequired`
- `classification`
- `recommendedMode`
- `warnings`

Suggested classification:

```text
text:
  textCharsPerPage is high and imageCoverageRatio is low/medium.

hybrid:
  meaningful text layer exists, but image coverage or blank text regions are substantial.

scan:
  text layer missing/near-empty and page image coverage is high.

unknown:
  encrypted, malformed, or extractor cannot inspect enough evidence.
```

Do not classify a PDF as text-only just because it has a tiny hidden/OCR text layer. Check text density and geometry coverage.

## Local Pipeline

### Mode Auto

`--mode auto` runs:

1. `pdf check`
2. If classification is `text`: native text/image extraction, optional local OCR on image crops.
3. If classification is `hybrid`: native extraction plus page/region rendering and local OCR.
4. If classification is `scan`: page rendering plus local OCR.
5. Build reading order.
6. Write JSON streams and NongMark/Pandoc-enhanced text projection.

### Mode Text

`--mode text` forces native text extraction and fails with clear warnings if the PDF has no useful text layer.

### Mode OCR

`--mode ocr` renders pages and OCRs them, even if text exists.

### Mode Hybrid

`--mode hybrid` extracts text and OCRs image/blank regions.

## Local OCR Role

Use the repaired `PpOcrV5Client` only for text recognition:

- Page render OCR.
- Cropped image OCR.
- Figure caption OCR if caption is inside an image.
- Stamp/scan text OCR.

Do not ask local PP-OCRv5 to do layout classification. Any layout classification in Stage18 must be heuristic or a separate model/runtime, not hidden inside `ocr local`.

If local OCR emits numeric warnings, preserve text but downgrade confidence/geometry claims.

## Page Rendering

A pure .NET PDF parser may not be enough for rendering. The implementation agent must choose a package strategy that fits Nong's distribution rules:

- No Python.
- No client-side source builds.
- Prefer NuGet packages with runtime assets.
- If native runtime is large, split it into first-party runtime packages like `Angri450.Nong.OcrRuntime.*`.
- Install from Huawei NuGet mirror/cache when possible.

Candidate rendering approaches to evaluate:

- PDFium-based .NET package.
- MuPDF-based .NET package.
- SkiaSharp-compatible renderer.

Do not hardcode a renderer until a real smoke test renders at least one page on Windows. Linux/macOS can be packaged later, but must not be claimed stable without target-machine smoke tests.

## Text Extraction

The implementation agent must evaluate .NET text extraction libraries before coding. The required output is text with page, bbox, font name/size when possible.

Candidate approaches:

- UglyToad.PdfPig or equivalent for text positions and images.
- A PDFium/MuPDF wrapper if it exposes text page APIs.
- A custom parser only if packages fail; do not hand-roll PDF parsing as the first route.

The first release can accept approximate reading order. It must still preserve page and bbox evidence.

## Reading Order

Reading order is a required feature, not a nice-to-have.

Initial heuristic:

1. Group by page.
2. Remove obvious headers/footers using repeated text and top/bottom bands.
3. Detect columns by x-coordinate clustering.
4. Sort blocks top-to-bottom inside each column.
5. Merge adjacent text spans into paragraphs by line spacing, font continuity, and x alignment.
6. Keep page breaks as explicit blocks.

Record reading-order diagnostics:

```text
diagnostics/reading-order.json
```

If the order is uncertain, mark it in `issues` and in block-level `confidence`.

## Image and Caption Handling

Images must have provenance.

For every extracted or cropped image:

- Save asset file.
- Record source page.
- Record bbox.
- Record extraction method: embedded image, page crop, stitched crop.
- Link related OCR blocks.
- Try to associate nearby caption text:
  - below image first
  - above image second
  - same-page nearest text with keywords like Figure, Fig., 图, 表

Do not invent captions. If no caption is found, leave `caption` null and optionally provide OCR text as `altTextCandidate`.

## Cross-Page Stitching

Stage18 should include design and initial implementation for cross-page image/table continuation, but do it conservatively.

Rules:

1. Only adjacent pages are candidates.
2. Candidate bottom/top regions must have similar x ranges and width.
3. Continuation evidence must be recorded:
   - no caption before split
   - matching border/line at page edge
   - similar image background
   - table continues with no closing boundary
4. Never delete original segments.
5. Stitched assets go to `assets/stitches.jsonl`.

If confidence is not high, do not stitch; record a warning and keep segments separate.

## NongMark/Pandoc-Enhanced Text Projection

`content.nongmark` is the required AI-readable and human-auditable text projection. It must be generated from canonical blocks and must carry enough layout markers to line up with Word `nongmark/v1`.

This is not plain Markdown. Use Pandoc-style inline conventions where useful, then add Nong layout/provenance attributes.

Required language features:

- Block IDs on every block.
- Page number and bbox on every page-derived block.
- Source channel: `pdfText`, `pdfImage`, `localOcr`, `cloudOcr`, or `inferred`.
- Paragraph style hints: font, size, alignment, indent, line spacing when available.
- Inline format markers compatible with the Word/PaperWriter enhanced Pandoc subset:
  `*italic*`, `**bold**`, `***boldItalic***`, `==highlight==`, `~~strike~~`, `^sup^`, `~sub~`, `[@ref]`.
- Tables, images, captions, formulas, footnotes, comments, and warnings must stay typed instead of being flattened into anonymous text.

Recommended syntax:

```text
::: page {#page-0001 number=1 width=595.28 height=841.89 unit=pt}

# Heading {#h0001 page=1 bbox="72,80,520,112" source=pdfText font="SimHei" size=16 align=center}

::: paragraph {#p0002 page=1 bbox="72,128,520,170" source=pdfText font="SimSun" size=12 align=justify indent=2em lineSpacing=1.5}
Paragraph with **bold**, *italic*, ==highlight==, H~2~O, x^2^, and [@ref1].
:::

![Caption](assets/img0001.png){#img0001 page=2 bbox="60,180,520,420" source=embeddedImage captionBlock=p0018}

::: table {#tbl0001 page=3 bbox="60,120,520,360" source=pdfText confidence=medium}
<table>
...
</table>
:::

:::
```

Do not rely on HTML comments as the primary provenance mechanism. Provenance belongs in block attributes and canonical JSON.

`preview/content.md`, if generated, is a lossy projection from `content.nongmark` or from canonical JSON. It exists for tools that only accept Markdown and must not be treated as the final output.

Do not make Markdown the only output. Do not call the PDF feature done if the main readable artifact is `.md`.

## Word Alignment

The local PDF slice must be able to align later with Word `nongmark/v1`.

Shared concepts:

- `blockId`
- `kind`
- `page`
- `bbox`
- `assetId`
- `captionBlockId`
- `source`
- `text`

Future bridge command may be:

```powershell
nong pdf align-word <pdf-slice-dir> <docx-slice-dir> -o map.json --json
```

Do not implement this until `pdf dissect` works, but design block IDs and schemas now so the bridge is possible.

## Project Structure

Recommended new project:

```text
Pdf/
  PdfCore.csproj
  PdfCheck.cs
  PdfDocumentInspector.cs
  PdfTextExtractor.cs
  PdfImageExtractor.cs
  PdfPageRenderer.cs
  PdfReadingOrder.cs
  PdfSlice.cs
  PdfNongMarkTextWriter.cs
  PdfMarkdownPreviewWriter.cs
  PdfModels.cs
  README.md
```

CLI:

```text
Cli/Commands/PdfCommands.cs
Cli/Common/Manifest.cs
Cli/Program.cs
Cli.Tests/PdfCommandTests.cs
```

Optional runtime packaging if renderer requires native files:

```text
PdfRuntime/
  Angri450.Nong.PdfRuntime.WinX64
  Angri450.Nong.PdfRuntime.LinuxX64
  Angri450.Nong.PdfRuntime.OsxArm64
```

Do not dump PDF code into `OcrCommands.cs`.

## Multi-Agent Construction Plan

Run agents in parallel with disjoint write scopes.

### Agent A: PDF Library/Runtimes Spike

Scope:

- Evaluate PDF text extraction and render options.
- Create a small spike or report in `log/guidance` if code is not yet safe.
- Recommend final NuGet packages and runtime strategy.

Acceptance:

- Windows page render smoke test.
- Text extraction with coordinates from a text PDF.
- Package/runtime size notes.
- No Python.

### Agent B: PdfCore Models and Check

Scope:

- Create `Pdf/` project.
- Implement `pdf check`.
- Add JSON models and classification heuristics.

Acceptance:

- `nong pdf check sample.pdf --json` returns classification and metrics.
- Tests cover missing file, non-PDF, text PDF, dummy/malformed PDF.

### Agent C: Text PDF Dissect

Scope:

- Implement native text extraction path.
- Build content blocks, structure, format, and `content.nongmark`.
- Add Markdown preview only as a compatibility projection.

Acceptance:

- Text PDF produces full slice directory.
- `content.jsonl` has stable IDs, page, bbox, source=`pdfText`.
- `content.nongmark` is readable and includes block/page/bbox/format attributes.
- `preview/content.md`, if present, is marked as lossy preview.

### Agent D: Rendering + OCR Path

Scope:

- Implement page rendering.
- Run local OCR per page or region.
- Write `ocr/pages.jsonl` and `ocr/blocks.jsonl`.

Acceptance:

- Scan/image PDF produces OCR text blocks.
- JSON has no NaN/Infinity.
- Missing OCR runtime returns E005 with `ocr install-model` guidance.

### Agent E: Assets, Images, Captions, Cross-Page

Scope:

- Extract embedded images.
- Crop page regions when needed.
- Associate captions.
- Implement conservative cross-page continuation manifest.

Acceptance:

- `assets/manifest.json` records page/bbox/provenance.
- Captions are linked or explicitly null.
- Cross-page candidates are recorded without destructive merging.

### Agent F: CLI, Tests, Docs, GroundPA Sync

Scope:

- Register commands.
- Update manifest and docs.
- Add tests.
- Keep Word and PDF text projection language aligned through a shared NongMark text writer/spec.
- Update GroundPA skill only after CLI behavior is real.

Acceptance:

- `dotnet build .\Cli\NongCli.csproj -c Release --nologo`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo`
- `nong commands --json` lists implemented PDF commands only after real implementation.
- GroundPA skill scan remains 0 High+.

## Test Assets

Create deterministic test PDFs in tests, do not depend on external files for CI.

Required fixtures:

1. Text PDF with:
   - title
   - two paragraphs
   - simple table-like text
   - one embedded image if feasible

2. Image-only PDF:
   - one rendered page image containing `测试123` or English text

3. Hybrid PDF:
   - selectable title/body
   - embedded image with text inside

4. Multi-page PDF:
   - two pages
   - repeated header/footer
   - page break validation

5. Malformed/dummy PDF:
   - verifies E002/E007 behavior.

## Verification Commands

Run these before claiming done:

```powershell
dotnet build .\Cli\NongCli.csproj -c Release --nologo
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo
.\Cli\bin\Release\net8.0\nong.exe pdf check <text.pdf> --json
.\Cli\bin\Release\net8.0\nong.exe pdf dissect <text.pdf> --output tests-output\pdf-text-slice --mode auto --json
.\Cli\bin\Release\net8.0\nong.exe pdf dissect <scan.pdf> --output tests-output\pdf-scan-slice --mode ocr --json
Test-Path tests-output\pdf-text-slice\content.jsonl
Test-Path tests-output\pdf-text-slice\content.nongmark
Test-Path tests-output\pdf-text-slice\assets\manifest.json
```

If OCR runtime is unavailable on the machine, scan-mode tests may assert E005 only in dependency-specific tests. At least one real OCR smoke test must pass before scan-mode is called stable.

## Done Criteria

Stage18 is done only when:

- `pdf check` works.
- `pdf dissect --mode auto` works for text PDFs.
- `pdf dissect --mode ocr` works for image PDFs when local OCR runtime is installed.
- Slice artifacts are complete and non-empty.
- Content blocks include stable IDs, page, bbox, source.
- `content.nongmark` is generated from canonical JSON blocks and carries Word-aligned enhanced Pandoc/NongMark annotations.
- Plain Markdown is optional preview/export only.
- Assets have manifest provenance.
- Tests pass.
- Changelog records real command outputs and limitations.

Do not declare done if the result is only plain text extraction.
Do not declare done if the final readable output is only `.md`.
Do not declare done if PDF images are lost.
Do not declare done if scan PDFs silently return empty content.
Do not declare done if the user still needs cloud key for basic local PDF slicing.

## Changelog Requirement

After implementation, create a changelog:

```text
changelog/2026-06-06-stage18-local-pdf-one-cut-three-stream-result.md
```

It must include:

- Implemented commands.
- Package/runtime choices and sizes.
- Test summary.
- Real sample outputs.
- Known limitations.
- Whether Windows/Linux/macOS were actually smoke-tested.

## GroundPA Sync Requirement

Only after `nong pdf` commands are real:

- Add or update a `pdf` skill in GroundPA.
- Route PDF requests:
  - `pdf check` first.
  - `pdf dissect` for local one-cut three-stream.
  - Read `content.nongmark` as the primary text artifact.
  - `ocr cloud/to-word` only when cloud key exists and stronger parsing is desired.
- Keep `word` skill focused on `.doc/.docx`.
- Keep `multimodal` skill honest: local OCR is still text recognition, while `pdf dissect` is the local document parser.

Validate:

```powershell
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill validate .\Nong.Toolkit.Net\pdf --json
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill scan .\Nong.Toolkit.Net --json
claude plugin validate .\Nong.Toolkit.Net
```
