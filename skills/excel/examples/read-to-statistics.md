# 读表 → to-groups → Chart 完整管线

## 用户想要什么

从实验记录 Excel 中读取处理组数据，转成 groups JSON，然后做统计分析。

## 做了什么

```powershell
# 1. 列出所有 sheet
nong excel sheets experiment.xlsx --json

# 2. 读取"产量数据" sheet 的内容
nong excel read experiment.xlsx --sheet "产量数据" --json

# 3. 将处理列和值列转成 groups JSON
nong excel to-groups experiment.xlsx --group "处理" --value "产量(kg)" -o yield.json --json

# 4. 现在交给 chart 做统计分析
nong chart analyze yield.json --json
nong chart bar yield.json -o yield_chart.png --json
```

## 结果

`excel to-groups` 输出 groups JSON，直接可用于 `chart analyze` 和 `chart bar`。统计分析完整跑通。

## 关键点

- `excel to-groups` 是 Excel 到 Chart 的桥接命令。指定处理列和值列，输出标准 groups JSON。
- `--treatment-col` 是分类变量（处理组、品种等），`--value-col` 是数值变量。
- 如果 sheet 有多列数据，分多次 `to-groups` 导出，不要一次导全部。
- Groups JSON 格式是 `{"groups": [{"name": "CK", "values": [...]}, ...]}`，这是 chart/ANOVA/Duncan 的标准输入。
