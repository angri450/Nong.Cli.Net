# Chart CLI Reference

Nong.Toolkit.Net uses the implemented `nong chart` CLI commands. Do not call ChartCore directly for normal skill routing.

## Statistics

Full analysis:

```powershell
nong chart analyze groups.json --alpha 0.05 --json
```

ANOVA only:

```powershell
nong chart anova groups.json --json
```

Duncan MRT only:

```powershell
nong chart duncan groups.json --alpha 0.05 --json
```

Input is grouped JSON:

```json
{
  "Control": [1.2, 1.3, 1.1],
  "Treatment": [2.0, 2.2, 2.1]
}
```

Each group should contain numeric observations suitable for the requested analysis. Fix `E006 validation_failed` before trusting results.

## Bar Chart

```powershell
nong chart bar groups.json -o fig.png --title "Yield" --ylabel "kg/ha" --error sem --json
```

Options:

- `--error sem|none`
- `--no-significance`
- `--title <text>`
- `--ylabel <text>`

## Line Chart

```powershell
nong chart line line.json -o line.png --json
```

Spec:

```json
{
  "title": "Growth",
  "xLabel": "Days",
  "yLabel": "Height",
  "series": [
    { "name": "A", "x": [0, 7, 14], "y": [1.0, 2.0, 3.0] },
    { "name": "B", "x": [0, 7, 14], "y": [1.1, 2.4, 3.4] }
  ]
}
```

Validation requires a non-empty `series` array. Each series needs `name`, `x`, and `y`; `x` and `y` must be non-empty arrays of the same length.

## Scatter Plot

```powershell
nong chart scatter scatter.json -o scatter.png --json
```

Spec:

```json
{
  "title": "Correlation",
  "xLabel": "pH",
  "yLabel": "Yield",
  "points": [
    { "x": 6.1, "y": 12.3, "group": "A" },
    { "x": 6.5, "y": 13.1, "group": "A" },
    { "x": 7.0, "y": 15.2, "group": "B" }
  ],
  "trendline": true
}
```

Validation requires a non-empty `points` array with finite numeric `x` and `y`. `group` is optional. `trendline` draws one overall linear trendline when there are at least two points.

## Pie Chart

```powershell
nong chart pie pie.json -o pie.png --json
```

Spec:

```json
{
  "title": "Composition",
  "values": [
    { "label": "A", "value": 30 },
    { "label": "B", "value": 70 }
  ]
}
```

Validation requires at least two values. Each value needs a non-empty `label` and a positive numeric `value`.

## Output and QA

PNG-generating commands return the generated path in `artifacts.png` when `--json` is used. After rendering, optional structural QA can be run with:

```powershell
nong ocr analyze-image fig.png -o fig.analysis --json
```

This checks image structure such as dimensions, whitespace, blankness, and content regions. It is not OCR, text recognition, or semantic figure interpretation.

## Unsupported Chart Requests

Do not promise box plots, histograms, heatmaps, radar charts, combined panels, or arbitrary post-render editing through the current `nong chart` command surface.
