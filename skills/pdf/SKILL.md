---
name: pdf
description: Local PDF document slicing and editing through nong. Trigger on PDF preflight, selectable-text PDF extraction, hybrid/scan routing, PDF page rendering, embedded PDF image extraction, PDF merge, PDF split, PDF OCR, PDF compression, PDF dissect --ingest, unified NongDb import/list/block/image reads, content.nongmark, page/bbox evidence, or PDF-to-AI-readable document packages.
---

# PDF

Use `nong pdf` as the deterministic local PDF entrypoint. Nong.Toolkit.Net routes, reads JSON/slice outputs, and reports evidence; it does not recreate PDF parsing with Python, Pandoc, MinerU, or ad hoc scripts.

The primary readable artifact is `content.nongmark`, not plain Markdown. `preview/content.md` is a lossy compatibility preview only.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `pdf` command group.

**Modular:** `nong pdf` routes to the standalone `Angri450.Nong.Tool.Pdf` dotnet tool. First use auto-installs. Command surface unchanged.
## Default Workflow

For detailed routing notes, read [references/pdf-routing.md](references/pdf-routing.md).

1. Always classify the PDF first:

```powershell
nong pdf check <file.pdf> --json
```

Use `classification`, `recommendedMode`, `hasTextLayer`, `textCharsPerPage`, `imageCoverageRatio`, and `warnings` to choose the path.

2. For selectable text PDFs, local slicing is the default:

```powershell
nong pdf dissect <file.pdf> --output <slice-dir> --mode auto --ingest --json
```

Read at least:

- `<slice-dir>/content.nongmark`
- `<slice-dir>/content.jsonl`
- `<slice-dir>/structure.json`
- `<slice-dir>/format.json`
- `<slice-dir>/diagnostics/check.json`
- `<slice-dir>/diagnostics/reading-order.json`

Current text-layer slicing removes repeated running headers/footers when they repeat across pages near page edges, detects simple two-column reading order, emits simple aligned-row table blocks, and warns about suspicious custom-encoded fonts. Treat those as deterministic heuristics with page/bbox evidence, not as semantic certainty.

If the PDF slice needs to join the unified NongDb object model, import it immediately after a successful dissect:

```powershell
nong pdf db-import <slice-dir> <file.pdf> --json
nong pdf db-list --json
nong pdf db-blocks <document-id> --type paragraph --limit 20 --json
nong pdf db-images <document-id> --json
```

3. For page images or visual inspection:

```powershell
nong pdf render <file.pdf> --output <pages-dir> --dpi 150 --json
```

4. For embedded image provenance:

```powershell
nong pdf images <file.pdf> --output <assets-dir> --json
```

5. For scan PDFs without a useful text layer, use `pdf dissect --mode ocr` only after local OCR runtime is installed and has passed a real smoke test:

```powershell
nong ocr check-env --json
nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json
nong pdf dissect <scan.pdf> --output <slice-dir> --mode ocr --json
```

Local OCR runtime bundles track the CLI version (`Angri450.Nong.OcrRuntime.*` for the installed Nong). Right after a fresh NuGet release, domestic mirrors can lag; if Huawei has not synced the runtime package yet, use NuGet.org explicitly or report mirror lag instead of falling back silently.

Local OCR is text recognition only. It does not provide cloud-grade layout labels, table structure, Word formatting, or reliable cross-page reconstruction. For page-faithful PDF-to-Word/NongMark output, prefer readable-text `pdf dissect` first; use cloud OCR/to-word when scan layout, tables, or page alignment matter and a token is available. For PDF-to-DOCX through the NongPandoc slice pipeline, use: `nong pdf to-word <file.pdf> --output <slice-dir> --json`

## Evidence Rules

- Do not answer PDF layout or extraction questions from copied plain text alone.
- Use `content.nongmark` for AI-readable text with page, bbox, source, font, size, and block IDs.
- Use `content.jsonl` as the block-level source of truth for page/bbox/source evidence.
- Use `diagnostics/check.json` to explain why a PDF was routed as `text`, `hybrid`, or `scan`.
- Use `diagnostics/reading-order.json` to explain whether the text stream used single-column or two-column reading order.
- If `diagnostics/warnings.json` mentions repeated header/footer removal or suspicious custom-encoded fonts, report that qualification before making layout-sensitive claims.
- If `preview/content.md` exists, treat it as lossy preview and say so when precision matters.
- If `pdf check` reports `scan`, do not claim local parsing can recover full layout unless OCR mode actually succeeds and the output contains blocks.
- If cloud OCR is available, `nong ocr cloud` or `nong ocr to-word` can be stronger for full PDF layout/table reconstruction, but the cloud key is not a prerequisite for basic local PDF slicing.

## Output Contract

`pdf dissect` should produce:

```text
slice-dir/
  manifest.json
  document.json
  content.jsonl
  structure.json
  format.json
  content.nongmark
  preview/content.md
  asset manifest
  diagnostics/check.json
  diagnostics/reading-order.json
  diagnostics/warnings.json
```

For a non-empty text PDF, do not treat the command as successful unless `content.nongmark`, `content.jsonl`, `document.json`, `structure.json`, `format.json`, and the slice asset manifest exist and are non-empty.

## Boundaries

Implemented local commands:

```powershell
nong pdf check <file.pdf> --json
nong pdf dissect <file.pdf> --output <slice-dir> --mode auto --ingest --json
nong pdf render <file.pdf> --output <pages-dir> --dpi 150 --json
nong pdf images <file.pdf> --output <assets-dir> --json
nong pdf merge <file1.pdf> <file2.pdf> ... -o <merged.pdf> --json
nong pdf split <file.pdf> -o <split.pdf> --pages <range> --json
nong pdf ocr <scan.pdf> -o <output.pdf> --dpi 200 --json
nong pdf compress <file.pdf> -o <compressed.pdf> --json
nong pdf create <spec.json> -o <out.pdf> --json                      # v7: PDF generation
nong pdf form-fields <file.pdf> --json                                # v7: AcroForm field listing (read-only)
nong pdf metadata <file.pdf> --json                                   # v7: info dict (Title/Author/Subject)
nong pdf annotations <file.pdf> --json                                # v7: annotation listing
nong pdf db-import <slice-dir> <file.pdf> --json
nong pdf db-list --json
nong pdf db-blocks <document-id> --type paragraph --limit 20 --json
nong pdf db-images <document-id> --json
```

Do not use Pandoc as a PDF parser. Do not require Python, pip, MinerU, or a Pandoc executable on the client machine.

`pdf dissect --mode auto` is reliable for selectable text PDFs in the current Nong release. Hybrid mode preserves native text and embedded image evidence, while image-region OCR/layout enrichment is still limited. OCR mode depends on Nong's local PP-OCRv5 runtime and should be treated as text extraction, not full document reconstruction.

`pdf ocr` renders each page of a scanned PDF as JPEG images and embeds them in a new PDF with placeholder text markers. For full text recognition, combine with `nong ocr cloud` or local OCR. `--dpi` controls render quality (default 200).

`pdf compress` rebuilds content streams and removes unused objects to reduce file size.

## Error Contract

Always pass `--json` when output feeds another tool or model decision. Treat `status: "error"` as failed.

Common codes:

- `E001 file_not_found`: fix the input path.
- `E002 unsupported_format`: the input is not a valid `.pdf`.
- `E005 dependency_missing`: install/check the PDF/OCR native runtime; for OCR mode run `nong ocr install-model pp-ocrv5-mobile --json`.
- `E006 validation_failed`: fix mode, DPI, or output path.
- `E007 read_failed`: PDF exists but parsing/extraction failed; try render or cloud OCR if appropriate.
- `E008 write_failed`: output artifacts were not created or are empty.
- `E004 internal_error`: unexpected bug; keep stderr/stdout and command JSON for diagnosis.
