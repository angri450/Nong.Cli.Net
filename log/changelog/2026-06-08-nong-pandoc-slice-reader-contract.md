# 2026-06-08 NongPandoc Slice Reader Contract

## Impacted packages

- `Angri450.Nong.Pandoc`
- `Angri450.Nong.Cli`
- Excel/PPT slice output

## What changed

- Added `NongPandocSlicePackageReader` for `nong-pandoc/package/v1` slice directories.
- Made `diagnostics.json` part of the required package contract, matching writer behavior and AI read order.
- Added `nong slice inspect <slice-dir> --json`.
- Added cross-format contract tests for Word, Excel, PPTX, and PDF-like packages.
- Deepened Excel slice evidence:
  - used range
  - formulas
  - merged ranges
  - table/range regions
  - related entries in `assets/manifest.json`
- Deepened PPTX slice evidence:
  - shape layout boxes
  - picture/chart/table relationship evidence
  - speaker notes preview evidence
  - related entries in `assets/manifest.json`
- Updated CLI agent contract docs to use `slice inspect` and the unified AI read order.

## Why

The slice package is now a stable contract before Word/PDF/Excel/PPT details continue expanding. AI callers can inspect the package once, read `content.nongmark -> structure.json -> format.json -> diagnostics.json`, and avoid treating lossy previews as canonical content.

## Files touched

- `Pandoc/NongPandocSlicePackage.cs`
- `Pandoc/NongPandocSlicePackageReader.cs`
- `Pandoc/NongPandocSlicePackageWriter.cs`
- `Tests/NongPandocTests.cs`
- `Cli/Commands/SliceCommands.cs`
- `Cli/Program.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Cli/NongCli.csproj`
- `Cli.Tests/Cli.Tests.csproj`
- `Cli.Tests/CliContractTests.cs`
- `Excel/ExcelSlice.cs`
- `Pptx/PptxSlice.cs`

## Verification

- `dotnet build .\Pandoc\PandocCore.csproj -c Release`
- `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`
- `dotnet build .\Excel\ExcelCore.csproj -c Release`
- `dotnet build .\Pptx\PptxCore.csproj -c Release`
- `dotnet build .\Cli\NongCli.csproj -c Release`
- `dotnet build .\Cli.Tests\Cli.Tests.csproj -c Release`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests"`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect"`

## Remaining risks

- Excel chart extraction is still basic and represented only as package-level region evidence when available through existing workbook APIs.
- PPTX chart/table payloads are surfaced as relationship/layout evidence, not reconstructed into editable chart/table AST yet.
- `preview/content.txt` remains intentionally lossy and must not be used as the canonical document source.
