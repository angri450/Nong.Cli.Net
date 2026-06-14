# Nong.Cli.Net Project State

Last updated: 2026-06-13

This file is the current truth source for agents. Read it before `CLAUDE.md`, `AGENTS.md`, `README.md`, or any file under `log/`.

## Current Work

Active plan/handoff:

- `log/plans/2026-06-13-4.1.2-new-publish.md`

Current immediate objective:

- 4.1.2 modular tool packages are published on NuGet. Root `nupkg/` contains exactly the 7 current 4.1.2 tool packages.

Do not infer current work from older `log/plans/*` files unless this file or the active handoff links to them.

## Planning Workflow

Development plans live in `log/plans/`.

Only the plan linked above is active for a builder window. Older plans remain as history and must not be scanned to infer current work.

Two-window workflow:

- Planner window: reads history, writes or updates `log/plans/YYYY-MM-DD-topic.md`, updates `log/plans/index.md`, then updates this file's active plan pointer.
- Builder window: reads this file and the active plan only, then implements and verifies.

Detailed policy:

- `docs/wiki/planning-workflow.md`
- `log/plans/README.md`

## Current Architecture

Nong.Cli.Net is a pure .NET document and research CLI toolkit.

The current 4.1.x line uses a modular tool architecture:

- `nong` is a light router plus pure .NET command groups.
- Heavy command groups are external dotnet tools.
- User command names stay stable, for example `nong chart bar ...`.

Main CLI package:

- `Cli/NongCli.csproj`
- Package: `Angri450.Nong.Cli`
- Tool command: `nong`
- Version line: `4.1.2`

External tool packages:

- `Angri450.Nong.Tool.Chart` -> `nong-chart`
- `Angri450.Nong.Tool.Diagram` -> `nong-diagram`
- `Angri450.Nong.Tool.Pdf` -> `nong-pdf`
- `Angri450.Nong.Tool.Pptx` -> `nong-pptx`
- `Angri450.Nong.Tool.Ocr` -> `nong-ocr`
- `Angri450.Nong.Tool.Imaging` -> `nong-imaging`

The main CLI still owns the light command groups:

- `word`
- `excel`
- `inspect`
- `lit`
- `genre`
- `icons`
- `slice`
- `skill`
- `progress`

## Current Command Surface

Current local command discovery:

```powershell
.\Cli\bin\Release\net8.0\nong.exe commands --json
.\Cli\bin\Release\net8.0\nong.exe commands --format openai-tools
```

Expected command count:

- `126 commands available`
- `126` OpenAI tool schemas

This count includes `progress report`, which is a real light command group owned by the main CLI.

If command count or version metadata disagrees with this file, treat that as a current-state drift to investigate.

## OCR Contract

Active local OCR line:

- PP-OCRv6 first.
- `pp-ocrv6-medium` is the default local install target.
- `pp-ocrv5-mobile` remains a legacy compatibility path.
- No Python, pip, or external OCR executable for local OCR core functionality.

Native OCR runtime contract:

- Runtime packages live in sibling repo `Nong.OcrRuntime`.
- NuGet package prefix remains `Angri450.Nong.OcrRuntime.*`.
- `Cli/Common/OcrRuntimeVersion.cs` is intentionally independent from CLI version.
- Do not bump `OcrRuntimeVersion.Current` unless the sibling runtime repo has published a validated native runtime.

## Current Risks

These are active risks, not historical notes:

- Root `nupkg/` was refreshed on 2026-06-13 and currently contains only the 7 current `Angri450.Nong.Cli` / `Angri450.Nong.Tool.*` 4.1.2 packages. Re-run pack audit before any future publish.
- Older handoffs, commit subjects, or changelog entries can mention `125` commands as historical evidence; current local discovery is `126`.
- NuGet already contains 4.1.0, mixed 4.1.1 for CLI/PDF, and the current 4.1.2 release for all 7 current tool packages.

## Current Release Status

4.1.2 release checks passed on 2026-06-13:

- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-restore` -> `155 passed, 0 failed, 0 skipped`
- root `nupkg/` pack audit -> `status: ok`, `packageCount: 7`
- fresh local `dotnet tool install --tool-path <temp>` from root `nupkg/` installed all 7 packages at `4.1.2`
- installed `nong --version` -> `nong v4.1.2`
- installed `nong commands --json` -> `126 commands available`, `meta.version = 4.1.2`
- installed `nong commands --format openai-tools` -> 126 tool schemas
- installed direct/routed chart and PDF missing-file JSON smokes returned structured `E001`
- NuGet push with `--skip-duplicate` -> all 7 4.1.2 packages returned `Created`
- NuGet flat-container index -> all 7 package IDs show 4.1.2 and latest = 4.1.2

Do not publish or push without an explicit user request.

## Information Sources

Use this order:

1. `PROJECT_STATE.md` for current truth.
2. `CLAUDE.md` and `AGENTS.md` for agent behavior.
3. The active handoff or active plan linked above.
4. `docs/wiki/` for stable project knowledge, including `docs/wiki/planning-workflow.md`.
5. `log/` only as historical evidence.

Never bulk-read `log/` to decide current state. Read only files linked from this file, the active plan, or a specific task.

## Verification Baseline

Known-good local verification from the latest audit:

```powershell
dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-restore
```

Expected:

```text
155 passed, 0 failed, 0 skipped
```

Current-project pack audit must use a clean output directory, not root `nupkg/`.
