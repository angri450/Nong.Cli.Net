# Search Bioicons — No Results

## What the user wants

Find a "rocket launch" icon.

## What happened (failure)

```powershell
nong icons search "rocket" --json
```

```json
{
  "status": "ok",
  "command": "icons search",
  "data": {
    "query": "rocket",
    "results": []
  }
}
```

## How it was resolved

1. Told the user Bioicons only covers scientific/lab icons, not general-purpose icons.
2. Suggested browsing all categories: `nong icons list --json`.
3. If the user needs a non-scientific icon, suggested external libraries (FontAwesome, Material Design) — outside Toolkit scope.

## Key takeaways

- Bioicons is NOT a general-purpose icon library. Empty results for non-scientific queries are expected.
- When results are empty, suggest `nong icons list` to browse available categories, or route the user to external icon sources.
- Do NOT promise that Bioicons will have every icon a user might want. The boundary is scientific icons only.
