# Write Word

Use Nong CLI commands for Word generation and edits. Nong.Toolkit.Net should prepare NongMark or small JSON specs and route the work to `nong`; it should not recreate the DOCX writer pipeline in local code.

For edits to existing user documents, especially legacy `.doc` conversions or table-heavy contracts, read [existing-document-editing.md](existing-document-editing.md) before choosing commands.

Do not write `python-docx`, Markdown-to-DOCX, or PowerShell + Word COM scripts for normal Word generation, formatting, or edits. COM automation patterns have moved to Nong.Dev.Net (`references/word-com-automation.md`). Load that reference only when installed Microsoft Word must be driven explicitly, and treat COM as the final escape hatch.

## 1. Choose the Output Path

Before creating or changing a document, choose the output path explicitly:

```powershell
$out = "paper.out.docx"
```

Use `-o <out.docx>` on every command that writes a DOCX.

## 2. Template Fill

## 2. NongMark Long Documents

For new long documents, reports, proposals, or paper-like deliverables, write `document.nongmark` first:

```powershell
nong word create document.nongmark -o document.docx --json
nong word validate document.docx --json
nong word dissect document.docx --output document.slice --json
```

Use `.nongmark` or `.nmk`. Do not create `content.md` or use Markdown as the source. NongMark should carry the content, structure, tables, references, figures, and formatting intent.

Use block forms for structure:

```nongmark
---
title: Document Title
author: Author
date: 2026-06-07
---

# 一级标题

正文（Latin name）继续写在 NongMark 里。

::: table {caption="表1 结果"}
| 处理 | 指标 |
| --- | --- |
| A | 12.3 |
:::

::: references
[1] Smith J. Example reference. Journal, 2024.
:::
```

## 3. Template Fill

Use template fill when the source document already contains placeholders:

```powershell
nong word fill template.docx data.json -o filled.docx --json
```

`data.json` should contain the values needed by the template. Keep the data file small enough to review, and validate the result with:

```powershell
nong word preview filled.docx --json
nong word validate filled.docx --json
```

## 4. Append Content

Use the nested `word add ...` commands as the canonical editing path:

```powershell
nong word add paragraph paper.docx --spec paragraph.json -o paper.p1.docx --json
nong word add table paper.p1.docx --spec table.json -o paper.table.docx --json
nong word add image paper.table.docx --src fig.png --caption "Figure 1" -o paper.fig.docx --json
nong word add math paper.fig.docx --latex "E=mc^2" --display -o paper.math.docx --json
```

Use `--after <blockId>` when a prior `word dissect --output` slice identified the insertion point. Prefer the `blockId` field from `content.jsonl`; `id` is the same anchor in recent slices.

Paragraph spec:

```json
{
  "text": "This paragraph was added by Nong.",
  "style": "Normal",
  "bold": false,
  "italic": false
}
```

Table spec:

```json
{
  "caption": "Table 1. Main results",
  "headers": ["Group", "Mean", "SD"],
  "rows": [
    ["Control", "10.1", "1.0"],
    ["Treatment", "13.4", "1.2"]
  ]
}
```

Other add leaves:

```powershell
nong word add footnote paper.docx --text "Footnote text" -o out.docx --json
nong word add endnote paper.docx --text "Endnote text" -o out.docx --json
nong word add toc paper.docx --title "Contents" -o out.docx --json
nong word add xref paper.docx --to "_Toc001" --text "see Table 1" -o out.docx --json
nong word add link paper.docx --url "https://example.com" --text "Example" -o out.docx --json
nong word add bookmark paper.docx --name "_Toc001" -o out.docx --json
nong word add comment paper.docx --text "Review note" -o out.docx --json
```

`word add-*` is a compatibility alias pattern only.

### Extract embedded images

```powershell
nong word extract <file.docx> --output <assets-dir> --json
```

Extracts all embedded images from a DOCX to the specified output directory. Use this before `word images` if you need raw image files rather than metadata.

### Rebuild (strip style pollution)

```powershell
nong word rebuild <file.docx> -o <clean.docx> --json
```

Cleans OOXML style pollution: removes latent styles, unused numbering definitions, and orphaned theme references. Useful before comparing documents or when file size is unexpectedly large.

## 5. Existing DOCX Academic Formatting

For a draft DOCX that needs paper-style formatting, use the deterministic formatter before considering COM:

```powershell
nong word repair-plan --json
nong word academic-format draft.docx -o draft.academic.docx --json
nong word validate draft.academic.docx --json
nong word format-audit draft.academic.docx --profile academic --min-score 80 --json
nong word dissect draft.academic.docx --output draft.academic.slice --json
```

This applies Chinese/Latin font defaults, heading levels, three-line table borders, centered table cells, and italic Latin text inside parentheses where possible.

Do not call the result complete from `validate` alone. Run `dissect` and inspect format evidence:

```powershell
nong word format-audit draft.academic.docx --profile academic --min-score 80 --json
nong word dissect draft.academic.docx --output draft.academic.slice --json
```

Minimum evidence:

- `format.json` shows expected page and table formats.
- `content.jsonl` shows font and line-spacing evidence.
- `preview/content.txt` shows no missing sections.
- Direct OOXML checks are acceptable when a property is not yet surfaced by the slice.

If long or wide tables remain unreadable after formatting, reflow them explicitly:

```powershell
nong word table-reflow draft.academic.docx -o draft.tables.docx --max-rows 20 --max-cols 6 --repeat-left-cols 1 --json
nong word validate draft.tables.docx --json
nong word format-audit draft.tables.docx --profile academic --min-score 80 --json
```

## 6. Official-Document Drafts and Gongwen Formatting

For an official-document draft from structured fields, use Inspect to generate the DOCX, then validate and slice with Word:

```powershell
nong inspect write-official official-spec.json -o official.docx --json
nong word validate official.docx --json
nong word dissect official.docx --output official.slice --json
```

For an existing DOCX that needs gongwen/public-document formatting, use the Word formatter:

```powershell
nong word format-gongwen draft.docx -o draft.gongwen.docx --json
nong word validate draft.gongwen.docx --json
nong word dissect draft.gongwen.docx --output draft.gongwen.slice --json
```

## 7. Merge, Protect, Embed, and Repair

```powershell
nong word merge intro.docx body.docx appendix.docx -o merged.docx --json
nong word protect merged.docx -o protected.docx --mode readonly --json
nong word embed-font merged.docx simsun.ttf -o embedded.docx --json
nong word repair-plan --json
nong word fix-order merged.docx -o fixed.docx --json
nong word rebuild merged.docx -o rebuilt.docx --json
```

Run validation after each destructive or structural edit:

```powershell
nong word validate rebuilt.docx --json
nong word preview rebuilt.docx --json
```

## 8. Paper Drafts

For full paper drafting, prefer NongMark + `word create`. Use Inspect JSON spec generation only when the workflow already has a structured paper spec:

```powershell
nong inspect write-paper paper-spec.json -o paper.docx --json
```

Then use Word commands for DOCX-level validation, slicing, and post-editing:

```powershell
nong word dissect paper.docx --output paper.slice --json
nong word validate paper.docx --json
```

## 9. Error Handling

Treat `status: "error"` as a hard stop. Common fixes:

- `E001`: verify the input file exists.
- `E002`: run `word check`; convert `.doc` to `.docx` before editing.
- `E003`: supply the required option such as `--spec`, `--text`, `--latex`, `--src`, or `-o`.
- `E005`: install/update `nong`, or install a boundary converter when `word convert` needs one.
- `E006`: repair invalid JSON specs, invalid format descriptions, or validation failures.
- `E009`: stop and report the limitation; do not use stale output.
