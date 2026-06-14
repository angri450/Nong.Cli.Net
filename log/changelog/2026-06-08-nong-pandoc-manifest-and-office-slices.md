# 2026-06-08 NongPandoc Manifest And Office Slices

## What Changed

- Upgraded Word and PDF slice `manifest.json` to the shared `nong-pandoc/package/v1` schema.
- Preserved Word `document.json` as `nongmark/v1` and PDF `document.json` as `nongpdf/v1`.
- Added `ExcelSlice` in `Angri450.Nong.Excel`.
- Added `PptxSlice` in `Angri450.Nong.Pptx`.
- Added CLI commands:
  - `nong excel dissect input.xlsx -o input.slice --json`
  - `nong pptx dissect input.pptx -o input.slice --json`
- Wired Excel/PPT slices through `NongPandocSlicePackageWriter`.
- Updated command manifest, module README files, and CLI agent guidance.

## Shared Output Contract

Word, PDF, Excel, and PPTX slice commands now write the same top-level package:

```text
manifest.json              schemaVersion = nong-pandoc/package/v1
document.json              domain-specific model
content.jsonl              block stream
content.nongmark           primary AI-readable stream
structure.json             structure/index evidence
format.json                format/style evidence
diagnostics.json           warnings/quality evidence
assets/manifest.json       asset manifest
preview/content.txt        lossy text preview
```

AI read order remains:

```text
content.nongmark -> structure.json -> format.json -> diagnostics.json
```

## Files Touched

- `Docx/WordSlice.cs`
- `Pdf/PdfSlice.cs`
- `Excel/ExcelCore.csproj`
- `Excel/ExcelSlice.cs`
- `Excel/README.md`
- `Pptx/PptxCore.csproj`
- `Pptx/PptxSlice.cs`
- `Pptx/README.md`
- `Cli/NongCli.csproj`
- `Cli/Commands/ExcelCommands.cs`
- `Cli/Commands/PptxCommands.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Cli.Tests/Cli.Tests.csproj`
- `Cli.Tests/CliContractTests.cs`
- `Cli.Tests/WordCommandTests.cs`
- `Cli.Tests/PdfCommandTests.cs`
- `Docx/README.md`
- `Pdf/README.md`

## Verification

- `dotnet build .\Excel\ExcelCore.csproj -c Release`
  - Passed, 0 warnings.
- `dotnet build .\Pptx\PptxCore.csproj -c Release`
  - Passed, 0 warnings in the final run.
- `dotnet build .\Cli\NongCli.csproj -c Release`
  - Passed, 0 warnings in the final run.
- `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`
  - Passed, 6/6 tests.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect"`
  - Passed, 9/9 tests.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests.Commands_Json|FullyQualifiedName~CliContractTests.ExcelCreate|FullyQualifiedName~CliContractTests.PptxDissect|FullyQualifiedName~CliContractTests.NewCommand_MissingFile"`
  - Passed, 16/16 tests.

## Remaining Risks

- Excel/PPTX slicing is a first contract-aligned implementation: it captures text, sheet/slide structure, basic format evidence, and package streams. It does not yet reconstruct rich layout, embedded charts, drawing geometry, or binary assets.
- Existing downstream consumers that hard-coded old Word/PDF manifest stream property names must read the new `nong-pandoc/package/v1` stream names.
- PPTX tests use a minimal OpenXML fixture, not a visually complex deck.
