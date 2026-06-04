# 阶段 4：生成核心接入结果

日期：2026-06-03
状态：完成

---

## 实现

### P0：chart analyze

```
nong chart analyze data.json --json
```

一次调用输出 ANOVA + Duncan + 分组描述统计。

- 内部调用 `StatsEngine.FullAnalysis(groups, alpha)`
- JSON 含 `anova` (F/P/SSB/MSB/df) + `duncan` (groups/significance) + `groups` (N/mean/SD/SEM/min/max)
- 非 JSON 调用 `FullAnalysisResult.Print()`

### P0：chart bar

```
nong chart bar data.json -o fig1.png --title "发酵产量" --ylabel "OD600" --json
```

生成带误差棒和显著性字母的柱状图。

- 自动计算 Duncan，若 P < 0.05 则标注字母
- `--no-significance` 禁用显著性字母
- `--error sd|sem|none` 控制误差棒
- `--json` 输出 `artifacts.png`
- 使用 `ChartBuilder.BarChart` / `BarChartWithSignificance`

---

## 验收结果

| 命令 | 结果 |
|------|------|
| `nong chart analyze anova-test.json` | ANOVA F=301.28, Duncan a/b/c, 3组描述统计 |
| `nong chart analyze --json` | 结构化 JSON (anova/duncan/groups) |
| `nong chart bar data.json -o fig1.png` | PNG 生成成功，含显著性标注 |
| `nong chart bar --json` | artifacts.png 返回绝对路径 |
| `dotnet build -c Release` | 0 错误 |

---

## 暂缓（按指导）

- pptx / diagram / ocr / official format
- excel sheets/read（P1）
- 其余图表类型（line/scatter/pie等）
