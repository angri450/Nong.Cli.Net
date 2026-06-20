# Bioicons Scope and Limitations

## What Bioicons Provides

Bioicons is a collection of open-source scientific icons covering:
- Biology (DNA, cells, organisms, lab equipment)
- Chemistry (molecules, flasks, reactions)
- General lab equipment (microscopes, pipettes, centrifuges)
- Medical and healthcare symbols

The icons are designed for use in scientific publications, presentations, and diagrams.

## What Bioicons Does NOT Provide

- General-purpose UI icons (arrows, buttons, navigation)
- AI-generated or custom-designed icons
- Company logos or brand assets
- Non-scientific icon sets (FontAwesome, Material Design, etc.)
- SVG editing or icon composition tools

## CLI Commands

```powershell
# Browse all available icons
nong icons list --json

# Search by keyword
nong icons search "microscope" --json
nong icons search "dna" --json
```

## When to Route Elsewhere

- **Embedding icons in diagrams**: Use the `diagram` skill. Icons can be referenced as assets in diagram specs but rendering is done by `nong diagram`, not `nong icons`.
- **Embedding icons in charts**: Use the `chart` skill.
- **Custom icon design**: This is outside the Toolkit scope. Suggest external vector graphics tools.
- **General-purpose icons**: Suggest FontAwesome, Material Design, or similar icon libraries — not Bioicons.
