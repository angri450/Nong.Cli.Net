# 申报表表格重排

## 用户想要什么

一份课题申报表的 DOCX，里面的表格列太宽、跨页时没有续表标记、长表格在页末被截断。

## 做了什么

```powershell
# 1. 先做格式审计，看清楚表格现状
nong word format-audit application.docx --json

# 2. 重排表格：每页最多 30 行，续表标注"续表"
nong word table-reflow application.docx -o application.reflow.docx --max-rows 30 --continuation-label "续表" --json

# 3. 验证
nong word format-audit application.reflow.docx --json
```

## 结果

`format-audit` 确认：长表格被拆分为合理的分页块，续表标注清晰，列宽合理。原来的内容、表头、行顺序全部保留。

## 关键点

- `table-reflow` 只拆分表格，不改内容。原来的行列数据、表头信息完全保留。
- `--max-rows` 控制每页最多多少行（含表头）。
- `--continuation-label` 控制续表前面的文字（如"续表"、"Continued"）。
- 不要用 `word fix-order` 来修表格问题——那是底层 OOXML 元素顺序修复，不解决分页和列宽问题。
- 如果表格还有边框样式问题，配合 `word academic-format` 一起用。
