# 2026-06-10 Chart heatmap + radar

## What changed

新增 2 个 Chart 命令：`chart heatmap` 和 `chart radar`。

| 命令 | 参数 | 说明 |
|------|------|------|
| `chart heatmap` | `<spec.json> -o <png> [--title] [--colormap]` | 热力图（2D 数组可视化）|
| `chart radar` | `<spec.json> -o <png> [--title]` | 雷达图/蜘蛛图（多指标对比）|

## Why

Chart 命名审计识别热力图和雷达图是农业常见图种。底层 ChartTypes 已有完整实现，只需补命令面。

## Files touched

- `Cli/Commands/ChartCommands.cs` — CreateHeatmapChart、CreateRadarChart + HeatmapSpec/RadarSpec 模型
- `Cli/Commands/RenderWorkerCommands.cs` — heatmap/radar switch cases + RenderChartHeatmap/RenderChartRadar
- `Cli/Common/Manifest.cs` — chart heatmap、chart radar

## Tests

- `Commands_Json_ExposesHeatmapAndRadar`
- `ChartHeatmap_GeneratesPng`
- `ChartRadar_GeneratesPng`

## Verification

```text
nong commands --json → 106 commands (was 104)
dotnet test → 143 passed, 0 failed
```
