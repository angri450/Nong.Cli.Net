# Read Word

Use `nong word check` before reading user-supplied `.doc`/`.docx`, then use `nong word dissect` as the main path for complex `.docx` reading. It creates a NongMark slice that separates readable content, structural hierarchy, formatting facts, and assets.

For existing user-supplied contracts, old Word files, table-heavy forms, or `.doc` conversion handoffs, also read [existing-document-editing.md](existing-document-editing.md).

For formatting or layout questions, `nong word read` is not enough. Plain text cannot prove fonts, font sizes, line spacing, indentation, alignment, table borders, page margins, or caption formatting. If a workflow starts with `word read` and then needs formatting judgment, stop and run the primary slice path.

## Primary Path

```powershell
nong word check paper.docx --json
nong word dissect paper.docx --output paper.slice --json
```

Slice files:

| File | Purpose |
|------|---------|
| `document.json` | Document metadata, package facts, relationships, and top-level counts. |
| `content.nongmark` | Primary NongMark semantic stream for review, generation handoff, and source-like reasoning. |
| `content.jsonl` | Ordered block stream with stable IDs for paragraphs, tables, images, comments, and other content units. |
| `structure.json` | Heading tree, section hierarchy, outline order, and block-to-section mapping. |
| `format.json` | Styles, fonts, page setup, paragraph/run formatting, and detected format features. |
| `assets/manifest.json` | Media assets and their relationship IDs, paths, captions, and linked blocks. |
| `preview/content.txt` | Lossy plain-text preview for quick reading only. |

When you need to insert content later, keep block IDs from `content.jsonl` or `structure.json` and pass them with `--after <blockId>`. Recent slices expose both `id` and `blockId`, plus `index`, in each JSONL line.

For formatting review, inspect these files before answering:

```text
paper.slice/format.json
paper.slice/content.jsonl
paper.slice/structure.json
```

Then add inventory commands as needed:

```powershell
nong word fonts paper.docx --json
nong word styles paper.docx --json
nong word preview paper.docx --json
nong word validate paper.docx --json
```

Do not tell the user that formatting cannot be checked merely because the first extraction was plain text. Say the first extraction was text-only, run the format-oriented commands, then separate supported facts from current CLI limitations.

## Lightweight Reads

Use these commands when a full slice is unnecessary:

```powershell
nong word read paper.docx --json
nong word preview paper.docx --json
nong word outline paper.docx --json
nong word stats paper.docx --json
```

- `read`: plain text extraction for quick inspection. Do not use it as evidence for formatting or layout.
- `preview`: seven-step document structure diagnostics and OOXML warnings.
- `outline`: heading and section outline.
- `stats`: document statistics.

## Format and Asset Reads

```powershell
nong word fonts paper.docx --json
nong word styles paper.docx --json
nong word images paper.docx --json
nong word comments paper.docx --json
nong word revisions paper.docx --json
nong word validate paper.docx --json
```

Only export media when needed:

```powershell
nong word extract paper.docx -o paper.images --json
```

VML formula/picture content from legacy documents should appear in `word check`, `word images`, `content.jsonl`, and `assets/manifest.json` as image evidence. Do not treat a blank line in `content.md` as proof that the source had no content.
Do not look for `content.md`; current slices use `content.nongmark` plus `preview/content.txt`.

## Feature Deposition

Do not save extracted format facts or assets into `word/formats/` by default. After reading, report the useful findings and ask whether the user wants to save a reusable format profile.

Only write to `formats/` and update [INDEX.md](../formats/INDEX.md) when the user explicitly asks to save or record the format. Skip this step when the user says no.

## Boundaries

Do not parse raw OOXML with ad hoc scripts as a Word reading path. If `nong word dissect`, `read`, or `preview` returns `status: "error"`, fix the input or command arguments and rerun the CLI.

Current format extraction can report run-level fonts and font sizes, paragraph alignment, paragraph spacing and indentation, style IDs and names, document font inventory, page and section dimensions, table style IDs, table widths, table borders, cell widths, cell vertical alignment, structure, and OOXML validation warnings. Resolved style inheritance and exact Word visual rendering are still partial and should be reported as current limitations instead of guessed.
