## 来源

基于 office-skill-research 58 个项目深度调研，详见 changelog/2026-06-02-competitive-analysis-conclusions.md。

## 六条核心结论

### 1. 论文诊断是我们的独家壁垒
没有任何竞品具备论文级语义诊断能力。Inspect 包（PaperDiagnostics/ReferenceAnalyzer/VariablePlanGenerator）是全行业独一无二的能力。

### 2. 109 命令 CLI 路线被验证
所有成功项目（OfficeCLI、gongwen-paiban、docx-review、pandoc）都走单二进制 CLI 路线。Skill-Gated Shell 是行业共识。

### 3. 可合并的代码（直接吃进 ThirdParty，README 致谢）
- gongwen-paiban（MIT）：公文排版核心算法
- ClosedXML（MIT）：Excel 行业标准（已有源码）
- ShapeCrawler（MIT）：PPT 形状操作最佳方案
- Clippit（MIT）：文档合并/拆分/模板填充
- ScottPlot / MSAGL / SkiaSharp 已在 ThirdParty 中

### 4. 公文三件套整合方案
gongwen-paiban（排版）+ official-document-drafting（起草）+ official-document-writing-skill（知识库）→ nong official write → format → check

### 5. OfficeCLI 是最强竞品但不是替代
它的 L1/L2/L3 架构是标杆，但没有论文诊断、数学公式、参考文献管理、公文格式。

### 6. 协议和分发
MIT 为主流，后续统一升 Apache 2.0。分发走 dotnet tool + 单文件二进制 + SKILL.md。

## 路线确认

CLI 方向不变，实施策略调整：外部库源码合并进 ThirdParty，不通过 NuGet 引用。优先开发 word P0 → inspect P0 → chart P0。