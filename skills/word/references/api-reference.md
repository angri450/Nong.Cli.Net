# Word CLI Reference

This reference describes the Word command surface exposed by `nong`. Use the CLI directly; do not build a temporary project to access Word internals.

For workflows that start from an existing `.doc`/`.docx` and need repair or layout changes, pair this command reference with [existing-document-editing.md](existing-document-editing.md).

## Read and Inspect

```powershell
nong word check paper.docx --json
nong word convert legacy.doc -o legacy.docx --json
nong word create document.nongmark -o document.docx --json
nong word read paper.docx --json
nong word preview paper.docx --json
nong word dissect paper.docx --output paper.slice --json
nong word stats paper.docx --json
nong word fonts paper.docx --json
nong word styles paper.docx --json
nong word outline paper.docx --json
nong word images paper.docx --json
nong word comments paper.docx --json
nong word revisions paper.docx --json
nong word compare doc1.docx doc2.docx --json
nong word render-preview paper.docx --output pages --dpi 150 --json
nong word db-import paper.slice paper.docx --json
nong word db-list --json
nong word db-blocks <document-id> --type paragraph --limit 20 --json
nong word db-images <document-id> --json
nong word to-pdf paper.docx --output paper.pdf --json
```

Use `word check` before working on user-supplied `.doc`/`.docx`. Use `word convert` only as a `.doc -> .docx` boundary step, then return to OpenXML commands. Use `word dissect --output` for complex documents. Use `read` only when plain text is enough, and `preview` or `validate` when you need OOXML diagnostics.

Use `word db-import` after `word dissect --output` when the document must join the unified `nong.db` object model. Then use `word db-list`, `word db-blocks`, and `word db-images` for read-only evidence from the unified store. Use `render-preview` when page image evidence matters, `compare` when checking a before/after DOCX, and `to-pdf` when the requested deliverable is PDF.

For layout or formatting questions, `read` is insufficient evidence. Run `word dissect --output`, inspect `format.json`, `content.jsonl`, and `structure.json`, then add `fonts`, `styles`, `preview`, or `validate` as targeted follow-ups. Do not answer "open Word manually" unless the CLI failed or the exact visual property is outside the current extraction contract.

Do not use desktop Word COM automation as a fallback for these commands. COM automation is a developer tool in Nong.Dev.Net (`references/word-com-automation.md`).

## Validate, Repair, and Infer

```powershell
nong word validate paper.docx --json
nong word repair-plan --json
nong word infer-format "宋体 小五号 首行缩进" --json
nong word fix-order paper.docx -o fixed.docx --json
nong word academic-format paper.docx -o paper.academic.docx --json
nong word format-gongwen paper.docx -o paper.gongwen.docx --json
nong word format-audit paper.academic.docx --profile academic --min-score 80 --json
nong word table-reflow paper.academic.docx -o paper.tables.docx --max-rows 20 --max-cols 6 --repeat-left-cols 1 --json
nong word rebuild paper.docx -o rebuilt.docx --json
```

`validate` reports schema issues. It does not prove typography or visual layout quality. `repair-plan` explains which repair command matches the user's goal. `fix-order` corrects OOXML child ordering and known dirty-OOXML artifacts. `academic-format` applies deterministic paper formatting to an existing DOCX. `format-gongwen` applies Chinese official-document formatting to an existing DOCX and accepts optional `--config <style.json>`. `format-audit` is a read-only visible-format evidence gate for academic documents. `table-reflow` explicitly splits long or wide tables into continuation tables. `rebuild` cleans style pollution. `infer-format` maps a natural-language Chinese formatting description to an OpenXML-oriented format result.

## Generate and Combine

```powershell
nong word create document.nongmark -o document.docx --json
nong word fill template.docx data.json -o filled.docx --json
nong word merge intro.docx body.docx appendix.docx -o merged.docx --json
nong word extract paper.docx -o extracted-images --json
nong word protect paper.docx -o protected.docx --mode readonly --json
nong word embed-font paper.docx simsun.ttf -o embedded.docx --json
```

Use `fill` for template placeholders, `merge` for ordered DOCX composition, `extract` for media export, `protect` for document protection, and `embed-font` when a real font file is available.

For a new document, `word create` from `.nongmark` is the default route. Do not generate DOCX from Markdown, `python-docx`, or a temporary Word COM script.

## Add Leaves

Canonical add examples use nested commands:

```powershell
nong word add paragraph paper.docx --spec paragraph.json -o out.docx --json
nong word add table paper.docx --spec table.json -o out.docx --json
nong word add footnote paper.docx --text "Footnote text" -o out.docx --json
nong word add endnote paper.docx --text "Endnote text" -o out.docx --json
nong word add image paper.docx --src fig.png --caption "Figure 1" -o out.docx --json
nong word add toc paper.docx --title "Contents" -o out.docx --json
nong word add xref paper.docx --to "_Toc001" --text "see Table 1" -o out.docx --json
nong word add link paper.docx --url "https://example.com" --text "Example" -o out.docx --json
nong word add bookmark paper.docx --name "_Toc001" -o out.docx --json
nong word add comment paper.docx --text "Review note" -o out.docx --json
nong word add math paper.docx --latex "E=mc^2" --display -o out.docx --json
```

The flattened `word add-*` forms are compatibility aliases. Prefer the nested form in documentation and automation.

## Spec Shapes

Paragraph spec:

```json
{
  "text": "New paragraph text.",
  "style": "Normal",
  "bold": false,
  "italic": false
}
```

Table spec:

```json
{
  "caption": "Table 1. Treatment summary",
  "headers": ["Treatment", "Mean", "SD"],
  "rows": [
    ["A", "12.4", "1.1"],
    ["B", "15.2", "0.9"]
  ]
}
```

Both specs can be passed as a file path or inline JSON through `--spec`.

## JSON Contract

All automation paths should include `--json`. A successful command returns `status: "ok"` and may include `data`, `issues`, `artifacts`, `metrics`, and `meta`. A failed command returns `status: "error"` and structured `errors`.

Common Word-facing error codes:

| Code | Meaning | Action |
|------|---------|--------|
| `E001` | `file_not_found` | Fix the path and rerun. |
| `E002` | `unsupported_format` | Run `word check`; convert `.doc` to `.docx` first. |
| `E003` | `missing_argument` | Add the required argument or option. |
| `E005` | `dependency_missing` | Install the CLI/runtime or a boundary converter such as LibreOffice/Word. |
| `E006` | `validation_failed` | Repair the spec, document, or format input. |
| `E009` | `not_implemented` | Stop and report the limitation; do not continue from a failed command. |

Do not continue from stale output after any `status: "error"` response.
