# Excel Create Reference

Use `nong excel create` for workbook creation. Nong.Toolkit.Net should not scaffold temporary .NET projects or call ClosedXML directly as its main path.

## Command

```powershell
nong excel create spec.json -o out.xlsx --json
```

`-o` is required. The input file is a JSON spec; the output is an `.xlsx` file.

## Supported Spec

The current CLI contract supports simple worksheets with `name`, `headers`, and `rows`:

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
    },
    {
      "name": "Summary",
      "headers": ["Metric", "Value"],
      "rows": [
        ["n", 3],
        ["note", "created by nong excel create"]
      ]
    }
  ]
}
```

Validation rules enforced by the CLI:

- `sheets` must be non-empty.
- Each sheet must have a `name`.
- Sheet names must be 31 characters or fewer.
- Each sheet must include `headers` and `rows`.
- Row values may be numbers, booleans, strings, or nulls.

## Follow-Up Checks

After creation, verify the generated workbook with the read commands:

```powershell
nong excel sheets out.xlsx --json
nong excel read out.xlsx --sheet Data --json
```

Use `status: "ok"` and `artifacts.xlsx` from the JSON response as the success signal.

## Boundaries

Do not promise arbitrary styling, conditional formatting, complex formulas, pivot tables, dashboards, macros, charts embedded in Excel, or general workbook editing. Those capabilities are not exposed by the current `nong excel` command surface.
