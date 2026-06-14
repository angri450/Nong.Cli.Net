# 2026-06-10 PDF merge + split

## What changed

新增 2 个 PDF 编辑命令：`pdf merge` 和 `pdf split`。

| 命令 | 参数 | 说明 |
|------|------|------|
| `pdf merge` | `<files...> -o <out.pdf>` | 合并多个 PDF 文件（至少 2 个） |
| `pdf split` | `<file> -o <out.pdf> [--pages <range>]` | 拆分 PDF 页面到单独文件（默认第 1 页） |

## Why

PDF 命令命名审计识别出 merge 和 split 是最常被问到的 PDF 编辑需求。底层 DocLib/DocEditor 已有 PDFium 级别的 Merge/Split 实现，只需补 CLI 命令面。

## Files touched

- `Cli/Commands/PdfCommands.cs` — 新增 CreateMerge、CreateSplit
- `Cli/Common/Manifest.cs` — 注册 pdf merge、pdf split
- `Cli.Tests/CliContractTests.cs` — 4 个测试（Commands_Json_Exposes、Merge_TwoFiles、Merge_RequiresTwo、Split_ByPageRange）

## Tests

- `Commands_Json_ExposesPdfMergeAndSplit` — 验证命令面
- `PdfMerge_TwoFiles_ProducesPdf` — 用 PdfPig 创建 2 个真实 PDF 并合并
- `PdfMerge_RequiresAtLeastTwoFiles` — 验证 < 2 文件时返回错误
- `PdfSplit_ByPageRange_ProducesPdf` — 用 PdfPig 创建 PDF 并按页拆分

## Verification

```text
dotnet build Cli/NongCli.csproj -c Release
Result: 0 errors

dotnet test Cli.Tests/Cli.Tests.csproj -c Release
Result: 133 passed, 0 failed, 0 skipped

nong commands --json
Result: 100 commands (was 98), pdf merge + split confirmed
```
