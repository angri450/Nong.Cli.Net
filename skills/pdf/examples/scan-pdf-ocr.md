# 扫描件 PDF OCR 路由

## 用户想要什么

处理一份扫描版 PDF（全是图片，没有内嵌文字），需要 OCR 识别。

## 做了什么

```powershell
# 1. 判断 PDF 类型
nong pdf check scanned.pdf --json

# 结果：scan，imageCoverageRatio > 0.8，textCharsPerPage < 50
# → 这是扫描件，需要 OCR
```

由于是扫描件，`pdf dissect` 本身无法提取文字。路由到 OCR：

```powershell
# 2. 检查 OCR 环境
nong ocr check-env --json

# 3. 云 OCR 处理整个 PDF（需要 PADDLEOCR_ACCESS_TOKEN）
nong ocr cloud scanned.pdf -o ocr-out --json

# 4. 如果不需要版面理解，也可以用 OCR 转 Word
nong ocr to-word scanned.pdf -o scanned.docx --json
```

## 结果

云 OCR 输出包含版面标签、表格重建、阅读顺序。转 Word 后可以继续走 Word 管线。

## 关键点

- `pdf check` 判断为 scan 后，PDF 本身的技术路线就是"需要 OCR"。不要硬用 `pdf dissect` 去提取 scan PDF 的文字。
- OCR 有两种路径：`ocr cloud`（版面理解）和 `ocr local`（单图文字识别）。扫描件 PDF 应该走 `ocr cloud` 或 `ocr to-word`。
- 云 OCR 需要 `PADDLEOCR_ACCESS_TOKEN`。如果用户没有 token，告诉他去 `https://aistudio.baidu.com/account/accessToken` 获取。
- OCR 输出可以进入 Word 管线（`ocr to-word`），之后用 `word` skill 继续处理。
