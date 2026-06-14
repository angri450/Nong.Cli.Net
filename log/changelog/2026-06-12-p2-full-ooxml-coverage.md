# 2026-06-12 OOXML 全覆盖施工 — P2 image-wrap + cell-format + run-format + 4 bug fixes

## P2 新命令

### word image-wrap (alias: word wrap)

11 张图片 inline → 浮动 anchor 转换。6 种环绕模式：square (四周型)、topAndBottom (上下型)、tight (紧密型)、through (穿越型)、behind (衬于文字下方)、inFront (浮于文字上方)。可设文字偏移和水平/垂直对齐。

```
nong word image-wrap <file.docx> --mode square --align-h center -o <out.docx>
```

### word cell-format

单元格级格式化：边框（四边独立线宽+颜色）、底纹（hex 色值）、垂直对齐（top/center/bottom）、内边距（四边独立 mm）。按 table index/row/col 定位，null=全部。

```
nong word cell-format <file.docx> --table 4 --row 0 --shading 1A3A3A -o <out.docx>
```

### word run-format (alias: word char-format)

字符级格式化：下划线（single/double）、删除线、字体颜色、高亮色、字间距、上下标。用 regex pattern 匹配文字或按段落 role 定位。

```
nong word run-format <file.docx> --highlight yellow --pattern "艾草|政策|投资" -o <out.docx>
```

## Bug 修复

| # | 问题 | 修复 | 验证 |
|---|------|------|------|
| heading 检测 | `ps.Attribute("val")` 在 OOXML 命名空间下返回 null | `ps.Attributes().Any(a => a.Name.LocalName == "val")` | 23 heading 检出 |
| mm→twips | 56/10 (误差 1.4%) | 567/10 (1mm=56.7twips) | PASS |
| compact-tables | 无法绑定行高到内容 | `--auto-height` flag: exact/atLeast→auto | PASS |
| table-reflow | 合并单元格崩溃？ | 本就 graceful (skip+warn) | PASS |

## 审计完成态

OOXML 51 属性全覆盖施工结束。P0 (2 项) → P1 (4 项) → P2 (4 项) 全部完成。

新增类: DocxImageWrap.cs, DocxCellFormatter.cs, DocxRunFormatter.cs, DocxPageSetup.cs, DocxIndenter.cs, DocxParagraphControl.cs, DocxPageEstimator.cs

总计: 7 个新 DocxCore 类 (约 1800 行) + 7 个新 CLI 命令 (约 600 行) = ~2400 行 C#。

Demo 输出在 `C:\Users\Administrator\workspace\wormwood\demos\` — 10 个展示 DOCX 覆盖全部新命令。
