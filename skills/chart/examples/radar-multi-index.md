# 雷达图多指标对比

## 用户想要什么
比较三个小麦品种在产量、蛋白质含量、抗病性、适应性、品质评分五个维度的表现。

## 数据
```json
{"categories":["产量","蛋白质","抗病性","适应性","品质"],"series":[{"name":"品种A","values":[8,6,7,9,7]},{"name":"品种B","values":[9,8,5,7,6]},{"name":"品种C","values":[7,7,8,8,8]}]}
```

## 做了什么
```powershell
nong chart radar data.json -o radar.png --title "品种综合评价" --json
```

## 结果
雷达图输出，三个品种各有优势：品种A适应性最强，品种B产量蛋白质高，品种C最均衡。

## 关键点
- radar 输入：`categories` 数组 + `series` 数组（每个有 `name` 和 `values`）
- values 长度必须和 categories 一致
- 适合综合评价和多指标对比，不做单指标分析
