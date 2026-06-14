# Word Capability Matrix

This matrix is scenario-based. It tracks whether Nong can create, read/slice,
repair/format, and regress-test real Word behavior. A green OOXML validator is
required, but it is not enough for visual quality.

## Acceptance Rule

Every Word deliverable must have evidence for:

- `word validate`: no schema errors.
- `word dissect`: `content.nongmark`, `preview/content.txt`, `content.jsonl`, `structure.json`, and `format.json` exist.
- Formatting claims: verified from `format.json.visualEvidence`, `word format-audit`, `content.jsonl`, `fonts`, `styles`, or direct OOXML inspection.
- Existing-document formatting: schema-valid plus visual intent checks for fonts, spacing, paragraph layout, table borders, table shading, table-cell indentation, Latin-name italics, and chemistry subscripts.
- CI visual-quality gates: `word format-audit --fail-on-warning --min-score <n> --json` must return success for protected regression samples.

## Paragraph Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| Title and Heading 1-3 | partial | yes | yes | yes |
| Body paragraphs | yes | yes | yes | yes |
| Quote paragraphs | partial | partial | planned | planned |
| Ordered/unordered lists | partial | partial | planned | planned |
| Footnotes/endnotes | add-only | yes | partial | yes |
| Headers/footers | partial | partial | planned | planned |
| Page breaks/section breaks | partial | partial | planned | planned |
| Table of contents | add-only | detected as fields | planned | yes |

## Font Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| Chinese/English mixed runs | yes | yes | yes | yes |
| Times New Roman Latin | yes | yes | yes | yes |
| Songti Chinese body | yes | yes | yes | yes |
| Heiti Chinese headings | yes | yes | yes | yes |
| Italic parenthesized Latin | partial | yes | yes | yes |
| Superscript/subscript | partial | yes | partial | yes |

Notes:

- Subscript support is productionized for chemistry formulas such as `N2O`, `H2O2`, `O2-`, and `CO2` in `word academic-format`.
- `word format-audit` and `word dissect` now expose chemistry/subscript evidence; full arbitrary superscript/subscript authoring remains partial.

## Line-Spacing Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| Single spacing | partial | yes | planned | planned |
| 1.5 spacing | partial | yes | planned | planned |
| Fixed spacing | partial | yes | avoid for body repair | yes |
| At least spacing | yes | yes | yes | yes |
| Document grid conflict | partial | yes | yes | yes |

Notes:

- `word academic-format` removes document grid conflicts from formatted deliverables and normalizes body/table spacing to `atLeast`.
- `word format-audit` reports document-grid presence when a source document still carries `w:docGrid`.
- Controlled dirty fixtures cover exact-line compression plus `w:docGrid` conflicts and assert that formatted outputs pass `format-audit --fail-on-warning --min-score 95`.

## Table Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| Three-line tables | yes | yes | yes | yes |
| Merged cells | partial | yes | planned | planned |
| Widths and percent widths | yes | yes | yes | yes |
| Auto-fit/table layout | planned | partial | planned | planned |
| Borders and header rules | yes | yes | yes | yes |
| Repeating header rows | yes | yes | yes | yes |
| Dirty OOXML order | n/a | diagnostic | yes | yes |

Notes:

- Controlled generated fixtures now cover legacy `tblLook` attributes, misplaced `tcPr`, direct `style/tcPr`, table shading, table-cell first-line indent, `m:mathPr` settings order, mixed fonts, compressed line spacing, and chemistry subscripts.

## Table Reflow Rules

`word academic-format` repairs visible academic table styling, but it does not change table structure. Structural table layout changes are explicit and must use `word table-reflow`.

| Scenario | Rule | Command Evidence | Regression |
|---|---|---|---:|
| Long table | Split data rows into continuation tables; repeat the header row; insert continuation labels; previous parts use a thin 0.75 pt bottom line; final part uses a 1.5 pt bottom line. | `word table-reflow --max-rows <n>` | yes |
| Wide table | Split columns into column groups; repeat left key columns when requested; repeat the header row in every part; keep three-line table borders. | `word table-reflow --max-cols <n> --repeat-left-cols <n>` | yes |
| Cross-page table | Use explicit row chunks as continuation tables; repeat table number/title/header; continuation top line is 1.5 pt; previous-page/previous-part bottom line is 0.75 pt; final bottom line is 1.5 pt. | `word table-reflow --max-rows <n> --continuation-label 续表` | yes |

Current boundary: Nong does not yet run a pagination/rendering engine, so cross-page behavior is deterministic continuation-table reflow by explicit row thresholds, not automatic page-overflow detection.

Regression now includes combined long+wide reflow: row chunks, column groups, repeated left key columns, continuation labels, repeated header rows, 0.75 pt previous-part bottom lines, and 1.5 pt final bottom lines.

## Image Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| Inline images | add-only | yes | partial | yes |
| Floating images | planned | partial | planned | planned |
| Size extraction | partial | partial | planned | planned |
| Alt text | planned | partial | planned | planned |
| VML/legacy pictures | n/a | yes | preserve as assets | yes |

## Academic Document Scenarios

| Scenario | Create | Read/Slice | Repair/Format | Regression |
|---|---:|---:|---:|---:|
| References section | yes | yes | partial | yes |
| Math/equations | add-only | yes | preserve | yes |
| Figure/table captions | partial | partial | partial | yes |
| TOC | add-only | detected | planned | yes |
| Page numbers | planned | partial | planned | planned |

## Slice Visual Evidence

`word dissect` writes `format.json.visualEvidence` under the unified `nong-pandoc/package/v1` contract. Word now exposes:

- `headings`: style, level, font, alignment, and spacing samples.
- `body`: body paragraph font, indentation, justification, and line-spacing samples.
- `fonts`: document font families plus audit-count summaries.
- `lineSpacing`: paragraph line/rule evidence and audit-count summaries.
- `tables`: three-line table evidence including top/header/bottom widths, shading count, table-cell first-line indent count, and repeated header evidence.
- `latinNames`: parenthesized Latin scientific names with italic/font evidence.
- `chemistry`: chemistry formula samples with digit-subscript evidence.
- `audit`: `word format-audit` status, score, issue count, and summary counts.

## Dirty Document Sources

| Source | Known Risks | Required Path |
|---|---|---|
| Word COM generated | reordered properties, extra style XML | `check -> dissect -> fix-order -> validate` |
| WPS generated | legacy table/style attributes, dirty styles | `check -> fix-order -> validate -> dissect` |
| python-docx generated | minimal styles, table property ordering gaps | `check -> fix-order -> validate` |
| Old Word generated | VML images, legacy `tblLook`, style pollution | `check -> fix-order -> academic-format -> validate -> dissect` |
| AI generated DOCX | invalid OOXML and weak typography | reject ad hoc path; use NongMark or repair path |
