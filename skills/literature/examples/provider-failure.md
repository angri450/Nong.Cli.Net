# Provider 失败时的处理

## 用户想要什么

检索文献时，其中一个 provider 不可用。

## 出了什么问题

```powershell
nong lit search --query "SU=水稻 AND SU=氮肥 AND YE BETWEEN(2020,2026)" --json
```

```json
{
  "status": "partial",
  "data": {
    "providers": {
      "openalex": {"status": "ok", "results": 23},
      "crossref": {"status": "error", "reason": "rate_limited"},
      "unpaywall": {"status": "ok", "results": 5}
    }
  },
  "issues": [{
    "severity": "warning",
    "message": "Crossref rate-limited; results from OpenAlex and Unpaywall are available"
  }]
}
```

## 怎么处理

1. **结果仍可用**：OpenAlex 和 Unpaywall 的结果已经足够。告诉用户 Crossref 暂时不可用，但其他 provider 返回了结果。
2. **如果是全部 provider 都失败**：检查网络连接、环境变量（`NONG_LIT_OPENALEX_KEY`、`NONG_LIT_MAILTO`）。
3. **重试无需立即执行**：Crossref rate-limit 是暂时的。等几分钟后再试，或者只用现有结果。

## 关键点

- `lit search` 的 status 可能是 `partial`（部分 provider 失败），不要把它当成完全失败。
- 查看 `issues` 字段获取每个 provider 的状态和原因。
- OpenAlex 是主搜索源，即使只有它的结果也通常够用。
- 不要反复重试 rate-limited provider——等几分钟即可。
