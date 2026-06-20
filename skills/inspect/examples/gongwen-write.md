# 公文生成流程

## 用户想要什么

生成一份正式公文（通知），需要红头、发文字号、正文、落款。

## 做了什么

```powershell
# 1. 准备 JSON spec
echo '{
  "redHeader": "某某省农业农村厅文件",
  "docNumber": "农发〔2026〕15号",
  "title": "关于开展2026年春季农业生产技术指导的通知",
  "recipient": "各市、县农业农村局：",
  "body": [
    "为了做好2026年春季农业生产技术指导工作，确保粮食安全和重要农产品有效供给，现就有关事项通知如下：",
    "一、加强组织领导。各级农业农村部门要高度重视...",
    "二、强化技术指导。组织农业技术人员深入田间地头..."
  ],
  "closing": "特此通知。",
  "signature": "某某省农业农村厅",
  "date": "2026年6月10日"
}' > official.json

# 2. 生成公文 DOCX
nong inspect write-official official.json -o notice.docx --json

# 3. 做公文排版规范化
nong word format-gongwen notice.docx -o notice.gongwen.docx --json

# 4. 格式审计验证
nong word format-audit notice.gongwen.docx --json
```

## 结果

`inspect write-official` 生成初版公文 DOCX（带红头、发文字号、标题、正文、落款）。

`word format-gongwen` 规范化排版（红头对齐、字体规范、段距调整）。

`word format-audit` 确认格式合规。

## 关键点

- `inspect write-official` 是从零生成公文——输入是 JSON spec，输出是新 DOCX。
- `word format-gongwen` 是对已有 DOCX 做公文排版——输入是已有 DOCX，输出是排版后的 DOCX。
- 如果你已经有一份 .docx 公文，不要重新生成。直接用 `word format-gongwen` 排版即可。
- 公文生成的 JSON spec 必须包含 `redHeader`、`docNumber`、`recipient` 等公文特有字段——这些是公文格式的核心要素。
