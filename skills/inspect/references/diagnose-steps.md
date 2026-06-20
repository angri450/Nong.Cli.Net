# Diagnose 的 7 步诊断详解

`inspect diagnose` 是论文全质量诊断的一站式入口。它按顺序执行以下 7 个步骤，逐个给出判断。

## 7 步诊断

### 1. classify — 论文类型分类
判断论文属于 16 种类型中的哪一种：研究论文、综述、简报、案例报告、方法论文等。决定了后续诊断的标准。

### 2. structure — 结构提取
识别论文的章节结构：IMRaD（引言/方法/结果/讨论）标准结构，还是其他结构。标记缺失章节。

### 3. evidence — 证据链诊断
追踪论文的证据链完整性：每个结论是否有数据支撑、变量是否可追溯、统计方法是否正确。

### 4. refs — 引用风险检查
检查内部引用的质量：引用是否缺失、格式是否一致、年份是否矛盾、是否有"幽灵引用"（引用列表中不存在的文献）。

### 5. varplan — 变量操作化
检查变量定义是否清晰：自变量和因变量是否明确、测量方法是否描述、控制变量是否列出。

### 6. data-req — 数据需求诊断
每个变量需要什么数据、数据采集是否合理、样本量是否足够、缺失数据如何处理。

### 7. gap — 缺口等级评估
综合以上 6 步，给出论文的总体缺口等级：严重缺口（需要补充实验）、中等缺口（需要补充分析或引用）、轻微缺口（格式或描述即可修复）。

## 使用

```powershell
nong inspect diagnose paper.txt --json
```

输出 `gapGrade` 和 7 个维度的详细诊断结果。

## 关键点

- `inspect diagnose` 是一站式总诊断。如果只需要某一项，用单步命令：`inspect classify`、`inspect structure`、`inspect refs`、`inspect variables`、`inspect data-requirements`、`inspect evidence`、`inspect gap`。
- 诊断输入是纯文本（`.txt`），不是 `.docx`。如果是 docx，先 `word read paper.docx` 提文本。
- diagnose 不负责修复——它只告诉你问题在哪。修复需要走 `word` skill 的格式命令或者用户手工修改。
