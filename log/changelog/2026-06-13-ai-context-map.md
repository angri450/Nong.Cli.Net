# AI Context Map

## What Changed

- Added `PROJECT_STATE.md` as the current truth source for agents.
- Added `docs/wiki/` for stable project knowledge:
  - `README.md`
  - `architecture.md`
  - `development-history.md`
  - `planning-workflow.md`
- Added `log/README.md` to define `log/` as a historical archive, not the current truth source.
- Added `log/plans/README.md` to define the two-window planning workflow.
- Updated `CLAUDE.md` and `AGENTS.md` so agents read `PROJECT_STATE.md` first and avoid bulk-reading historical logs.
- Added `log/plans/2026-06-13-ai-context-map.md` as the plan for this navigation change.

## Why

The repository had enough history that agents could confuse completed plans, superseded ideas, and current work. The new structure gives agents a single current-state entry point while keeping the full log history available as evidence.

## Two-Window Workflow

- Planner window: reads history, writes `log/plans/YYYY-MM-DD-topic.md`, updates `log/plans/index.md`, and updates the active plan pointer in `PROJECT_STATE.md`.
- Builder window: reads `PROJECT_STATE.md` and the active linked plan only, then implements and verifies.

## Files Touched

- `PROJECT_STATE.md`
- `AGENTS.md`
- `CLAUDE.md`
- `docs/wiki/README.md`
- `docs/wiki/architecture.md`
- `docs/wiki/development-history.md`
- `docs/wiki/planning-workflow.md`
- `log/README.md`
- `log/plans/README.md`
- `log/plans/2026-06-13-ai-context-map.md`
- `log/plans/index.md`
- `log/changelog/index.md`

## Verification

- Read back the new current-state, wiki, log policy, and plan policy files.
- No code tests were run because this change only affects documentation and agent navigation.

## Remaining Risks

- Some current release docs still contain stale 4.0.0 / PP-OCRv5 wording. That is intentionally left to the active 4.1.0 release handoff plan.

