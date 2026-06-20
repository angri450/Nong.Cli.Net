# 复杂检索式

## 用户想要什么

检索近三年关于"玉米抗旱性"的中文文献，排除综述类。

## 做了什么

```powershell
# 构建检索式
nong lit parse --query "SU=玉米 AND SU=抗旱性 AND YE BETWEEN(2023,2026) - SU=综述" --json

# 验证
nong lit validate --query "SU=玉米 AND SU=抗旱性 AND YE BETWEEN(2023,2026) - SU=综述" --json

# 查看检索计划（看会用哪些 provider、生成多少查询）
nong lit plan --query "SU=玉米 AND SU=抗旱性 AND YE BETWEEN(2023,2026) - SU=综述" --json

# 执行检索
nong lit search --query "SU=玉米 AND SU=抗旱性 AND YE BETWEEN(2023,2026) - SU=综述" --json
```

## 结果

检索计划显示：
- provider：OpenAlex, Crossref, Unpaywall
- 生成 3 个粗查询（不超过 20 个上限）
- 已知限制：不支持中文同义词自动扩展，不支持 CNKI 内部检索

检索结果：找到 N 条记录，去重后 M 条。

## 关键点

- 使用 DSL 字段：`SU=主题`、`TI=题名`、`KY=关键词`、`AB=摘要`、`AU=作者`、`YE=年份`。
- 运算符：`*`=AND、`+`=OR、`-`=NOT、`()`、`YE BETWEEN`。
- 每个 provider 最多 20 个粗查询，超出截断并给 warning。
- 当前 provider 只有 OpenAlex、Crossref、Unpaywall。不支持 CNKI、万方、Semantic Scholar 等。
- 中英文标题不合并（除非 DOI 匹配）。
