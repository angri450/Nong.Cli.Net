# Excel Read Reference

This skill uses the current `nong` CLI for Excel inspection. Do not load `.xlsx` files through custom ClosedXML scripts unless the user explicitly asks for library-level development outside the Nong.Toolkit.Net skill workflow.

## Commands

List worksheets:

```powershell
nong excel sheets data.xlsx --json
```

Read the first sheet:

```powershell
nong excel read data.xlsx --json
```

Read a specific sheet or range:

```powershell
nong excel read data.xlsx --sheet Sheet1 --range A1:D20 --json
```

Convert treatment/value columns into grouped JSON:

```powershell
nong excel to-groups data.xlsx --group Treatment --value Yield --raw > groups.json
```

Column selectors for `to-groups` can be column letters or header names. Use `--raw` when the output will be piped to `nong chart`; use `--json` when the agent needs the standard response envelope.

## Expected JSON Handling

Every `--json` response uses the standard `nong` envelope:

```json
{
  "status": "ok",
  "command": "excel read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": {},
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 0, "version": "12.1.0" }
}
```

Treat `status: "error"` as failed. Inspect `errors[].code` and `errors[].message`, fix the workbook path, sheet name, range, or column selector, then retry.

## Boundaries

`excel read` and `excel sheets` are inspection commands. They do not edit workbooks. For creation, use `nong excel create` with the simple JSON spec described in `write-excel.md`.
