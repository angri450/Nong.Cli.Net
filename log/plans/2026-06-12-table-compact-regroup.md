# 2026-06-12 nong word compact-tables + regroup-images 施工方案

## 背景

`nong word estimate` 暴露了三类浪费：
1. 表格被 `w:trHeight w:rule=exact` 固定行高撑大
2. 表格列宽不合理——内容少但挤在一侧
3. 孤图独自一页——可与前面图片合并

## 两个新命令

### nong word compact-tables

```
nong word compact-tables <file.docx> -o <out.docx> --json
```

内部操作（纯 OOXML）：
1. 每表：去掉所有 `w:trHeight` 的 `w:rule=exact` → 改为 `w:rule=atLeast`
2. 表格宽度设 `w:w="5000" w:type="pct"`（100% 页宽）
3. 表格对齐 `w:jc w:val="center"`
4. 列宽重新均匀分配（`w:tcW` 改为百分比）
5. 保留表头行样式和三线边框

### nong word regroup-images

```
nong word regroup-images <file.docx> [--spec spec.json] -o <out.docx> --json
```

内部操作：
1. 默认模式（无 spec）：扫描所有孤立图片页（图片前后有 >30% 空白），尝试与其前一个图片段合并
2. 合并逻辑复用 DocxImageFitter 的 drawing run 移动
3. 合并后自动调用 fit-images 缩放

## 实现

纯 DocxCore：XDocument + System.IO.Compression。CLI 只路由。
