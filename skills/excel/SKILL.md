---
name: excel
description: Excel CLI operations via nong. Trigger on .xlsx, worksheet listing, table reading, grouped extraction, experiment workbook restructuring, workbook creation, cell styling, formula writing, pivot tables, or converting treatment/value columns into grouped JSON for statistics.
---

# Excel

Use `nong` for deterministic Excel reads, workbook creation, experiment workbook restructuring, and downstream data preparation. Nong.Toolkit.Net routes Excel work through the CLI; do not create ad hoc Excel writer projects or bypass `nong`.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and that the `excel` group is available.

## Implemented Commands

Current `nong commands --json` exposes these 11 Excel commands:

```powershell
nong excel sheets <file.xlsx> [--json]
nong excel read <file.xlsx> [--sheet <name>] [--range <A1:D20>] [--json]
nong excel to-groups <file.xlsx> --group <col> --value <col> [--sheet <name>] [--raw] [--json]
nong excel create <spec.json> -o <out.xlsx> [--json]
nong excel restructure <spec.json> -o <out.xlsx> [--json]
nong excel dissect <file.xlsx> -o <slice-dir> --ingest [--json]
nong excel style <file.xlsx> <spec.json> -o <out.xlsx> [--json]
nong excel formula <file.xlsx> <spec.json> -o <out.xlsx> [--json]
nong excel pivot <file.xlsx> <spec.json> -o <out.xlsx> [--json]
nong excel chart <file.xlsx> <spec.json> -o <out.xlsx> [--json]     # v9: bar/line/pie/scatter
nong excel evaluate <file.xlsx> [--sheet S] [--range A1:A10] [--json]  # v9: formula evaluation
```

## Dispatch

1. To list worksheets, run `nong excel sheets <file> --json`.
2. To inspect data, run `nong excel read <file> --json`; add `--sheet` and `--range` when known.
3. To prepare agricultural experiment data for statistics, run `nong excel to-groups ... --raw > groups.json`.
4. To create a simple workbook from JSON, write an Excel create spec and run `nong excel create spec.json -o out.xlsx --json`.
5. To normalize multi-file experiment sources into one workbook with descriptive statistics, write a restructure spec and run `nong excel restructure spec.json -o out.xlsx --json`.
6. To create a unified NongPandoc package from a workbook, run `nong excel dissect <file.xlsx> -o <slice-dir> --json`, then use the `slice` skill for block-level reads.
7. To apply cell styles from a JSON spec, run `nong excel style <file> spec.json -o out.xlsx --json`.
8. To write formulas, run `nong excel formula <file> spec.json -o out.xlsx --json`.
9. To create a pivot table, run `nong excel pivot <file> spec.json -o out.xlsx --json`.
10. Feed raw grouped JSON directly into `nong chart analyze`, `anova`, `duncan`, or `bar`.
11. Do not promise dashboards, macros, or general Excel editing beyond what `nong commands --json` exposes.

## Create Spec

`excel create` supports simple sheets with headers and rows:

```json
{
  "sheets": [
    {
      "name": "Data",
      "headers": ["Treatment", "Yield"],
      "rows": [
        ["A", 1.2],
        ["A", 1.3],
        ["B", 2.1]
      ]
    }
  ]
}
```

Sheet names are required and must be 31 characters or fewer. `headers` and `rows` are required for each sheet.

## Restructure Spec

`excel restructure` is for experiment workbooks that need a normalized data sheet plus descriptive statistics output. A minimal shape is:

```json
{
  "treatmentMap": { "ck": "CK", "n": "N" },
  "treatmentOrder": ["CK", "N"],
  "metrics": [
    { "key": "plantHeight", "title": "鏍珮(cm)", "decimals": 2 }
  ],
  "blocks": [
    {
      "headerRow": 1,
      "metricRows": { "plantHeight": 2 }
    }
  ],
  "weeklySources": [
    { "file": "week1.xlsx", "week": 1 }
  ],
  "legacySources": [
    {
      "file": "week0.xlsx",
      "week": 0,
      "treatment": "CK",
      "replicateColumn": "A",
      "metricColumns": { "plantHeight": "D" }
    }
  ],
  "output": {
    "allDataSheet": "鍏ㄩ儴鏁版嵁",
    "statsSheet": "缁熻鍒嗘瀽",
    "summarySheet": "缁熻鍒嗘瀽 (2)"
  }
}
```

Use `weeklySources` when the workbook repeats treatment blocks by week. Use `legacySources` for older flat tables where treatment, replicate, and metric columns must be mapped explicitly.

## Groups JSON

`chart` commands expect:

```json
{
  "Control": [1.2, 1.3, 1.1],
  "Treatment": [2.0, 2.2, 2.1]
}
```

Use `--raw` for pipeline files. Use `--json` for model-readable reports.
