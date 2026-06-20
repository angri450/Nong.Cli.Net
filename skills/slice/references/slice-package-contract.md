# 统一切片包的结构

NongPandoc 统一切片包是 `nong word/pdf/pptx/excel dissect` 输出的标准格式。理解这个结构是读 slice 的前置知识。

## 包内文件

```
<name>.slice/
  manifest.json        # 包总目录：有哪些流、版本、指标、warnings
  document.json        # canonical 文档对象，带 schemaVersion 和总元信息
  content.jsonl        # 逐块 canonical block 流，每行一个 JSON 块，带稳定 blockId
  content.nongmark     # 增强文本投影，AI 最先读的文件
  structure.json       # 层级和导航：标题树、块顺序、页/slide/sheet 映射
  format.json          # 格式和版式证据：字体、段距、表格边框、页设置、visibleEvidence
  diagnostics.json     # 不确定性、warnings、修复建议
  assets/
    manifest.json      # 图片/媒体/嵌入对象总清单
  preview/
    content.txt        # lossy 纯文本预览（只做快速扫读，不是 source of truth）
    content.md         # lossy Markdown 预览
```

## 流的分工

| 流 | 文件 | 回答什么 | 给谁看 |
|----|------|---------|--------|
| 内容流 | `content.jsonl`, `content.nongmark` | "写了什么" | AI 和人类 |
| 结构流 | `structure.json` | "怎么组织的" | AI 判断章节/页/slide/sheet 关系 |
| 格式流 | `format.json` | "长什么样" | 格式审计、重装回写 |
| 资产流 | `assets/manifest.json` | "有哪些素材" | AI 做图像语义和证据追踪 |
| 诊断流 | `diagnostics.json` | "哪里可能有问题" | AI 决策和风控 |

## BlockId 体系

每个内容块都有稳定的 blockId，前缀表示类型：
- `p0001` — 段落
- `h0001` — 标题
- `t0001` — 表格
- `img0001` — 图片
- `f0001` — 公式
- `c0001` — 图注

## 读取顺序

1. 先读 `manifest.json`，看有哪些流、多少块、有什么 warning。
2. 读 `content.nongmark` 快速理解内容。
3. 需要精确证据时读 `content.jsonl`（带 blockId 的逐块数据）。
4. 需要判断结构时读 `structure.json`。
5. 需要检查格式时读 `format.json`。
6. 有不确定性时读 `diagnostics.json`。
