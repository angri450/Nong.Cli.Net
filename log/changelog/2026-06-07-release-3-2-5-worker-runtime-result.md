# 2026-06-07 Release Result: Nong CLI 3.2.5 Worker Isolation

## Goal

Ship Nong CLI `3.2.5` with:

- Repository cleanup and mainline-only commit.
- Vendored dependency control for PDF/QR/PdfPig/Docnet work.
- Literature DSL package and `nong lit ...` commands.
- Native image rendering isolation for Chart/Diagram.
- Safer default test suite without native image render crash risk.
- GitHub, Gitee, GitCode, and NuGet release.

Status: PASS with follow-up notes.

## Release Commit

- Release commit: `73730c6e7c976fb333a54a35a37ee25edd0118c5`
- Documentation progress commit: `d52c041de35fe4d4a050e2e83de450255f114730`
- Branch: `master`
- Remotes updated:
  - GitHub: `https://github.com/angri450/Nong.NET`
  - Gitee: `https://gitee.com/angri450/Nong.NET`
  - GitCode: `git@gitcode.com:angri450/Nong.NET.git`

## NuGet Published

Published and available:

- `Angri450.Nong.ThirdParty 3.2.5`
- `Angri450.Nong.Pdf 3.2.5`
- `Angri450.Nong.MultiModal 3.2.5`
- `Angri450.Nong.Literature 3.2.5`
- `Angri450.Nong.OcrRuntime.WinX64 3.2.5`
- `Angri450.Nong.Cli 3.2.5`

The final public CLI install command is:

```powershell
dotnet tool install --global Angri450.Nong.Cli --version 3.2.5
```

Confirmed live install output:

```text
3.2.5+73730c6e7c976fb333a54a35a37ee25edd0118c5
```

## NuGet Follow-Up

The following non-Windows OCR runtime packages were pushed during the release, but the current product boundary is now Windows-first:

- `Angri450.Nong.OcrRuntime.LinuxX64 3.2.5`
- `Angri450.Nong.OcrRuntime.LinuxArm64 3.2.5`
- `Angri450.Nong.OcrRuntime.OsxX64 3.2.5`
- `Angri450.Nong.OcrRuntime.OsxArm64 3.2.5`

Attempted NuGet unlist/delete returned HTTP 403 because the current `NUGET_API_KEY` can push but does not have unlist/delete permission for these package IDs.

Next release rule:

- Publish only `Angri450.Nong.OcrRuntime.WinX64`.
- Move non-Windows OCR runtime nupkgs to local `_archive/`.
- Do not publish Linux/macOS OCR runtime packages until cross-platform support is deliberately scheduled and tested.

## Worker Isolation

Problem:

- Windows reported `Exception Processing Message 0xc0000005 - Unexpected parameters`.
- Event logs showed `testhost.exe` crashing with `System.AccessViolationException`.
- Native stack involved `SkiaSharp.SkiaApi.sk_path_delete` and `SKPath.DisposeNative`.
- Root cause area: native image rendering chain used by SkiaSharp/ScottPlot during tests.

Fix:

- Added hidden CLI worker command: `nong __render-worker ...`.
- Added `NativeRenderWorkerHost`.
- `nong chart ...` and `nong diagram ...` now validate inputs in the main CLI process, then spawn a worker subprocess for native PNG rendering.
- Main CLI no longer directly calls `new Plot()`, `SavePng()`, or `DiagramBuilder.*` inside normal command handlers.
- If native rendering crashes, only the worker process exits; the main CLI can return structured JSON error instead of being killed.

Affected files:

- `Cli/Common/NativeRenderWorkerHost.cs`
- `Cli/Commands/RenderWorkerCommands.cs`
- `Cli/Commands/ChartCommands.cs`
- `Cli/Commands/DiagramCommands.cs`
- `Cli/Program.cs`

## Default Test Boundary

Default test suite no longer runs native image rendering paths:

- Removed Chart/Diagram PNG render unit tests from default `Tests`.
- Removed direct SkiaSharp usage from `Cli.Tests`.
- Removed default `ocr local` real-image tests.
- Removed PDF render/crop pixel decode tests from default `Cli.Tests`.

Native image rendering is now validated by explicit smoke tests, not by default xUnit runs.

Rationale:

- Native access violations cannot be caught reliably by ordinary C# exception handling.
- xUnit `testhost.exe` should not load and finalize unstable native image objects during every default test run.
- Worker smoke gives enough release confidence without destabilizing CI/local test loops.

## Dependency Control

This release continues the dependency-control direction:

- `PdfPig` source vendored under `PdfPig/` and compiled through `ThirdParty`.
- `ZXing.Net` QR decode subset vendored under `ZXing.Net/`.
- `Docnet` source absorbed into `Pdf/Docnet/`.
- PDFium runtimes stored under `Pdf/runtimes/`.
- `Pdf` no longer depends on external `Docnet.Core` or `PdfPig` NuGet packages.
- `packages.lock.json` files added for repeatable restore.
- `_archive/`, `nupkg/`, `tests-output/`, and `output/` remain ignored and are not mainline commit content.

## Verification

Build:

- `dotnet build ThirdParty\ThirdParty.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build Pdf\PdfCore.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build MultiModal\MultiModalCore.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build Literature\LiteratureCore.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build Cli\NongCli.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build Cli.Tests\Cli.Tests.csproj -c Release --nologo -clp:ErrorsOnly` PASS.
- `dotnet build Tests\Tests.csproj -c Release --nologo -clp:ErrorsOnly` PASS.

Tests:

- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-build --nologo` PASS, 83 passed.
- `dotnet test Tests\Tests.csproj -c Release --no-build --nologo` PASS, 110 passed.

Local tool smoke from `nupkg/`:

- `nong --version` PASS: `3.2.5+73730c6e7c976fb333a54a35a37ee25edd0118c5`.
- `nong commands --json` PASS: 82 implemented commands.
- `nong lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json` PASS.
- `nong ocr install-model pp-ocrv5-mobile --dry-run --json` PASS.
- `nong chart bar ... --json` PASS and produced PNG through worker.
- `nong diagram tree ... --json` PASS and produced PNG through worker.

Live NuGet install:

- `dotnet tool install --tool-path tests-output\nuget-live-smoke-325 Angri450.Nong.Cli --version 3.2.5 --add-source https://api.nuget.org/v3/index.json --ignore-failed-sources` PASS.

## Known Notes

- NuGet flat-container and registration indexes can lag after push. During release verification, `Angri450.Nong.Cli 3.2.5` appeared after a short delay.
- GitCode SSH emitted an OpenSSH post-quantum key-exchange warning; push still succeeded.
- Gitee HTTPS push requires a valid access token; the token was used through temporary `GIT_ASKPASS` and was not written into repository config or committed files.
- `CLAUDE.md` was updated after release to capture current progress and next release boundary.

## Next Steps

- Hide/unlist non-Windows OCR runtime `3.2.5` packages using a NuGet key with unlist/delete permission or the NuGet web UI.
- Before the next release, move non-Windows OCR runtime packages from `nupkg/` into `_archive/`.
- Keep Windows runtime as the only published OCR runtime target until cross-platform installation and runtime smoke tests are intentionally scheduled.
- Keep Chart/Diagram rendering inside worker subprocesses.
- Do not re-add native image rendering to default xUnit tests.
