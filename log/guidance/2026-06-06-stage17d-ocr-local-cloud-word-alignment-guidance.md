# 2026-06-06 Stage17d OCR Guidance: Local Text, Cloud Layout, Word Alignment

## Current Decision

Do not stretch local PP-OCRv5 beyond its actual capability.

`ocr local` is the fast, private, pure .NET, single-image text recognizer. It is useful for quick text extraction when the input is already an image and layout does not matter.

`ocr cloud` / `ocr to-word` are the document OCR routes. They own PDF, multi-page input, page/block layout, table labels, figure labels, Word output, and alignment with `nongmark/v1`.

## Fixed in This Pass

Local OCR must not crash JSON because native inference produced invalid floats.

Implemented behavior:

- NaN/Infinity confidence is emitted as `confidence: null`.
- Blocks carry `confidenceValid`, `geometryValid`, and `numericIssue`.
- Invalid bbox/polygon points are removed before JSON serialization.
- Fast CPU inference with bad numeric output triggers one conservative CPU/BLAS retry.
- If fallback cannot improve the result, Nong returns the sanitized result and emits warnings.
- Text mode prints invalid confidence as `n/a`.
- PDF passed to `ocr local` returns `E002 unsupported_format` with cloud/to-word guidance.

Do not solve this by enabling named floating point literals in JSON. That makes non-standard JSON and pushes the failure to downstream agents.

## Local OCR Boundary

`ocr local` must explicitly say it does not support:

- PDF input.
- Cross-page image stitching.
- Page-level document parsing.
- Layout labels.
- Table reconstruction.
- Word formatting recovery.
- pandoc/NongMark annotation alignment.

GroundPA and skills must not infer layout, table structure, formatting, or Word edit anchors from local OCR text.

## Cloud + Word Alignment Target

The target system is "shape and spirit aligned":

1. Cloud OCR output is normalized into page-aware structured evidence.
2. Word output is sliced into `nongmark/v1`.
3. The two sides are joined by page, bbox, block text, asset IDs, and stable `blockId`.
4. Markdown/Pandoc text is preview/import/export evidence, not the canonical edit model.

Canonical Word side remains:

```text
document.json
content.jsonl
structure.json
format.json
assets/manifest.json
```

Do not replace `nongmark/v1` with plain Markdown. Pandoc can be an adapter layer, not the source of truth.

## PDF and Cross-Page Image Plan

PDF image extraction must preserve provenance.

Required future artifacts:

```text
pages/page-0001.png
pages/page-0002.png
assets/original-images.jsonl
assets/stitches.jsonl
ocr/pages.jsonl
ocr/blocks.jsonl
ocr/nong-ocr.json
bridge/nongmark-map.json
```

Minimum schema expectations:

- Every page has `pageNumber`, `width`, `height`, `dpi`, and source path.
- Every extracted image has `assetId`, `sourcePage`, `bbox`, `width`, `height`, and source relationship/path when available.
- Every stitched image has `stitchedAssetId`, ordered source segments, source page/bbox list, stitch direction, and confidence/reason.
- Every OCR block has page coordinates and stable ID.
- The bridge map records cloud block ID -> NongMark `blockId` / asset ID / page evidence.

Cross-page stitching rules:

1. Only stitch adjacent pages.
2. Candidate regions must be near bottom/top page margins.
3. Horizontal bounds must substantially overlap.
4. Image/text continuation must be recorded as evidence.
5. Never discard the original page assets.
6. Stitched output must map back to source page/bbox segments.

## Cloud/Local Combination

Use local OCR for:

- Privacy-sensitive quick text checks.
- Single image text extraction.
- Smoke tests for local deployment.
- Fallback text when cloud token is unavailable and the user accepts text-only output.

Use cloud OCR for:

- PDF.
- Multi-page scans.
- Table recognition.
- Layout and figure/title labels.
- Word conversion.
- Page/block alignment.
- Any result that must line up with `nongmark/v1`, Word slices, or pandoc-facing exports.

## Validation Gates

Before saying local OCR is stable on a machine:

```powershell
nong ocr check-env --json
nong ocr local <real-image.png> --json
```

Before saying cloud OCR is available:

```powershell
$env:PADDLEOCR_ACCESS_TOKEN = "<access-token>"
nong ocr cloud <real-image-or.pdf> -o <out-dir> --json
```

Before saying OCR-to-Word aligns with Word evidence:

```powershell
nong ocr to-word <input.pdf> -o <out.docx> --json
nong word dissect <out.docx> --output <slice-dir> --json
```

Then inspect `content.jsonl`, `structure.json`, `format.json`, and `assets/manifest.json`. Do not claim alignment from `content.md` or plain text alone.

## Regression Tests to Keep

- `ocr local --json` on real image returns standard JSON without `NaN` or `Infinity`.
- `ocr local` text mode never prints `NaN`; invalid confidence prints `n/a`.
- `ocr local <file.pdf> --json` returns `E002`.
- JSON data includes the local capability boundary.
- GroundPA `multimodal` skill says local OCR is text-only and cloud owns document layout.
