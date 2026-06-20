---
name: word
description: Word document workflows through nong. Trigger on .doc/.docx conversion handoff, DOCX reading, formatting/layout inspection, NongMark slicing, unified NongDb import/list/block/image reads, semantic search, document ingest (--ingest), existing-document repair, template fill, document edits, validation, merge, compare, render-preview, to-pdf, protection, comments, images, fonts, or Word-to-paper/official-document preparation.
---

# Word

Use `nong` as the deterministic Word entrypoint. Do not use desktop Word COM automation, `python-docx`, Markdown-to-DOCX, or ad hoc PowerShell scripts as the normal editing path. NongMark is the primary authoring language 鈥?write `.nongmark` and run `nong word create`.

Schema-valid is not visual-quality complete. Cite format evidence from slice artifacts (format.json, content.jsonl, structure.json) 鈥?not plain text alone.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `word` command group.

**Image analysis routing:** `nong word images --analyze`, `--crop`, and `nong word crop` route to the standalone `Angri450.Nong.Tool.Imaging` dotnet tool. First use auto-installs. Other word commands run inline in the main CLI.

## Three-Layer Workflow

### New documents

```powershell
nong word create document.nongmark -o document.docx --json
nong word validate document.docx --json
nong word dissect document.docx --output document.slice --json
```

### Existing documents 鈥?read & inspect

```powershell
nong word check <file.docx> --json                      # preflight
	nong word convert <file.doc> -o <file.docx> --json       # .doc boundary (no external dependency — .doc gives guidance)
	nong word tab-stops <file.docx> [--paragraph <id>] [--set <spec>] [--json]  # paragraph tab stops (v5)
	nong word fields <file.docx> --json                     # list field codes + cached results (v5)
nong word dissect <file.docx> --output <slice> --json    # full slice
nong word db-import <slice> <file.docx> --json           # unify slice into nong.db
nong word db-list --json
nong word db-blocks <document-id> --limit 20 --json
nong word render-preview <file.docx> -o <pages> --dpi 150 --json
nong word compare <before.docx> <after.docx> --json
nong word to-pdf <file.docx> -o <file.pdf> --json
nong word fonts <file.docx> --json
nong word styles <file.docx> --json
nong word preview <file.docx> --json
nong word validate <file.docx> --json
```

### Semantic search + document ingest

Dissected documents can be ingested into NongDb and searched across:

```powershell
nong word dissect document.docx -o document.slice --ingest --json
nong lit search "query" --ingest --json
nong search "水稻产量" --limit 5 --json                # cross-source semantic search
```

### Existing documents 鈥?repair & format

```powershell
nong word fix-order <file.docx> -o <fixed.docx> --json
nong word academic-format <input.docx> -o <academic.docx> --json
nong word format-gongwen <input.docx> -o <gongwen.docx> --json
nong word format-audit <academic.docx> --profile academic --min-score 80 --json
nong word table-reflow <file.docx> -o <out.docx> --max-rows 20 --max-cols 6 --json
```

Write to explicit `-o` paths. Never overwrite the user's source.

### Page layout & compaction

For tightening layout, blank space, split tables, image pairs, or page-break control 鈥?load [references/page-layout.md](references/page-layout.md).

Key commands: `nong word estimate`, `nong word fit-images`, `nong word compact-tables`, `nong word regroup-images`, `nong word crop`, `nong word page-setup`, `nong word indent`, `nong word paragraph-control`, `nong word render-preview`, `nong word compare`, `nong word image-wrap`, `nong word cell-format`.

### Document editing (in-place)

For template fill, bulk repair, asset extraction, or merging two documents — load [references/write-word.md](references/write-word.md).

```powershell
nong word fill <template.docx> --data <data.json> -o <out.docx> --json   # template fill
nong word rebuild <file.docx> -o <clean.docx> --json                     # strip style pollution
nong word extract <file.docx> --output <assets-dir> --json               # extract embedded images
nong word merge <a.docx> <b.docx> -o <merged.docx> --json                # merge two docx
nong word repair-plan <request> --json                                   # explain which repair command fits
```

### Reading & inspection (read-only)

```powershell
nong word images <file.docx> --json                         # list images with content-aware border detection
nong word outline <file.docx> --json                        # extract heading outline
nong word stats <file.docx> --json                          # document statistics
nong word comments <file.docx> --json                       # read comments
nong word revisions <file.docx> --json                      # list tracked changes
nong word infer-format <description> --json                 # infer OpenXML format from Chinese description
nong word db-images <document-id> --json                    # list extracted images from NongDb
```

### Format operations (layout & style in-place)

```powershell
nong word protect <file.docx> -o <protected.docx> --json           # apply document protection
nong word embed-font <file.docx> --font <name> --json              # embed font into document
nong word cell-format <file.docx> --spec <spec.json> --json        # format table cells: borders, shading, alignment
nong word run-format <file.docx> --spec <spec.json> --json         # character-level: underline, strikethrough, color, highlight
nong word image-wrap <file.docx> --mode square --json              # convert inline→floating with text wrap modes
```

### Add family (append content)

All `add` commands take `--json` and write to explicit `-o <out.docx>` paths. Detailed JSON spec shapes are in [references/write-word.md](references/write-word.md).

```powershell
nong word add paragraph <file.docx> --text "..." -o <out.docx> --json
nong word add table <file.docx> --spec <table.json> -o <out.docx> --json
nong word add image <file.docx> --src <image.png> -o <out.docx> --json
nong word add toc <file.docx> -o <out.docx> --json
nong word add xref <file.docx> --type bookmark --name <bkm> -o <out.docx> --json
nong word add link <file.docx> --url <url> --text "..." -o <out.docx> --json
nong word add bookmark <file.docx> --name <name> -o <out.docx> --json
nong word add comment <file.docx> --text "..." -o <out.docx> --json
nong word add footnote <file.docx> --text "..." -o <out.docx> --json
nong word add endnote <file.docx> --text "..." -o <out.docx> --json
nong word add math <file.docx> --latex "E=mc^2" -o <out.docx> --json
```

## Evidence Rules

- `word read` is text-only 鈥?cannot prove fonts, size, spacing, indentation, borders.
- Format claims must cite format.json, content.jsonl, fonts, styles, preview, or validate.
- Schema-valid 鈮?visually ideal. `word format-audit` is the read-only visual evidence gate.
- `content.nongmark` is the semantic stream; `preview/content.txt` is lossy plain text.
- `word db-import` stores dissect output in unified `nong.db`; use `db-list`, `db-blocks`, and `db-images` only after importing the slice.

## Existing Documents

For contracts, old .doc, table-heavy forms, or format repairs 鈥?load [references/existing-document-editing.md](references/existing-document-editing.md).

Core stance: `.doc` 鈫?convert boundary 鈫?return to `nong word`. Use `word repair-plan` to disambiguate repair vs format requests. COM is a developer escape hatch in Nong.Dev.Net 鈥?do not use for document workflows.

## Reference Routing

Load only the reference needed for the task:

| Task | Reference |
|------|-----------|
| Reading, slicing, evidence, assets | [read-word.md](references/read-word.md) |
| Template fill, add, merge, protect | [write-word.md](references/write-word.md) |
| .doc handoff, legacy repair, contracts | [existing-document-editing.md](references/existing-document-editing.md) |
| Page layout, compaction, images, tables | [page-layout.md](references/page-layout.md) |
| CLI syntax & JSON spec shapes | [api-reference.md](references/api-reference.md) |
| Workspace layout | [workspace-setup.md](references/workspace-setup.md) |
| Word鈫抜nspect/paper feed | [paper-analysis.md](references/paper-analysis.md) |

## COM Escape Hatch

COM automation lives in **Nong.Dev.Net** as a developer reference, not inside this skill. It is a developer tool 鈥?not a document-skill concern. Do not write PowerShell + Word COM scripts for normal editing. If installed Word must be driven, load the Word COM reference from Nong.Dev.Net.

## Error Contract

Always pass `--json`. Treat `status: "error"` as failed.

- `E001 file_not_found`: fix path.
- `E002 unsupported_format`: `word check`; `.doc` 鈫?`word convert`.
- `E003 missing_argument`: supply `--spec`, `--text`, `--latex`, `--src`, or `-o`.
- `E005 dependency_missing`: install/update `Angri450.Nong.Cli`.
- `E006 validation_failed`: repair spec or document before retry.
- `E009 not_implemented`: do not continue as success; use implemented path.
