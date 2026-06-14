# Stage 14 Gate Report — 开阶段 15 前置检查

日期：2026-06-04
状态：PASS — 可以开阶段 15

---

## 1. Build + Tests

### Build
```
dotnet build Cli/NongCli.csproj -c Release
→ 已成功生成。0 个警告。0 个错误。
```

### Contract Tests
```
dotnet test Cli.Tests/Cli.Tests.csproj -c Release
→ 已通过! 失败:0，通过:24，已跳过:0，总计:24，持续时间: 1m32s
```

### 命令数
```
nong commands --json        → 46 implemented
nong commands --all --json   → 46 implemented (无 stub)
```

---

## 2. 6 项审计修复验证

| # | 级别 | 修复项 | 验证结果 |
|---|------|--------|---------|
| 1 | HIGH | word validate 改用 ValidationErrorType 枚举 | 代码用 `Schema/Semantic/Package` 枚举，不再字符串匹配。缺失文件返回 E001 |
| 2 | MEDIUM | ocr cloud --format 移除 | `ocr cloud --help` 不含 `--format`，handler 签名不含 format 参数 |
| 3 | MEDIUM | ocr local 契约统一 | Manifest status=implemented，命令返回 E005（dependency_missing），EXIT:1 |
| 4 | MEDIUM | pptx read/slides 使用 SlideIdList | PptxReader.cs 两处均用 `SlideIdList.ChildElements.OfType<SlideId>()` 遍历 |
| 5 | MEDIUM | word merge 文档说明 | DocxAnalysis.cs MergeDocx 有 XML doc 标注"Shallow merge — copies body elements only" |
| 6 | LOW | release-checklist + AGENT.md 同步 | 两份文件均写 46 implemented，AGENT.md 含新命令输入格式和 spec 示例 |

---

## 3. Security Scan

执行 `nong skill scan <repo-root> --json`：

| 等级 | 数量 | 分析 |
|------|------|------|
| Critical | 0 | — |
| High | 17 | 全部为文档路径引用（ABSOLUTE_USER_PATH 在 changelog/log 中）、第三方代码（ClippitPowerTools 作者邮箱、ScottPlot CDN）、P1 未激活代码（viewer.html innerHTML）、测试夹具（verify.ps1 故意植入的假邮箱） |
| Medium | 3 | — |
| Low | 401 | 主要为 ThirdParty 源码中的 HTTP URLs |

**结论**: 无真实密钥泄露。所有 HIGH 均可解释，无阻塞项。

---

## 4. Word 相关代码可复用点检查

### Docx/ 层 — 已暴露给 CLI

| 组件 | 文件 | CLI 命令 | 状态 |
|------|------|---------|------|
| WordTextReader | Docx/WordTextReader.cs | `word read` | implemented |
| WordPreview | Docx/WordPreview.cs | `word preview` | implemented |
| DocxTemplate.Fill | Docx/DocxTemplate.cs | `word fill` | implemented |
| StyleRebuilder | Docx/StyleRebuilder.cs | `word rebuild` | implemented |
| DocxAnalysis.* | Docx/DocxAnalysis.cs | `word stats/fonts/styles/validate/extract/dissect/merge` | implemented |

### Docx/ 层 — 未暴露给 CLI（可复用）

| 组件 | 文件 | 潜在 CLI 命令 | 能力 |
|------|------|-------------|------|
| MathRenderer | Docx/MathRenderer.cs | `word math` 或在 fill 中内联 | LaTeX→OMML 转换。RenderInline() 和 RenderDisplay() 已完整实现 |
| AdvancedFeatures | Docx/AdvancedFeatures.cs | `word merge` 升级 | AppendDocument() 已实现 deeper merge（含 part/relationship）；内容控件、修订模式、字体嵌入、文档保护 |
| ImageEmbedder | Docx/ImageEmbedder.cs | `word embed-images` | 将图片嵌入 docx，含标题和尺寸控制 |
| ImageHeaderReader | Docx/ImageHeaderReader.cs | 被动工具 | 读取图片尺寸（JPEG/PNG/GIF/BMP），已实现 |
| TocBuilder | Docx/TocBuilder.cs | `word toc` | 插入目录域代码、追加柱状图到文档 |
| TableStyles | Docx/TableStyles.cs | 被动资源 | 130 套表格样式预设（Dictionary），可被 fill 引用 |
| ElementOrder | Docx/ElementOrder.cs | `word rebuild` 增强 | OOXML 元素顺序修正 + 孤立边框修复 |

### Inspect/ 层 — 未暴露给 CLI

| 组件 | 文件 | 潜在 CLI 命令 | 能力 |
|------|------|-------------|------|
| GongWenFormatter | Inspect/GongWenFormatter.cs | `inspect format-gongwen` | GB/T 9704 公文格式化（Analyze→ApplyFormatting→SetupSections） |
| OfficialDocWriter | Inspect/OfficialDocWriter.cs | `inspect write-gongwen` | 公文生成（红头、发文字号、印章位） |
| LetterWriter | Inspect/LetterWriter.cs | `inspect write-letter` | 信函生成 |

### MultiModal/ 层 — 已有边界

| 组件 | 文件 | 当前状态 |
|------|------|---------|
| PaddleOcrVlClient | MultiModal/PaddleOcrVlClient.cs | `ocr cloud` 使用，需 PADDLEOCR_TOKEN |
| LocalOcrClient | MultiModal/LocalOcrClient.cs | `ocr local` 返回 E005（运行时不可用） |
| LayoutToWordConverter | MultiModal/LayoutToWordConverter.cs | OCR 结果→Word 转换 |
| ImageAnalyzer | MultiModal/ImageAnalyzer.cs | 图片布局分析（ContentRegion, ImageLayout），未暴露 |

### 阶段 15 建议优先复用

1. `MathRenderer` → 当前能力最高、差异最大，可支撑公式渲染
2. `AdvancedFeatures.AppendDocument` → 可升级当前 shallow merge
3. `GongWenFormatter` → GB/T 9704 公文格式化完整管线已就绪
4. `ImageAnalyzer` → 已实现但无 CLI 入口

---

## 5. 结论

| 检查项 | 结果 |
|--------|------|
| Build (0 errors, 0 warnings) | PASS |
| Tests (24/24) | PASS |
| 6 audit fixes verified | PASS |
| Security scan (0 real secrets) | PASS |
| Word code reuse potential mapped | PASS |

**阻塞项**: 无。

**可以开阶段 15**。

阶段 15 应优先利用已发现的未暴露能力（MathRenderer、GongWenFormatter、AdvancedFeatures.AppendDocument），避免从零造轮子。
