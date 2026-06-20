# Search Bioicons Successfully

## What the user wants

Find a microscope icon for a scientific diagram.

## What was done

```powershell
nong icons search "microscope" --json
```

## Result

```json
{
  "status": "ok",
  "command": "icons search",
  "data": {
    "query": "microscope",
    "results": [
      {"name": "microscope-1", "category": "lab-equipment"},
      {"name": "microscope-2", "category": "lab-equipment"}
    ]
  }
}
```

The user selected `microscope-1` and used it in their diagram via the `diagram` skill.

## Key takeaways

- Use `nong icons search <keyword>` for targeted discovery.
- Use `nong icons list --json` to browse all categories when you're not sure what keyword to use.
- Bioicons is for scientific icons only. Do not search for UI icons (arrows, buttons) here.
- Once you find the icon, route to `diagram` or `chart` skill to embed it.
