# Log Directory Policy

`log/` is a historical archive, not the current truth source.

Agents must not bulk-read this directory to decide what the project is doing now. Read `../PROJECT_STATE.md` first, then read only the specific plan/changelog/guidance/debug files linked from the current state file or active task.

Directory roles:

- `plans/`: proposed or active construction plans. Old plans may be completed or superseded.
- `changelog/`: completed work records.
- `guidance/`: design guidance, audits, roadmaps, and reference notes.
- `debug/`: user feedback and debugging records.
- `reports/`: generated reports and status pages.

If a historical file conflicts with `PROJECT_STATE.md`, `PROJECT_STATE.md` wins unless the user explicitly says otherwise.

