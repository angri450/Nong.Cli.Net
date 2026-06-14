# Repository Document Layout Cleanup

## What Changed

- Moved repository-level maintenance documents into `docs/`.
- Moved user feedback notes from the root `用户反馈/` directory into `docs/feedback/2026-06-08-word-repair/`.
- Added `docs/README.md` to explain the documentation layout.
- Removed two obsolete GitHub issue-body drafts from `log/changelog/`.
- Removed the generated HTML duplicate of the OCR runtime decoupling changelog; the Markdown changelog remains the source of truth.
- Kept source project directories, third-party source directories, and build metadata in place.

## Why

The repository root mixed source projects, vendored source trees, release documents, capability tables, and user feedback notes. Keeping maintenance documents under `docs/` makes the root easier to scan without disturbing build-sensitive source layout.

## Files Touched

- `docs/README.md`
- `docs/CAPABILITY.md`
- `docs/DEPENDENCY_CONTROL.md`
- `docs/release-checklist.md`
- `docs/feedback/2026-06-08-word-repair/`
- `README.md`
- `README.zh-CN.md`
- `CLAUDE.md`
- `log/changelog/issue-body.md`
- `log/changelog/issue2-body.md`
- `log/changelog/2026-06-08-ocr-runtime-version-decoupling.html`

## Tests Run

- `git diff --check`
- `dotnet build .\Cli\NongCli.csproj -c Release --nologo`

## Remaining Risk

- Historical changelog and guidance files may still mention old paths as part of their original task history.
