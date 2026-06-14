# 2026-06-08 NongPandoc contract alignment

## What changed

- Aligned Word and PDF slice stream names with `Angri450.Nong.Pandoc`.
- Added `PandocCore` project references to `DocxCore` and `PdfCore`.
- Replaced local hard-coded slice artifact defaults with `NongPandocArtifactNames`.
- Added root-level `diagnostics.json` for Word and PDF slices while keeping existing PDF diagnostic detail files.
- Added CLI test assertions for the new shared diagnostics stream.
- Updated Word/PDF docs to treat `preview/content.txt` as optional lossy preview rather than the primary content stream.

## Why

All document packages should produce a predictable `a.slice/` package so AI tools can read Word, PDF, PPT, and Excel through the same artifact contract without repeated format conversions.

## Files touched

- `Docx/DocxCore.csproj`
- `Docx/NongMarkModels.cs`
- `Docx/WordSlice.cs`
- `Docx/WORD_CORE_LAYERS.md`
- `Docx/packages.lock.json`
- `Pdf/PdfCore.csproj`
- `Pdf/PdfModels.cs`
- `Pdf/PdfSlice.cs`
- `Pdf/README.md`
- `Pdf/packages.lock.json`
- `Cli.Tests/WordCommandTests.cs`
- `Cli.Tests/PdfCommandTests.cs`
- `Cli.Tests/packages.lock.json`
- `Pandoc/PandocCore.csproj`
- `Pandoc/packages.lock.json`

## Verification

- Passed: `dotnet build .\Docx\DocxCore.csproj -c Release`
- Passed: `dotnet build .\Pdf\PdfCore.csproj -c Release`
- Passed: `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect"`

## Remaining risks

- This pass intentionally aligns the contract without migrating Word/PDF internals to a shared adapter implementation.
- PPT/Excel still need adapters in later stages.
