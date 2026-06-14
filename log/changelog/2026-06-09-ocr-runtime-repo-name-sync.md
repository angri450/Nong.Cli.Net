# OCR Runtime Repository Name Sync

## What Changed

- Updated documentation references for the OCR runtime repository from `Angri450.Nong.OcrRuntime` to `Nong.OcrRuntime`.
- Kept NuGet package IDs such as `Angri450.Nong.OcrRuntime.WinX64` unchanged.

## Why

The actual remote repository is `angri450/Nong.OcrRuntime`, while the runtime package IDs still use the `Angri450.Nong.OcrRuntime.*` prefix.

## Files Touched

- `CLAUDE.md`
- `docs/CAPABILITY.md`
- `README.md`
- `README.zh-CN.md`
- `Cli/README.md`
- `Cli/AGENT.md`
- `MultiModal/README.md`
- `docs/release-checklist.md`
- `log/changelog/2026-06-08-ocr-runtime-version-decoupling.md`
- Local recovery note pages under `../codex-history-recovery/worktree-cleanup-html/`

## Tests Run

- Documentation-only change; verified with repository text search.

## Remaining Risk

- None known.
