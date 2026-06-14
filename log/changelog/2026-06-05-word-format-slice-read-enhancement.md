# Word format slice read enhancement

Date: 2026-06-05

## Problem

`nong word read` is text-only, and GroundPA could mistakenly use that output to answer DOCX formatting questions. `word dissect --output` was the correct path, but the slice model still exposed only part of the formatting surface.

Before this change, `content.jsonl` preserved run-level formatting such as fonts and font sizes, while paragraph layout and table borders were not directly exposed in block JSON.

## Change

Enhanced the NongMark Word slice model:

- `ParagraphBlock.format`
- `HeadingBlock.format`
- `TableBlock.format`
- `TableCellBlock.format`
- `NongFormat.tables[].format`

New extracted fields include:

- paragraph alignment
- first-line, left, and right indentation
- line spacing and line rule
- paragraph spacing before/after
- keep-next flag
- table justification and width
- table top/bottom/left/right/insideH/insideV borders
- cell width
- cell vertical alignment
- cell shading fill
- cell borders

## Validation

Added `WordDissect_Output_IncludesParagraphAndTableFormatting`.

Validation run:

```powershell
dotnet build .\Nong.Cli.Net\Cli\NongCli.csproj -c Release --nologo
dotnet test .\Nong.Cli.Net\Cli.Tests\Cli.Tests.csproj -c Release --nologo
```

Result:

- Build: PASS, 0 errors.
- Tests: PASS, 59/59.

## Remaining Work

Resolved style inheritance and exact Word visual rendering are still not complete. Stage 18 should continue with `word format-audit` and `word apply-format`.
