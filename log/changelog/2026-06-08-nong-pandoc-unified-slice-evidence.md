# 2026-06-08 NongPandoc Unified Slice Evidence

## Impacted packages

- `Angri450.Nong.Pandoc`
- `Angri450.Nong.Docx`
- `Angri450.Nong.Pdf`
- `Angri450.Nong.Excel`
- `Angri450.Nong.Pptx`
- `Angri450.Nong.Cli.Tests`

## What changed

- Added shared `NongPandocBlockProvenance` and `NongPandocLayoutEvidence` models in `Pandoc`.
- Added `structure.json -> blockIndex[*].provenance` for Word, PDF, Excel, and PPTX slices.
- Word provenance now records `format=docx`, block position, source kind, image relationship IDs, and EMU layout where available.
- PDF provenance now records `format=pdf`, page, bbox, source, asset ID, confidence, and block warnings.
- Excel provenance now records `format=xlsx`, sheet, cell address, position, and formula/merge/table notes.
- PPTX provenance now records `format=pptx`, slide, source shape kind, layout, asset ID, relationship ID, and confidence.
- Added cross-format CLI contract coverage that asserts all four formats expose the same block-level provenance field shape.

## Why

The package contract was already unified at the stream level (`nong-pandoc/package/v1`), but detailed block evidence still risked drifting per package. This change gives AI callers one stable place to read source evidence for a block regardless of whether it came from Word, PDF, Excel, or PPT.

## Files touched

- `Pandoc/NongPandocEvidence.cs`
- `Docx/NongMarkModels.cs`
- `Docx/WordSlice.cs`
- `Pdf/PdfModels.cs`
- `Pdf/PdfSlice.cs`
- `Excel/ExcelSlice.cs`
- `Pptx/PptxSlice.cs`
- `Cli.Tests/CliContractTests.cs`
- `Cli.Tests/WordCommandTests.cs`
- `Cli.Tests/PdfCommandTests.cs`

## Verification

- `dotnet build .\Pandoc\PandocCore.csproj -c Release`
- `dotnet build .\Docx\DocxCore.csproj -c Release`
- `dotnet build .\Pdf\PdfCore.csproj -c Release`
- `dotnet build .\Excel\ExcelCore.csproj -c Release`
- `dotnet build .\Pptx\PptxCore.csproj -c Release`
- `dotnet build .\Cli\NongCli.csproj -c Release`
- `dotnet build .\Cli.Tests\Cli.Tests.csproj -c Release`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests.SliceStructure_BlockIndexUsesUnifiedProvenanceContract|FullyQualifiedName~CliContractTests.SliceInspect|FullyQualifiedName~CliContractTests.ExcelDissect_IncludesFormulaMergeAndTableEvidence|FullyQualifiedName~CliContractTests.PptxDissect"`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect"`
- `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests"`

## Remaining risks

- Word layout evidence is still limited to extracted image EMU sizing and block order; paragraph/table visual layout evidence still lives in `format.json` and content block format fields.
- PPTX text-to-shape matching is text-preview based, so duplicate text on the same slide may need stronger shape IDs later.
- Excel chart/pivot evidence is not yet modeled at the same depth as formulas, merged ranges, and tables.
