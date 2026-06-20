# ANOVA + Duncan + 柱状图完整流

## 用户想要什么

有四个处理组（CK、N1、N2、N3）的产量数据，需要做方差分析、Duncan 多重比较，并生成带显著性标记的柱状图。

## 数据

```json
{
  "groups": [
    {"name": "CK", "values": [5.2, 5.1, 5.3, 5.0, 5.2]},
    {"name": "N1", "values": [6.1, 6.3, 5.9, 6.2, 6.0]},
    {"name": "N2", "values": [7.5, 7.3, 7.6, 7.4, 7.2]},
    {"name": "N3", "values": [7.0, 6.9, 7.1, 6.8, 7.0]}
  ]
}
```

## 做了什么

```powershell
# 1. 先跑一站式分析
nong chart analyze data.json --json

# 2. 如果需要单独跑方差分析
nong chart anova data.json --json

# 3. 做 Duncan MRT 多重比较
nong chart duncan data.json --json

# 4. 生成柱状图（带误差线和显著性字母）
nong chart bar data.json -o yield_bar.png --json
```

## 结果

ANOVA 结果：F=XX, p<0.01，处理间差异极显著。

Duncan MRT：N2^a > N3^b > N1^c > CK^d（不同字母表示差异显著）。

柱状图输出：`yield_bar.png`，带 error bar 和显著性字母标注。

## 关键点

- `chart analyze` 是一站式：ANOVA + Duncan + 描述统计。适合标准分析流程。
- 如果需要单独跑某个统计，用 `chart anova`、`chart duncan` 分步命令。
- 柱状图的输入 JSON 必须包含 `groups` 数组，每个 group 有 `name` 和 `values`。
- 如果显著性字母显示不对，检查 `chart duncan` 的输出——它确定哪些组之间有显著差异。
