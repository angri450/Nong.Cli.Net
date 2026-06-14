# 2026-06-08 NongPandoc Internal Writer Alignment

## What Changed

- Added `NongPandocSlicePackageWriter` in `Angri450.Nong.Pandoc`.
- Centralized slice package writing for:
  - `manifest.json`
  - `document.json`
  - `content.jsonl`
  - `content.nongmark`
  - `structure.json`
  - `format.json`
  - `diagnostics.json`
  - `assets/manifest.json`
  - optional `preview/content.txt`
- Added required-artifact verification in the shared writer, including empty/BOM-only file rejection.
- Switched Word and PDF slice writers to call the shared Pandoc writer while preserving their existing `nongmark/v1` and `nongpdf/v1` JSON schemas.
- Kept PDF detailed diagnostics under `diagnostics/check.json`, `diagnostics/reading-order.json`, and `diagnostics/warnings.json`.

## Why

The next internal step is to make Word/PDF/PPT/Excel converge on one predictable slice package contract instead of letting every package hand-roll stream paths, directory creation, diagnostics, and artifact validation.

This change moves the package-writing responsibility into `Angri450.Nong.Pandoc` without forcing a risky parser or schema migration in the same pass.

## Files Touched

- `Pandoc/NongPandocSlicePackageWriter.cs`
- `Docx/WordSlice.cs`
- `Pdf/PdfSlice.cs`
- `Tests/NongPandocTests.cs`

## Verification

- `dotnet build .\Pandoc\PandocCore.csproj -c Release`
  - Passed, 0 warnings.
- `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`
  - Passed, 6/6 tests.
- `dotnet build .\Docx\DocxCore.csproj -c Release`
  - Passed with existing nullable warnings in old Docx files.
- `dotnet build .\Pdf\PdfCore.csproj -c Release`
  - Passed, 0 warnings.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordDissect|FullyQualifiedName~PdfCommandTests.PdfDissect"`
  - Passed, 9/9 tests.

## Remaining Risks

- Word and PDF still keep their legacy manifest shapes for compatibility. A later migration can introduce `nong-pandoc/package/v1` as the common manifest schema.
- The shared writer validates stream existence/non-empty status, not semantic correctness of each stream.
- PPT/Excel are not wired to the shared package writer yet.
