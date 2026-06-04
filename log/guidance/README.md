# Nong Development Guidance

Date: 2026-06-03

## Purpose

This folder is the advisory workspace for `Angri450.Nong`.

It records decisions, task prompts, review criteria, and handoff notes for developing `nong` as an AI-agent tool-calling foundation.

The current strategic position:

> `nong` is not another Office .NET library. It is a low-token CLI tool layer for AI agents, focused on agricultural research, academic papers, experiment data, charts, Word, Excel, PPT, and Chinese official documents.

## Roles

### User

- Owns product direction and final decisions.
- Runs or delegates implementation to ClaudeCode.
- Keeps development logs in `log/`.

### Codex

- Acts as project adviser.
- Reviews plans, architecture, tradeoffs, and ClaudeCode results.
- Produces task prompts, acceptance criteria, and review checklists.
- Does not need to implement unless explicitly asked.

### ClaudeCode

- Reads large context.
- Implements scoped tasks.
- Writes code, runs builds/tests, and records results.

## Development Rule

Do not measure this project by generic Office-library completeness.

Measure it by:

1. How few tokens an AI agent needs to complete a document task.
2. Whether the agent can avoid writing PowerShell/C# for common workflows.
3. Whether command output is stable JSON.
4. Whether problems can be referenced by IDs and fixed incrementally.
5. Whether agricultural research workflows are better than generic tools.

## Current Priority

Build `nong` CLI as the primary product surface.

Suggested first-stage commands:

```text
nong commands --json
nong word preview <file> --json
nong word read <file> --json
nong paper diagnose <file> --json
nong excel read <file> --json
nong chart bar --spec <spec.json> --out <chart.png> --json
nong diagram flowchart --spec <spec.json> --out <diagram.png> --json
```

## Log Convention

Use numbered files for each development turn:

```text
log/
  2026-06-03-001-cli-architecture.md
  2026-06-03-002-claudecode-task.md
  2026-06-03-003-claudecode-result.md
  2026-06-03-004-review.md
```

Each log should include:

```text
Goal:
Scope:
Changed files:
Build/test result:
Problems:
Decision:
Next step:
```

