# 2026-06-08 Angri450.Nong.Pandoc package kickoff

## What changed

- Added the initial `Angri450.Nong.Pandoc` package under `Pandoc/`.
- Added a pure .NET `nong-pandoc/v1` semantic document model.
- Added shared stream names and package manifest types for Word/PDF/PPT/Excel slice alignment.
- Added NongMark text reader/writer support for metadata, headings, paragraphs, lists, block quotes, tables, figures, references, and raw blocks.
- Added tests for writer output, fenced table/reference parsing, and runtime policy.
- Added `Pandoc/CONSTRUCTION_PLAN.md` to define the cross-package adapter plan.

## Why

Nong needs a Pandoc-style Markdown-upgrade layer without vendoring GPL Pandoc or depending on `pandoc.exe`. This package starts the Apache-2.0 core boundary that can later bridge NongMark, DOCX, PDF, and optional external Pandoc interop.

## Files touched

- `Pandoc/PandocCore.csproj`
- `Pandoc/NongPandocDocument.cs`
- `Pandoc/NongPandocSlicePackage.cs`
- `Pandoc/NongMarkTextWriter.cs`
- `Pandoc/NongMarkTextReader.cs`
- `Pandoc/README.md`
- `Pandoc/CONSTRUCTION_PLAN.md`
- `Pandoc/packages.lock.json`
- `Docx/README.md`
- `Tests/Tests.csproj`
- `Tests/NongPandocTests.cs`
- `Tests/packages.lock.json`

## Verification

- Passed: `dotnet build .\Pandoc\PandocCore.csproj -c Release`
- Passed: `dotnet test .\Tests\Tests.csproj -c Release --filter NongPandocTests`

## Remaining risks

- The first reader is intentionally conservative and does not yet parse full Pandoc Markdown inline grammar.
- DOCX adapters still need to be connected after the package boundary stabilizes.
