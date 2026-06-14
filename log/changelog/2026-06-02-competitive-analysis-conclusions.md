# 竞品调研结论与路线确认

日期：2026-06-02
来源：office-skill-research 58 个项目深度调研
状态：已确认，待执行

---

## 一、调研范围

两波并行调研，覆盖 58 个项目，分为 10 组：

- CLI/文档生成工具：OfficeCLI、gongwen-paiban、docxMiniWord、OfficeIMO
- AI Skills/Agent 技能：anthropics-skills、kimi-skills、minimax-skills、glm-skills
- PPT 专项：PPTAgent/DeepPresenter、ppt-master、pptx-tools
- 平台/框架：AionUi、nanobot、kimi-agent-internals
- Word/文档处理库：ClosedXML、ClosedXML.Report、DocX-master、ShapeCrawler、Clippit、Open-Xml-PowerTools、openize-open-xml-sdk-net
- 图像/图表/可视化：ImageSharp、SkiaSharp、Magick.NET、ScottPlot、MSAGL、ExcelNumberFormat、RBush
- AI Skills 合集：awesome-agent-skills、awesome-claude-skills、microsoft-skills、GLM/Kimi/MiniMax 内值版对比
- 平台/转换/框架：cherry-studio、Nong.NanoBot.Net、Office-PowerPoint-MCP-Server、frontend-slides、AIWriteX、pandoc
- 公文专项：gongwen-paiban、official-document-drafting、official-document-writing-skill
- 微信/文章：wechat-article-skills、微信公众号 AI 运营助手

---

## 二、核心结论

### 结论 1：论文诊断是我们的独家壁垒

没有任何竞品具备论文级语义诊断能力。OfficeCLI 的 L1 view issues 只做格式检查，不做内容诊断。Inspect 包的 PaperDiagnostics（证据链/数据需求/缺口等级/语义诊断）是全行业独一无二的能力。

### 结论 2：109 命令 CLI 路线被验证

所有成功项目（OfficeCLI、gongwen-paiban、docx-review、pandoc）都走单二进制 CLI 路线。Skill-Gated Shell（Kimi/GLM/MiniMax/Anthropic 共同采用）是行业共识：SKILL.md 注入语境 + 通用 CLI 工具执行。

### 结论 3：可合并的代码（直接吃进 ThirdParty，README 致谢）

| 项目 | 合并原因 | 协议 |
|------|---------|------|
| gongwen-paiban | 公文排版核心算法，约 1000 行，直接集成进 OfficialDocWriter | MIT |
| ClosedXML | Excel 行业标准，ThirdParty 已有源码 | MIT |
| ShapeCrawler | PPT 形状操作最佳方案 | MIT |
| Clippit | 文档合并/拆分/模板填充/Track Changes | MIT |
| ScottPlot | 60+ 图表类型，ThirdParty 已有源码 | MIT |
| MSAGL | Sugiyama/力导向图布局，ThirdParty 已有源码 | MIT |
| SkiaSharp | 工业级 2D 渲染，ThirdParty 已有源码 | MIT |

策略：不通过 NuGet 引用外部包。源码直接合并进 ThirdParty 目录，编译进单一 DLL。README 中致谢原作者。

### 结论 4：公文三件套整合方案

三个公文项目分工明确，可以直接整合：

| 项目 | 能力 | 整合为 |
|------|------|--------|
| gongwen-paiban | 排版（洗脏文档） | `nong official format` |
| official-document-drafting | 起草（15 种公文智能生成+防编造） | `nong official write` |
| official-document-writing-skill | 知识库（规范/模板/检查清单） | `nong official check` |

形成 `nong official write → format → check` 完整公文工作流。

### 结论 5：OfficeCLI 是最强竞品但不是替代

它的 L1/L2/L3 三层 API 设计、内置渲染引擎、skill 系统、plugin protocol 都是标杆。Apache 2.0 协议允许我们复用它的渲染引擎和 plugin 架构。但它没有论文诊断、数学公式、参考文献管理、公文格式——这些是我们的差异化空间。

### 结论 6：协议和分发

- **协议**：保持 MIT（58 个项目中大部分用 MIT，与生态一致）。后续统一升级 Apache 2.0 时整体切换
- **分发**：`dotnet tool install --global Angri450.Nong.Cli` 主力 + GitHub Releases 单文件二进制 + SKILL.md 注入

---

## 三、内值版 vs 公开版的重要教训

GLM/Kimi/MiniMax 三个模型的内置版 vs 公开版对比揭示：

1. **内置版能力远超公开版**（GLM 有 Office 四件套、Kimi 有 webapp-building、MiniMax 有 contract-review）
2. **公开版存在大量工程化 bug**（MiniMax：ZIP 未闭合导致文件损坏、全角引号 C# 语法错误、表格列宽公式错误）
3. **必须遵守的工程原则**：所有路径用 `$SKILL_DIR` 绝对路径、支持离线安装、不要"凭感觉"写 OpenXML（必须走 sample+validator+preview loop）

---

## 四、路线确认

1. CLI 路线不修改方向，109 命令全量规划保持不变
2. 实施策略调整：ShapeCrawler/ClosedXML/Clippit/gongwen-paiban 不再独立引用 NuGet，源码合并进 ThirdParty
3. 优先开发顺序不变：word P0（9 个命令）→ inspect P0（4 个命令）→ chart P0（3 个命令）
4. 公文整合作为独立模块，排在 chart 之后
5. 协议暂保持 MIT，CLI 就绪后统一升 Apache 2.0
