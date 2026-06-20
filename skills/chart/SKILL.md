---
name: chart
description: Agricultural statistics and chart CLI via nong. Trigger on agricultural statistics and charting (NOT flowcharts or network graphs — see diagram skill). ANOVA, Duncan MRT,, Duncan MRT, 鏂瑰樊鍒嗘瀽, 鏄捐憲鎬? treatment groups, error bars, bar charts, line charts, scatter plots, pie charts, box plots, histograms, heatmaps, or radar charts.
---

# Chart

Use `nong` for statistical analysis and implemented figure generation. Nong.Toolkit.Net routes to the CLI; do not recreate chart rendering logic in scripts or temporary projects.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `chart` command group.

**Modular:** `nong chart` routes to the standalone `Angri450.Nong.Tool.Chart` dotnet tool. First use auto-installs via `dotnet tool install --global`. Command surface unchanged.
## Implemented Commands

```powershell
nong chart analyze <groups.json> [--alpha 0.05] [--ingest] [--json]
nong chart anova <groups.json> [--json]
nong chart duncan <groups.json> [--alpha 0.05] [--json]
nong chart bar <groups.json> -o <out.png> [--title <text>] [--ylabel <text>] [--error sem|none] [--no-significance] [--json]
nong chart line <spec.json> -o <out.png> [--json]
nong chart scatter <spec.json> -o <out.png> [--json]
nong chart pie <spec.json> -o <out.png> [--json]
nong chart boxplot <groups.json> -o <out.png> [--title <text>] [--ylabel <text>] [--json]
nong chart histogram <values.json> -o <out.png> [--title <text>] [--xlabel <text>] [--ylabel <text>] [--bin-count 20] [--json]
nong chart heatmap <spec.json> -o <out.png> [--title <text>] [--colormap <name>] [--json]
nong chart radar <spec.json> -o <out.png> [--title <text>] [--json]
```

## Dispatch

1. For one-shot agricultural treatment analysis, run `nong chart analyze <groups.json> --json`.
2. For ANOVA only, run `nong chart anova <groups.json> --json`.
3. For Duncan multiple comparison only, run `nong chart duncan <groups.json> --json`.
4. For a publication-style bar chart with optional significance letters, run `nong chart bar <groups.json> -o <out.png> --json`.
5. For trends, use `nong chart line <spec.json> -o <out.png> --json`.
6. For x/y relationships, use `nong chart scatter <spec.json> -o <out.png> --json`.
7. For composition shares, use `nong chart pie <spec.json> -o <out.png> --json`.
8. For treatment group distribution comparison, use `nong chart boxplot <groups.json> -o <out.png> --json`.
9. For data distribution visualization, use `nong chart histogram <values.json> -o <out.png> --json`.
10. If data starts in Excel, use `excel to-groups --raw` first for analysis/bar charts.
11. For treatment group distribution, use `nong chart heatmap <spec.json> -o <out.png> --json` (heatmap input: `{"data":[[...]],"rows":N,"cols":M}`).
12. For multi-index comparison, use `nong chart radar <spec.json> -o <out.png> --json` (radar input: `{"categories":["A","B"],"series":[{"name":"X","values":[1,2]}]}`).
13. Do not promise combined panels or figure editing unless a future `nong commands --json` exposes those as implemented CLI commands.

## Input Contracts

Grouped JSON for `analyze`, `anova`, `duncan`, and `bar`:

```json
{
  "Control": [1.2, 1.3, 1.1],
  "Treatment": [2.0, 2.2, 2.1]
}
```

Line chart spec:

```json
{
  "title": "Growth",
  "xLabel": "Days",
  "yLabel": "Height",
  "series": [
    { "name": "A", "x": [0, 7, 14], "y": [1.0, 2.0, 3.0] }
  ]
}
```

Scatter plot spec:

```json
{
  "title": "Correlation",
  "xLabel": "pH",
  "yLabel": "Yield",
  "points": [
    { "x": 6.1, "y": 12.3, "group": "A" }
  ],
  "trendline": true
}
```

Pie chart spec:

```json
{
  "title": "Composition",
  "values": [
    { "label": "A", "value": 30 },
    { "label": "B", "value": 70 }
  ]
}
```

Treat `E006 validation_failed` as a data or spec problem to fix before analysis or rendering.

## Optional Visual QA

After generating a PNG, you may suggest:

```powershell
nong ocr analyze-image fig.png -o fig.analysis --json
```

This is structural image QA for dimensions, blankness, whitespace, and content regions. It is not OCR, text recognition, or semantic understanding of the figure.
