# 文字层 PDF 切片

## 用户想要什么

读取一篇带结构（标题、段落、表格）的 PDF 论文，把内容切成 AI 可读的结构化包。

## 做了什么

```powershell
# 1. 先判断 PDF 类型
nong pdf check paper.pdf --json

# 结果：textPdf，textCharsPerPage > 500

# 2. 切片
nong pdf dissect paper.pdf -o paper.slice --json

# 3. 读切片包里的主内容
nong slice inspect paper.slice --json
```

## 结果

`pdf check` 判断为 `text` 类型——文字层丰富，不需要 OCR。

切片包内容：
- `content.nongmark`：带块属性的增强文本投影，AI 可读
- `content.jsonl`：逐块 canonical block 流
- `structure.json`：章节层级和页面映射
- `format.json`：字体、bbox 等格式证据
- `assets/manifest.json`：内嵌图片清单

## 关键点

- 先跑 `pdf check` 判断类型。text/hybrid/scan 三类走不同路由。
- text PDF 直接切片即可，不需要 OCR。
- AI 应该读切片包（`content.nongmark`、`content.jsonl`），不要直接读原始 PDF。
- `slice inspect` 先看包里有啥，再决定读哪个文件。
