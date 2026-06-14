# Crossref REST API 完整参考

> 编译日期: 2026-06-14
> 基于: Crossref Swagger 文档 v3.54.1, REST API 官方文档, 实时 API 响应采样

---

## 1. 概述

Crossref REST API 提供对 Crossref 注册的学术元数据的编程访问。无需注册即可使用公开接口。返回 JSON 格式。

- **Base URL**: `https://api.crossref.org`
- **Swagger 交互文档**: `https://api.crossref.org/swagger-docs`
- **状态页**: `http://status.crossref.org`
- **社区支持**: `https://community.crossref.org`
- **学习中心**: `https://www.crossref.org/learning/`

### 1.1 响应信封

所有响应共享相同的外层结构:

```json
{
  "status": "ok",
  "message-type": "work-list",
  "message-version": "1.0.0",
  "message": { ... }
}
```

三种消息类型:
1. **Singleton** -- 单个对象元数据 (`message-type: "work"`)
2. **Headers only** -- HTTP HEAD 请求返回 200(存在) 或 404(不存在)
3. **List** -- 查询/过滤结果 (`message-type: "work-list"`)

### 1.2 HTTP 状态码

| 状态码 | 含义 |
|--------|------|
| 200 | 成功 |
| 301 | 重定向 (DOI 变更/删除) |
| 401 | 无效 API key (Metadata Plus) |
| 403 | 手动封禁, 联系 support 解决 |
| 404 | 资源不存在 |
| 429 | 速率限制超限, 稍后重试 |
| 4XX | 请求错误 |
| 5XX | 服务器错误 |

### 1.3 响应头

| 头 | 含义 |
|----|------|
| `x-rate-limit-limit` | 每时间周期允许的请求数 |
| `x-rate-limit-interval` | 速率限制时间窗口 |
| `x-concurrency-limit` | 并发连接数限制 |
| `x-api-pool` | 当前池: `public`, `polite`, `plus` |

---

## 2. 速率限制与访问层级

### 2.1 三层访问

| 层级 | 认证方式 | 每秒请求 | 并发 | 响应头 x-api-pool |
|------|---------|---------|------|-------------------|
| **Public** | 无需认证 | 5 | 1 | `public` |
| **Polite** | `mailto` 参数或 `agent` 头 | 10 | 3 | `polite` |
| **Metadata Plus** | `Crossref-Plus-API-Token` 头 | 150 | 无限制 | `plus` |

### 2.2 Polite 池使用

通过 `mailto` 查询参数或 `User-Agent` 头标识自己:

```
GET /works?mailto=yourmail@example.org
```

或使用 HTTP 头:

```
User-Agent: MyApp (mailto:yourmail@example.org)
```

### 2.3 Metadata Plus

订阅服务, 适用于将 Crossref 元数据集成到生产系统。使用 API key:

```
Crossref-Plus-API-Token: Bearer <your-api-token>
```

### 2.4 最佳实践

- 始终包含 `mailto` 参数和描述性 `User-Agent` 头
- 缓存结果, 避免重复相同的请求
- 监控 HTTP 状态码, 遇到 4XX 时退避
- 确保前一个请求完成后再发送下一个 (避免超并发)
- 对超过 1000 条的结果集使用 cursor 深分页

---

## 3. API 端点

### 3.1 端点总览

| 端点 | 方法 | 说明 |
|------|------|------|
| `/works` | GET | 所有注册内容列表 |
| `/works/{doi}` | GET | 单个 DOI 元数据 |
| `/works/{doi}/agency` | GET | DOI 注册机构 |
| `/journals` | GET | 期刊列表 |
| `/journals/{issn}` | GET | 期刊详情 |
| `/journals/{issn}/works` | GET | 期刊中的作品 |
| `/members` | GET | 成员组织列表 |
| `/members/{id}` | GET | 成员详情 |
| `/members/{id}/works` | GET | 成员关联的作品 |
| `/prefixes/{prefix}` | GET | DOI 前缀管理方 |
| `/prefixes/{prefix}/works` | GET | 前缀关联的作品 |
| `/funders` | GET | 资助方列表 |
| `/funders/{id}` | GET | 资助方详情 |
| `/funders/{id}/works` | GET | 资助方关联的作品 |
| `/types` | GET | 所有作品类型 |
| `/types/{id}` | GET | 作品类型详情 |
| `/types/{id}/works` | GET | 指定类型的作品 |
| `/licenses` | GET | 许可证列表 |

---

## 4. /works 端点 -- 完整参数

所有以 `/works` 结尾的端点都支持相同的查询参数 (`/works`, `/members/{id}/works`, `/journals/{issn}/works`, `/prefixes/{prefix}/works`, `/funders/{id}/works`, `/types/{id}/works`)。

### 4.1 通用查询参数

| 参数 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `query` | string | -- | 全字段自由文本搜索 |
| `filter` | string | -- | 精确字段过滤 (见第5节) |
| `rows` | integer | 20 (最大1000) | 每页结果数 |
| `offset` | integer | 0 (最大10000) | 跳过前 N 条 |
| `cursor` | string | -- | 深分页游标 (从 `*` 开始) |
| `sort` | string | -- | 排序字段 |
| `order` | string | `desc` | 排序方向: `asc` 或 `desc` |
| `select` | string | -- | 逗号分隔的返回字段列表 |
| `facet` | string | -- | 分面计数 |
| `sample` | integer | -- | 随机返回 N 条 (最大100) |
| `mailto` | string | -- | 邮箱地址 (进入 polite 池) |

### 4.2 字段查询 (query.* 参数)

`query.*` 参数限定搜索范围, 多个 query 参数之间是 AND 关系。

| 参数 | 搜索范围 |
|------|---------|
| `query` | 所有字段 |
| `query.affiliation` | 作者所属机构 |
| `query.author` | 作者 given + family 名称 |
| `query.bibliographic` | 标题、作者、ISSN、出版年 |
| `query.chair` | 会议主席姓名 |
| `query.container-title` | 容器(期刊/书籍)名称 |
| `query.contributor` | 作者、编辑、主席、译者 |
| `query.degree` | 学位 |
| `query.description` | 描述 |
| `query.editor` | 编辑姓名 |
| `query.event-acronym` | 活动缩写 |
| `query.event-location` | 活动地点 |
| `query.event-name` | 活动名称 |
| `query.event-sponsor` | 活动赞助方 |
| `query.event-theme` | 活动主题 |
| `query.funder-name` | 资助方名称 |
| `query.publisher-location` | 出版社位置 |
| `query.publisher-name` | 出版社名称 |
| `query.standards-body-acronym` | 标准组织缩写 |
| `query.standards-body-name` | 标准组织名称 |
| `query.title` | 标题 |
| `query.translator` | 译者姓名 |

### 4.3 排序字段 (sort)

```
created  deposited  indexed  is-referenced-by-count  issued
published  published-online  published-print  references-count
relevance  score  updated
```

`order` 取值: `asc` (升序) 或 `desc` (降序, 默认).

### 4.4 分页策略对比

| 特性 | offset | cursor |
|------|--------|--------|
| 深度限制 | 最大 offset + rows = 10000 | 无限制 |
| 性能 | 深分页变慢 | 稳定 |
| 实时性 | 结果可能漂移 | 游标快照 |
| 过期 | 不过期 | 5分钟后游标过期 |

**offset 用法**: `?rows=100&offset=200`

**cursor 用法**:
1. 首次: `?cursor=*&rows=1000`
2. 从响应中取 `message.next-cursor`
3. 后续: `?cursor=<next-cursor>&rows=1000`
4. 当返回条数 < rows 时停止

---

## 5. filter 参数 -- 完整参考

### 5.1 过滤语法

```
filter=字段:值[,字段:值]...
```

规则:
- 不同 filter 名: **AND** 逻辑 -- `filter=type:journal-article,has-orcid:1` 同时满足两个条件
- 相同 filter 名重复: **OR** 逻辑 -- `filter=funder:10.13039/100000001,funder:10.13039/100000050` 满足任一
- 点号 filter (`award.funder`, `award.number`) 在**同一子对象**上匹配

### 5.2 日期过滤 (元数据沉积/处理)

时间格式: ISO 8601 -- `2025`, `2025-01`, `2025-01-04`, `2025-01-04T00`, `2025-01-04T00:12`, `2025-01-04T00:12:32`

| 过滤键 | 说明 |
|--------|------|
| `from-created-date` | 首次沉积不早于指定时间 |
| `until-created-date` | 首次沉积早于指定时间 |
| `from-update-date` | 最后(重新)沉积不早于指定时间(含成员变更) |
| `until-update-date` | 最后(重新)沉积早于指定时间(含成员变更) |
| `from-deposit-date` | 同 `from-update-date` |
| `until-deposit-date` | 同 `until-update-date` |
| `from-index-date` | 重新索引不早于指定时间(含 Crossref 和第三方修改) |
| `until-index-date` | 重新索引不晚于指定时间(含 Crossref 和第三方修改) |

### 5.3 日期过滤 (出版流程)

接受简写日期: `2025`, `2025-01`, `2025-01-05`

| 过滤键 | 说明 |
|--------|------|
| `from-pub-date` | 出版日期不早于 |
| `until-pub-date` | 出版日期不晚于 |
| `from-print-pub-date` | 印刷出版不早于 |
| `until-print-pub-date` | 印刷出版不晚于 |
| `from-online-pub-date` | 在线出版不早于 |
| `until-online-pub-date` | 在线出版不晚于 |
| `from-issued-date` | 发行日期不早于 |
| `until-issued-date` | 发行日期不晚于 |
| `from-accepted-date` | 接受日期不早于 |
| `until-accepted-date` | 接受日期不晚于 |
| `from-posted-date` | 发布日期不早于 (posted-content) |
| `until-posted-date` | 发布日期不晚于 (posted-content) |
| `from-approved-date` | 批准日期不早于 (dissertation, standard, report) |
| `until-approved-date` | 批准日期不晚于 (dissertation, standard, report) |
| `from-awarded-date` | 授予日期不早于 (grant) |
| `until-awarded-date` | 授予日期不晚于 (grant) |
| `from-event-start-date` | 活动开始不早于 |
| `until-event-start-date` | 活动开始不晚于 |
| `from-event-end-date` | 活动结束不早于 |
| `until-event-end-date` | 活动结束不晚于 |

### 5.4 布尔过滤 -- 标识符存在性

取值: `0`/`1` 或 `false`/`true`

| 过滤键 | 检查项 |
|--------|--------|
| `has-orcid` | 有 ORCID |
| `has-authenticated-orcid` | 有经认证的 ORCID (沉积成员背书) |
| `has-clinical-trial-number` | 有临床试验编号 |
| `has-funder-doi` | 有资助方 DOI |
| `has-ror-id` | 有 ROR 机构 ID |
| `has-alias` | 有别名 DOI 指向此记录 |
| `has-prime-doi` | 此 DOI 是别名, 重定向到另一 DOI |

### 5.5 布尔过滤 -- 属性存在性

| 过滤键 | 检查项 |
|--------|--------|
| `has-abstract` | 有摘要 |
| `has-description` | 有描述字段 |
| `has-license` | 有许可信息 |
| `has-update-policy` | 有更新策略链接 |
| `has-domain-restriction` | 有域限制 (Crossmark) |
| `has-assertion` | 有断言 (Crossmark) |
| `has-content-domain` | 有内容域 (Crossmark) |

### 5.6 布尔过滤 -- 关系存在性

| 过滤键 | 检查项 |
|--------|--------|
| `has-affiliation` | 有作者所属机构 |
| `has-award` | 有资助奖项 |
| `has-event` | 关联活动 |
| `has-funder` | 有资助方标识符 |
| `has-relation` | 有任何关系 |
| `has-update` | 被另一 DOI 更新 (如更正或撤稿) |
| `is-update` | 是对另一 DOI 的更新 |
| `has-full-text` | 有全文链接 |
| `has-references` | 有参考文献列表 |
| `has-archive` | 有存档合作方名称 |

### 5.7 精确值匹配 -- 标识符

| 过滤键 | 匹配字段 |
|--------|---------|
| `alternative-id` | 替代 ID (成员指定) |
| `doi` | 内容 DOI |
| `orcid` | 贡献者 ORCID |
| `ror-id` | 贡献者所属/资助 ROR ID |
| `isbn` | ISBN |
| `issn` | 期刊 ISSN (格式: `1234-5678`) |
| `member` | Crossref 成员 ID |
| `prefix` | DOI 所有者前缀 (如 `10.5555`) |
| `article-number` | 文章编号 |
| `clinical-trial-number` | 临床试验编号 |

### 5.8 精确值匹配 -- 元数据属性

| 过滤键 | 匹配字段 |
|--------|---------|
| `group-title` | 组标题 (posted-content 类型) |
| `license.url` | 许可 URL |
| `license.version` | 许可适用版本 (`vor`, `am`, `tdm`, `stm-asf`) |
| `license.delay` | 延迟天数 (查找 >= 给定值的) |
| `type` | 作品类型 ID (来自 `/types` 端点) |
| `type-name` | 作品类型名称 |
| `assertion` | 断言名称 (Crossmark) |
| `assertion-group` | 断言组名称 (Crossmark) |
| `content-domain` | 内容域名称 (Crossmark) |
| `archive` | 存档合作方名称 |

### 5.9 精确值匹配 -- 资助

| 过滤键 | 匹配字段 |
|--------|---------|
| `funder` | 资助方 ID (FundRef) |
| `funder-doi-asserted-by` | 资助方 DOI 断言方 (`crossref` 或 `publisher`) |
| `award.funder` | 奖项资助方 ID |
| `award.number` | 奖项编号 |
| `gte-award-amount` | 奖项金额 >= 给定值 |
| `lte-award-amount` | 奖项金额 <= 给定值 |

### 5.10 精确值匹配 -- 全文链接

| 过滤键 | 匹配字段 |
|--------|---------|
| `full-text.type` | MIME 类型 (如 `application/pdf`) |
| `full-text.application` | `text-mining`, `similarity-checking`, `unspecified` |
| `full-text.version` | 内容版本 |

### 5.11 精确值匹配 -- 关系

| 过滤键 | 匹配字段 |
|--------|---------|
| `container-title` | 容器名称精确匹配 |
| `relation.type` | 关系类型 (如 `is-referenced-by`, `is-parent-of`, `is-preprint-of`) |
| `relation.object-type` | 关联对象类型 (如 `doi`, `issn`) |
| `relation.object` | 关联对象标识符 |
| `update-type` | 更新类型 (如 `correction`, `retraction`) |
| `updates` | 更新的目标 DOI |

### 5.12 按字母排序的完整过滤键清单 (86个)

```
alternative-id              article-number
assertion                   assertion-group
award.funder                award.number
clinical-trial-number       container-title
content-domain              doi
from-accepted-date          from-approved-date
from-awarded-date           from-created-date
from-deposit-date           from-event-end-date
from-event-start-date       from-index-date
from-issued-date            from-online-pub-date
from-posted-date            from-print-pub-date
from-pub-date               from-update-date
funder                      funder-doi-asserted-by
full-text.application       full-text.type
full-text.version           group-title
gte-award-amount            has-abstract
has-affiliation             has-alias
has-archive                 has-assertion
has-authenticated-orcid     has-award
has-clinical-trial-number   has-content-domain
has-description             has-domain-restriction
has-event                   has-full-text
has-funder                  has-funder-doi
has-license                 has-orcid
has-prime-doi               has-references
has-relation                has-ror-id
has-update                  has-update-policy
is-update                   isbn
issn                        license.delay
license.url                 license.version
lte-award-amount            member
orcid                       prefix
relation.object             relation.object-type
relation.type               ror-id
type                        type-name
until-accepted-date          until-approved-date
until-awarded-date           until-created-date
until-deposit-date           until-event-end-date
until-event-start-date       until-index-date
until-issued-date            until-online-pub-date
until-posted-date            until-print-pub-date
until-pub-date               until-update-date
update-type                 updates
```

### 5.13 作品类型 (type-name 值)

```
book  book-chapter  book-part  book-section  book-series  book-set
book-track  component  database  dataset  dissertation  edited-book
grant  journal  journal-article  journal-issue  journal-volume  monograph
other  peer-review  posted-content  proceedings  proceedings-article
proceedings-series  reference-book  reference-entry  report
report-component  report-series  standard
```

---

## 6. select 参数

### 6.1 用法

`select` 仅适用于 **list 端点**, 不适用于单个 DOI 请求。格式: `select=字段1,字段2,...`

```
GET /works?select=DOI,title,author,published-print
GET /works?filter=issn:0172-4770&select=title,author,published&rows=1000
```

对单个 DOI 的替代方案: 使用 `filter=doi:` + `select`:

```
GET /works?filter=doi:10.1007/978-3-540-68161-8_1&select=DOI,prefix,title
```

### 6.2 效率建议

Crossref 建议仅当需要 **3-4 个字段或更少** 时使用 `select`。字段列表越长查询越慢, 此时获取完整记录后丢弃不需要的字段更高效。

### 6.3 可选字段完整列表

```
DOI          ISBN             ISSN            URL
abstract     accepted          alternative-id  approved
archive      article-number    assertion       author
chair        clinical-trial-number  container-title
content-created  content-domain  contributor    created
degree       deposited         editor          event
funder       group-title       indexed         is-referenced-by-count
issn-type    issue             issued          license
link         member            original-title  page
posted       prefix            published       published-online
published-print  publisher      publisher-location  reference
references-count  relation      resource        score
short-container-title  short-title  standards-body  subject
subtitle     title             translator      type
update-policy  update-to        updated-by      volume
```

---

## 7. facet 参数

分面计数从匹配查询的结果中返回聚合统计。格式: `facet=字段名[:最大数量]`, 默认为10, 最大1000, `*` 返回全部。

```
GET /works?facet=type-name:10
GET /works?facet=type-name:10,license:5
GET /works?facet=publisher-name:*
```

**注意**: 分面计数是近似值, 可能与 filter 获得的精确计数不一致。

### 可用分面字段

```
affiliation        archive              assertion
assertion-group    category-name        container-title (最大100)
funder-doi         funder-name          issn (最大100)
journal-issue      journal-volume       license
link-application   orcid (最大100)      published
publisher-name     relation-type        ror-id
source             type-name            update-type
```

---

## 8. 工作项 (Work) 完整 Schema

### 8.1 List 响应外层

```json
{
  "status": "ok",
  "message-type": "work-list",
  "message-version": "1.0.0",
  "message": {
    "facets": {},
    "total-results": 183309957,
    "items": [ ... ],
    "items-per-page": 20,
    "next-cursor": "AoJ+...",    // 仅在 cursor 分页时出现
    "query": {
      "start-index": 0,
      "search-terms": null
    }
  }
}
```

### 8.2 工作项字段 (按出现频率)

**核心标识字段:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `DOI` | string | 数字对象标识符 |
| `prefix` | string | DOI 前缀 (如 `10.1038`) |
| `member` | string | Crossref 成员 ID |
| `source` | string | 记录来源 ("Crossref") |
| `URL` | string | 规范 URL (doi.org 链接) |
| `alternative-id` | [string] | 替代标识符列表 |
| `score` | number | 相关性评分 |

**标题与类型:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `title` | [string] | 正标题 |
| `subtitle` | [string] | 副标题 |
| `short-title` | [string] | 短标题 |
| `original-title` | [string] | 原文标题 |
| `container-title` | [string] | 容器(期刊/书)标题 |
| `short-container-title` | [string] | 容器短标题 |
| `type` | string | 作品类型 (如 `journal-article`) |

**作者/贡献者:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `author` | [Author] | 作者列表 |

Author 对象:
```json
{
  "given": "G.",
  "family": "Kucsko",
  "sequence": "first",          // first / additional
  "affiliation": [{ "name": "Harvard University" }],
  "ORCID": "https://orcid.org/...",   // 可选
  "authenticated-orcid": true,         // 可选
  "role": [{ "role": "author", "vocabulary": "crossref" }]
}
```

**出版/日期字段 (均为 Date 对象):**

```
published           published-online       published-print
issued              accepted               posted
created             deposited              indexed
approved            awarded
```

Date 对象:
```json
{
  "date-parts": [[2013, 7, 31]],
  "date-time": "2013-07-31T00:00:00Z",
  "timestamp": 1375228800000
}
```

**期刊/卷期信息:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `volume` | string | 卷 |
| `issue` | string | 期 |
| `page` | string | 页码范围 (如 `"54-58"`) |
| `journal-issue` | object | `{ issue, published-print }` |
| `ISSN` | [string] | ISSN 列表 |
| `issn-type` | [object] | `[{ value, type: "print"|"electronic" }]` |
| `ISBN` | [string] | ISBN 列表 |

**摘要:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `abstract` | string/XML | 摘要内容 (可选; 并非所有记录都有) |

摘要格式: 通常为 HTML/XML 标记文本, 也可能为纯文本。**并非所有工作项都包含摘要字段** -- 取决于出版商是否沉积了摘要。使用 `has-abstract:1` 过滤器或检查字段是否存在。

**许可信息:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `license` | [License] | 许可列表 |

License 对象:
```json
{
  "URL": "http://www.springer.com/tdm",
  "content-version": "tdm",
  "delay-in-days": 0,
  "start": { "date-parts": [[2013, 7, 31]], ... }
}
```

**链接 (Link):**

```json
{
  "URL": "http://www.nature.com/articles/nature12373.pdf",
  "content-type": "application/pdf",
  "content-version": "vor",
  "intended-application": "text-mining"
}
```

**参考文献 (Reference):**

```json
{
  "key": "ref1",
  "DOI": "10.1038/nature03509",
  "doi-asserted-by": "publisher",
  "author": "E Lucchetta",
  "year": "2005",
  "volume": "434",
  "first-page": "1134",
  "journal-title": "Nature",
  "article-title": "Dynamics of Drosophila...",
  "unstructured": "Lucchetta, E. et al. ...",
  "issn": "0028-0836",
  "isbn": "...",
  "series-title": "...",
  "volume-title": "...",
  "edition": "...",
  "component": "...",
  "standard-designator": "...",
  "standards-body": "...",
  "issue": "...",
  "issn-type": "...",
  "isbn-type": "..."
}
```

**资助信息:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `funder` | [Funding] | 资助信息列表 |

Funding 对象:
```json
{
  "name": "National Science Foundation",
  "DOI": "10.13039/100000001",
  "doi-asserted-by": "publisher",
  "award": ["DMR-1234567"],
  "award-amount": { ... }
}
```

**关系 (Relation):**

| 字段 | 类型 | 说明 |
|------|------|------|
| `relation` | object | 关系图, key 为关系类型, value 为关联对象数组 |
| `update-to` | [object] | 此作品被哪些 DOI 更新 |
| `updated-by` | [object] | 哪些 DOI 更新了此作品 |

Relation 对象: `{ "id-type": "doi", "id": "10.xxx/yyy", "asserted-by": "subject" }`

**其他字段:**

| 字段 | 类型 | 说明 |
|------|------|------|
| `publisher` | string | 出版商名称 |
| `publisher-location` | string | 出版商位置 |
| `language` | string | 语言代码 (如 `"en"`) |
| `reference-count` | integer | 参考文献数量 |
| `references-count` | integer | 参考文献总数 |
| `is-referenced-by-count` | integer | 被引次数 |
| `subject` | [string] | 主题词 |
| `resource` | object | `{ primary: { URL } }` |
| `assertion` | [object] | Crossmark 断言 |
| `content-domain` | object | `{ domain: [], crossmark-restriction: false }` |
| `free-to-read` | object | 自由阅读信息 |
| `event` | object | 会议/活动信息 |
| `standards-body` | object | 标准组织信息 |
| `clinical-trial-number` | [object] | 临床试验编号 |
| `article-number` | string | 文章编号 |
| `group-title` | string | 组标题 (posted-content) |
| `archive` | [string] | 存档合作方 |
| `update-policy` | string | 更新策略 URL |

---

## 9. select 参数局限性

### 9.1 仅限 List 端点

`select` 不适用于单个 DOI 请求 (`/works/{doi}`)。

### 9.2 替代方法

对于单个 DOI, 使用 filter 包装:

```
GET /works?filter=doi:10.1007/978-3-540-68161-8_1&select=DOI,prefix,title
```

### 9.3 性能权衡

| 场景 | 推荐方法 |
|------|---------|
| 需要 1-4 个字段 | 使用 `select` |
| 需要 5+ 个字段 | 获取完整记录后丢弃不需要的字段 |
| 单个 DOI 查询 | 使用 `filter=doi:` + `select` |

---

## 10. 增量同步策略

保持本地数据与 Crossref 同步的三种日期过滤器:

| 过滤器 | 覆盖范围 | 适用场景 |
|--------|---------|---------|
| `from-created-date` | 仅新记录 | 追加新内容 |
| `from-update-date` | 新记录 + 成员修改 | 追踪出版商更新 |
| `from-index-date` | 新记录 + 成员修改 + Crossref/第三方修改 | 最完整同步 |

建议:
- 使用 cursor 当时间范围结果超过 1000 条时
- 时间戳是包含性的, 据此规划同步频率
- 缓存到本地, 用更新版本替换旧记录

---

## 11. 请求示例

### 11.1 基础查询

```bash
# 获取10条记录
curl "https://api.crossref.org/works?rows=10&mailto=your@email.com"

# 标题搜索
curl "https://api.crossref.org/works?query.title=climate+change&rows=5"

# 参考文献搜索
curl "https://api.crossref.org/works?query.bibliographic=nanoscale+thermometry&rows=5"
```

### 11.2 过滤查询

```bash
# 期刊文章, 2020-2023年
curl "https://api.crossref.org/works?filter=type:journal-article,from-pub-date:2020-01-01,until-pub-date:2023-12-31&rows=20"

# 有摘要+有ORCID的期刊文章
curl "https://api.crossref.org/works?filter=has-abstract:1,has-orcid:1&rows=10"

# 特定期刊ISSN + 选择字段
curl "https://api.crossref.org/works?filter=issn:0028-0836&select=DOI,title,author,published-print&rows=100"
```

### 11.3 深分页 (cursor)

```bash
# 第一步
curl "https://api.crossref.org/works?cursor=*&rows=1000&mailto=your@email.com"
# 响应: {"message": {"next-cursor": "AoJ+/...", "items": [...]}}

# 第二步
curl "https://api.crossref.org/works?cursor=AoJ+/...&rows=1000&mailto=your@email.com"
```

### 11.4 分面查询

```bash
# 按作品类型分面
curl "https://api.crossref.org/works?facet=type-name:*&rows=0"
```

### 11.5 随机采样

```bash
# 随机获取10条
curl "https://api.crossref.org/works?sample=10&select=DOI,title"
```

### 11.6 按 DOI 获取单个作品

```bash
curl "https://api.crossref.org/works/10.1038/nature12373"
```

---

## 12. 源码引用

| 信息 | 来源 |
|------|------|
| 完整参数和 schema | `https://api.crossref.org/swagger-docs` (v3.54.1) |
| REST API 官方文档 | `https://www.crossref.org/documentation/retrieve-metadata/rest-api/` |
| 过滤器完整列表 | `https://www.crossref.org/documentation/retrieve-metadata/rest-api/rest-api-filters/` |
| 使用技巧 | `https://www.crossref.org/documentation/retrieve-metadata/rest-api/tips-for-using-the-crossref-rest-api/` |
| 访问与认证 (速率限制) | `https://www.crossref.org/documentation/retrieve-metadata/rest-api/access-and-authentication/` |
| 非技术入门 (学习中心) | `https://www.crossref.org/learning/` |
| 实时响应采样 | `https://api.crossref.org/works?rows=1&select=...` |
| 单作品采样 | `https://api.crossref.org/works/10.1038/nature12373` |
