# 2026-06-12 nong word 文档紧缩管线 — compact-tables + regroup-images + estimate

## What changed

三条新命令，组成完整的"分析→调整→验证"文档紧缩管线。

### nong word estimate

```
nong word estimate <file.docx> --json
```

逐段累计行高，根据 section 页尺寸估算断页位置。输出每页：
- 内容高度（mm）、空白高度（mm）、浪费百分比
- 是否含图片/表格
- 标记 >30% 浪费的问题页

### nong word compact-tables

```
nong word compact-tables <file.docx> -o <out.docx> --json
```

纯 OOXML 表格紧缩：
1. 去掉 `w:trHeight w:rule=exact` → 改为 `w:rule=atLeast`，行高减半
2. 表格宽度设为 100% 页面文字宽
3. 表格居中 `w:jc w:val="center"`
4. 列宽等分（所有列均分页宽）

### nong word regroup-images

```
nong word regroup-images <file.docx> -o <out.docx> --json
```

跨段落图片重组：maxGap=10 扫描孤图，移动 drawing runs 合并段落，调用 FitImagesWide 缩放。

### 全管线实战

对"北方荒山荒地艾草产业开发战略汇报书"原始文件：

| 步骤 | 命令 | 结果 |
|------|------|------|
| 并排 | `fit-images` | 4 图并排 2 段（76.5% 缩放） |
| 紧缩 | `compact-tables` | 7 表：100% 宽 + 居中 + 等列 |
| 重组 | `regroup-images` | 15 图跨段收拢 6 段 |
| 验证 | `estimate` | 8 页，1 问题页 |

对比：

| 指标 | 原始 | 最终 | 改进 |
|------|------|------|------|
| 页数 | 25 | 8 | -68% |
| 问题页(>30%空白) | 11 | 1 | -91% |
| 浪费总量 | 569mm | 258mm | -55% |

## 技术要点

- `DocxTableCompactor` — XDocument + local-name 匹配，处理命名空间变体
- `DocxImageFitter.FitImagesWide` — 将 maxGap 从 3 扩展到 10
- `DocxPageEstimator` — twip→EMU 转换修正（原用 `*20` 误，改为 `*635`）
- 全栈纯 OOXML — 零 COM、零 Python、零 SkiaSharp

## Files touched

- `Docx/DocxTableCompactor.cs` — 新建：表格紧缩逻辑
- `Docx/DocxPageEstimator.cs` — 新建：页面空白估算
- `Docx/DocxImageFitter.cs` — 重构：提取 FitImagesCore + FitImagesWide，maxGap 参数化
- `Cli/Commands/WordCommands.cs` — 新增 CreateCompactTables + CreateRegroupImages + CreateEstimate
- `Cli/Common/Manifest.cs` — 注册 3 个命令（各带 alias）
