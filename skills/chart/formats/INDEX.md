# Chart Color Schemes

## Built-in (in BarChartConfig.DefaultColors)

色盲友好的默认六色方案，适合农学实验多处理组比较：

| # | Color | Hex | Use |
|---|-------|-----|-----|
| 1 | Blue | `#5B9BD5` | 对照组 |
| 2 | Orange | `#ED7D31` | 处理 1 |
| 3 | Green | `#70AD47` | 处理 2 |
| 4 | Yellow | `#FFC000` | 处理 3 |
| 5 | Purple | `#A574B6` | 处理 4 |
| 6 | Red | `#DB4453` | 处理 5 |

## Custom Scheme

在 Program.cs 中定义自定义配色：

```csharp
var myColors = new ScottPlot.Color[] {
    new(91, 155, 213),   // 蓝
    new(237, 125, 49),   // 橙
    new(112, 173, 71),   // 绿
};
ChartBuilder.BarChart(new BarChartConfig {
    Groups = data,
    Colors = myColors,
    // ...
});
```

## Adding a Scheme

创建 `formats/<name>.json`:

```json
{
  "name": "nature-palette",
  "colors": ["#E64B35", "#4DBBD5", "#00A087", "#3C5488", "#F39B7F", "#8491B4"],
  "description": "Nature journal inspired palette"
}
```

然后更新此文件的表格。
