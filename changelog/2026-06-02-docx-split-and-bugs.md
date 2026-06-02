# Docx 拆包 + 6 个报错诊断

日期：2026-06-02
影响包：Angri450.Nong.Docx, Angri450.Nong.Genre（新建）
变更类型：架构重构 + Bug 修复

---

## 变更概要

1. Docx 瘦身为纯引擎（3.0.2 → 3.1.0）
2. 新建 Genre 文体包（3.0.0）

---

## 6 个报错诊断

### 1. [HIGH] 中文弯引号导致 CS1003 — 非包问题

**关键词**：Write 工具、中文引号归一化、U+201C/U+201D → U+0022

**判定**：这是 Claude Code 的 Write 工具问题，不是 NuGet 包的问题。Write 工具在写入文件时可能将 Unicode 弯引号转为 ASCII 直引号，导致 C# 编译器将字符串中的引号误认为字符串终结符。

**归属**：Claude Code 工具链。包层面无法修复——即使包代码完全不涉及字符串处理。

**Workaround**：在 Program.cs 中避免在 C# 字符串字面量内使用 "" (U+201C/U+201D)，改用「」或直接避免引号。

---

### 2. [MEDIUM] format JSON 缺少 lineRule 字段 — Docx 包可修

**关键词**：StyleBuilder.BuildFromJson、lineRule、固定行距

**原因**：`StyleBuilder.BuildFromJson` 的 `S()` 方法硬编码 `LineRule = LineSpacingRuleValues.Auto`。

**归属**：Docx 包。`StyleBuilder.cs:BuildFromJson` 需要增加对 `lineRule` 字段的解析。

**修复方案**：在 JSON 解析逻辑中增加 lineRule 可选字段（"auto"/"exact"/"atLeast"），默认 "auto" 保持向后兼容。

**受影响的包**：`Angri450.Nong.Docx`（StyleBuilder 位于此包）。

---

### 3. [LOW] Body() 逐字 w:t 切分 — Genre 包可修

**关键词**：DocumentWriter.Body、Regex、w:t 元素

**原因**：`PaperWriter.Body()` 中正则表达式 `\[\d+(?:[,-]\d+)*\]|.` 的 `.` 匹配任意单字符，导致非引文文本每个字独立成为一个 Run/w:t。

**影响**：XML 体积膨胀，不影响渲染。但文件可读性差，调试不便。

**归属**：Genre 包。`PaperWriter.Body()` 位于此包（已从 Docx.DocumentWriter 移出）。

**修复方案**：改用 `Regex.Split` 按引文模式分割，连续文本合并为单个 Run。

---

### 4. [MEDIUM] dissect 脚本无 subcommand 路由检测 — Skill 层问题

**关键词**：dissect-docx.ps1、subcommand 路由、Program.cs

**判定**：这是 groundpa-toolkit word skill 的脚本问题，非包问题。`dissect-docx.ps1` 假设 Program.cs 包含 subcommand 路由（if args[0] == "preview"），当用户自定义 Program.cs 不包含此路由时，dotnet run 直接进入默认生成路径。

**归属**：groundpa-toolkit word skill。建议在 dissect-docx.ps1 中检测 Program.cs 是否包含必要路由，缺失时给出明确报错。

---

### 5. [LOW] 无用辅助样式常驻生成 — Docx 包设计决策

**关键词**：StyleBuilder.BuildFromJson、FootnoteText、FootnoteReference

**原因**：`BuildFromJson` 方法末尾无条件添加 BodyTextNoIndent、FootnoteText、FootnoteReference 作为兜底样式。这是防御性设计——如果 JSON 未定义这些样式，后续代码引用它们时不会报 StyleNotFound 错误。

**判定**：这是设计决策，不是 bug。代价是 styles.xml 多出 2-3 个样式定义（加起来约 200 字节）。validate 报告中出现未使用的字号（18/9pt）确实容易困惑，但属于信息噪音不是功能缺陷。

**归属**：Docx 包。可作为改进项，提供 `skipAuxiliaryStyles` 参数。优先级低。

---

### 6. [MEDIUM] Skill 未调用 .NET CLI，直接上 PowerShell 解析 docx — Skill 层问题

**关键词**：word skill、dispatch 路由、PowerShell vs .NET CLI

**现象**：用户调用 word skill 读取 docx，skill 没有路由到 .NET CLI（dotnet run -- preview），而是直接用 PowerShell 正则解析 XML，导致命名空间错误、正则匹配失败等连锁问题。

**判定**：这是 word skill 的 dispatch 逻辑问题。skill 应优先检测 DocxWriter 工程是否存在，存在则通过 .NET CLI 调用 TemplateEngine/WordPreview，不存在时才降级为 PowerShell 方案。

**归属**：groundpa-toolkit word skill。需要修改 SKILL.md 的 dispatch 逻辑和/或 dissect-docx.ps1 脚本。

---

## 归属汇总

| # | 严重度 | 归属 | 修复位置 |
|---|--------|------|---------|
| 1 | HIGH | Claude Code Write 工具 | 工具链，非包 |
| 2 | MEDIUM | Docx 包 | `Docx/StyleBuilder.cs` — 增加 lineRule JSON 解析 |
| 3 | LOW | Genre 包 | `Genre/PaperWriter.cs` — 改用 Regex.Split |
| 4 | MEDIUM | word skill | `groundpa-toolkit/word/scripts/dissect-docx.ps1` |
| 5 | LOW | Docx 包（设计决策） | 可选改进，不修 |
| 6 | MEDIUM | word skill | `groundpa-toolkit/word/SKILL.md` dispatch 逻辑 |

**本次可修**：#2（Docx）、#3（Genre）
**需 Skill Agent 修**：#4、#6
**不可修/不需要修**：#1（工具链）、#5（设计决策）

---

## 技能须知

1. Docx 包的 StyleBuilder.BuildFromJson 增加了 `lineRule` 字段支持，format JSON 可在任意 style 定义中添加 `"lineRule": "exact"` 或 `"lineRule": "atLeast"`。默认 auto 向后兼容。

2. Genre 包的 PaperWriter.Body() 已修复逐字 w:t 问题，连续文本合并为单个 Run，XML 输出显著缩小。

3. word skill 的 dissect-docx.ps1 需要增加 Program.cs 路由检测。检测不到时给出明确报错："Program.cs 不包含 subcommand 路由，请使用 workspace-setup.md 的模板重新生成。"

4. word skill 的 dispatch 逻辑需要优化：遇到 read/extract 请求时，优先检测 DocxWriter 工程是否就绪，就绪则走 .NET CLI 路径（速度快、准确），否则降级 PowerShell。
