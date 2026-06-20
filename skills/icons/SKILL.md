---
name: icons
description: Scientific icon discovery via Bioicons and nong CLI. Trigger on bioicons, scientific icons, lab icons, biology icons, chemistry icons, icon list, or icon search. Not for general-purpose icon design or AI icon generation.
---

# Icons

Scientific icon discovery through the Bioicons library. Use `nong icons` for the implemented command surface.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong command. Confirm the `nong` CLI is installed.

## Route Table

| User wants | Command |
|------------|---------|
| Browse available icons | `nong icons list --json` |
| Search by keyword | `nong icons search <query> --json` |

## Boundaries

- Bioicons only — scientific lab icons. See [references/scope-and-limits.md](references/scope-and-limits.md) for supported categories.
- Not for general-purpose icon design, AI-generated icons, or non-scientific icon sets.
- Do not promise icon rendering or SVG export unless the current `nong icons` output provides it.
- When embedding icons into diagrams or charts, route through the `diagram` or `chart` skill instead.
