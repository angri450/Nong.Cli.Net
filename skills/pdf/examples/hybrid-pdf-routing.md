# 混合 PDF 判断

## 用户想要什么

一份 PDF 有些页是文字（可复制），有些页是扫描图片（不可复制），需要判断每页的路线。

## 做了什么

```powershell
nong pdf check mixed.pdf --json
```

## 结果

```json
{
  "status": "ok",
  "command": "pdf check",
  "data": {
    "classification": "hybrid",
    "textCharsPerPage": 320,
    "imageCoverageRatio": 0.45,
    "pages": [
      {"page": 1, "route": "text", "textChars": 850},
      {"page": 2, "route": "text", "textChars": 720},
      {"page": 3, "route": "scan", "imageCoverage": 0.92, "textChars": 12}
    ]
  }
}
```

## 怎么处理

```powershell
# 整体切片（文字页有内容，扫描页内容稀疏）
nong pdf dissect mixed.pdf -o mixed.slice --json

# 对扫描页单独做 OCR
nong pdf render mixed.pdf --page 3 -o page3-images --json
nong ocr cloud page3-images/page-3.png -o page3-ocr --json
```

## 关键点

- `pdf check` 会逐页判断。`classification: hybrid` 表示不是所有页都是同一种类型。
- 文字页走 `pdf dissect`，扫描页走 OCR。不需要对整个 PDF 做 OCR。
- 用 `pdf render` 把需要 OCR 的页面渲染成图片，再用 `ocr cloud` / `ocr local` 处理。
- 不要看到一个字少的页面就认为整个 PDF 都需要 OCR。逐页判断，精确路由。
