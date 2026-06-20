# 读取 PPT Slide 列表

## 用户想要什么

了解一个 PPTX 文件的每页结构（slide 数、每页有什么内容）。

## 做了什么

```powershell
# 1. 列出所有 slide
nong pptx slides presentation.pptx --json

# 2. 读取全部文本
nong pptx read presentation.pptx --json

# 3. 对感兴趣的 slide 做切片
nong pptx dissect presentation.pptx --slide 3 -o slide3.slice --json
```

## 结果

`pptx slides` 输出每页的标题、占位符类型、元素数量。

`pptx read` 输出全部 slide 文本内容，按 slide 顺序排列。

`pptx dissect --slide 3` 对第 3 页切片，输出 NongPandoc 包（包含 slide 结构、文本框、图片引用）。

## 关键点

- PPTX 当前只读。不支持创建、编辑、生成新 PPT。
- `pptx slides` 是快速摸清结构的命令，`pptx read` 是全部文本提取，`pptx dissect` 是深度分析单页。
- 如果用户要求"做一个 PPT"，告诉用户当前 Toolkit 只能读取和分析 PPTX，不能创建。这是已知的功能缺口。
