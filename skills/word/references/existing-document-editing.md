# Existing Document Editing

Use this reference when the user provides an existing Word file and asks to change formatting, convert a contract, extract a template, repair a document, or compare Nong against raw Word COM automation.

## Boundary: `.doc` vs `.docx`

`nong word` works on `.docx` OpenXML. Legacy binary `.doc` must be converted before deterministic Nong processing.

Recommended stance:

1. Run `nong word check <file> --json` before reading or editing.
2. Treat `.doc -> .docx` as a boundary conversion step.
3. Use `nong word convert <file.doc> -o <file.docx> --json` for that boundary.
4. After conversion, return immediately to `nong word`.
5. Do not use COM as the main reading/editing engine.

`word convert` tries available conversion engines such as LibreOffice and, on Windows, hidden Word COM as a fallback. If you must write a custom Word COM script anyway, COM automation patterns are in Nong.Dev.Net (`references/word-com-automation.md`). Prefer hidden Word, unique output paths, `SaveAs2(..., 16)`, explicit document close, `Quit()`, COM release, and no blanket `Stop-Process WINWORD -Force`.

## Real-Case Pipeline

For a user-supplied legacy contract or form:

```powershell
# 1. Preflight and convert when needed.
nong word check contract.doc --json
nong word convert contract.doc -o contract.docx --json

# 2. Preflight the converted DOCX, then inspect and slice.
nong word check contract.docx --json
nong word dissect contract.docx --output contract.slice --json
nong word preview contract.docx --json
nong word validate contract.docx --json
nong word fonts contract.docx --json
nong word styles contract.docx --json

# 3. Repair converted legacy OOXML.
nong word fix-order contract.docx -o contract.fixed.docx --json
nong word validate contract.fixed.docx --json

# 4. Rebuild styling if needed, then validate again.
nong word rebuild contract.fixed.docx -o contract.rebuilt.docx --json
nong word validate contract.rebuilt.docx --json
nong word preview contract.rebuilt.docx --json

# 5. For academic/paper formatting, use the deterministic OpenXML formatter.
nong word repair-plan --json
nong word academic-format contract.rebuilt.docx -o contract.academic.docx --json
nong word validate contract.academic.docx --json
nong word format-audit contract.academic.docx --profile academic --min-score 80 --json
nong word dissect contract.academic.docx --output contract.academic.slice --json

# 6. If long or wide tables still need explicit continuation tables.
nong word table-reflow contract.academic.docx -o contract.tables.docx --max-rows 20 --max-cols 6 --repeat-left-cols 1 --json
nong word validate contract.tables.docx --json
nong word format-audit contract.tables.docx --profile academic --min-score 80 --json
```

Do not stop after `read`. For table-heavy contracts, `preview/content.txt` or plain text may show text but not the visible contract layout. Use `format.json`, `content.jsonl`, `structure.json`, `fonts`, `styles`, `preview`, and `validate`.

Use `content.jsonl` or `structure.json` for insertion anchors. Recent Nong builds emit both `id` and `blockId`, plus `index`, in each JSONL line.

## Legacy Word/WPS Artifacts

Real converted `.doc` files may contain schema-invalid but Word-tolerated XML. `word fix-order` is the first repair tool.

Known artifacts handled by recent Nong builds:

- false `w:noWrap w:val="0"` in table cells
- table-row `tblPrEx` ordering
- section `cols` ordering
- paragraph `framePr` ordering
- style `next` ordering
- invalid `tblStyle` children inside table-style `tblPr`
- style-reference diagnosis where legacy numeric `styleId` maps to a style name such as `Normal`
- VML picture/formula references (`w:pict` / `v:imagedata`) are surfaced as image blocks/assets with warnings instead of silent blank lines

After repair, always run:

```powershell
nong word validate fixed.docx --json
nong word preview fixed.docx --json
```

Treat `validate` errors as blocking. Treat `preview` warnings as quality or formatting issues that may still need user-facing explanation.

## Contract Transformations

For "contract to official document", "contract to paper", or "extract contract template":

- Slice the source first.
- Extract parties, project name, fees, dates, term, confidentiality, acceptance, IP ownership, and signature/contact tables.
- Produce explicit artifacts instead of only prose:
  - repaired source DOCX
  - extracted field JSON
  - reusable template DOCX
  - filled template DOCX
  - paper draft DOCX when requested
  - official-document draft DOCX when requested
  - report with commands, logs, pass/fail status, and limitations

Use `nong inspect write-paper` for paper-style drafts. Use `nong inspect write-official <spec.json> -o <official.docx> --json` for official-document drafts. Use `nong word format-gongwen <input.docx> -o <gongwen.docx> --json` when the source is an existing DOCX that needs gongwen/public-document formatting. Validate and slice the generated DOCX before calling the deliverable complete.

## Consumer Feedback Response

The practical complaint was valid: a prior session fell back to raw COM and made Nong look absent. The correct Nong.Toolkit.Net behavior is:

- Acknowledge that `.doc` conversion may require Word/LibreOffice.
- Do not continue with naked COM after conversion.
- Use Nong for deterministic inspection, repair, edits, generation, validation, and reporting.
- Surface product gaps instead of pretending they are solved.

Current remaining gap:

- Nong now has `word check`, `word convert`, and `word academic-format`. For academic formatting complaints, use `academic-format` before any COM fallback.
- Nong now has `word repair-plan`, `word format-audit`, and `word table-reflow`. Use them to avoid confusing schema repair with visible formatting repair and to handle table continuation explicitly.
- Higher-level arbitrary existing-document editing is still composable. Use `check`, `convert`, `dissect`, `repair-plan`, `fix-order`, `academic-format`, `format-gongwen`, `format-audit`, `table-reflow`, `rebuild`, `add`, `fill`, `word create`, `inspect write-paper`, and `inspect write-official` with explicit artifacts and validation.

## Formatting Quality Gate

Do not confuse schema repair with acceptable layout. `word validate` can pass while the document still has poor typography or unreadable spacing.

For academic or proposal documents, inspect at least:

- Headings: level, font family, font size, alignment, and keep-next behavior.
- Body: Chinese font, Latin font, first-line indent, line rule, spacing before/after.
- Tables: top/bottom/header rules, inside borders, width, header shading, merged cells, and cell vertical alignment.
- Mixed text: Chinese/Latin run segmentation, Times New Roman for Latin, Songti/Heiti for Chinese, italics where requested.
- Non-text assets: VML pictures, images, formulas, captions, TOC, and page numbers.

If slice evidence cannot prove a property, inspect the relevant OOXML part directly instead of falling back to COM.

## Success Criteria

For real user documents, do not call the task done until:

- Source was copied or converted without modifying the original.
- Every generated DOCX has an explicit output path.
- `word check` was reviewed for `.doc`, VML, image, and blockId risks.
- `word validate` passes for deliverable DOCX files.
- `word preview` diagnostics are reviewed.
- `word format-audit` passes or its warnings are reported honestly for academic-style deliverables.
- `word dissect` artifacts provide positive format evidence for the user's visual requirements.
- A report records command outcomes and remaining limitations.
