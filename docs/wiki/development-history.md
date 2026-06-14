# Development History

Last updated: 2026-06-13

This file summarizes the development line. It is intentionally not a full log index. Use it to understand how the project arrived at the current architecture.

## 2026-06-01 to 2026-06-02: Initial Architecture and CLI Specs

The project began as a pure .NET document workflow CLI. Early work defined command groups for:

- Word
- Excel
- chart
- diagram
- PPTX
- OCR
- icons and genre templates

The design goal was already clear: deterministic .NET tools should do document/file work, while agents handle planning and interpretation.

## 2026-06-03: Core Command Surface Buildout

The first broad command surface landed:

- CLI architecture was created.
- Word read/preview, generation, inspection, Excel bridge, diagram/PPTX, assets, and skill manager work were staged.
- Early stub and completion audits drove missing command implementation.

Historical references:

- `log/changelog/001-cli-architecture-result.md`
- `log/changelog/014-full-stub-completion-blueprint.md`
- `log/changelog/017-stage14-stub-completion-result.md`

## 2026-06-05 to 2026-06-06: Pure .NET OCR and Local PDF

Local OCR moved away from Python fallback plans and toward a .NET PP-OCR path.

Key decisions:

- local OCR must not require Python or pip;
- first-party native runtime packages are installed through Nong commands;
- PDF slicing gained local package contracts.

Historical references:

- `log/changelog/2026-06-05-stage17c-pure-dotnet-ppocrv5.md`
- `log/changelog/2026-06-06-stage18-local-pdf-one-cut-three-stream-result.md`

## 2026-06-08: 4.0.0 Release Line

The 4.0.0 line consolidated the main Nong packages and separated OCR native runtime maintenance into the sibling `Nong.OcrRuntime` repository.

Important outcomes:

- `Cli/Common/OcrRuntimeVersion.cs` became the independent native runtime pin.
- Word/PDF/Excel/PPTX slice contracts aligned around NongPandoc package inspection.
- Visible Word formatting evidence and regression workflows were strengthened.

Historical references:

- `log/changelog/2026-06-08-nong-4.0.0-release.md`
- `log/changelog/2026-06-08-ocr-runtime-version-decoupling.md`

## 2026-06-10 to 2026-06-12: Feature Expansion

After 4.0.0, the command surface expanded:

- P1 aliases for command naming compatibility.
- PDF merge/split/compress.
- Excel style/formula/pivot.
- PPTX create.
- Chart boxplot/histogram/heatmap/radar.
- Word image and OOXML formatting commands.
- OCR batch/video/screen/camera.

Historical references:

- `log/guidance/2026-06-10-phase2-cli-feature-gaps-roadmap.md`
- `log/changelog/2026-06-12-ocr-expansion.md`
- `log/changelog/2026-06-12-p2-full-ooxml-coverage.md`

## 2026-06-12: PP-OCRv6 Adaptation

PP-OCRv6 support was added without changing the native OCR runtime version.

Current OCR line:

- `pp-ocrv6-medium` is the default local OCR install target.
- `pp-ocrv6-small` and `pp-ocrv6-tiny` are supported.
- `pp-ocrv5-mobile` remains legacy.
- `Nong.OcrRuntime` stays pinned separately.

Historical references:

- `log/plans/2026-06-12-ppocrv6-adaptation.md`
- `log/changelog/2026-06-12-ppocrv6-adaptation.md`

## 2026-06-12 to 2026-06-13: Modular Split

The CLI package became too large for comfortable distribution, so heavy native modules were split into external dotnet tools.

Outcomes:

- main `nong` became a 12 MB light router;
- heavy modules became `Angri450.Nong.Tool.*` packages;
- Chart/Diagram/Imaging removed non-Windows Skia/HarfBuzz native assets for the first 4.1.0 release line;
- pack audit gates were added;
- ThirdParty was audited but not split.

Historical references:

- `log/plans/2026-06-12-cli-modular-split.md`
- `log/plans/2026-06-13-modular-release-to-audit-roadmap.md`
- `log/changelog/2026-06-13-modular-release-final-audit.md`

## Current Caveat

Some older README, AGENT, and docs files still contain 4.0.0 or PP-OCRv5-only wording. Treat `PROJECT_STATE.md` as current until those truth sources are fully synchronized.

