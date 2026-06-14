# 2026-06-08 - Word format gates and slice strict regression

## Time

2026-06-08

## Affected Packages

- Angri450.Nong.Cli
- Angri450.Nong.Docx
- Angri450.Nong.Pdf
- Angri450.Nong.Pandoc
- Cli.Tests

## Change Type

- Feature
- Regression coverage
- Documentation

## What Changed

- Upgraded `nong word format-audit` into a CI-capable visual quality gate.
  - Added `--fail-on-warning`.
  - Added `--min-score <0-100>`.
  - Gate failures return E006 and non-zero exit code while preserving full audit `data` and issue evidence in JSON.
- Added generated Word regression fixtures for controlled dirty OOXML and visual formatting failures.
  - Legacy `tblLook` attributes.
  - Misplaced `tcPr`.
  - Direct `style/tcPr`.
  - `m:mathPr` settings order.
  - `docGrid` line-spacing conflict.
  - Mixed fonts, table shading, table-cell first-line indent, and chemistry subscripts.
- Strengthened table reflow regression.
  - Combined long+wide table sample.
  - Verifies row chunks, column groups, repeated left key columns, continuation labels, repeated header rows, thin previous-part bottom lines, and thick final bottom lines.
- Connected real Word/PDF/Excel/PPT `dissect` outputs to `slice inspect --strict` in unified contract tests.
  - PDF now uses a generated real PDF plus `pdf dissect`, not only a synthetic package.

## Files Touched

- `Cli/Commands/WordCommands.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Docx/WORD_CAPABILITY_MATRIX.md`
- `Cli.Tests/WordCommandTests.cs`
- `Cli.Tests/CliContractTests.cs`
- `Cli.Tests/TestAssets/WordRegression/MANIFEST.md`

## Tests Run

- `dotnet build .\Cli\NongCli.csproj -c Release`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordFormatAudit|FullyQualifiedName~WordCommandTests.WordAcademicFormat_ControlledDirty|FullyQualifiedName~WordCommandTests.WordAcademicFormat_RealZeoliteRegressionAssets|FullyQualifiedName~WordCommandTests.WordTableReflow|FullyQualifiedName~CliContractTests.SliceInspect_WordExcelPptxPdfPackages|FullyQualifiedName~CliContractTests.SliceStructure_BlockIndexUsesUnifiedProvenanceContract|FullyQualifiedName~CliContractTests.SliceFormats_IncludeUnifiedVisualEvidence"`

## Remaining Risks

- `word table-reflow` still uses explicit row/column thresholds. It does not yet detect actual Word pagination overflow.
- Full arbitrary superscript/subscript authoring remains partial; chemistry formula subscripts are covered.
