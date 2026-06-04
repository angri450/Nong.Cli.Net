# 2026-06-03 后续总路线

## 当前状态

已完成：

1. 阶段 1：CLI 架构
2. 阶段 2：Word 读取与预览
3. 阶段 3：科研核心诊断与统计

正在推进：

4. 阶段 4：统计结果生成图表

## 后续阶段

| 阶段 | 名称 | 核心目标 |
|------|------|----------|
| 4 | 生成核心 | `chart analyze` + `chart bar` |
| 5 | Excel 数据入口 | `excel sheets/read/to-groups` |
| 6 | Word/Inspect 生成 | `word fill/rebuild` + `inspect write paper` |
| 7 | Diagram/PPTX | 方法图、网络图、汇报 PPT |
| 8 | Assets/Genre/OCR | 模板、图标、OCR 依赖探测 |
| 9 | Agent Contract | 命令 schema、examples、skill 同步准备 |
| 10 | 稳定发布 | CLI 测试、pack、tool install、release checklist |

## 总原则

1. 每阶段只做 2-4 个真实命令。
2. 每个真实命令必须有 `--json`。
3. 生成文件必须写入 `artifacts`。
4. 不在 CLI 里重写底层业务逻辑。
5. 每阶段结束必须写 changelog。
6. 阶段 9 前不动 GroundPA-Toolkit。
7. 阶段 10 前不纠结许可证，但发布前必须回头处理。

## 最重要的闭环

最终要优先保证这条链：

```powershell
nong word read paper.docx --json
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
nong excel to-groups data.xlsx --sheet Sheet1 --group A --value B --json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
nong word fill template.docx data.json -o paper.docx --json
```

这条链跑通，`nong` 就已经不是普通 Office .NET 库，而是面向农学生和 AI agent 的低 token 科研工具底座。
