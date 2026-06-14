# 2026-06-10 Excel style + formula

## What changed

新增 2 个 Excel 编辑命令：`excel style` 和 `excel formula`。

| 命令 | 参数 | 说明 |
|------|------|------|
| `excel style` | `<file> <spec.json> -o <out.xlsx>` | 从 JSON spec 应用样式（预设或逐项） |
| `excel formula` | `<file> <spec.json> -o <out.xlsx>` | 从 JSON spec 写入公式 |

## Why

Excel 命令面之前只有读写和统计管线准备，缺少样式和公式编辑。底层 ClosedXML(StylePresets/ExcelBuilder/AdvancedBuilder) 已有完整能力。

## Files touched

- `Cli/Commands/ExcelCommands.cs` — CreateStyle、CreateFormula + 三个 spec 模型
- `Cli/Common/Manifest.cs` — excel style、excel formula
- `Cli.Tests/CliContractTests.cs` — 3 个测试

## Tests

- `Commands_Json_ExposesExcelStyleAndFormula`
- `ExcelStyle_AppliesPresetAndFormatting`
- `ExcelFormula_WritesFormula`

## Verification

```text
nong commands --json → 102 commands (was 100)
dotnet test → 136 passed, 0 failed, 0 skipped
```
