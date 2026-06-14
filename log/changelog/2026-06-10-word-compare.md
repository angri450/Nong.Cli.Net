# 2026-06-10 Word compare

## What changed

新增 `word compare` 命令。对两份 DOCX 做段落级 diff，报告 added/removed/modified 变更。

```
nong word compare <file1.docx> <file2.docx> --json
```

输出包含每个变更的段落索引、类型（added/removed/modified）、新旧文本和样式。

## Why

Word compare 是文档审阅最常被问到的能力。基于 OpenXML SDK 直接提取段落并做标准化文本比较。

## Implementation

- `ExtractParagraphs`: 读取 DOCX 正文中所有 `<w:p>` 元素，提取内联文本和样式 ID
- `DiffParagraphs`: 简单逐行比较，标准化空白后判定变更
- `ValidateDocxFile`: 复用已有验证函数

## Files touched

- `Cli/Commands/WordCommands.cs` — CreateCompare + ExtractParagraphs + DiffParagraphs + 辅助类型
- `Cli/Common/Manifest.cs` — word compare

## Tests

- `Commands_Json_ExposesWordCompare`
- `WordCompare_IdenticalFiles_NoChanges`
- `WordCompare_DifferentFiles_ReportsChanges`
- `WordCompare_AddedParagraph_Detected`

## Verification

```text
nong commands --json → 107 commands (was 106)
dotnet test → pending (full suite running)
```
