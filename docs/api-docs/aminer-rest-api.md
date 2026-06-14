# AMiner REST API

**Research date**: 2026-06-14
**Status**: Reverse-engineered from web documentation, Apifox specs, and MCP server source references.

---

## 1. Overview

AMiner (https://www.aminer.cn) is a Chinese academic knowledge graph platform with ~180M papers, ~38M scholars, ~150M patents, ~90K journals, and ~1M institutions. The open platform exposes REST APIs under a unified gateway.

The APIs are also exposed through:
- **MCP Server** (`@scipen/aminer-mcp-server` npm, `aminer_mcp` Python package)
- **302.ai proxy** (a third-party API marketplace that re-exposes AMiner APIs)
- **OpenClaw skills marketplace**

The user already has a working MCP configuration that connects via SSE with Bearer token auth. This document reverse-engineers the underlying REST API.

---

## 2. Base URL

### Official AMiner endpoint

```
https://datacenter.aminer.cn/gateway/open_platform
```

### 302.ai proxy (alternative)

```
https://api.302.ai/aminer/gateway/open_platform
```

### Docs (SPA -- require browser)

```
https://open.aminer.cn/docs
https://www.aminer.cn/open/board?tab=control   (console/API key management)
```

Individual API doc pages use document IDs:
- `https://www.aminer.cn/open/docs?id=671a19a46e728a29db292f73` (Scholar Search)
- `https://www.aminer.cn/open/docs?id=64f03e746221825d961dbde4` (Paper Search)
- `https://www.aminer.cn/open/docs?id=66471a81d0efa2bb8d4e8c08` (Paper Search Pro)
- `https://www.aminer.cn/open/docs?id=64f03bf46221825d961dbde3` (Patent Search)
- `https://www.aminer.cn/open/docs?id=64f0386a6221825d961dbde2` (Paper Info)

These pages are SPA-rendered and return empty HTML via curl/WebFetch.

---

## 3. Authentication

All API calls require a token. Two patterns are observed:

### Pattern A: Bare token (datacenter.aminer.cn)

```http
Content-Type: application/json;charset=utf-8
Authorization: <raw-token>
```

The token is used as-is, without a `Bearer` prefix. This is the pattern shown in the AMiner docs themselves.

Example:
```bash
curl -X POST \
  'https://datacenter.aminer.cn/gateway/open_platform/api/person/search' \
  -H 'Content-Type: application/json;charset=utf-8' \
  -H 'Authorization: YOUR_TOKEN' \
  -d '{"offset":0,"query":"小明"}'
```

### Pattern B: Bearer token (302.ai proxy)

```http
Authorization: Bearer <API_KEY>
```

The 302.ai proxy and some MCP wrappers require the Bearer prefix.

### Token acquisition

1. Register at https://www.aminer.cn
2. Go to https://www.aminer.cn/open/board?tab=control
3. Generate an API Key/Token in the console

### MCP environment variable

MCP configurations use:
- `AMINER_API_KEY` (npm MCP server `@scipen/aminer-mcp-server`)
- `AMINER_TOKEN` (Python MCP server `aminer_mcp`)

The user's existing MCP SSE configuration uses a JWT Bearer token.

---

## 4. Complete Endpoint Catalog

### 4.1 Paper Endpoints

| Endpoint | Method | Price | Description |
|----------|--------|-------|-------------|
| `/api/paper/search` | GET | **Free** | Basic paper search by title. Params: `title` (string), `page` (int, 0-based), `size` (int, max 20). |
| `/api/paper/search/pro` | GET | 0.01 CNY | Multi-condition filtered search. Params: `title`, `keyword`, `author`, `page`, `size`. |
| `/api/paper/qa/search` | POST | 0.05 CNY | Natural-language semantic search over papers. |
| `/api/paper/info` | POST | **Free** | Batch basic info by IDs. Body: `{"ids": ["id1","id2",...]}`. |
| `/api/paper/detail` | GET | 0.01 CNY | Single paper full detail. Param: `paper_id` (string, singular -- not `ids`). |
| `/api/paper/relation` | GET | 0.10 CNY | Paper citations (cited papers list). |
| `/api/paper/list/by/keywords` | GET | 0.10 CNY | Batch retrieval by multiple keywords. |
| `/api/paper/list/by/search/venue` | GET | 0.05 PTC* | Search papers by venue/journal. Params: `keyword`/`venue`/`author` (pick one), `page`, `size`, `order` (`year` or `n_citation`). |
| `/api/paper/list/citation/by/keywords` | GET | -- | Citation list by keywords. |
| `/api/paper/deep_research` | POST | 0.08 PTC* | AI deep research (AMiner Meditation). Body: `{"message":"...","type":1}` where type=1 AMiner, 2 arXiv, 3 PubMed. |

*PTC = 302.ai platform token currency. AMiner direct pricing in CNY.

### 4.2 Person/Scholar Endpoints

| Endpoint | Method | Price | Description |
|----------|--------|-------|-------------|
| `/api/person/search` | POST | **Free** | Search scholars by name or org. Body: `{"query":"name","offset":0,"size":10}`. Optional: `org`, `org_id`. |
| `/api/person/detail` | GET | 1.00 CNY | Full scholar bio. Param: `person_id` (string, singular -- not `ids`). |
| `/api/person/figure` | GET | 0.50 CNY | Scholar portrait: research interests, work history, education. |
| `/api/person/paper/relation` | GET | 1.50 CNY | Scholar's paper list by scholar ID. |
| `/api/person/patent/relation` | GET | 1.50 CNY | Scholar's patent list by scholar ID. |

### 4.3 Patent Endpoints

| Endpoint | Method | Price | Description |
|----------|--------|-------|-------------|
| `/api/patent/search` | POST | **Free** | Search patents by keyword query. Body: `{"query":"carbon nanotube","page":0,"size":10}`. |
| `/api/patent/info` | GET | **Free** | Basic patent info by ID. |
| `/api/patent/detail` | GET | 0.01 CNY | Full patent detail: abstract, filing date, application number, assignee, country. |

### 4.4 Organization/Venue/Project Endpoints

| Endpoint | Method | Price | Description |
|----------|--------|-------|-------------|
| `/api/organization/search` | POST | **Free** | Search institutions/orgs. |
| `/api/venue/search` | POST | **Free** | Search journals/venues. |
| `/api/venue/detail` | GET | 0.01 CNY | Journal detail by ID. |
| `/api/project/person/v3/open` | GET | 3.00 CNY | Scholar's research projects/funding. |
| Org name normalization V2 | -- | 0.05 CNY | Normalize institution names. |

---

## 5. Key Request/Response Patterns

### 5.1 Person Search

```
POST /api/person/search
Content-Type: application/json;charset=utf-8
Authorization: <token>

{
  "offset": 0,
  "query": "小明"
}
```

### 5.2 Paper QA Search (semantic)

```
POST /api/paper/qa/search
Authorization: Bearer <token>
Content-Type: application/json

{
  "query": "生首乌和何首乌与肝损伤的关系",
  "topic_high": "[[\"生首乌\"]]",
  "use_topic": true,
  "size": 20,
  "offset": 0
}
```

Optional QA search parameters:
- `topic_middle` -- moderate boost terms (same format as topic_high)
- `topic_low` -- small boost terms
- `title` -- string array (used when `use_topic=false`)
- `doi` -- DOI filter
- `year` -- number array, year range
- `sci_flag` -- boolean, SCI-only filter
- `n_citation_flag` -- boolean, boost high-citation papers
- `force_citation_sort` / `force_year_sort` -- boolean, absolute sort
- `author_terms` -- string array, author name variants (OR within array)
- `org_terms` -- string array, institution name variants (OR within array)

### 5.3 Patent Search

```
POST /api/patent/search
Authorization: Bearer <token>
Content-Type: application/json

{
  "query": "carbon nanotube",
  "page": 0,
  "size": 10
}
```

### 5.4 Deep Research (AMiner Meditation)

```
POST /api/paper/deep_research
Authorization: Bearer <token>
Content-Type: application/json

{
  "message": "AI是什么",
  "type": 1,
  "web_search": false
}
```

type: 1=AMiner, 2=arXiv, 3=PubMed

---

## 6. Response Format

Standard response envelope:

```json
{
  "code": 200,        // 0 or 200 = success
  "success": true,
  "msg": "",
  "log_id": "33Mf4NbmpwKQI9oLH5EQ9WmYp4b",
  "total": 2645,
  "data": [
    {
      "id": "53e9a8ccb7602d970321b422",
      "title": "Paper Title",
      "year": 2024
    }
  ]
}
```

In some endpoints (person search), the response wraps differently:
```json
{
  "code": 0,
  "status": 0,
  "msg": "",
  "data": [{
    "items": [...],
    "total": 14286,
    "offset": 0,
    "size": 10,
    "succeed": true
  }]
}
```

### Paper response fields (full detail)

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Paper ID |
| `doi` | string | DOI |
| `title` | string | English title |
| `title_zh` | string | Chinese title |
| `abstract` | string | English abstract |
| `abstract_zh` | string | Chinese abstract |
| `authors` | array | Author objects (see below) |
| `keywords` | string[] | English keywords |
| `keywords_zh` | string[] | Chinese keywords |
| `n_citation` | integer | Citation count |
| `year` | integer | Publication year |
| `url` | string | Paper URL on aminer.cn |
| `venue` | object | Venue info: `{alias, name_en, name_zh}` |
| `venue_hhb_id` | string | Venue HHB ID |

### Author object

| Field | Type | Description |
|-------|------|-------------|
| `_id` | string | Author ID |
| `name` | string | English name |
| `name_zh` | string | Chinese name |
| `org_name` / `org` | string | Organization name |
| `orgid` | string | Organization ID |
| `aliases` | string[] | Alternative names |
| `acronyms` | string[] | Acronyms |

### Person response fields (search result)

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Scholar ID |
| `name` | string | English name |
| `name_zh` | string | Chinese name |
| `interests` | string[] | Research interests |
| `n_citation` | integer | Citation count |
| `org` | string | English org name |
| `org_id` | string | Org ID |
| `org_zh` | string | Chinese org name |
| `nation` | string | Nationality |

### Person detail fields (from /api/person/detail)

| Field | Description |
|-------|-------------|
| `id` | Scholar ID |
| `name` / `name_zh` | English/Chinese name |
| `bio` / `bio_zh` | Biography (EN/ZH) |
| `edu` / `edu_zh` | Education history |
| `honor` | Honors/awards list |
| `orgs` / `org_zhs` | Organization affiliations |
| `position` / `position_zh` | Current position |
| `interests` | Research interests |

---

## 7. Pagination

All list/search endpoints use these parameters:

| Param | Type | Default | Max | Description |
|-------|------|---------|-----|-------------|
| `page` | integer | 0 | -- | 0-based page number |
| `size` | integer | 10 | 10 or 20 | Results per page |

The `total` field in the response indicates total matching items. Calculate pages as `ceil(total / size)`.

Check response `total` to determine if more results exist before requesting additional pages.

---

## 8. Important Usage Rules

From the API catalog constraints:

1. **paper_info**: Only for batch basic info. Body must be `{"ids": [...]}`.
2. **paper_detail**: Only for single paper. Param must be `paper_id` (singular) -- never pass `ids`.
3. **person_detail**: Only for single scholar. Param must be `person_id` (singular) -- never pass `ids`.
4. **Efficiency pattern**: Use low-cost endpoints (`paper_info`, `paper_search_pro`) first for filtering, then call `paper_detail` only for the target subset (default top 10).
5. **Default order**: comprehensive/ relevance when `order` not specified.

---

## 9. MCP Tool to REST API Mapping

When running the MCP server, each tool maps to a REST endpoint:

| MCP Tool | REST Endpoint | Method |
|----------|---------------|--------|
| `search_scholar` | `/api/person/search` | POST |
| `search_paper` | `/api/paper/search` | GET |
| `search_patent` | `/api/patent/search` | POST |
| `get_papers_by_ids` | `/api/paper/info` | POST |
| `search_paper_pro` | `/api/paper/search/pro` | GET |
| `search_papers_by_keyword` | `/api/paper/list/by/search/venue` | GET |
| `search_papers_by_venue` | `/api/paper/list/by/search/venue` | GET |
| `search_papers_by_author` | `/api/paper/list/by/search/venue` | GET |
| `search_papers_advanced` | `/api/paper/list/by/search/venue` | GET |

Note: The `search_papers_by_*` tools on the npm MCP server all hit the same `/api/paper/list/by/search/venue` endpoint with different parameter combos (`keyword`, `venue`, `author`).

---

## 10. Rate Limits

No specific QPS or rate limit documentation was found for the AMiner open platform directly. The API console at `https://www.aminer.cn/open/board?tab=control` may show per-token quotas.

Observations:
- Free endpoints (person search, paper search, patent search) are likely rate-limited more aggressively than paid ones.
- The paid endpoints have per-call pricing, suggesting no hard QPS cap on paid calls beyond what the account balance supports.
- Standard best practice: implement exponential backoff on 429 responses.

---

## 11. Quick Start (curl)

```bash
# Set your token
AMINER_TOKEN="your_token_here"

# Search scholars
curl -X POST \
  'https://datacenter.aminer.cn/gateway/open_platform/api/person/search' \
  -H 'Content-Type: application/json;charset=utf-8' \
  -H "Authorization: $AMINER_TOKEN" \
  -d '{"offset":0,"query":"Hinton","size":5}'

# Search papers
curl -X GET \
  'https://datacenter.aminer.cn/gateway/open_platform/api/paper/search?title=BERT&page=0&size=5' \
  -H "Authorization: $AMINER_TOKEN"

# Search patents
curl -X POST \
  'https://datacenter.aminer.cn/gateway/open_platform/api/patent/search' \
  -H 'Content-Type: application/json;charset=utf-8' \
  -H "Authorization: $AMINER_TOKEN" \
  -d '{"query":"graphene","page":0,"size":5}'

# Semantic paper search
curl -X POST \
  'https://datacenter.aminer.cn/gateway/open_platform/api/paper/qa/search' \
  -H 'Content-Type: application/json;charset=utf-8' \
  -H "Authorization: $AMINER_TOKEN" \
  -d '{"query":"deep learning for NLP","use_topic":false,"size":5}'

# Get paper detail by ID
curl -X GET \
  'https://datacenter.aminer.cn/gateway/open_platform/api/paper/detail?paper_id=53e9a8ccb7602d970321b422' \
  -H "Authorization: $AMINER_TOKEN"

# Batch get paper info
curl -X POST \
  'https://datacenter.aminer.cn/gateway/open_platform/api/paper/info' \
  -H 'Content-Type: application/json;charset=utf-8' \
  -H "Authorization: $AMINER_TOKEN" \
  -d '{"ids":["53e9a8ccb7602d970321b422","53e9a8ccb7602d970321b423"]}'
```

---

## 12. Sources

1. AMiner Open Platform docs: https://open.aminer.cn/docs
2. AMiner API Console: https://www.aminer.cn/open/board?tab=control
3. 302.ai Apifox -- Paper Search API: https://302ai-en.apifox.cn/357751149e0
4. 302.ai Apifox -- Scholar Search: https://s.apifox.cn/apidoc/docs-site/4012774/357270678e0
5. 302.ai Apifox -- Paper QA Search: https://s.apifox.cn/apidoc/docs-site/4012774/357473941e0
6. 302.ai Apifox -- AMiner Meditation: https://302ai-en.apifox.cn/357688192e0
7. npm @scipen/aminer-mcp-server: https://www.npmjs.com/package/@scipen/aminer-mcp-server
8. AMiner MCP Server docs (detailed): https://himcp.ai/server/aminer-mcp-server-qle
9. Glama schema: https://glama.ai/mcp/servers/scipenai/aminer-mcp-server/schema
10. WeChat article (AMiner platform intro): http://mp.weixin.qq.com/s?__biz=MzkyMzI3NzQ0Mg==&mid=2247485645&idx=1
11. Cnblogs AMiner + OpenClaw: https://www.cnblogs.com/AI4Science/p/19660345/aminer-researchlabs
12. Web search across multiple engines for endpoint/parameter details (2026-06-14).
