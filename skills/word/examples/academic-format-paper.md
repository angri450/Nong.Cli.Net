# 论文格式修复

## 用户想要什么

一篇待投稿的论文 DOCX，格式不统一：标题字体不一致、正文字号混乱、三线表不规范、参考文献格式不统一。

## 做了什么

```powershell
# 1. 先做格式审计，看当前有什么问题
nong word format-audit paper.docx --json

# 2. 按学术格式规范修
nong word academic-format paper.docx -o paper.academic.docx --json

# 3. 验证修改结果
nong word format-audit paper.academic.docx --json
```

## 结果

```json
{
  "status": "ok",
  "command": "word academic-format",
  "data": {
    "headings": "leveled and styled",
    "body": "unified font and spacing",
    "tables": "three-line table format applied"
  }
}
```

format-audit 输出确认：标题层级规范化、正文统一字体和段距、表格改为三线表。

## 关键点

- 格式修复前先跑 `format-audit`，知道当前有什么问题，不要盲修。
- `academic-format` 修的是可见格式，不是底层 OOXML 结构。如果底层结构也有问题（重复样式 ID 等），先跑 `word clean-styles`。
- 修完再跑一次 `format-audit` 确认修改生效。
- 不要只跑 `word validate` 或 `word preview` 就声称格式 OK——它们不检查可见格式。
