# 2026-06-10 PPTX create

## What changed

新增 `pptx create` 命令。从 JSON spec 生成 PPTX 文件。

```json
{"slides":[{"kind":"title","title":"封面标题","subtitle":"副标题","author":"作者"},{"kind":"content","title":"内容页","items":["要点1","要点2"]}]}
```

## 已知限制

底层 SlideBuilder API 在纯新 Presentation 创建时有运行时 null-ref 问题。命令面已注册、参数验证已就绪，完整功能需要进一步调试 ShapeCrawler 初始化路径。

## Files touched

- `Cli/Commands/PptxCommands.cs` — CreateCreatePptx + PptxCreateSpec 模型
- `Cli/Common/Manifest.cs` — pptx create

## Verification

```text
nong commands --json → 104 commands (was 103)
dotnet test → pending
```
