# write-official 的 JSON Spec 格式

`inspect write-official` 从 JSON spec 生成公文 DOCX。以下是完整的 spec 格式。

## 基本结构

```json
{
  "redHeader": "某某单位文件",
  "docNumber": "某某〔2026〕1号",
  "title": "关于xxx的通知",
  "recipient": "各有关单位：",
  "body": ["第一段内容。", "第二段内容。"],
  "closing": "特此通知。",
  "signature": "某某单位",
  "date": "2026年6月10日"
}
```

## 字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `redHeader` | string | 是 | 红头标题（发文机关名称） |
| `docNumber` | string | 是 | 发文字号 |
| `title` | string | 是 | 公文标题 |
| `recipient` | string | 是 | 主送机关 |
| `body` | string[] | 是 | 正文段落数组 |
| `closing` | string | 否 | 结束语 |
| `signature` | string | 是 | 落款（发文机关） |
| `date` | string | 是 | 成文日期 |

## 和 inspect write-paper 的区别

- `inspect write-official`：生成公文格式 DOCX，自动处理红头、发文字号、落款、日期等公文要素。
- `inspect write-paper`：生成学术论文格式 DOCX，处理标题/作者/章节/引用。

如果你有了一份公文 DOCX 但格式不对（比如红头没对齐、发文字号不对），用 `word format-gongwen` 做格式修复，而不是重新生成。

## 生成后的验证

生成公文 DOCX 后，建议跑：

```powershell
nong word format-gongwen official.docx -o official.gongwen.docx --json
nong word format-audit official.gongwen.docx --json
```

`format-gongwen` 会对已有公文 DOCX 做排版规范化，`format-audit` 验证格式合规。
