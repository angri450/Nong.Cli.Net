# OCR Runtime Version Decoupling

## What Changed

- Added `Cli/Common/OcrRuntimeVersion.cs` as the single pinned version for first-party `Angri450.Nong.OcrRuntime.*` native bundles.
- Changed `nong ocr install-model` runtime plans to use `OcrRuntimeVersion.Current` instead of `CliVersion.Current`.
- Split OCR runtime package maintenance into sibling repository `Nong.OcrRuntime`; Nong.NET now consumes runtime packages instead of owning their pack project.
- Added regression tests that verify:
  - dry-run JSON reports the pinned OCR runtime bundle version;
  - first-party runtime package plans no longer use `CliVersion.Current`;
  - `OcrRuntimeVersion.Current` is a valid independent pinned runtime version;
  - local runtime nupkg discovery uses the independent runtime version.
- Updated README, CLI agent contract, capability table, release checklist, and OCR runtime docs to state that OCR runtime packages are not republished for routine CLI/Word/PDF patch releases.

## Why

The OCR runtime package is a large native Paddle/OpenCV bundle. It should only move when the native bundle contents or install contract changes. CLI, Word, PDF, Excel, and PPT patch releases must not force a matching OCR runtime package push.

## Files Touched

- `Cli/Common/OcrRuntimeVersion.cs`
- `Cli/Commands/OcrCommands.cs`
- `Cli.Tests/OcrCommandTests.cs`
- `Cli.Tests/OcrRuntimePackageTests.cs`
- `README.md`
- `README.zh-CN.md`
- `Cli/README.md`
- `Cli/AGENT.md`
- `docs/CAPABILITY.md`
- `CLAUDE.md`
- `docs/release-checklist.md`
- sibling repo `../Nong.OcrRuntime/`

## Verification

- `dotnet build .\Cli\NongCli.csproj -c Release`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~OcrCommandTests|FullyQualifiedName~OcrRuntimePackageTests"`
- `.\Cli\bin\Release\net8.0\nong.exe ocr install-model pp-ocrv5-mobile --dry-run --json`
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release`

## Remaining Risk

When native OCR runtime contents actually change, maintainers must explicitly bump `VERSION` and `OcrRuntime.csproj` in sibling repo `Nong.OcrRuntime`, publish the validated runtime packages, then update `Cli/Common/OcrRuntimeVersion.cs` in Nong.NET and validate install from the target NuGet source.
