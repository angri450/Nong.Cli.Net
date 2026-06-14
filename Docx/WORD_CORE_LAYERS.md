# Word Core Layers

Nong Word work is split into three deterministic layers. Do not mix these with
ad hoc COM, python-docx, or Markdown-to-DOCX paths.

## Layer 1: NongMark -> DOCX

Purpose: generate new documents from a stable source.

Default command:

```powershell
nong word create document.nongmark -o document.docx --json
nong word validate document.docx --json
nong word dissect document.docx --output document.slice --json
```

Contract:

- Source file is `.nongmark` or `.nmk`.
- `document.nongmark` carries structure, tables, references, figures, and formatting intent.
- Generated DOCX must pass `word validate`.
- Slice artifacts must follow the NongPandoc package contract:
  `manifest.json`, `document.json`, `content.jsonl`, `content.nongmark`,
  `structure.json`, `format.json`, `diagnostics.json`, `assets/manifest.json`,
  and optional `preview/content.txt`.

Do not use Markdown as the canonical Word source.

## Layer 2: DOCX -> NongPandoc package

Purpose: read, slice, inspect, and reason about existing Word files.

Default command:

```powershell
nong word check input.docx --json
nong word dissect input.docx --output input.slice --json
nong word fonts input.docx --json
nong word styles input.docx --json
nong word validate input.docx --json
```

Contract:

- `content.nongmark` is the semantic stream.
- `preview/content.txt` is plain-text preview only.
- `content.jsonl`, `structure.json`, and `format.json` are formatting evidence.
- `diagnostics.json` is the shared diagnostics entry point.
- `word read` is not enough for layout or visual claims.

## Layer 3: DOCX Repair/Format

Purpose: repair or format user-provided DOCX without leaving the deterministic
OpenXML route.

Default command:

```powershell
nong word fix-order input.docx -o input.fixed.docx --json
nong word academic-format input.fixed.docx -o input.academic.docx --json
nong word validate input.academic.docx --json
nong word dissect input.academic.docx --output input.academic.slice --json
```

Contract:

- Do not overwrite the source.
- Do not use Python helpers for ordinary Word formatting.
- Do not use desktop Word COM except as a final escape hatch or `.doc` boundary conversion.
- Schema-valid output is necessary but not sufficient; verify fonts, spacing, heading style, table borders, and key visual intent.

Current repair coverage includes:

- OOXML child order for common property containers.
- Legacy `tblLook` attributes rejected by strict validation.
- Table cell property placement and duplicate merging.
- Invalid direct `style/tcPr` artifacts from legacy table styles.
- False `noWrap` compatibility artifacts.
