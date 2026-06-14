# 2026-06-08 NongPandoc Strict Query Visual Evidence

## Impacted packages

- `Angri450.Nong.Pandoc`
- `Angri450.Nong.Cli`
- `Angri450.Nong.Docx`
- `Angri450.Nong.Pdf`
- `Angri450.Nong.Excel`
- `Angri450.Nong.Pptx`
- `Angri450.Nong.Cli.Tests`

## What changed

- Added `slice inspect --strict` for hard block-level provenance evidence checks.
- Added shared slice query commands:
  - `slice blocks`
  - `slice block <slice-dir> <block-id>`
  - `slice assets`
- Added shared `NongPandocSliceQuery` API so AI callers can read block content, structure, format, diagnostics, and assets through one path.
- Added `format.json.visualEvidence` for Word, PDF, Excel, and PPTX slices.
- Extended slice reader results to include `content.jsonl`, assets, and evidence validation.
- Added `word repair-plan` to make Word repair command routing machine-readable.
- Clarified `word fix-order` as internal OOXML/structure repair only.
- Clarified `word academic-format` as the current visible academic formatting path.
- Updated CLI command manifest and `Cli/AGENT.md`; command discovery now reports 91 implemented commands.

## Why

The previous package contract unified file streams, but AI callers still needed stronger guardrails:

- strict proof that every block has provenance evidence;
- one query path instead of each package reading its own files;
- visual layout evidence in `format.json`;
- clearer Word repair command names so models do not mistake `fix-order` or `validate` for user-visible formatting completion.

The Word naming work directly addresses `用户反馈/反馈1`, where internal OOXML repair was confused with visible Word formatting repair.

## Files touched

- `Pandoc/NongPandocSlicePackageReader.cs`
- `Pandoc/NongPandocSliceQuery.cs`
- `Pandoc/NongPandocVisualEvidence.cs`
- `Docx/NongMarkModels.cs`
- `Docx/WordSlice.cs`
- `Pdf/PdfModels.cs`
- `Pdf/PdfSlice.cs`
- `Excel/ExcelSlice.cs`
- `Pptx/PptxSlice.cs`
- `Cli/Commands/SliceCommands.cs`
- `Cli/Commands/WordCommands.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Cli.Tests/CliContractTests.cs`

## Verification

- `dotnet build .\Pandoc\PandocCore.csproj -c Release`
- `dotnet build .\Docx\DocxCore.csproj -c Release`
- `dotnet build .\Pdf\PdfCore.csproj -c Release`
- `dotnet build .\Excel\ExcelCore.csproj -c Release`
- `dotnet build .\Pptx\PptxCore.csproj -c Release`
- `dotnet build .\Cli\NongCli.csproj -c Release`
- `dotnet build .\Cli.Tests\Cli.Tests.csproj -c Release`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests.SliceInspect_Strict|FullyQualifiedName~CliContractTests.SliceFormats_IncludeUnifiedVisualEvidence|FullyQualifiedName~CliContractTests.Commands_Json_DistinguishesInternalAndVisibleWordRepair|FullyQualifiedName~CliContractTests.WordRepairPlan|FullyQualifiedName~CliContractTests.SliceStructure_BlockIndexUsesUnifiedProvenanceContract|FullyQualifiedName~CliContractTests.SliceInspect_WordExcelPptxPdfPackages"`
- `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests"`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect|FullyQualifiedName~WordCommandTests.WordAcademicFormat"`

## Remaining risks

- `word repair`, `word format-audit`, and `word compare-format` are still planned product commands; `word repair-plan` only prevents routing confusion for now.
- Word visual evidence is a summary over extracted format data, not a full rendered comparison.
- PPTX text-to-shape matching still uses text preview, so duplicate slide text may need stronger shape IDs.
- Excel chart and pivot evidence still need deeper extraction.
