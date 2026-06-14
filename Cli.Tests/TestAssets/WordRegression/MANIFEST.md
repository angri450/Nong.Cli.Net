# Word Regression Assets

Every real Word bug file should become a test asset or a generated fixture.
Use stable ASCII filenames and record the original issue in this manifest.
Generated fixtures are also first-class regression samples when they encode a
specific dirty OOXML or visual-format failure more precisely than a real file.

## Categories

- `normal`: valid ordinary documents.
- `dirty-ooxml`: schema-invalid or Word-tolerated XML.
- `table-anomalies`: table order, merge, width, border, and header issues.
- `mixed-fonts`: Chinese/Latin font and run segmentation cases.
- `line-spacing`: exact, atLeast, auto, and document-grid conflicts.
- `ai-failures`: invalid or visually poor documents produced by an AI route.

## Assets

| Path | SHA256 | Category | Expected Route | Bug Types |
|---|---|---|---|---|
| `dirty-ooxml/zeolite-workstation-beautified-dirty.docx` | `8A43052E8DD49F83AB9F0F6B9065705DCBD66D752444F34CA3BBED14736FEA8F` | dirty-ooxml, table-anomalies, line-spacing, mixed-fonts | `academic-format -> validate -> dissect` | legacy `tblLook` attrs, misplaced `tcPr`, direct `style/tcPr`, compressed line spacing risk, academic table formatting |
| `academic-format/zeolite-workstation-handwritten.docx` | `6B072D5876F1A7CC2EB340A6F492A1DD9144C3F5C063BB908ED6BEDFE71B9A81` | normal, mixed-fonts, line-spacing, table-anomalies | `academic-format -> validate -> format-audit -> dissect` | smaller handwritten-source formatting gaps, three-line table normalization, chemistry subscript evidence |
| `academic-format/zeolite-workstation-original.docx` | `13C3C924BAC235BECD8732D9277ED408B840F8C812375A9E9FB655DEBDE425C9` | dirty-ooxml, mixed-fonts, line-spacing, table-anomalies | `academic-format -> validate -> format-audit -> dissect` | weak visible typography, table border cleanup, Latin-name italics, chemistry subscript evidence |
| `academic-format/zeolite-workstation-beautified.docx` | `8A43052E8DD49F83AB9F0F6B9065705DCBD66D752444F34CA3BBED14736FEA8F` | dirty-ooxml, mixed-fonts, line-spacing, table-anomalies, ai-failures | `academic-format -> validate -> format-audit -> dissect` | visually compressed document, legacy table/style artifacts, table shading/indent cleanup, chemistry subscript evidence |

## Generated Fixtures

| Test | Category | Expected Route | Bug Types |
|---|---|---|---|
| `WordAcademicFormat_ControlledDirtyOoxmlMatrix_RepairsVisualAndSchemaEvidence` | dirty-ooxml, table-anomalies, line-spacing, mixed-fonts | generated docx -> `academic-format -> validate -> format-audit --fail-on-warning --min-score 95` | legacy `tblLook`, misplaced `tcPr`, direct `style/tcPr`, `m:mathPr` settings order, `docGrid` conflict, table shading, table cell indent, compressed line spacing, bad fonts, chemistry subscripts |
| `WordTableReflow_LongWideTable_SplitsRowsAndColumnsWithContinuationEvidence` | table-anomalies | generated docx -> `table-reflow -> validate` | long table row chunks, wide table column groups, repeated key columns, continuation labels, repeated header rows, previous-part thin bottom line, final thick bottom line |

## Required Checks

For each deliverable generated from a regression asset:

```powershell
nong word validate <out.docx> --json
nong word format-audit <out.docx> --fail-on-warning --min-score 95 --json
nong word dissect <out.docx> --output <slice-dir> --json
nong slice inspect <slice-dir> --strict --json
```

Then inspect:

- `format.json.visualEvidence` for headings, body, fonts, line spacing, tables, Latin names, and chemistry/subscript evidence.
- `format.json` for table borders and page settings.
- `content.jsonl` for font evidence and line spacing.
- `preview/content.txt` for missing text.
- Direct OOXML only when `format.json` cannot expose a property yet.
- Direct OOXML for strict Word regression checks: three-line table widths, no table shading, no table-cell first-line indent, chemical formula digit subscripts, and settings child order around `m:mathPr`.
