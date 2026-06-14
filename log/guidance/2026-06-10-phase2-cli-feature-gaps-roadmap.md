# 2026-06-10 CLI 功能缺口路线图

## 背景

2026-06-10 命令命名审计（`log/reports/toolkit-cli-command-naming-audit.html`）识别出 6 类功能缺口。这些不是改名或 alias 能解决的问题，需要新增底层能力、CLI 命令面和测试。

## 缺口分类

### 1. PPTX 生成和编辑

**当前状态**：`pptx read`、`pptx slides`、`pptx dissect` 三个只读命令。可以读 PPT、列 slide、切片。不能创建、修改、生成。

**需要的命令**：
- `pptx create`：从 JSON spec 生成新 PPTX（slide 结构、文本框、图片、表格）
- `pptx edit`：修改已有 PPTX（增删 slide、替换文本、更新图片）

**底层依赖**：PptxCore（`Angri450.Nong.Pptx`）已有部分写入能力（通过 ThirdParty 的 Open XML SDK 源码）。需要审计现有 PptxCore 的写入面，确定 create/edit 命令的 spec。

**影响**：Toolkit 的 pptx skill 当前标注"只读"，如果做了 create 就要更新 pptx skill 暴露面。

**优先级**：中。当前 Toolkit 只承诺读取，但用户如果要做交付型 PPT 就没有 CLI 路径。

### 2. Excel 高级编辑

**当前状态**：`excel sheets`、`excel read`、`excel create`、`excel dissect`、`excel to-groups`。`create` 只能做简单 JSON rows 到 xlsx。没有样式设置、公式写入、透视表、宏、工作簿内图表。

**需要的命令**：
- `excel style`：设置单元格/区域样式（字体、颜色、边框、数字格式）
- `excel formula`：写入公式
- `excel pivot`：创建透视表
- `excel edit`：通用编辑（插入/删除行列、合并单元格、修改数据）

**底层依赖**：ExcelCore（`Angri450.Nong.Excel`）基于 ClosedXML（通过 ThirdParty 编译），ClosedXML 本身支持这些能力。需要把这些能力包装成 CLI 命令。

**影响**：Toolkit 的 excel skill 当前标注"创建简单 workbook"。如果做了高级编辑要更新。

**优先级**：中。当前够做基础读写和统计管线准备，但如果用户要做复杂 Excel 就需要新命令。

### 3. Chart 新图种

**当前状态**：`chart bar`、`chart line`、`chart scatter`、`chart pie`、`chart analyze`、`chart anova`、`chart duncan`。7 个命令覆盖了农业统计最常用的图，但缺少箱线图、直方图、热力图、雷达图。

**需要的命令**（按农业场景优先级排）：
- `chart boxplot`：箱线图（处理组分布对比，农业实验最常见需要）
- `chart histogram`：直方图（数据分布）
- `chart heatmap`：热力图（多变量相关性、田间分布）
- `chart radar`：雷达图（多指标综合评价）

**底层依赖**：ChartCore（`Angri450.Nong.Chart`）基于 ScottPlot（通过 ThirdParty 编译），ScottPlot 已支持这些图种。需要补 CLI 命令 + 参数 spec。

**影响**：Toolkit 的 chart skill 用户会期望这些图种能用。

**优先级**：中高。农业数据最常见的箱线图和直方图应该优先。

### 4. PDF 编辑

**当前状态**：`pdf check`、`pdf dissect`、`pdf render`、`pdf images`。四个只读/切片/渲染/提取命令。没有合并、拆分、加水印、生成可搜索 PDF。

**需要的命令**：
- `pdf merge`：合并多个 PDF
- `pdf split`：拆分 PDF（按页或按书签）
- `pdf ocr`：对扫描 PDF 跑 OCR 生成可搜索 PDF
- `pdf watermark`：加水印

**底层依赖**：PdfCore（`Angri450.Nong.Pdf`）基于 PdfPig（通过 ThirdParty 编译）。PdfPig 支持读取和部分写入。PDF 写入复杂度高，merge/split 相对简单，ocr-pdf 需要整合 MultiModal OCR 管线。

**影响**：Toolkit 的 pdf skill 当前标注"读取/切片/渲染/提图"。需要更新。

**优先级**：中。merge 和 split 是最常被问到的 PDF 编辑需求。

### 5. Inspect 扩展

**当前状态**：11 个命令覆盖论文诊断和公文/论文生成。缺少信函写作和正式公文审核。

**需要的命令**：
- `inspect write-letter`：从 JSON spec 生成正式信函 DOCX
- `inspect official-check`：对已有公文 DOCX 做格式合规审核
- `lit format-refs`：引用格式自动修复（GB/T 7714 等）

**底层依赖**：Inspect 库（`Angri450.Nong.Inspect`）有 LetterWriter/ReferenceManager 的历史规划。需要审计底层 API，确定哪些已可实现。

**影响**：Toolkit 的 inspect skill 会多两个路由入口。

**优先级**：中。公文检查（official-check）是最贴近现有功能的。

### 6. Word 高级审阅

**当前状态**：39 个命令已经很强。但缺少文档 diff、真实视觉预览和任意样式应用。

**需要的命令**：
- `word compare`：两份 DOCX 的差异对比（类似 Word 的比较文档功能）
- `word render-preview`：渲染为一组页面图片（用于 AI 看排版）
- `word apply-style`：对指定段落/范围应用预定义样式

**底层依赖**：
- compare 需要 Open XML SDK 级别的元素 diff 算法，不是简单字符串 diff
- render-preview 需要 Word 渲染引擎或至少 SkiaSharp 排版模拟（复杂度极高）
- apply-style 相对简单，DocxCore 已有样式读写

**影响**：Toolkit 的 word skill 会多三个路由入口。

**优先级**：中低。render-preview 是复杂度最高的，建议最后做或考虑替代方案（用 LibreOffice headless 渲染）。

## 建议开发顺序（按 ROI 排序）

| 顺序 | 缺口 | 理由 | 估时 |
|------|------|------|------|
| 1 | Chart boxplot + histogram | ScottPlot 已有，纯加命令面和参数 spec | **DONE** |
| 2 | PDF merge + split | PdfPig/PDFium 已支持，实现直接 | **DONE** |
| 2 | PDF merge + split | PdfPig/PDFium 已支持，实现直接 | **DONE** |
| 3 | Excel style + formula | ClosedXML 已有，需要 CLI spec 设计 | **DONE** |
| 4 | Inspect official-check | 贴近现有 inspect 管线 | **DONE** |
| 5 | PPTX create | 命令面已注册，运行时已实现 BuildPptx | **DONE** |
| 6 | Word compare | diff 算法设计和实现 | **DONE** |
| 7 | Chart heatmap + radar | ScottPlot 已有，纯加命令面和参数 spec | **DONE** |
| 8 | PDF ocr-pdf | PDFium 渲染 + PdfOcrRecognizerAdapter 调用 nong-ocr local 子进程嵌入 OCR 文本层 | **DONE** |
| 9 | Excel pivot | ClosedXML 透视表 API 较复杂 | **DONE** |
| 10 | Word render-preview | LibreOffice DOCX→PDF→PNG pipeline, reuse existing soffice detection + nong-pdf render | **DONE** |

## 新增命令的标准流程

每个新命令都走：
1. 审计底层库（确定能做什么，不能做什么）
2. 设计 CLI 命令参数和输出 spec
3. 实现命令（`Cli/Commands/` 新命令类 + 注册到 `Program.cs`）
4. 注册到 `Cli/Common/Manifest.cs`
5. 写测试（命令参数解析 + 输出契约）
6. 更新 Toolkit 对应 skill（如果该模块有 CLI 命令变化）
7. 写 changelog
8. `nong commands --json` 验证命令面正确

## 不做的事

- 不让 `chart analyze` 的语义继续膨胀（analyze 是 ANOVA+Duncan+描述统计的一站式，不要往里塞箱线图）
- 不让 `excel create` 的语义扩大（create 就是简单 JSON 到 xlsx，不做样式和公式）
- 不让 `pdf dissect` 承担写入语义
- 不让 `pptx read` 改名来"装作"能写

## 和包依赖的关系

- 所有新命令都在现有包结构内实现，不引入新 .csproj
- Chart 新图种不引入新 NuGet 包（ScottPlot 已在 ThirdParty 中）
- PDF edit 不引入新 NuGet 包（PdfPig 已在 ThirdParty 中）
- PPTX create 不引入新 NuGet 包（Open XML SDK 已在 ThirdParty 中）
- OCR PDF 可能需要在 MultiModal 和 Pdf 之间建 adapter（CLI 层协调，不引入循环依赖）

## 状态

plan — 10 项缺口全部 DONE。
