# Agent Development Rules

This file governs the whole repository.

## Working Style

- Move fast. If the goal is clear, inspect the local context and start building.
- Do not turn normal implementation work into an approval workflow.
- Ask only when the next action is destructive, requires credentials, publishes externally, or the codebase gives two genuinely incompatible paths.
- Prefer concrete output over long planning text.

## Required Loop

1. Read guidance first.
   - Start with `PROJECT_STATE.md`. It is the current truth source.
   - Then read `CLAUDE.md` for repository workflow and constraints.
   - Read only the active plan/handoff linked from `PROJECT_STATE.md`, unless the user asks for historical research.
   - Read module docs near the work area, such as `Docx/README.md`, `Pdf/README.md`, `Cli/AGENT.md`, or `docs/wiki/`.
   - Treat `log/` as historical archive. Do not bulk-read `log/` to infer current state.
   - If an old log conflicts with `PROJECT_STATE.md`, trust `PROJECT_STATE.md` unless the user explicitly says otherwise.
   - For two-window work, the planner window writes plans under `log/plans/` and updates `PROJECT_STATE.md`; the builder window reads only `PROJECT_STATE.md` plus the active linked plan.

2. Build the change.
   - Follow existing project patterns.
   - Keep the edit focused, but do not underbuild a feature just to stay small.
   - For bugs, turn the failing file or scenario into a regression asset when practical.
   - For CLI behavior, prefer command-level tests and JSON-contract checks.

3. Verify.
   - Run the narrowest useful build/test command first.
   - For document formatting work, validation alone is not enough; check visual-format evidence in OOXML or generated artifacts.
   - Report any tests that could not be run.

4. Write changelog last.
   - Add a short entry under `log/changelog/`.
   - Use `YYYY-MM-DD-short-topic.md`.
   - Include: what changed, why, files touched, tests run, and remaining risks if any.

## Project Bias

- This repo is aggressive: prefer shipping a working implementation plus regression coverage over leaving a proposal.
- Pure .NET paths are preferred. Do not add JavaScript or Python for core functionality.
- For Word/PDF repair and formatting, prefer OpenXML and Nong commands over COM or helper libraries unless the task explicitly calls for a boundary conversion.
- Existing dirty worktree changes may belong to the user. Work with them; do not revert unrelated files.
- Keep context-entry files short and current: `PROJECT_STATE.md` is for present truth, `docs/wiki/` is for stable knowledge, and `log/` is for history.
- Development plans still live in `log/plans/`; the active one is the path named in `PROJECT_STATE.md`.
