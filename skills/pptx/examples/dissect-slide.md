# 切片单个 Slide

## 用户想要什么

分析一个 PPTX 的某页 slide 的详细结构——有哪些文本框、图片、表格，它们的布局关系怎样。

## 做了什么

```powershell
# 1. 先看有哪些 slide
nong pptx slides deck.pptx --json

# 2. 选择需要分析的 slide（第 5 页）
nong pptx dissect deck.pptx --slide 5 -o slide5.slice --json

# 3. 读切片包
nong slice inspect slide5.slice --json
nong slice blocks slide5.slice --json
```

## 结果

`slice blocks` 输出 slide5 的所有内容块：4 个文本框、1 个图片、1 个表格。

每个块带位置信息（bbox），可以重建 slide 的视觉布局。

## 关键点

- `pptx dissect` 可以指定 `--slide N` 单独切某一页。默认切全部。
- 切片包里的 structure.json 会标注文本框之间的层次关系（如标题框 vs 内容框）。
- 图片块会出现在 assets 清单中，可以追踪图片的尺寸和位置。
- PPTX 只读模式：可以分析现有 PPT 的结构和内容，但不能修改。
