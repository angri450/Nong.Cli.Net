# 2026-06-10 Chart 新图种：boxplot + histogram

## What changed

新增 2 个 chart 命令：`chart boxplot` 和 `chart histogram`。底层 ScottPlot 已有 ChartTypes.BoxPlot/Histogram 实现，本变更只加 CLI 命令面和 worker 渲染。

| 命令 | 参数 | 说明 |
|------|------|------|
| `chart boxplot` | `<file> -o <png> [--title] [--ylabel]` | 箱线图，处理组分布对比 |
| `chart histogram` | `<file> -o <png> [--title] [--xlabel] [--ylabel] [--bin-count]` | 直方图，合并所有组值 |

输入格式与其他 chart 命令一致：`{"组名": [值1, 值2, ...], ...}`。

## Why

Chart 命令命名审计识别出箱线图和直方图是农业数据最常见缺失的图种。底层 ChartTypes.cs 已有完整实现，只需补命令面。

## Files touched

- `Cli/Commands/ChartCommands.cs` — 新增 CreateBoxplotChart、CreateHistogramChart，注册到 Create()
- `Cli/Commands/RenderWorkerCommands.cs` — 新增 --bin-count、--xlabel 选项；boxplot/histogram switch case；RenderChartBoxplot、RenderChartHistogram 渲染方法
- `Cli/Common/Manifest.cs` — 注册 chart boxplot 和 chart histogram
- `Cli.Tests/CliContractTests.cs` — 4 个测试（Commands_Json_Exposes、ChartBoxplot_GeneratesPng、ChartHistogram_GeneratesPng、ChartHistogram_WithBinCount）

## Tests

- `Commands_Json_ExposesBoxplotAndHistogram` — 验证命令面包含新命令
- `ChartBoxplot_GeneratesPng` — 验证箱线图 PNG 生成
- `ChartHistogram_GeneratesPng` — 验证直方图 PNG 生成
- `ChartHistogram_WithBinCount_GeneratesPng` — 验证 --bin-count 参数

## Verification

```text
dotnet build Cli/NongCli.csproj -c Release
Result: 0 errors

dotnet test Cli.Tests/Cli.Tests.csproj -c Release
Result: 129 passed, 0 failed, 0 skipped

nong commands --json
Result: 98 commands (was 96), chart boxplot + histogram confirmed
```

## Remaining

- Chart heatmap + radar 仍在功能缺口路线图中（优先级较低）
- 不纳入 chart analyze（analyze 保持 ANOVA+Duncan+MST 语义）
