# 2026-06-10 Excel pivot

## What changed

新增 `excel pivot` 命令。从 JSON spec 在已有 xlsx 上创建数据透视表。

```
nong excel pivot <file.xlsx> <spec.json> -o <out.xlsx> --json
```

Spec 格式：
```json
{"sheet":"Data","range":"A1:D100","pivotSheet":"Pivot","rowLabels":["Region"],"columnLabels":["Product"],"values":[{"field":"Sales","summary":"sum"}]}
```

## Why

Excel 命名审计识别 pivot table 是常用缺失能力。ClosedXML 已有完整 PivotTable API，只需 CLI 封装。

## Files touched

- `Cli/Commands/ExcelCommands.cs` — CreatePivot + ExcelPivotSpec/ExcelPivotValue 模型
- `Cli/Common/Manifest.cs` — excel pivot

## Tests

- `Commands_Json_ExposesExcelPivot`
- `ExcelPivot_CreatesPivot`

## Verification

```text
nong commands --json → 108 commands (was 107)
dotnet test → pending (full suite running)
```
