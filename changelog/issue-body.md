## 概述

本次重构将 Angri450.Nong.Docx 拆分为三个包，新增 Pandoc 内联格式标记、LaTeX 数学公式渲染、参考文献自动管理系统。

详见 changelog/2026-06-02-architecture-decision.md。

## 变更清单

### 包结构调整
- Docx v3.0.2 → v3.1.0：移除论文 API（迁至 Inspect），新增 MathRenderer
- Inspect v3.0.0（新建）：论文诊断+写作+公文+信函+参考文献管理
- Genre v3.0.0（新建）：6 个 JSON 格式模板

### 新功能
- Pandoc 7 种内联标记（*italic* **bold** ==highlight== ~~strikethrough~~ ^sup^ ~sub~）
- LaTeX → OMML 数学公式渲染（DocxCore.MathRenderer）
- [@key] 引用键 → 自动编号 + GB/T 7714 格式化

### Bug 修复
- StyleBuilder JSON lineRule 字段（#2）
- Body() 逐字 w:t 优化（#3）
- 参考文献序号重复/缺失（#7）

### 协议计划
- 暂保持 MIT，后续统一切换 Apache 2.0

## 待办
- [ ] CLI 工具开发（Angri450.Nong.Cli）
- [ ] 协议切换 MIT → Apache 2.0
- [ ] NuGet 旧包 unlist
- [ ] Skill 适配
- [ ] 打包发版