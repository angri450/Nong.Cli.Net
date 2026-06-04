# Review Checklist

Use this after ClaudeCode finishes a task.

## Product Fit

1. Does this improve `nong` as a low-token AI-agent tool layer?
2. Does it reduce the need for models to write PowerShell or C#?
3. Does it serve agricultural research, academic paper, experiment-data, or Office workflow needs?

## CLI Quality

1. Is the command name stable and predictable?
2. Does it support `--json` where useful?
3. Is JSON compact enough for model consumption?
4. Does it avoid dumping full document text by default?
5. Does it return IDs, summaries, counts, and artifact paths?
6. Are errors machine-readable?

Recommended JSON shape:

```json
{
  "status": "ok",
  "summary": "",
  "issues": [],
  "artifacts": {},
  "metrics": {}
}
```

## Engineering Quality

1. Did the affected project build in Release?
2. Were focused tests added or run?
3. Were third-party sources left untouched unless required?
4. Was unrelated refactoring avoided?
5. Are logs updated?

## Risk Flags

Flag for review if any of these appear:

1. More external repositories were merged.
2. CLI output is prose-only.
3. A command requires generated C# for ordinary use.
4. A command dumps large text by default.
5. The implementation duplicates existing library capability without adding AI-agent value.
6. A package API changed without updating CLI and logs.

## Review Output Format

```text
PASS / PARTIAL / FAIL

Findings:
1. <severity> <file/path>: <specific issue>

Decision:
Accept / revise / rollback / defer

Next step:
<one concrete task>
```

