# PPTX Write Boundary

Current Nong.Toolkit.Net PPTX support is read-only. The exposed Nong command surface is:

```powershell
nong pptx read deck.pptx --json
nong pptx slides deck.pptx --json
nong pptx dissect deck.pptx -o deck.slice --json
```

Do not promise or route PPTX generation, editing, theme selection, chart embedding, image placement, animation, speaker-note authoring, or layout repair from this skill.

## If The User Asks To Create Or Edit A Deck

Explain that the Nong.Toolkit.Net PPTX skill currently supports reading and slide inspection only. Ask for an existing `.pptx` if they want analysis, extraction, or structure review.

## Disallowed Nong.Toolkit.Net Paths

- Do not scaffold a temporary .NET project for deck generation.
- Do not call PPTX libraries directly as the Nong.Toolkit.Net path.
- Do not modify raw OOXML parts.
- Do not claim generated or edited deck artifacts from this skill.
