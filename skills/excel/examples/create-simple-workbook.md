# 从 JSON 创建简单 Workbook

## 用户想要什么

把统计数据结果写成一个简单的 Excel 文件。

## 做了什么

```powershell
# 准备 JSON spec
echo '{
  "sheets": [{
    "name": "方差分析",
    "headers": ["处理", "均值", "标准差", "显著性"],
    "rows": [
      ["CK", "5.16", "0.11", "d"],
      ["N1", "6.10", "0.16", "c"],
      ["N2", "7.40", "0.16", "a"],
      ["N3", "6.96", "0.11", "b"]
    ]
  }]
}' > stats.json

# 创建 xlsx
nong excel create stats.json -o stats.xlsx --json
```

## 结果

生成了 `stats.xlsx`，包含一个名为"方差分析"的 sheet，有表头和数据行。

## 关键点

- `excel create` 做的是简单 JSON rows 到 xlsx。不支持样式、公式、透视表、图表。
- 如果需要设置样式或公式，这是当前的功能缺口。告诉用户当前只能做简单数据导出。
- JSON spec 的 `sheets` 数组可以包含多个 sheet。
- 输出的 xlsx 可以用 `excel read` 验证内容是否正确。
