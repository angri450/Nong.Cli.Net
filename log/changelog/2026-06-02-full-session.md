# Docx 拆包 + Genre/Inspect 新建 + 数学公式 + 参考文献管理

日期：2026-06-02
影响包：Angri450.Nong.Docx, Angri450.Nong.Inspect, Angri450.Nong.Genre
变更类型：架构重构 + 新功能

---

## 包结构变更

### Angri450.Nong.Docx（v3.0.2 → v3.1.0）— 纯 Word 引擎

**移除**：DocumentWriter 论文方法（Title/Abstract/Keywords/Heading/BibHeading/Body/Figure/VariableTable/References）、StyleBuilder.BuildAll/BuildNumbering

**新增**：MathRenderer（LaTeX → OMML 数学公式渲染）

**修复**：StyleBuilder.BuildFromJson 增加 lineRule 字段支持（"auto"/"exact"/"atLeast"）

### Angri450.Nong.Inspect（v3.0.0，新建）— 内容审查+写作

**论文写作**：
- PaperWriter：链式 API，Body() 支持 Pandoc 7 种内联标记（*Italic* **Bold** ==Highlight== ~~Strikethrough~~ ^Sup^ ~Sub~）+ 拉丁名自动斜体 + 引文上标 + [@key] 引用解析
- Gbt7714Style：GB/T 7714 样式+编号

**论文诊断**：PaperTypeClassifier / PaperStructureExtractor / PaperDiagnostics / ReferenceAnalyzer / VariablePlanGenerator

**公文/信函**：OfficialDocWriter / LetterWriter

**参考文献管理**（新增）：
- RefEntry：文献条目数据模型，支持 article/book/thesis/conference/patent/report/standard/online 8 种类型
- ReferenceManager：[@key] 引用键解析 → 自动编号 → GB/T 7714 格式化
- PaperWriter.SetReferenceDatabase() + AutoReferences() 自动生成参考文献列表

### Angri450.Nong.Genre（v3.0.0，新建）— 格式模板库

纯 JSON 模板，6 个：期刊论文/毕业论文/竞赛论文/答辩PPT/通知公文/商务信函

---

## 6 个报错修复状态

| # | 严重度 | 状态 | 说明 |
|---|--------|------|------|
| 1 | HIGH | 不可修（工具链） | Write 工具中文引号归一化 |
| 2 | MEDIUM | 已修复 | StyleBuilder.BuildFromJson 增加 lineRule 字段 |
| 3 | LOW | 已修复 | Body() 改用 Regex.Split + 统一标记解析器 |
| 4 | MEDIUM | Skill 层 | dissect-docx.ps1 路由检测 |
| 5 | LOW | 设计决策 | BuildFromJson 辅助样式保留 |
| 6 | MEDIUM | Skill 层 | word skill dispatch 优先 .NET CLI |

---

## 技能须知

1. Docx v3.1.0 的 DocumentWriter 不再包含论文方法，迁移到 Inspect.PaperWriter
2. Inspect.PaperWriter.Body() 支持 Pandoc 内联标记：*Italic* **Bold** ==Highlight== ~~Strikethrough~~ ^Sup^ ~Sub~
3. 参考文献用 [@key] 标记替代手写 [1]，ReferenceManager 自动编号+格式化
4. 数学公式用 DocxCore.MathRenderer.RenderInline/RenderDisplay
5. Genre 包提供 6 个 JSON 格式模板，用 Nong.Genre.GenreTemplate.Load("journal-paper") 加载
