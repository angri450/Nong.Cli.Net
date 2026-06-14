# Angri450.Nong.Pdf

Local PDF document slicing engine for Nong.NET.

This package provides the deterministic PDF backend used by `nong pdf`:

- `pdf check`: classify text, hybrid, or scan PDFs.
- `pdf dissect`: write a NongPandoc-aligned PDF slice with `content.nongmark`, JSONL blocks, structure, format, diagnostics, assets, and optional `preview/content.txt`.
- `pdf render`: render pages to PNG through the PDFium runtime vendored in `Angri450.Nong.Pdf`.
- `pdf images`: extract embedded image evidence with page and bbox provenance, including page-crop fallback when a PDF image stream cannot be decoded directly.

The primary AI-readable output is `content.nongmark`, aligned with the shared `Angri450.Nong.Pandoc` package contract. `preview/content.txt` is a lossy plain-text preview only. The shared stream set is `manifest.json`, `document.json`, `content.jsonl`, `content.nongmark`, `structure.json`, `format.json`, `diagnostics.json`, and `assets/manifest.json`.

The slice `manifest.json` uses `schemaVersion: "nong-pandoc/package/v1"` so
PDF slices share the same top-level contract as Word, PPT, and Excel slices.

Text-layer slicing includes deterministic heuristics for repeated header/footer removal, two-column reading order, simple aligned-row table blocks, and suspicious custom-encoded font warnings. These heuristics preserve page/bbox provenance in `content.jsonl` and report routing/quality concerns through diagnostics and warnings.

No Python, Pandoc executable, MinerU executable, or external OCR process is required for text-layer PDF slicing.

Pdf text/image inspection uses the PdfPig source vendored in `Angri450.Nong.ThirdParty`; this package no longer depends on the `PdfPig` NuGet package. Page rendering uses Docnet/PDFium source and native assets vendored directly in `Angri450.Nong.Pdf`.

## Install

Most users should install the CLI:

```bash
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
```

Library consumers can reference this package directly when embedding the PDF slice engine in .NET code.

## License

Apache-2.0
