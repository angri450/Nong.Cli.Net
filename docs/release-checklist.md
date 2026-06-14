# Release Checklist - Nong.NET v4.1.2

Date: 2026-06-13

Use this checklist before publishing 4.1.2 packages. Do not publish to NuGet, GitHub, Gitee, or GitCode unless the user explicitly asks for that action.

## Build And Test

- [ ] `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-restore`
- [ ] `.\Cli\bin\Release\net8.0\nong.exe --version` prints `nong v4.1.2`
- [ ] `.\Cli\bin\Release\net8.0\nong.exe commands --json` reports `126 commands available`
- [ ] `commands --json` returns `meta.version = "4.1.2"`
- [ ] `.\Cli\bin\Release\net8.0\nong.exe commands --format openai-tools` emits 126 tool schemas

Native image rendering tests remain opt-in. Do not add Chart/Diagram/OCR/PDF real PNG rendering paths back to the default `dotnet test` run.

## Publishable Tool Packages

Pack these 7 dotnet tool packages from current project files:

- `Cli\NongCli.csproj` -> `Angri450.Nong.Cli`
- `Chart\tools\nong-chart.csproj` -> `Angri450.Nong.Tool.Chart`
- `Diagram\tools\nong-diagram.csproj` -> `Angri450.Nong.Tool.Diagram`
- `Pdf\tools\nong-pdf.csproj` -> `Angri450.Nong.Tool.Pdf`
- `Pptx\tools\nong-pptx.csproj` -> `Angri450.Nong.Tool.Pptx`
- `MultiModal\tools\nong-ocr.csproj` -> `Angri450.Nong.Tool.Ocr`
- `Imaging\tools\nong-imaging.csproj` -> `Angri450.Nong.Tool.Imaging`

Use a clean output directory:

```powershell
$out = Join-Path $env:TEMP ("nong-pack-audit-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $out | Out-Null
dotnet pack Cli\NongCli.csproj -c Release --no-restore -o $out
dotnet pack Chart\tools\nong-chart.csproj -c Release --no-restore -o $out
dotnet pack Diagram\tools\nong-diagram.csproj -c Release --no-restore -o $out
dotnet pack Pdf\tools\nong-pdf.csproj -c Release --no-restore -o $out
dotnet pack Pptx\tools\nong-pptx.csproj -c Release --no-restore -o $out
dotnet pack MultiModal\tools\nong-ocr.csproj -c Release --no-restore -o $out
dotnet pack Imaging\tools\nong-imaging.csproj -c Release --no-restore -o $out
powershell -NoProfile -ExecutionPolicy Bypass -File tools\pack-audit.ps1 -Path $out -Json
```

Expected pack audit:

- `status = ok`
- `packageCount = 7`
- Package IDs are `Angri450.Nong.Cli` or `Angri450.Nong.Tool.*`
- No old tool package IDs such as `Angri450.Nong.Chart` are emitted by dotnet tool projects

## Local Install Candidate Gate

Before publishing, install the freshly packed packages into a clean temp `--tool-path` from the same clean pack directory. Use a temporary NuGet config with only the local pack source, so the smoke cannot accidentally consume public packages.

```powershell
$toolPath = Join-Path $env:TEMP ("nong-toolpath-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $toolPath | Out-Null
$configPath = Join-Path $env:TEMP ("nong-local-source-" + [guid]::NewGuid().ToString("N") + ".config")
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$out" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $configPath -Encoding UTF8
dotnet tool install Angri450.Nong.Cli --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Chart --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Diagram --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Pdf --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Pptx --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Ocr --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
dotnet tool install Angri450.Nong.Tool.Imaging --version 4.1.2 --tool-path $toolPath --configfile $configPath --ignore-failed-sources
```

Installed smoke expectations:

- [ ] `dotnet tool list --tool-path $toolPath` shows all 7 packages at `4.1.2`.
- [ ] `$toolPath\nong.exe --version` prints `nong v4.1.2`.
- [ ] `$toolPath\nong.exe commands --json` reports `126 commands available` and `meta.version = "4.1.2"`.
- [ ] `$toolPath\nong.exe commands --format openai-tools` emits 126 tool schemas.
- [ ] Direct external tools start with `--help`: `nong-chart`, `nong-diagram`, `nong-pdf`, `nong-pptx`, `nong-ocr`, `nong-imaging`.
- [ ] At least one direct and one routed external JSON error path returns structured `E001`, for example `nong-chart anova nonexistent.json --json` and `nong chart anova nonexistent.json --json`.

## Version Contract

- [ ] Project package versions are `4.1.2`.
- [ ] `Cli/Common/CliVersion.cs` reports `4.1.2`.
- [ ] `Cli/Common/OcrRuntimeVersion.cs` is not bumped unless sibling repo `Nong.OcrRuntime` published a new validated runtime.
- [ ] `README.md`, `README.zh-CN.md`, `Cli/AGENT.md`, and `docs/CAPABILITY.md` document the 4.1.x modular architecture.
- [ ] PP-OCRv6 is the default local OCR model line; `pp-ocrv5-mobile` is documented only as legacy compatibility.

## OCR Runtime Boundary

- [ ] Routine CLI/Word/PDF/Excel/PPT releases do not pack or push OCR runtime packages.
- [ ] OCR runtime packages remain in sibling repo `Nong.OcrRuntime`.
- [ ] Native runtime package prefix remains `Angri450.Nong.OcrRuntime.*`.
- [ ] Verify `nong ocr install-model pp-ocrv6-medium --json` before claiming local OCR is ready on a target machine.

## Git And NuGet

- [ ] Use branch `main`.
- [ ] Confirm local commits are intended before pushing.
- [ ] Do not publish from root `nupkg/` unless stale package outputs have been cleared and current 7 packages were packed there.
- [ ] Do not push NuGet packages unless explicitly requested.
- [ ] Do not push GitHub/Gitee/GitCode unless explicitly requested.

## Known Limits

- Chart, Diagram, and Imaging 4.1.2 packages currently use the Windows native asset strategy.
- Linux/macOS native rendering needs source-build validation or a later native runtime packaging pass.
- PDF scan-mode searchable text quality depends on local PP-OCRv6 model/runtime availability.
