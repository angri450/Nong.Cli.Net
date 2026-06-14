# nong diagram CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong diagram` 是科学图表层（Angri450.Nong.Diagram）的 CLI 入口。覆盖流程图、网络图、系统发育树三类图形，支持 JSON DSL 输入和 Bioicon 图标。

底层实现：DiagramBuilder、FlowchartRenderer、NetworkGraphRenderer、TreeRenderer、BioIconRenderer、ForceDirectedLayout、SugiyamaLayout、TreeLayout。

---

## Diagram 包 11 个文件 → CLI 命令映射

### 1. DiagramBuilder.cs（统一入口）

对应命令：

#### `nong diagram flowchart <spec> [-o <png>] [--width <w>] [--height <h>]`
流程图。

输入：JSON 规格文件
```json
{
  "nodes": [
    { "id": "1", "label": "菌种活化", "shape": "roundedRect" },
    { "id": "2", "label": "发酵培养", "shape": "roundedRect" },
    { "id": "3", "label": "产物提取", "shape": "roundedRect" }
  ],
  "edges": [
    { "from": "1", "to": "2", "label": "接种" },
    { "from": "2", "to": "3", "label": "离心" }
  ]
}
```

实现：DiagramBuilder.Flowchart(graph, ...)，内部使用 SugiyamaLayout + FlowchartRenderer

#### `nong diagram network <spec> [-o <png>] [--width <w>] [--height <h>]`
网络图/关系图。

输入：JSON 规格文件（同 flowchart 结构，但布局不同）
```json
{
  "nodes": [
    { "id": "A", "label": "温度", "iconCategory": "Experimental", "iconName": "thermometer" },
    { "id": "B", "label": "pH", "iconCategory": "Experimental", "iconName": "ph-meter" },
    { "id": "C", "label": "生长速率", "iconCategory": "Biology", "iconName": "growth-curve" }
  ],
  "edges": [
    { "from": "A", "to": "C", "label": "影响" },
    { "from": "B", "to": "C", "label": "调节" }
  ]
}
```

实现：DiagramBuilder.NetworkGraph(graph, ...)，内部使用 ForceDirectedLayout + NetworkGraphRenderer

#### `nong diagram tree <newick> [-o <png>] [--radial] [--width <w>] [--height <h>]`
系统发育树。

输入：Newick 格式字符串或文件，如 `(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);`
选项：`--radial` 辐射状布局，默认矩形

实现：DiagramBuilder.PhylogeneticTree(newick, ...)，内部使用 NewickTree.Parse → TreeLayout + TreeRenderer

#### `nong diagram dsl <json> [-o <png>] [--width <w>] [--height <h>]`
JSON DSL 驱动。JSON 中指定 `renderer` 字段自动选择渲染器。

```json
{
  "renderer": "flowchart",
  "graph": { "nodes": [...], "edges": [...] },
  "title": "实验流程"
}
```

实现：DiagramBuilder.FromDsl(json, ...)

#### `nong diagram icons [-o <png>] [--width <w>] [--height <h>]`
生成 Bioicon 图标参考表（40 个图标一览）。

实现：DiagramBuilder.BioIconSheet(...)

---

### 2-3. Graph.cs + NewickTree.cs（数据模型）

Graph 适配 JSON 输入格式（nodes + edges），NewickTree 支持标准 Newick 格式解析。CLI 内部使用，不暴露。

---

### 4-7. FlowchartRenderer / NetworkGraphRenderer / TreeRenderer / BioIconRenderer（渲染器）

四个渲染器分别对应 flowchart、network、tree、icons 四个命令。基于 SkiaSharp（via ThirdParty）。

---

### 8-10. ForceDirectedLayout / SugiyamaLayout / TreeLayout（布局算法）

内部被渲染器调用，不暴露命令。

---

### 11. FontHelper.cs（字体检测）

内部使用，不暴露命令。

---

## 命令总数：5 个

| 命令 | 输入 | 输出 |
|------|------|------|
| flowchart | JSON (nodes+edges) | PNG |
| network | JSON (nodes+edges+icons) | PNG |
| tree | Newick string/file | PNG |
| dsl | JSON (renderer+graph) | PNG |
| icons | 无 | PNG |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| flowchart | P0 | 流程图，最常用 |
| tree | P1 | 系统发育树 |
| network | P2 | 网络图 |
| dsl | P2 | JSON DSL |
| icons | P2 | 图标参考表 |
