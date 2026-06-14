# 用户反馈：三个核心问题 + 三个次要问题

Date: 2026-06-13
来源: 用户测试反馈（概念验证大赛 + 专利申请文档处理）

## 核心问题 1: nong word fill 无占位符模板无法填充

**现象**: 模板 14 张表，单元格为空或提示文字，无 {{tag}} 占位符。`nong word fill` 返回 status:ok 但零填充。

**诊断**:
- `DocxTemplate.Fill()` 依赖 `{{tag}}` 占位符定位
- `GenerateTableRows` 只能处理含占位符的表格行
- 没有按表格索引+行列位置直接填值的模式

**修复** (`DocxTemplate.cs`):
- 新增 `FillTablesByPosition`: 检测 data 中顶层 `"tables"` key
- JSON schema: `{"tables": [{"index": 0, "rows": [{"cells": ["A1","B1"]}]}]}`
- 先处理 tables → 移除 key → 再走原来的标签替换流程
- 新增 `SetCellText`: 清空单元格并填入纯文本

---

## 核心问题 2: NongMark 不支持编排字体

**现象**: `nong word create` 生成 DOCX 全部默认字体(宋体+Times New Roman+10.5pt)。承诺函等需要格式区分的段落必须退回 OOXML 操作。

**诊断**:
- `NongRunFormat` 已有 `fontEastAsia`、`fontAscii`、`fontSizePt` 属性(JSON 层面)
- `NongMarkDocumentBuilder.MakeRun()` 硬编码字体和字号
- NongMark 文本语法无字体属性能力

**修复** (`NongMarkDocumentBuilder.cs`):
- 新增实例字段: `_fontEastAsia`、`_fontAscii`、`_fontSizeHalfPt`(默认宋体+TNR+21=10.5pt)
- `MakeRun` 从 static 改为实例方法，读取当前字体状态
- `AppendBlock` paragraph case 调用 `ApplyParagraphAttrs(attrs)` 解析 font/size
- `ResetParagraphState()` 在段落结束后恢复默认值
- 语法: `::: paragraph {font="仿宋_GB2312" fontAscii="Arial" size=16}`
- 支持: font(中文字体)、fontAscii(西文字体)、size(pt)、sizeHalfPt(半点数)

---

## 核心问题 3: OCR preflight 过于激进

**现象**: 考试试卷图片(文字密度高)被判定为 QR/code 图形，需 --force 才能跑。两张不同试卷都触发误判。

**诊断**:
- `LooksLikeQrOrCodeGraphic` 的 graphicRatio>=0.60 阈值过低
- `LooksLikeGraphicOnlyImage` 和 `LooksLikeQrOrCodeGraphic` 都是 blocking
- 二者 combined 拦截了所有密集文字内容的试卷

**修复** (`LocalOcrInputPreflight.cs` + `OcrCommands.cs`):
- `LooksLikeQrOrCodeGraphic`: 阈值收紧 (graphicRatio 0.60→0.70, largestRatio 0.65→0.72, Regions 12→8)
- 启发式检测从 blocking 改为 warning: 标记 Classification+Recommendation 但不设 ShouldSkip
- CLI handler 打印 preflight warning 到 stderr 后继续 OCR 推理
- 仅 ZXing 解码成功的条码保持 ShouldSkip=true（需要 --force 跳过）

---

## 次要问题 1: OCR 推理 stdout 噪音

**现象**: PaddleInference 打印 ~100 行 "ReduceMeanCheckIfOneDNNSupport" 到终端。

**诊断**:
- 原 `ConfigureNativeLogEnvironment()` 在 `EnsureNativeRuntimeLoaded()` 内部调用 → native DLL 已加载后才设环境变量，时序错误
- 缺少 `GLOG_v=0`、`FLAGS_v=0`、`PADDLE_DISABLE_SIGNAL_HANDLER=1`

**修复** (`PpOcrV6Client.cs`):
- 静态构造函数中调用 `ConfigureNativeLogEnvironment()` → 类型加载时即设环境变量
- 新增 GLOG_v、FLAGS_v、PADDLE_DISABLE_SIGNAL_HANDLER 抑制项
- 移除 EnsureNativeRuntimeLoaded 和 CreateEngine 中的冗余调用

---

## 次要问题 2: PDF 图片解码

**现象**: 20/24 张图片解码失败，线框图无法提取。

**状态**: 留待后续 PDF 模块专项优化，不在本迭代范围。

---

## 次要问题 3: check-env meta.version 误报

**现象**: check-env 返回 meta.version=4.1.2，实际运行 4.1.4。

**诊断**: `CliVersion.Current` 硬编码 "4.1.2"。

**修复**: bump `CliVersion.Current` 到 "4.1.5"。

---

## 状态

已修复 (nong-ocr 4.1.5) — 5/6 issues resolved
