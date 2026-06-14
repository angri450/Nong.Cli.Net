# 2026-06-12 keepNext pagination control 施工方案

## 背景

表4 跨页问题暴露了 OOXML 分页控制的盲区。

## 实施

1. DocxTableCompactor 加 ApplyPaginationControl——保留已有 keepNext/keepLines，表头行加 keepNext，多行行加 cantSplit
2. DocxPageEstimator 感知 keepNext——不在 keepNext 段与下段之间分页
3. word SKILL.md 加 Page Layout 节——完整的紧缩管线 + keepNext 四种分页控制表 + 表4 案例

## 状态: done
- CLI 0c3d706
- Toolkit 87a3f5b
