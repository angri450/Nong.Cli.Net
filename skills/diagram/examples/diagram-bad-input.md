# 流程图输入描述不够

## 用户想要什么

画"实验技术路线"的流程图。

## 出了什么问题

用户只给了一句模糊描述："画出实验流程图"。没有节点、没有步骤顺序、没有分支条件。

## 怎么处理的

1. 反问用户确认流程步骤：
   - 实验分几个阶段？
   - 每个阶段的关键步骤是什么？
   - 有没有分支条件（如"如果 A 则走 B"）？

2. 用户确认后，构建 JSON spec：
```json
{
  "nodes": [
    {"id": "1", "label": "田间试验", "shape": "box"},
    {"id": "2", "label": "样本采集", "shape": "box"},
    {"id": "3", "label": "室内分析", "shape": "box"},
    {"id": "4", "label": "数据处理", "shape": "box"}
  ],
  "edges": [
    {"from": "1", "to": "2"},
    {"from": "2", "to": "3"},
    {"from": "3", "to": "4"}
  ]
}
```

3. 生成流程图：
```powershell
nong diagram flowchart spec.json -o tech-route.png --json
```

## 结果

输出清晰的四步技术路线流程图。

## 关键点

- `diagram flowchart` 需要完整的 JSON spec（nodes + edges）。不要只给一句模糊描述就去生成。
- 如果用户描述不够，先问清楚：节点有哪些、怎么连接、有没有分支。
- nodes 的 shape 支持 box、diamond（决策/分支）、ellipse（开始/结束）。
- edges 如果带 label（如"是"、"否"），在 edge 对象里加 `label` 字段。
