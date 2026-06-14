# OOXML Deep Audit -- nong word 命令覆盖分析

日期: 2026-06-12
审计范围: DocxCore (Docx/) + Cli/Commands/WordCommands.cs
审计目标: 逐元素比对 nong word 对 OOXML 属性的覆盖程度

---

## 1. Coverage Table

### 1.1 Paragraph Properties (w:pPr)

| OOXML Element | Word UI Name | Covered By | Coverage Level | Notes |
|---|---|---|---|---|
| keepNext | 与下段同页 | word academic-format, word format-gongwen, word compact-tables | partial | 三个命令都写入 keepNext；但只有 Heading/Title/Caption 段落写入，无通用 set/unset CLI |
| keepLines | 段中不分页 | word compact-tables | partial | compact-tables 保留已有 keepLines 但不主动生成；无独立命令 |
| pageBreakBefore | 段前分页 | ParagraphBuilder.PageBreakBefore() | partial | Builder 有方法，但写作者 NEW DOCX 用；无修改已有文档的 CLI |
| widowControl | 孤行控制 | (none) | none | StyleRebuilder 显式清除；无命令设置或保留 |
| ind:firstLine | 首行缩进 | word academic-format, word format-gongwen, word format-audit, StyleRebuilder | full | 格式化写 480/200/420 twips；审核读且报告 |
| ind:hanging | 悬挂缩进 | word academic-format (List role) | partial | 仅 List 角色写入 hanging；无通用 set |
| ind:left | 左缩进 | word format-audit (读) | partial | 审核读 left/hanging 用于判断 list indent；无命令写 left |
| ind:right | 右缩进 | (none) | none | 不读也不写 |
| spacing:before | 段前间距 | word academic-format, word format-gongwen, word format-audit (读) | full | 格式化设置具体值；审核读且统计 |
| spacing:after | 段后间距 | word academic-format, word format-gongwen, word format-audit (读) | full | 同上 |
| spacing:line | 行距值 | word academic-format, word format-gongwen, word format-audit, word rebuild | full | 格式化设置 atLeast 值；审核统计分布 |
| spacing:lineRule | 行距规则 | word academic-format, word rebuild, word format-audit | full | atLeast/auto/exact 均处理 |
| jc (alignment) | 对齐方式 | word academic-format, word format-gongwen, word format-audit | full | both/center/left/right/distribute 均处理 |
| outlineLvl | 大纲级别 | word format-gongwen (读), word academic-format (读) | partial | 阅读时有判断；无独立设置命令 |
| autoSpaceDE | 自动调整中日文间距 | (none) | none | ElementOrder 认识此元素名但只做顺序纠正 |
| contextualSpacing | 忽略相同样式的段间距 | (none) | none | 同上 |
| snapToGrid | 对齐文档网格 | word academic-format | partial | 格式化时在 pPr/rPr 上显式移除；无设置命令 |

### 1.2 Run Properties (w:rPr)

| OOXML Element | Word UI Name | Covered By | Coverage Level | Notes |
|---|---|---|---|---|
| rFonts:eastAsia | 中文字体 | word academic-format, word format-gongwen, word fonts, word format-audit | full | 格式化写宋体/黑体；审核读且报告；fonts 统计 |
| rFonts:ascii | 西文字体 | word academic-format, word format-gongwen, word fonts, word format-audit | full | 同上 Times New Roman |
| rFonts:hAnsi | 高ANSI字体 | word academic-format, StyleRebuilder | full | 与 ascii 同步设置 |
| rFonts:complexScript | 复杂文种字体 | StyleRebuilder | partial | 仅 rebuild 设置；无独立命令 |
| bold | 加粗 | word academic-format, word format-gongwen, word format-audit | full | 写加粗/不加粗；审核判断 |
| italic | 斜体 | word academic-format, word format-audit | full | 拉丁学名斜体化；审核检测 |
| underline | 下划线 | (none) | none | 不读不写。公文/学术论文不需要 |
| strikethrough | 删除线 | (none) | none | 不读不写。公文/学术论文不需要 |
| fontSize | 字体大小 | word academic-format, word format-gongwen, word format-audit, StyleRebuilder | full | 写具体 half-point 值；审核统计 |
| fontSizeComplexScript | 复杂文种字号 | StyleRebuilder | partial | 仅 rebuild 设置；无独立命令 |
| color | 字体颜色 | (none) | none | 不读不写 |
| highlight | 字体高亮 | (none) | none | 不读不写 |
| characterSpacing | 字符间距 | (none) | none | 不读不写 |
| superscript | 上标 | ParagraphBuilder.Sup() | partial | Builder 有方法，仅写作者 NEW DOCX |
| subscript | 下标 | word academic-format (化学式) | partial | 化学式数字下标；审核检测漏标；无通用 set |

### 1.3 Table Properties (w:tblPr + w:trPr + w:tcPr)

| OOXML Element | Word UI Name | Covered By | Coverage Level | Notes |
|---|---|---|---|---|
| tblW | 表格宽度 | word compact-tables, word academic-format | full | compact-tables 设 100%pct；academic-format 设 5000 |
| tblHeader | 标题行重复 | word academic-format, word format-audit | full | 三线表 header row 加 tblHeader；审核检测 |
| tblCellMar | 单元格默认边距 | word academic-format | partial | 三线表统一 80/120 dxa；无通用 set |
| tblLook | 表格样式条件 | word academic-format (移除) | partial | 仅移除；无设置命令 |
| tblInd | 表格缩进 | (none) | none | 不读不写 |
| tblLayout | 表格布局 | word academic-format (Fixed) | partial | 仅 Fixed；无 AutoFit 选项 |
| trHeight (hRule=exact/atLeast/auto) | 行高 | word compact-tables | full | compact-tables 将 exact→atLeast；审核无 trHeight |
| cantSplit | 行不跨页拆分 | word compact-tables | partial | 对多行内容行加 cantSplit；无 set/unset CLI |
| tcW | 列宽 | word compact-tables, word academic-format | full | compact-tables 均等列宽 pct/dxa；academic-format 均等 pct |
| gridSpan | 合并单元格 | word table-reflow | partial | table-reflow 读取列数时计算 span；无 set/unset CLI |
| vMerge | 垂直合并单元格 | (none) | none | 不读不写 |
| tcBorders | 单元格边框 | word academic-format | partial | 仅 header bottom border 0.75pt；无完整 set |
| shd (cell shading) | 单元格底纹 | word academic-format (移除), word format-audit (检测) | partial | 仅移除/检测；无设置命令 |
| tcMar | 单元格边距 | word academic-format | partial | 三线表 80/120 dxa；无通用 set |
| vAlign | 垂直对齐 | word academic-format (Center) | partial | 仅 Center；无 top/bottom 切换 |

### 1.4 Section Properties (w:sectPr)

| OOXML Element | Word UI Name | Covered By | Coverage Level | Notes |
|---|---|---|---|---|
| pgSz:width/height | 纸张大小 | word academic-format, SectionBuilder, word dissect | full | 写 A4 11906x16838；dissect 读 pgSz 尺寸 |
| pgSz:orient | 纸张方向 | SectionBuilder (Portrait) | partial | 仅纵向；无横向设置 CLI |
| pgMar (所有) | 页边距 | word academic-format, SectionBuilder | full | 写 2.54cm 四边；builder 支持 cm/mm/twips |
| cols | 分栏 | (none) | none | 不读不写 |
| headerReference | 页眉引用 | SectionBuilder, HeaderFooterBuilder | full | builder 支持 setForSection；无修改已有文档的独立 CLI |
| footerReference | 页脚引用 | SectionBuilder, HeaderFooterBuilder | full | 同上 |
| titlePg | 首页不同 | SectionBuilder.DifferentFirstPage() | partial | Builder 有方法；无独立 CLI |
| pgBorders | 页面边框 | (none) | none | 不读不写 |
| pgNumType | 页码格式 | word format-gongwen (页脚 page number field) | partial | 公文通过 PAGE field 加页脚实现；无 pgNumType 直接设置 |
| docGrid | 文档网格 | word academic-format (移除), word format-audit (检测) | partial | 格式化时移除 docGrid；审核检测并 warning |

### 1.5 Image Properties

| OOXML Element | Word UI Name | Covered By | Coverage Level | Notes |
|---|---|---|---|---|
| wp:extent (cx/cy) | 图片显示尺寸 | word fit-images, word regroup-images, word images --crop | full | fit-images 缩放 extent；crop 修裁剪后的 extent |
| wp:effectExtent | 效果扩展 | (none) | none | 不读不写 |
| wrap types (square) | 四周型环绕 | (none) | none | 不读不写。所有图片操作假定 inline |
| wrap types (topAndBottom) | 上下型环绕 | (none) | none | 同上 |
| wrap types (tight) | 紧密型环绕 | (none) | none | 同上 |
| wrap types (through) | 穿越型环绕 | (none) | none | 同上 |
| image border | 图片边框 | (none) | none | 不读不写 |

---

## 2. Gap Summary

| Coverage Level | Count | Percentage |
|---|---|---|
| full | 20 | 39.2% |
| partial | 18 | 35.3% |
| none | 13 | 25.5% |
| **Total** | **51** | 100% |

按类别分:

| Category | full | partial | none |
|---|---|---|---|
| Paragraph Properties (15) | 5 | 6 | 4 |
| Run Properties (14) | 7 | 3 | 4 |
| Table Properties (12) | 3 | 6 | 3 |
| Section Properties (9) | 3 | 4 | 2 |
| Image Properties (6) | 1 | 0 | 5 |

---

## 3. Priority Construction List

### P0 -- 中文文档基本功 (must fix)

**3.1 word format-gongwen -- 补全页面设置**
- 已有: 字体/字号/行距/首行缩进
- 缺口: pgNumType (页码格式)、titlePg (首页不同)、cols (分栏)、orient (横向)
- 建议: 扩展 `SetupSections` 方法，支持 `--orient landscape`、`--columns 2`、`--page-number roman` 等参数
- 工作量: 约 80 行 C#

**3.2 word table-reflow -- 补全 vMerge/gridSpan 处理**
- 已有: 行列拆分
- 缺口: gridSpan (合并单元格跨列)、vMerge (合并单元格跨行)
- 建议: reflow 前检测合并单元格，warning 跳过并在 continue-table 中保留 span 结构
- 工作量: 约 120 行 C#

### P1 -- 专业排版 (should fix)

**3.3 New command: word page-setup**
- OOXML: pgSz (orient), pgMar, cols, titlePg, pgNumType, docGrid, pgBorders
- Priority: P1
- 功能: 统一设置页面尺寸/边距/方向/分栏/首页不同/页码格式。可批量作用于所有 section 或指定 section index。
- 估算: 1 个新命令 + 1 个 DocxPageSetup 类，约 200 行 C#

**3.4 New command: word indent**
- OOXML: ind (firstLine/hanging/left/right)、outlineLvl
- Priority: P1
- 功能: 按段落角色或 blockId 设置缩进和大纲级别。必用参数: `--first-line`、`--left`、`--hanging`、`--outline-level`
- 估算: 1 个新命令 + 1 个 DocxIndenter 类，约 150 行 C#

**3.5 word format-audit -- 补全缺失元素检测**
- 已有: 字体/字号/对齐/行距/三线表/拉丁名/化学式
- 缺口: widowControl、ind:right、tblInd、wrap type (非 inline)、tblLook
- 建议: 在 ParagraphFormat 和 table audit 中添加这些字段的检测，报告 "unexpected_property" warning
- 工作量: 约 100 行 C#

**3.6 word compact-tables -- 补全 trHeight:auto**
- 已有: exact→atLeast
- 缺口: 无法将 atLeast→auto (让行高完全由内容决定)
- 建议: 添加 `--auto-height` flag
- 工作量: 约 30 行 C#

### P2 -- 锦上添花 (nice to have)

**3.7 New command: word paragraph-control**
- OOXML: keepNext, keepLines, pageBreakBefore, widowControl
- Priority: P2
- 功能: 按 blockId 设置段落分页控制属性。适用: 表格标题与表格密不可分、关键标题不得孤行、显式分页。
- 估算: 1 个新命令 + ParagraphControl 方法，约 120 行 C#

**3.8 New command: word image-wrap**
- OOXML: wrap types (square/topAndBottom/tight/through)、effectExtent
- Priority: P2
- 功能: 将 inline 图片改为浮动图片，设置环绕方式。需要操作 wp:anchor 替代 wp:inline。
- 估算: 1 个新命令 + DocxImageWrap 类，约 200 行 C#

**3.9 New command: word cell-format**
- OOXML: tcBorders, shd, tcMar, vAlign, vMerge, gridSpan
- Priority: P2
- 功能: 按 table index + cell (row,col) 设置单元格格式。合并/拆分、底纹、边框、垂直对齐。
- 估算: 1 个新命令 + DocxCellFormatter 类，约 250 行 C#

**3.10 New command: word run-format**
- OOXML: underline, strikethrough, color, highlight, characterSpacing, superscript, subscript
- Priority: P2
- 功能: 按 blockId + run index 设置字符级格式。非学术论文常用场景（审阅标注、排版修正）。
- 估算: 1 个新命令 + DocxRunFormatter 类，约 200 行 C#

---

## 4. Work Estimate

### 总新增

| Item | Count |
|---|---|
| 新命令 | 6 (page-setup, indent, paragraph-control, image-wrap, cell-format, run-format) |
| 扩展现有命令 | 4 (format-gongwen, table-reflow, format-audit, compact-tables) |
| 新 DocxCore 类 | 6 (DocxPageSetup, DocxIndenter, ParagraphControl, DocxImageWrap, DocxCellFormatter, DocxRunFormatter) |
| 扩展现有类 | 3 (GongWenFormatter, DocxTableCompactor, WordFormatAuditor) |

### 代码量估算

| Priority | 新增 C# 行数 |
|---|---|
| P0 (2 items) | ~200 |
| P1 (4 items) | ~480 |
| P2 (4 items) | ~770 |
| **Total** | **~1,450** |

### 附加工作

- 每个新命令需要 SKILL.md 补全、1-2 个 example、至少 1 个 eval
- 每个新 DocxCore 类需要单元测试覆盖核心路径
- Manifest.cs 补全新的 CommandInfo 条目

### 建议实施顺序

1. P0: format-gongwen 页面设置补全 + table-reflow 合并单元格处理
2. P1: page-setup + indent 两个新命令最通用，优先级最高
3. P1: format-audit + compact-tables 小补丁
4. P2: 按需求驱动，每季度处理 1-2 个
