# 2026-06-12 keepNext/cantSplit pagination control — compact-tables upgrade

## What changed

`word compact-tables` 现在感知并应用 OOXML 分页控制。

### 四种分页属性

| OOXML | 含义 | compact-tables 行为 |
|-------|------|---------------------|
| `w:keepNext` | 与下段同页 | 保留已有；表头行自动添加 |
| `w:keepLines` | 段中不分页 | 保留已有 |
| `w:cantSplit` | 行不拆分 | 多行内容行自动添加 |
| `w:pageBreakBefore` | 段前分页 | 不改动 |

### ApplyPaginationControl 逻辑

1. 扫描每格每个段落的 keepNext/keepLines → 标记 preserved
2. 检测表头行（首格 bold 文本 or w:tblHeader）→ 每格每段加 keepNext
3. 检测多行行（>200 字 or >1 段）→ 加 cantSplit
4. 输出：keepNext-preserved:N, keepNext-added:N, keepLines-preserved:N, cantSplit-added:N

### PageEstimator keepNext 感知

keepNext 取消页面断裂点——如果上一段有 keepNext，不在它与当前段之间分页。估页逻辑遵循此规则，不产生虚假断裂。

### 实战

表4（6 列政策表）原始 `w:tblW w:type="auto"` 被窄列撑高，跨两页。compact-tables 后：
- 100% 页宽 + 均分列 → 长文不换行 → 行高降低
- 表头行加 keepNext → 表头与数据不分离
- 整表高度 < 文字区高度 → 分页消失

## Files touched

- `Docx/DocxTableCompactor.cs` — 新增 ApplyPaginationControl（120 行）
- `Docx/DocxPageEstimator.cs` — keepNext 感知分页 + PageInfo 加 KeepNextCount/CantSplitCount
- `Nong.Toolkit.Net/word/SKILL.md` — 新增 Page Layout and Compaction 节（45 行）
