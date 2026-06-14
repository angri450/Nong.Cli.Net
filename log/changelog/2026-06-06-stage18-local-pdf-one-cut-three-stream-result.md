# 2026-06-06 Stage18 Local PDF One-Cut Three-Stream Result

## Summary

Implemented the first real local PDF pipeline in Nong.

New command group:

```powershell
nong pdf check <file.pdf> --json
nong pdf dissect <file.pdf> --output <slice-dir> --mode auto --json
nong pdf render <file.pdf> --output <pages-dir> --dpi 150 --json
nong pdf images <file.pdf> --output <assets-dir> --json
```

The main readable output of `pdf dissect` is `content.nongmark`, aligned with Nong's enhanced Pandoc/NongMark direction. `preview/content.md` is generated only as a lossy compatibility preview.

## Implementation

Added `Angri450.Nong.Pdf`:

- `PdfDocumentInspector`: preflight and classification metrics.
- `PdfTextExtractor`: text-layer extraction with page, bbox, font, size, and stable block IDs.
- `PdfImageExtractor`: embedded image extraction with page/bbox provenance.
- `PdfPageRenderer`: PDFium-backed page rendering through Docnet.Core.
- `PdfSlice`: deterministic slice writer for manifest, document, JSONL, structure, format, NongMark, preview, assets, OCR diagnostics, and reading-order diagnostics.
- `PdfNongMarkTextWriter`: primary AI-readable enhanced text projection.

CLI integration:

- Added `Cli/Commands/PdfCommands.cs`.
- Added `PdfOcrRecognizerAdapter` for OCR mode.
- Added `Pdf` project reference to `NongCli.csproj`.
- Added implemented PDF commands to `nong commands --json`.

GroundPA sync:

- Added `Nong.Toolkit.Net/pdf/SKILL.md`.
- Added `./pdf` to the plugin manifest and skill grouping.
- The skill routes `pdf check` first, reads `content.nongmark` as primary evidence, and keeps cloud OCR as an optional stronger path.

## Package Choices

Text extraction:

- `PdfPig 0.1.14`
- Reason: net8.0 target, text/word/letter extraction, bbox, font name, point size, and image object discovery.

Rendering:

- `Docnet.Core 2.6.0`
- Reason: PDFium-backed rendering with native assets for Windows/Linux/macOS already packaged by NuGet. This gives a working pure .NET client path without Python, Pandoc, MinerU, or local native builds.

Known follow-up: a direct `PDFiumCore 150.0.7869` adapter remains a strong candidate if the runtime packaging strategy needs tighter first-party control.

## Real Smoke Output

Generated sample:

```text
tests-output/stage18-smoke/stage18-text.pdf
```

`pdf check`:

```text
status: ok
command: pdf check
classification: text
pageCount: 1
textCharCount: 159
imageCount: 0
renderRequired: false
```

`pdf render`:

```text
status: ok
command: pdf render
pages: 1
dpi: 150
artifact: tests-output/stage18-smoke/pages-white/page-0001.png
rendered size: 1239x1754
```

Rendering note: PDFium transparent page output is composited over a white background before PNG encoding. A visual check confirmed the rendered page is white-background and readable, not black-background.

`pdf dissect`:

```text
status: ok
command: pdf dissect
blocks: 5
assets: 0
slice: tests-output/stage18-smoke/slice
```

Critical artifacts created and non-empty:

```text
manifest.json
document.json
content.jsonl
structure.json
format.json
content.nongmark
preview/content.md
assets/manifest.json
diagnostics/check.json
diagnostics/reading-order.json
diagnostics/warnings.json
```

`content.nongmark` includes:

```text
::: page {#page-0001 number=1 width=595 height=842 unit=pt}
# **Stage18** **PDF** **Title** {#h0001 page=1 bbox="72,760,224.046,773.266" source=pdfText font="Helvetica-Bold" size=18 align=unknown confidence=medium}
```

`content.jsonl` blocks include stable:

```text
id
blockId
index
kind
page
bbox
source
text
runs
format
```

OCR-mode empty-output guard:

```text
nong pdf dissect .\tests-output\stage18-smoke\stage18-text.pdf --output .\tests-output\stage18-smoke\ocr-slice-2 --mode ocr --dpi 120 --json
status: error
code: E007 read_failed
message: Local OCR returned no text blocks; Nong will not publish an empty PDF slice as success.
```

## Real User PDF Regression

Tested real file:

```text
C:\Users\Administrator\Downloads\2026先导杯赛题征集申报指南-盖章版.pdf
```

`pdf check`:

```text
status: ok
classification: text
pageCount: 3
textCharCount: 1380
imageCount: 1
renderRequired: false
page 2 imageCount: 1
```

`pdf images` now preserves the page 2 QR-code asset even when PdfPig cannot decode the embedded image stream:

```text
status: ok
images: 1
asset: tests-output/stage18-real-xiandao-images-fixed/img0001.png
page: 2
bbox: 126,84.7,384.6,300.7
extractionMethod: pageCrop
warning: Image could not be decoded as PNG; page crop fallback saved.
```

`pdf dissect`:

```text
status: ok
blocks: 58
assets: 1
warnings: 1
slice: tests-output/stage18-real-xiandao-fixed2
```

The primary NongMark stream now includes the QR-code image block:

```text
![image](assets/img0001.png){#img0001 page=2 bbox="126,84.7,384.6,300.7" source=pageCrop asset=img0001 confidence=medium}
```

Visual inspection of the generated asset confirmed that `assets/img0001.png` is the real QR-code region from page 2, including the "扫码在线申请" and "先导杯赛题征集" text.

## Verification

Build:

```text
dotnet build .\Pdf\PdfCore.csproj -c Release --nologo
PASS, 0 warnings, 0 errors

dotnet build .\Cli\NongCli.csproj -c Release --nologo
PASS, 2 existing warnings in Chart/Word code, 0 errors
```

Tests:

```text
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo
PASS: 81/81

dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo --filter PdfCommandTests
PASS: 7/7
```

GroundPA:

```text
nong skill validate .\Nong.Toolkit.Net\pdf --json
PASS: 0 errors, 0 warnings

nong skill scan .\Nong.Toolkit.Net --json
PASS: 0 findings, 0 High+

claude plugin validate .\Nong.Toolkit.Net
PASS
```

## Current Limits

- Text PDF path is implemented and tested.
- Hybrid mode currently preserves native PDF text and embedded image evidence; image-region OCR/layout enrichment is still limited.
- OCR mode is wired through Nong's local PP-OCRv5 adapter and renders pages first, but local OCR remains text recognition only.
- If OCR mode returns zero text blocks, `pdf dissect` fails with `E007 read_failed` instead of publishing an empty successful slice.
- Local OCR does not claim cloud-grade layout analysis, table structure, Word formatting, or reliable cross-page stitching.
- Cross-page image/table continuation is not yet a confident merge feature.
- Windows smoke was run in this environment. Linux/macOS runtime restore was not smoke-tested on target machines.
