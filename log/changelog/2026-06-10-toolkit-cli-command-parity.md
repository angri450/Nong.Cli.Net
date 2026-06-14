# Toolkit / CLI command parity

## What changed

- Added `nong word format-gongwen <input.docx> -o <out.docx> [--config <style.json>] --json`.
- Added `nong inspect write-official <spec.json> -o <out.docx> --json`.
- Registered both commands in the manifest; `nong commands --json` now reports 96 implemented commands.
- Added CLI regression coverage for manifest exposure, missing-file errors, official-document generation, and gongwen formatting.
- Updated the Toolkit/CLI HTML map to show 96/96 command coverage and the official-document parity fix.

## Why

Nong.Cli.Net already had `GongWenFormatter` and `OfficialDocWriter`, while Toolkit docs still had to describe official-document work as a library-only gap. The command surface and skill layer now agree.

## Files touched

- `Cli/Commands/WordCommands.cs`
- `Cli/Commands/InspectCommands.cs`
- `Cli/Common/Manifest.cs`
- `Cli.Tests/CliContractTests.cs`
- `log/reports/toolkit-vs-cli-full-map.html`

## Tests

- `dotnet build SkillManagerCore\SkillManagerCore.csproj -c Release --no-restore -maxcpucount:1`
- `dotnet build Cli\NongCli.csproj -c Release --no-restore -maxcpucount:1`
- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-build`

## Remaining risks

- `word format-gongwen` is an OpenXML formatting pass. It validates artifact creation but does not replace human review of final official-document typography.
- NuGet lock-file normalization remains separate because restore may churn unrelated package graphs.
