# write-paper 的 JSON Spec 格式

`inspect write-paper` 从 JSON spec 生成论文 DOCX。以下是完整的 spec 格式。

## 基本结构

```json
{
  "title": "论文标题",
  "authors": ["第一作者", "第二作者"],
  "abstract": "摘要内容...",
  "keywords": "关键词1; 关键词2",
  "sections": [
    {
      "heading": "引言",
      "body": ["第一段内容", "第二段内容"]
    },
    {
      "heading": "材料与方法",
      "content": ["..."]
    }
  ],
  "references": ["[1] 作者. 标题. 期刊, 年份", "[2] ..."]
}
```

## 字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `title` | string | 是 | 论文标题 |
| `authors` | string[] | 是 | 作者列表 |
| `abstract` | string | 否 | 摘要文本 |
| `keywords` | string | 否 | 关键词，分号分隔（如 "kw1; kw2"） |
| `sections` | object[] | 是 | 章节数组，每个有 `heading` 和 `body` |
| `references` | string[] | 否 | 参考文献列表 |

## 章节结构

每个 section 包含：
- `heading`: 章节标题（string）
- `body`: 段落数组（string[]），每个元素是一个段落（也接受 `content` 作为别名）

## 和 word create 的区别

- `inspect write-paper`：从结构化 JSON spec 生成论文 DOCX，会自动处理标题样式、作者格式、章节层级。
- `word create`：从 NongMark 源文件生成 DOCX，适合自由格式写作。

论文写作应该用 `inspect write-paper`，不要用 `word create` 去模拟论文结构。
