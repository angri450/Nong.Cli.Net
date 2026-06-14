# 2026-06-11 nong word fit-images — inline 多图缩放至并排

## 背景

实战发现：两图在同一 `<w:p>` 段落（连续 `<w:r>` InlineShape），原始尺寸超出页宽 → Word 自动换行。缩小即可并排。不需要 COM。

## 新命令

```
nong word fit-images <file.docx> -o <out.docx> [--gap <mm>] --json
```

扫描整个文档，找到含 2+ inline 图片的段落。总宽 > 页内文字宽时等比缩放。加上可选间距。

## 实现

纯 OOXML：打开 document.xml → 读 section pgSz + pgMar → 算 textWidth EMU → 找多图段落 → 改 wp:extent + a:ext → 写回。

逻辑放在 DocxCore。CLI 只路由。
