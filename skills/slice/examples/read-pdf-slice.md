# 读一个 PDF 切片包

## 用户想要什么

把一篇 PDF 论文切片后，检查包结构和取证。

## 做了什么

```powershell
# 1. 先判断 PDF 类型
nong pdf check paper.pdf --json
# → textPdf

# 2. 切片
nong pdf dissect paper.pdf -o paper.slice --json

# 3. 用 slice 命令读包
nong slice inspect paper.slice --json
nong slice blocks paper.slice --json

# 4. 检查某个文本块的出处
nong slice block paper.slice p0042 --json
```

## 结果

`slice block p0042` 返回：

```json
{
  "blockId": "p0042",
  "kind": "paragraph",
  "text": "结果表明，氮肥施用量对玉米产量有极显著影响...",
  "provenance": {
    "format": "pdf",
    "position": {"page": 3, "bbox": {"x": 72, "y": 420, "w": 468, "h": 24}},
    "source": "pdfText",
    "confidence": 1.0
  }
}
```

provenance 字段说明了这个块的来源：PDF 第 3 页、原生文字提取、高置信。

## 关键点

- PDF 切片包的 block 带有 provenance 信息——知道每个块来自哪个页面、什么位置、提取来源。
- `source: pdfText` 表示原生文字提取（高质量），`source: ocr` 表示 OCR 识别（可能有不准确）。
- `confidence` 告诉你这个块的可信度。pdfText 的 confidence 通常为 1.0。
- PDF 切片包的 `diagnostics.json` 特别重要——会标注阅读顺序不确定和 OCR 低置信区域。
