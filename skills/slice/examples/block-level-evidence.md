# 块级证据追踪

## 用户想要什么

一篇论文审稿时，需要确认"材料与方法"部分的某个数据是否有问题，定位到原始文档中的确切位置。

## 做了什么

```powershell
# 1. 切片文档
nong word dissect paper.docx -o paper.slice --json

# 2. 查看结构，找到"材料与方法"部分的块范围
nong slice inspect paper.slice --json
# 看 structure.json 的 section tree：Materials and Methods 在 blocks 42-78

# 3. 逐块检查可疑内容
nong slice block paper.slice p0055 --json
nong slice block paper.slice t0008 --json  # 可能是数据表

# 4. 检查格式证据
# 打开 format.json 查看 p0055 的字体/段距是否和其他正文段落一致
```

## 结果

发现 p0055 的字体大小为 11pt，而其他正文段落为 12pt——格式不一致。同时 t0008（数据表）的边框信息显示部分列边框缺失。

## 关键点

- `slice block` 可以精准定位到某个块，不用读整个文档。
- provenance 信息（+ format.json）让你可以追踪格式不一致、来源不确定的块。
- 如果有 OCR 内容，在 provenance 中看 `source`——`ocr` 来源的 confidence 可能较低。
- 块级证据追踪是 AI 审稿/校对的核心优势：精准定位问题，不给模糊结论。
