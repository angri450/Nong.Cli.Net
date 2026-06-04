# nong chart CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong chart` 是图表层（Angri450.Nong.Chart）的 CLI 入口。覆盖 18 种图表类型、统计分析、数据加载三类操作。

底层实现：ChartBuilder、ChartTypes、StatsEngine、DataLoader、FontHelper、ChartCombine。

---

## Chart 包 7 个文件 → CLI 命令映射

### 1. ChartBuilder.cs（柱状图构建器）

对应命令：

#### `nong chart bar <data> [-o <png>] [--title <text>] [--ylabel <text>]`
柱状图（带误差棒、显著性标注、均值显示）。

输入：分组数据 JSON/CSV/xlsx，每组包含多个重复值
选项：
```
nong chart bar data.json -o bar.png --title "发酵产量" --ylabel "OD600" --error-bar --significance sig.json
```

significance.json 格式：`{"A": "a", "B": "b", "C": "a"}`，不同字母表示显著性差异组。

实现：ChartBuilder.BarChart() / BarChartWithSignificance() / BarChart(BarChartConfig)

---

### 2. ChartTypes.cs（18 种图表类型）

对应 18 个命令：

#### `nong chart pie <data> [-o <png>] --title <text>`
饼图。数据：`{"A": 30, "B": 20, "C": 50}`

#### `nong chart donut <data> [-o <png>] --title <text> [--inner <ratio>]`
环形图。`--inner` 内径比例，默认 0.5。

#### `nong chart line <data> [-o <png>] --title <text> --xlabel <text> --ylabel <text>`
折线图。x 值数组 + 多系列 y 值。

#### `nong chart area <data> [-o <png>] --title <text>`
面积图。同折线图参数，区域填充。

#### `nong chart scatter <data> [-o <png>] --title <text> [--regression]`
散点图。`--regression` 叠加线性回归线。

#### `nong chart multi-scatter <data> [-o <png>] --title <text>`
多系列散点图。

#### `nong chart box <data> [-o <png>] --title <text> --ylabel <text>`
箱线图。

#### `nong chart histogram <data> [-o <png>] --title <text> [--bins <n>]`
直方图。`--bins` 分箱数。

#### `nong chart radar <data> [-o <png>] --title <text>`
雷达图。categories 数组 + 多系列数据。

#### `nong chart stock <data> [-o <png>] --title <text>`
K线图/OHLC。数据：`[{open, high, low, close, date}, ...]`

#### `nong chart bubble <data> [-o <png>] --title <text>`
气泡图。x + y + size 三维数据。

#### `nong chart heatmap <data> [-o <png>] --title <text> [--colormap <name>]`
热力图。二维数值矩阵。

#### `nong chart gauge <data> [-o <png>] --title <text>`
仪表盘。多指针值 + 标签。

#### `nong chart coxcomb <data> [-o <png>] --title <text>`
南丁格尔玫瑰图。

#### `nong chart lollipop <data> [-o <png>] --title <text>`
棒棒糖图。

#### `nong chart population <data> [-o <png>] --title <text>`
人口金字塔/双向条形图。

#### `nong chart function <expr> <xMin> <xMax> [-o <png>] --title <text>`
函数图。表达式如 `Math.Sin(x)`

#### `nong chart error-bar <data> [-o <png>] --title <text>`
误差棒图。x + y + xError + yError。

---

### 3. StatsEngine.cs（统计分析）

对应命令：

#### `nong chart anova <data>`
单因素方差分析。

输入：分组数据（同 bar chart 数据格式）
输出：
```
=== 方差分析 ===
F = 12.34, P = 0.0012
SSB = 45.6, SSW = 23.4
dfB = 2, dfW = 15
MSB = 22.8, MSW = 1.56

组别  N  均值    SD    SEM
A     6  23.5   2.1   0.86
B     6  18.2   1.8   0.73
C     6  28.1   3.2   1.31
```

实现：StatsEngine.OneWayAnova()

#### `nong chart duncan <data> [--alpha <0.05>]`
Duncan 多重比较检验。需先做过 ANOVA。

输出：
```
=== Duncan MRT (α=0.05) ===
组别  均值   显著性
C     28.1   a
A     23.5   b
B     18.2   c
```

实现：StatsEngine.DuncanMRT()

#### `nong chart analyze <data> [--alpha <0.05>]`
完整统计管线：ANOVA → Duncan → 输出完整报告。

实现：StatsEngine.FullAnalysis() → .Print()

---

### 4. DataLoader.cs（数据加载器）

被上述命令内部调用，不独立暴露为命令。

支持：FromJson、FromXlsx、FromXlsxMultiColumn、FromCsv。所有需要分组数据的命令自动识别文件格式。

---

### 5. ChartCombine.cs（图表拼接）

对应命令：

#### `nong chart combine <files...> [-o <png>] [--labels <A,B,C>]`
横向拼接多张图表为一张组合图。

```
nong chart combine fig1.png fig2.png fig3.png -o combined.png --labels A,B,C
```

实现：ChartCombine.MergeHorizontal()

---

### 6. FontHelper.cs（字体检测）

内部使用，不暴露命令。所有图表自动检测系统 CJK 字体，保证中文标题和标签正常显示。

---

### 7. BarChartConfig.cs + StatsModels.cs（数据模型）

纯数据类，内部使用。

---

## 命令总数：24 个

| 类别 | 数量 | 命令 |
|------|------|------|
| 柱状图 | 1 | bar |
| 其他图表 | 17 | pie, donut, line, area, scatter, multi-scatter, box, histogram, radar, stock, bubble, heatmap, gauge, coxcomb, lollipop, population, function, error-bar |
| 统计分析 | 3 | anova, duncan, analyze |
| 拼接 | 1 | combine |
| 数据加载 | 2 | 内部调用，不暴露 |
| **合计** | **24** | |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| bar | P0 | 柱状图，最常用图表 |
| line | P0 | 折线图 |
| scatter | P0 | 散点图 |
| pie | P1 | 饼图 |
| anova | P1 | 方差分析 |
| duncan | P1 | 多重比较 |
| analyze | P1 | 完整统计分析 |
| combine | P1 | 图表拼接 |
| 其余 16 个 | P2 | 后续迭代 |
