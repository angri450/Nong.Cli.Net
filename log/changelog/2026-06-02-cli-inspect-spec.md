# nong inspect CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong inspect` 是内容审查+写作层（Angri450.Nong.Inspect）的 CLI 入口。覆盖论文诊断、论文写作、公文写作、信函写作、参考文献管理五类操作。

底层实现：调用 Nong.Inspect 命名空间中的 PaperTypeClassifier、PaperStructureExtractor、PaperDiagnostics、ReferenceAnalyzer、VariablePlanGenerator、PaperWriter、OfficialDocWriter、LetterWriter、ReferenceManager、Gbt7714Style 等类。

所有命令支持 `--json` 输出结构化结果。

---

## Inspect 包 12 个文件 → CLI 命令映射

### 1. PaperModels.cs（12 个数据模型）

纯数据类（PaperTypeInfo、PaperStructure、ReferenceEntry、EvidenceChainItem、QualityDiagnosis 等），不独立暴露为命令。被其他命令内部使用。

---

### 2. PaperTypeClassifier.cs（论文类型分类器）

16 种论文类型关键词匹配。一个文件进去，类型+匹配度出来。

对应命令：

#### `nong inspect classify <file>`
输入：论文文本文件（.txt）
输出：
```
问卷调查型论文       匹配度: 75%  关键词: 问卷, 量表, 信度
实验研究型论文       匹配度: 30%
...
推荐数据: 问卷原始数据、题项...
推荐方法: 信度、效度、描述统计...
```

实现：PaperTypeClassifier.Classify(text)，`--json` 返回 List<PaperTypeInfo>

#### `nong inspect top-type <file>`
只返回匹配度最高的类型名称（一行文字）。

实现：PaperTypeClassifier.TopType(text)

---

### 3. PaperStructureExtractor.cs（论文结构提取器）

从纯文本中识别标题层级、摘要、关键词、章节边界、引用行号。

对应命令：

#### `nong inspect structure <file>`
输入：论文文本文件（.txt）
输出：
```
标题: 枯草芽孢杆菌发酵工艺优化研究
作者: 张三, 李四（线索）
摘要: 本文以枯草芽孢杆菌...（共 234 字）
关键词: 枯草芽孢杆菌, 发酵, 响应面
章节:
  1  引言 ............... 第 1-24 行
  2  材料与方法 ......... 第 25-68 行
    2.1  菌株与培养基 ... 第 26-38 行
  3  结果与分析 ......... 第 69 行
参考文献起始行: 第 156 行
```

实现：PaperStructureExtractor.BuildPaperStructure(text)，`--json` 返回 PaperStructure

---

### 4. PaperDiagnostics.cs（论文质量诊断器）

六项诊断：证据链 10 项、数据需求 9 项、缺口等级 A-E、图表建议 7 种、语义诊断 7 项、综合质量诊断。

对应命令：

#### `nong inspect diagnose <file>`
完整诊断管线。输入论文文本，输出综合质量报告。

输出：
```
=== 论文类型 ===
实验研究型论文（匹配度 75%）

=== 证据链（2/10 不充分） ===
[充分]   研究问题明确性
[不充分] 样本特异性 — 未说明样本量和抽样方法
[充分]   概念清晰性
...

=== 数据需求（3/9 不充分） ===
[充分]   有数据支撑
[不充分] 数据来源清晰 — 未声明数据采集时间
...

=== 缺口等级 ===
C 级 — 数据不足，需补充采集

=== 质量诊断 ===
致命问题: 1 项
结构问题: 2 项
表面问题: 3 项

=== 图表建议 ===
方法部分: 实验流程图
结果部分: 柱状图（组间比较）
```

实现：依次调用 Classify → DiagnoseEvidenceChain → DiagnoseDataRequirements → DiagnoseGapGrade → DiagnosePaperQuality → DiagnoseResearchDesignSemantics → RecommendChartsAndTables

#### `nong inspect evidence <file>`
仅证据链诊断（10 项检查，每项输出充分/不充分+修改建议）。

实现：PaperDiagnostics.DiagnoseEvidenceChain()

#### `nong inspect data-req <file>`
仅数据需求诊断（9 项检查）。

实现：PaperDiagnostics.DiagnoseDataRequirements()

#### `nong inspect gap <file>`
缺口等级评定（A-E），输出等级+判断标准+是否可继续分析。

实现：PaperDiagnostics.DiagnoseGapGrade()

#### `nong inspect semantics <file>`
语义诊断（因果语言/相关因果混淆/机制声称/贡献夸大 等 7 项）。

实现：PaperDiagnostics.DiagnoseResearchDesignSemantics()

---

### 5. ReferenceAnalyzer.cs（参考文献分析器）

解析参考文献块、提取条目、检查格式风险、交叉验证正文引用。

对应命令：

#### `nong inspect refs <file>`
输入：论文文本文件（.txt）
输出：
```
=== 参考文献列表（共 32 条） ===
[1] Smith J. ...  格式风险: 缺少 DOI
[2] 张三. ...      格式正常
[5] Wang L. ...   格式风险: 年份缺失
...

=== 正文引用（共 28 处） ===
[1] [2] [3] [4] [5] ... [28]

=== 未匹配引用 ===
正文引用 [15] 在参考文献列表中不存在
参考文献 [29] 未在正文中被引用

=== 文献检索策略建议 ===
CNKI: 枯草芽孢杆菌 + 发酵 + 响应面
WoS: Bacillus subtilis + fermentation + response surface
```

实现：ExtractReferences() + CheckReferenceRisks() + ExtractInlineCitations() + BuildLiteratureSearchStrategy()

#### `nong inspect cite-check <file>`
仅交叉验证：正文引用和参考文献列表是否一一对应。

---

### 6. VariablePlanGenerator.cs（变量操作化表生成器）

从论文文本中提取变量候选，生成 12 列标准变量操作化表，根据论文类型推荐数据采集方案。

对应命令：

#### `nong inspect varplan <file>`
输入：论文文本文件（.txt）
输出：
```
=== 变量操作化表 ===
变量名称 | 中文标签 | 变量角色 | 理论含义 | 操作化方式 | ...
---------|---------|---------|---------|-----------|----
OD600    | 菌液吸光度 | 因变量 | 细菌生长量 | 分光光度计 | ...
pH       | pH值 | 自变量 | 酸度 | pH计 | ...
...

=== 数据采集方案 ===
类型: 实验研究
方案:
  1. 实验组/控制组设置
  2. 前测/后测时间点
  3. ...
```

实现：GenerateVariablePlan(text, type) + GenerateDataCollectionPlan(type, vars)

---

### 7. PaperWriter.cs（论文写作器）

链式 API 生成完整论文。Title→Abstract→Heading→Body→References。

对应命令（从 JSON 规格生成 docx）：

#### `nong inspect write paper <spec> [-o <file>]`
从 JSON 规格生成完整论文 docx。

JSON 规格：
```json
{
  "template": "journal-paper",
  "title": "论文标题",
  "englishTitle": "English Title",
  "abstract": "摘要内容...",
  "keywords": "kw1; kw2; kw3",
  "englishAbstract": "Abstract...",
  "englishKeywords": "kw1; kw2",
  "sections": [
    { "heading": "引言", "level": 1, "body": ["段落1[@smith2024]", "段落2"] },
    { "heading": "材料与方法", "level": 1, "body": ["..."] }
  ],
  "references": [
    { "key": "smith2024", "type": "article", "author": ["Smith J."], "title": "...", "journal": "...", "year": 2024 }
  ],
  "figures": [
    { "caption": "实验流程", "image": "fig1.png" }
  ],
  "tables": [
    { "caption": "发酵参数", "headers": ["参数","值"], "rows": [["pH","7.0"]] }
  ]
}
```

实现：读 template JSON → Gbt7714Style.BuildAll → 创建 docx → new PaperWriter(body, doc) → 按 spec 调用 Title/Abstract/Heading/Body/Figure/Table → SetReferenceDatabase → AutoReferences → .Save()

---

### 8. Gbt7714Style.cs（GB/T 7714 样式预设）

硬编码的学术论文样式和编号格式。

对应命令：被 `nong inspect write paper` 内部调用，不独立暴露。

---

### 9. ReferenceManager.cs（参考文献管理器）

[@key] 引用键解析、自动编号、GB/T 7714 格式化、缺失引用检测。

对应命令：被 Body() 和 AutoReferences() 内部调用。独立暴露两个辅助命令：

#### `nong inspect refs resolve <text> --db <json>`
仅做引用键解析，把一段文本中的 [@key] 替换为 [N]，输出替换后的文本。

#### `nong inspect refs generate <keys> --db <json>`
根据键列表 + 数据库，生成格式化参考文献列表。

---

### 10. ReferenceModels.cs（RefEntry 数据模型）

纯数据模型类，不暴露为命令。

---

### 11. OfficialDocWriter.cs（公文写作器）

链式 API：红头→发文字号→标题→主送→正文→结束语→署名→日期。

对应命令：

#### `nong inspect write official <spec> [-o <file>]`
从 JSON 规格生成公文 docx。

JSON 规格：
```json
{
  "template": "official-notice",
  "redHeader": "XX市农业农村局文件",
  "docNumber": "X农发〔2026〕1号",
  "title": "关于开展2026年度农业科技项目申报的通知",
  "recipient": "各区县农业农村局：",
  "body": ["为贯彻落实...", "现将有关事项通知如下："],
  "closing": "特此通知。",
  "signature": "XX市农业农村局",
  "date": "2026年6月2日"
}
```

实现：读 template JSON → new OfficialDocWriter(body, doc) → 按 spec 调用各方法 → .Save()

---

### 12. LetterWriter.cs（信函写作器）

链式 API：日期→收信人→事由→正文→敬语→署名。

对应命令：

#### `nong inspect write letter <spec> [-o <file>]`
从 JSON 规格生成信函 docx。

JSON 规格：
```json
{
  "template": "business-letter",
  "date": "2026年6月2日",
  "recipient": "XX大学科技处",
  "subject": "关于合作开展枯草芽孢杆菌研究的函",
  "body": ["我院拟与贵校...", "具体合作方案如下：..."],
  "closing": "顺颂 商祺",
  "signature": "XX省农业科学院"
}
```

实现：读 template JSON → new LetterWriter(body, doc) → 按 spec 调用各方法 → .Save()

---

## 命令总数

| 类别 | 命令 | 数量 |
|------|------|------|
| 分析-分类 | classify, top-type | 2 |
| 分析-结构 | structure | 1 |
| 分析-诊断 | diagnose, evidence, data-req, gap, semantics | 5 |
| 分析-引文 | refs, cite-check | 2 |
| 分析-变量 | varplan | 1 |
| 参考文献 | refs resolve, refs generate | 2 |
| 写作-论文 | write paper | 1 |
| 写作-公文 | write official | 1 |
| 写作-信函 | write letter | 1 |
| **合计** | | **16** |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| classify | P0 | 论文类型分类，基础能力 |
| structure | P0 | 结构提取，基础能力 |
| diagnose | P0 | 完整诊断管线，核心能力 |
| refs | P0 | 参考文献检查 |
| varplan | P1 | 变量计划 |
| write paper | P1 | 论文生成 |
| evidence/data-req/gap/semantics | P1 | 独立诊断项 |
| top-type | P1 | 便捷命令 |
| cite-check | P2 | 引文交叉验证 |
| refs resolve/generate | P2 | 独立引用解析 |
| write official | P2 | 公文生成 |
| write letter | P2 | 信函生成 |
