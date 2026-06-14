# Third-party Source Snapshot Policy

## What Changed

- Expanded `docs/DEPENDENCY_CONTROL.md` with a third-party source snapshot policy.
- Added an inventory of source snapshot directories compiled by `ThirdParty/ThirdParty.csproj`.
- Marked fork URLs and locked commits as the checklist for the fork pass.
- Documented that third-party source is committed as source snapshots only, never as nested Git repositories.
- Clarified that local fork clones and temporary upstream checkouts belong under ignored `_archive/`.
- Added ignore guardrails for nested `.git/` directories and `.gitmodules`.
- Updated `README.md`, `README.zh-CN.md`, `docs/README.md`, and `CLAUDE.md` with the policy.

## Why

The repository directly compiles vendored source through `ThirdParty/ThirdParty.csproj`. These directories need a clear ownership model: fork upstream, pin a commit, copy the necessary source into this repository, delete upstream `.git/`, and keep licenses/notices.

## Files Touched

- `docs/DEPENDENCY_CONTROL.md`
- `README.md`
- `README.zh-CN.md`
- `docs/README.md`
- `CLAUDE.md`
- `.gitignore`
- `.claude/references/third-party-notes.md`

## Tests Run

- `git diff --check`
- `dotnet build .\Cli\NongCli.csproj -c Release --nologo`

## Remaining Risk

- Most source snapshots still need exact fork commits filled in after the fork pass.
