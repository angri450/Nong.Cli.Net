# AI Context Map Plan

日期: 2026-06-13
状态: done

## Problem

Agents were reading old plans, changelogs, guidance, and debug notes as if all of them were current. That made the active task hard to identify because `log/` contains both completed work and ideas that were never executed.

## Goal

Create a stable reading hierarchy:

1. current truth source;
2. agent behavior rules;
3. active plan or handoff;
4. stable wiki;
5. historical log archive.

## Changes

- Add `PROJECT_STATE.md` as the first current-state entry point.
- Add `docs/wiki/` for stable architecture and development history.
- Add explicit two-window planning workflow: planner writes `log/plans/*` and updates `PROJECT_STATE.md`; builder reads only the active linked plan.
- Add `log/README.md` to mark `log/` as archive, not current truth.
- Update `CLAUDE.md` and `AGENTS.md` so future agents do not bulk-read historical logs.
- Add a changelog entry after verification.

## Non-goals

- Do not move existing historical log files.
- Do not rewrite the full 4.1.0 release docs in this pass.
- Do not publish NuGet packages.
- Do not push remotes.

## Verification

This is a documentation/navigation change. Verify by reading:

- `PROJECT_STATE.md`
- `docs/wiki/architecture.md`
- `docs/wiki/development-history.md`
- `log/README.md`
- `CLAUDE.md`
- `AGENTS.md`

## Result

Done. Current-state, wiki, log archive policy, two-window planning policy, agent instructions, and changelog were added.
