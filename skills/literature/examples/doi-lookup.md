# DOI 查找和元数据获取

## 用户想要什么

通过 DOI 查找一篇论文的完整元数据（标题、作者、期刊、年份、摘要）。

## 做了什么

```powershell
# 1. 解析检索式
nong lit parse --query "DOI(10.1038/s41586-020-2649-2)" --json

# 2. 验证检索式是否合法
nong lit validate --query "DOI(10.1038/s41586-020-2649-2)" --json

# 3. 制定检索计划
nong lit plan --query "DOI(10.1038/s41586-020-2649-2)" --json

# 4. 执行检索
nong lit search --query "DOI(10.1038/s41586-020-2649-2)" --json

# 5. 导出结果
nong lit export --input search-results.json --format json --out results.json --json
```

## 结果

```json
{
  "status": "ok",
  "data": {
    "results": [{
      "title": "...",
      "authors": [...],
      "year": 2020,
      "journal": "Nature",
      "doi": "10.1038/s41586-020-2649-2",
      "provider": "crossref"
    }]
  }
}
```

## 关键点

- DOI 查找走 Crossref provider（最可靠的 DOI 解析源）。
- `lit validate` 在检索之前跑，确认 DSL 语法正确。
- `lit plan` 告诉你会调用哪些 provider、会生成多少查询、有什么已知限制。
- `lit export` 支持 json、markdown (GB/T 7714)、bibtex 三种导出格式。
