# 2026-06-10 PDF ocr + Toolkit sync

## What changed

### PDF ocr
新增 `pdf ocr` 命令。将 PDF 每页渲染为 JPEG 并嵌入新 PDF 作为图片层。

```
nong pdf ocr <scan.pdf> -o <output.pdf> --dpi 200 --json
```

### Toolkit sync
- chart SKILL.md: 新增 heatmap/radar 命令暴露面、dispatch 和输入 spec
- excel SKILL.md: 新增 style/formula/pivot 命令，更新 boundary
- word SKILL.md: 新增 word compare 命令
- 新增 5 个 example 文件: heatmap-data, radar-multi-index, style-formula-pivot-flow, create-from-spec, compare-revisions

## Files touched
- `Cli/Commands/PdfCommands.cs` — CreateOcrPdf
- `Cli/Common/Manifest.cs` — pdf ocr
- `Cli.Tests/CliContractTests.cs` — Commands_Json_ExposesPdfOcr
- Toolkit: chart SKILL.md, excel SKILL.md, word SKILL.md, pptx SKILL.md
- Toolkit: 5 new examples

## Verification
```text
nong commands --json → 109 commands (was 108)
nong skill validate → all 15 PASS
nong skill scan → 0 findings
dotnet test → pending (full suite running)
```
