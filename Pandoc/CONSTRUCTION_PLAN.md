# Angri450.Nong.Pandoc Construction Plan

## Goal

`Angri450.Nong.Pandoc` is Nong's shared document middle layer. It aligns Word,
PDF, PPT, Excel, and future document packages around one predictable
AI-readable package shape.

The goal is not to clone GPL Pandoc. The goal is to build Nong's own
Pandoc-style semantic model and stream package:

```text
source file: a.docx / a.pdf / a.pptx / a.xlsx
  -> a.slice/
     manifest.json
     document.json
     content.jsonl
     content.nongmark
     structure.json
     format.json
     diagnostics.json
     assets/manifest.json
     preview/content.txt
```

AI tools should be able to read `a.docx` and `a.pdf` through the same contract:
`content.nongmark` for content, `structure.json` for navigation, `format.json`
for layout evidence, and `assets/manifest.json` for binary resources.

## Package Roles

| Package | Role |
|---------|------|
| `Angri450.Nong.Pandoc` | Shared AST, stream names, manifest contract, NongMark projection |
| `Angri450.Nong.Docx` | DOCX adapter and OOXML repair/format writer |
| `Angri450.Nong.Pdf` | PDF adapter and page/layout evidence writer |
| `Angri450.Nong.Pptx` | PPTX adapter with slide structure and assets |
| `Angri450.Nong.Excel` | XLSX adapter with workbook/sheet/table structure |

## Required Streams

- `manifest.json`: source, stream map, metrics, warnings.
- `document.json`: canonical typed document model.
- `content.jsonl`: one block per line for streaming and retrieval.
- `content.nongmark`: human/AI-editable text projection.
- `structure.json`: outline, pages/slides/sheets, block index, captions.
- `format.json`: fonts, line spacing, table borders, layout evidence.
- `assets/manifest.json`: extracted images, media, embeddings.

Optional streams:

- `diagnostics.json`: validation and repair warnings.
- `preview/content.txt`: lossy plain text preview only.

## Rules

1. `content.nongmark` is the primary readable/editable text projection.
2. `preview/content.txt` is never canonical.
3. Format-specific visual evidence lives in `format.json`.
4. Raw OOXML/PDF/PPTX/XLSX details can be referenced, but not promoted into
   NongMark as rich text.
5. Adapters may add format-specific fields, but they must not rename the shared
   stream files.
6. AI should read the smallest useful set first:
   `content.nongmark -> structure.json -> format.json -> diagnostics.json`.

## Stages

### Stage 1: Core Contract

- Add `nong-pandoc/v1` AST.
- Add shared artifact names and package manifest.
- Add NongMark text reader/writer.
- Keep the package pure .NET and Apache-2.0.

### Stage 2: Word/PDF Alignment

- Move Word slice stream naming to `NongPandocArtifactNames`.
- Move PDF slice stream naming to `NongPandocArtifactNames`.
- Keep existing Word/PDF format evidence, but align field names where practical.

### Stage 3: Adapter Layer

- Add `Docx -> NongPandocDocument`.
- Add `Pdf -> NongPandocDocument`.
- Add `NongPandocDocument -> Docx` through the existing Word writer/formatter.

### Stage 4: PPT/Excel

- PPTX maps slides into structure + content blocks.
- XLSX maps sheets, tables, charts, and named ranges into structure + content
  blocks.

### Stage 5: Optional External Pandoc Bridge

- External `pandoc.exe` interop, if ever needed, must live behind an optional
  bridge and never become the core path.
- The core package must not vendor GPL Pandoc or require Haskell tooling.
