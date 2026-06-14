# Architecture

Last updated: 2026-06-13

## Overview

Nong.Cli.Net is a pure .NET CLI toolkit for scientific document generation, document repair, Office/PDF slicing, charting, diagrams, OCR, and skill lifecycle work.

The current architecture separates agent-facing planning from deterministic execution:

- AI agents choose workflows, prepare inputs, and interpret JSON evidence.
- Nong commands perform deterministic document, image, OCR, and package operations in .NET.

## 4.1.0 Modular Tool Line

The 4.1.0 line splits the CLI into a light router plus external tools.

Main package:

```text
Angri450.Nong.Cli -> nong
```

External tool packages:

```text
Angri450.Nong.Tool.Chart   -> nong-chart
Angri450.Nong.Tool.Diagram -> nong-diagram
Angri450.Nong.Tool.Pdf     -> nong-pdf
Angri450.Nong.Tool.Pptx    -> nong-pptx
Angri450.Nong.Tool.Ocr     -> nong-ocr
Angri450.Nong.Tool.Imaging -> nong-imaging
```

The user command surface remains:

```text
nong chart ...
nong diagram ...
nong pdf ...
nong pptx ...
nong ocr ...
nong word images --analyze ...
nong word crop ...
```

## Main CLI Responsibilities

The main `nong` tool owns:

- command discovery and OpenAI tool schema export;
- routing to external tools;
- pure .NET/light command groups;
- structured JSON output contracts;
- skill lifecycle commands.

Light command groups currently include:

```text
word / excel / inspect / lit / genre / icons / slice / skill / progress
```

Heavy/native command groups are routed to tools:

```text
chart / diagram / pdf / pptx / ocr / imaging
```

## Package Boundaries

Library package IDs and tool package IDs are intentionally separate.

Tool packages use:

```text
Angri450.Nong.Tool.*
```

Core/library packages use:

```text
Angri450.Nong.Docx
Angri450.Nong.Excel
Angri450.Nong.Pandoc
Angri450.Nong.ThirdParty
...
```

Do not publish a dotnet tool using a library package ID such as `Angri450.Nong.Chart`.

## ThirdParty Boundary

Third-party source snapshots are compiled through `ThirdParty/ThirdParty.csproj`.

Current decision:

- keep `Angri450.Nong.ThirdParty` as one merged foundation;
- do not split it during the 4.1.0 release sync;
- revisit splitting only if package size exceeds the pack audit gates again.

Reference:

- `log/guidance/2026-06-13-thirdparty-boundary-audit.md`

## OCR Runtime Boundary

Local OCR model logic is in Nong.Cli.Net / OCR tool code.

Native OCR runtime packages live in sibling repo `Nong.OcrRuntime`.

Important constants:

```text
Cli/Common/CliVersion.cs        -> CLI/package metadata version
Cli/Common/OcrRuntimeVersion.cs -> first-party native OCR runtime package version
```

These are deliberately separate. Do not couple them unless the native runtime actually changes.

## Release Verification Shape

Use tests for command contracts:

```powershell
dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-restore
```

Use `tools/pack-audit.ps1` against a clean pack output directory for package size gates.

Do not trust root `nupkg/` unless it has just been regenerated from the current project files.

