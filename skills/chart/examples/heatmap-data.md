# 热力图数据可视化

## 用户想要什么
看三个重复处理组在五个时间点的产量变化热力图。

## 数据
```json
{"data":[[5.2,6.1,7.5,7.0,6.5],[5.1,6.3,7.3,6.9,6.4],[5.0,5.9,7.6,6.8,6.6]],"rows":3,"cols":5,"colormap":"Viridis"}
```

## 做了什么
```powershell
nong chart heatmap data.json -o heatmap.png --title "产量热力图" --colormap Viridis --json
```

## 结果
输出 heatmap.png，Viridis 配色，颜色从深蓝→绿→黄渐变，直观看到 N2 组产量峰值在后期。

## 关键点
- heatmap 输入：`data` 二维数组 + `rows` + `cols`
- `--colormap` 可选：Viridis、Plasma、Inferno、Magma、Turbo 等
- 热力图适合展示多维数据，不是单组对比（用 boxplot 更合适）
