# ClaudeCode Task Template

Use this template when asking ClaudeCode to implement one scoped development task.

## Task

Implement:

```text
<one concrete task>
```

## Context

Project:

```text
C:\Users\Administrator\Documents\Github\Angri450.Nong
```

Strategic goal:

```text
Build nong as a low-token CLI tool layer for AI agents, focused on agricultural research and Office document workflows.
```

Relevant logs:

```text
log/guidance/README.md
<add current planning log path>
```

## Scope

Allowed:

```text
<files or modules ClaudeCode may edit>
```

Not allowed:

```text
Do not merge more external repositories.
Do not refactor unrelated third-party source.
Do not change package versions unless required.
Do not publish NuGet packages.
Do not change git branches.
```

## Requirements

1. Keep the task small and complete.
2. Prefer existing project patterns.
3. CLI output must support `--json` when applicable.
4. JSON output must be compact and stable.
5. Return file paths and issue IDs instead of dumping large document content.
6. Add or update focused tests when behavior changes.
7. Write a result log under `log/`.

## Acceptance Criteria

The task is done only if:

```text
dotnet build <affected project> -c Release
```

passes, and any relevant tests are run or explicitly marked skipped with reason.

Expected command behavior:

```text
<example command>
<expected short output or JSON shape>
```

## Result Log Required

Create:

```text
log/<YYYY-MM-DD>-<sequence>-claudecode-result.md
```

Include:

```text
Goal:
Files changed:
Commands run:
Build/test result:
Problems found:
Open questions:
Next recommended step:
```

