# 阶段 14：全量 stub 补全 —— 多 agent 并行执行

日期：2026-06-03
状态：COMPLETE — 46 implemented, 24/24 tests PASS

---

## 目标

按 `2026-06-03-014-full-stub-completion-blueprint.md` 蓝图，将 `nong` CLI 从 24 个 implemented 命令扩展到 46-47 个。

## 执行方式

Phase 1 建立 contract test 防线后，启动 4 个并行 agent：

| Agent | 负责领域 | 交付 |
|-------|---------|------|
| A | Word | stats, fonts, styles, validate, extract, dissect, merge (7) |
| B | Inspect | classify, structure, varplan, evidence, data-req, gap, semantics (7) |
| C | Chart + Excel | chart line/scatter/pie, excel create (4) |
| D | Diagram + PPTX + OCR | diagram tree, pptx read/slides, ocr cloud/local (5) |

## Phase 1：Contract test harness

- 新建 `Cli.Tests/Cli.Tests.csproj` — xUnit net8.0
- 新建 `Cli.Tests/CliContractTests.cs` — 24 个 contract 测试
- 覆盖：manifest 一致性、stub E009、错误路径 E001/E003/E006、JSON schema 完整性、skill 命令边界

## Agent A：Word（7 个命令）

| 命令 | 功能 | 新增文件 |
|------|------|---------|
| `word stats` | 段落/表格/图片/脚注/尾注/字符/节统计 | `Docx/DocxAnalysis.cs` |
| `word fonts` | 列出所有字体（run + style 来源） | |
| `word styles` | 列出所有样式定义（id/name/type/basedOn） | |
| `word validate` | OpenXmlValidator 校验 | |
| `word extract` | 提取嵌入图片到目录 | |
| `word dissect` | 格式指纹聚合（stats+fonts+styles+tables+numbering+sections） | |
| `word merge` | 合并多个 docx 文件 | |

## Agent B：Inspect（7 个命令）

拆分 `inspect diagnose` 管线为独立命令：

| 命令 | 来源 API |
|------|---------|
| `inspect classify` | PaperTypeClassifier.Classify |
| `inspect structure` | PaperStructureExtractor.BuildPaperStructure |
| `inspect evidence` | PaperDiagnostics.DiagnoseEvidenceChain |
| `inspect data-req` | PaperDiagnostics.DiagnoseDataRequirements |
| `inspect gap` | classify→structure→evidence→data-req→gap grade |
| `inspect varplan` | VariablePlanGenerator（best-effort） |
| `inspect semantics` | PaperDiagnostics.DiagnosePaperQuality |

## Agent C：Chart + Excel（4 个命令）

| 命令 | 功能 |
|------|------|
| `chart line` | 多系列折线图（JSON spec，每系列独立 x/y） |
| `chart scatter` | 散点图（可选分组着色 + trendline） |
| `chart pie` | 饼图（label/value，百分比标注） |
| `excel create` | 从 JSON spec 创建 xlsx（ClosedXML） |

## Agent D：Diagram + PPTX + OCR（5 个命令）

| 命令 | 功能 | 新增文件 |
|------|------|---------|
| `diagram tree` | 系统发育树（Newick 文本 + JSON） | |
| `pptx read` | 抽取全部 slide 文本 | `Pptx/PptxReader.cs` |
| `pptx slides` | 按 slide 统计形状/文本/图片/表格/图表 | |
| `ocr cloud` | PaddleOCR-VL 云端 OCR（需 token → E005） | `Cli/Commands/OcrCommands.cs` |
| `ocr local` | 诚实 E005（本地 PaddleOCR 运行时不可用） | |

## 改动文件汇总

### 新建（5 个）
- `Cli.Tests/Cli.Tests.csproj`
- `Cli.Tests/CliContractTests.cs`
- `Cli/Commands/OcrCommands.cs`
- `Docx/DocxAnalysis.cs`
- `Pptx/PptxReader.cs`

### 修改（15 个）
- `Cli/Commands/WordCommands.cs` — 7 个 stub → 真实实现
- `Cli/Commands/InspectCommands.cs` — 7 个 stub → 真实实现
- `Cli/Commands/ChartCommands.cs` — 3 个 stub → 真实实现
- `Cli/Commands/ExcelCommands.cs` — 1 个 stub → 真实实现
- `Cli/Commands/DiagramCommands.cs` — 1 个 stub → 真实实现
- `Cli/Commands/PptxCommands.cs` — 2 个 stub → 真实实现
- `Cli/Program.cs` — 注册 OcrCommands，移除 stub ocr group
- `Cli/NongCli.csproj` — 添加 PptxCore + MultiModalCore 引用
- `Cli/Common/Manifest.cs` — 22 个命令状态从 stub→implemented
- `Chart/ChartBuilder.cs` — 添加 GetCjkFontFamily
- `Docx/DocumentWriter.cs` — 添加 Title/Heading/Body 方法
- `Docx/StyleBuilder.cs` — 添加 BuildAll 方法
- `Pptx/PptxCore.csproj` — 添加 ThirdParty 引用
- `MultiModal/LayoutToWordConverter.cs` — 修复预存编译错误

## 验收结果

- **构建**: Release 0 错误, 0 警告
- **命令数**: 46 implemented, 1 ocr local (E005 honest stub)
- **Contract tests**: 24/24 PASS (1m32s)
- **回归**: inspect diagnose/refs/write-paper, chart analyze/anova/duncan/bar, word read/preview/fill/rebuild — 全部正常

## 命令清单（46 个）

```text
word:    read, preview, fill, rebuild, extract, dissect, stats, fonts, styles, validate, merge (11)
inspect: diagnose, refs, write-paper, classify, structure, varplan, evidence, data-req, gap, semantics (10)
chart:   analyze, bar, anova, duncan, line, scatter, pie (7)
excel:   sheets, read, to-groups, create (4)
diagram: flowchart, network, tree (3)
genre:   list, show (2)
icons:   list, search (2)
skill:   validate, scan, inventory, package (4)
pptx:    read, slides (2)
ocr:     cloud (1)
```

## 已知未完成

- `ocr local` — 诚实 E005（本地 PaddleOCR 运行时不可用）
- Phase 8（GroundPA skill sync）— 待执行
- Phase 9（release audit）— 待执行
