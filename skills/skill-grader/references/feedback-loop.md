# Feedback Loop

How to capture real-world failures and feed them back into skill documentation.

## Capture

When a skill fails in practice, record it in `log/debug/` with:
- Date and context
- What the user wanted
- What went wrong (exact error, wrong routing, missing capability)
- How it was resolved (if at all)

## Analyze

From the debug log, extract the failure pattern:
- **Trigger failure**: The wrong skill was activated. → Fix the description/triggers in SKILL.md.
- **Boundary failure**: The skill claimed a capability it doesn't have. → Add a boundary explanation in references/.
- **Routing failure**: The skill activated but sent the user to the wrong command. → Fix the route table.
- **Missing capability**: The CLI has the command but the skill doesn't teach it. → Add to route table or references.

## Feed Back

1. If the failure shows a trigger gap, update `SKILL.md` description.
2. If the failure shows a boundary gap, add or update a reference file.
3. If the failure is reproducible, add a failure example in `examples/`.
4. If the failure reveals a CLI gap, file it in the CLI feature gaps roadmap.

## Example

A user wrote about COM automation failures in Word:

1. **Capture**: Recorded in `log/debug/2026-06-05-word-com-automation-failure-report.md`.
2. **Analyze**: The failure pattern was "AI escaped to PowerShell COM automation instead of staying with `nong word`".
3. **Feed back**: Added a COM automation reference with explicit rules: no COM automation for normal Word operations, COM only as explicit escape hatch. This reference later moved to Nong.Dev.Net (`references/word-com-automation.md`) as a developer tool.
4. **Update SKILL.md**: Added boundary statement: "Do not use desktop Word COM automation for normal tasks."

## When to Consider Changing Triggers

If the SAME failure pattern appears 3+ times, the SKILL.md triggers or description likely need adjustment. Do not keep adding references to work around fundamentally wrong routing.
