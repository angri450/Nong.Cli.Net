# 2026-06-12 nong word fit-images — inline 多图缩放并排

## What changed

新命令 `nong word fit-images`。

### 功能

扫描文档，找到相邻的独立图片段落（最多间隔 3 段），将它们的 drawing runs 合并入同一段落，然后等比缩放至页宽内并排。

```
nong word fit-images <file.docx> -o <out.docx> [--gap <mm>] --json
```

### 实战结果

对"北方荒山荒地艾草产业开发战略汇报书.docx"（原始未裁剪，129 段 11 图）：

| 段落 | 图片对 | 原始总宽 | 缩放比 | 结果 |
|------|--------|---------|-------|------|
| para[49] | img0003+img0004 (图3+图4) | 7,200,000 EMU | 76.5% | 5,508,380 EMU |
| para[81] | img0008+img0009 (图8+图9) | 7,200,000 EMU | 76.5% | 并排 |

输出 127 段（原 129，合并 2 段），10 image parts 完整，0 警告。

### 关键发现

原始文档中两张图片其实是**相邻的独立段落**（中间隔一条 caption）。不是一开始就在同一段落。`fit-images` 先合并 drawing runs 到同一 `<w:p>`，再缩放 `wp:extent` + `a:ext`。

### 实现

纯 OOXML 操作（System.IO.Compression + XDocument）—— local-name 匹配规避命名空间问题。逻辑在 DocxCore.DocxImageFitter。CLI 只路由。

## Files touched

- `Docx/DocxImageFitter.cs` — 新建：段落合并 + 缩放逻辑
- `Cli/Commands/WordCommands.cs` — 新增 CreateFitImages()
- `Cli/Common/Manifest.cs` — 注册 word fit-images 命令（alias: word compact-images）
