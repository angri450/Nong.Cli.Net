# Metaso (秘塔AI搜索) API 文档

> 整理日期: 2026-06-14
> 来源: 官方 SDK 文档、MCP 服务端源码 (HundunOnline/mcp-metaso)、CSDN 技术文章、秘塔官方平台页面

---

## 1. 概述

秘塔AI搜索 (Metaso) 提供两种接入方式:

1. **REST API (官方)** -- 需要 API Key (以 `mk-` 开头)，通过 HTTPS 直接调用
2. **MCP Server (非官方封装)** -- 基于 FastMCP SDK，封装了 REST API，面向 Claude Desktop 等 MCP 客户端

两种方式底层调用同一套 REST 接口。

### 搜索能力

Metaso 支持六种搜索范围 (scope):

| scope | 中文名 | 说明 |
|-------|--------|------|
| `webpage` | 网页 | 新闻、博客、通用网页 |
| `document` | 文库 | PDF、技术文档 |
| `scholar` | 学术 | 论文、研究文献 |
| `image` | 图片 | 图片、图表、插图 |
| `video` | 视频 | 教程、演讲、娱乐视频 |
| `podcast` | 播客 | 音频节目、访谈 |

**关键结论: Metaso 支持学术搜索 (`scope="scholar"`)，不是纯网页搜索引擎。** Scholar 结果包含: 标题、作者、链接/URL、摘要、日期/年份、分数、位置、期刊/会议名、引用数、DOI。

### 搜索深度模式（仅逆向API / MCP 封装中确认）

根据 YXYAXA/metaso 逆向项目，秘塔前端支持三种深度模式:

| 模式 | 说明 |
|------|------|
| `concise` (简洁) | 快速回答，简短摘要 |
| `detail` (深入) | 更深入的分析 |
| `research` (研究) | 多线迭代追搜直到逻辑闭环 |

这些模式在**官方 REST API** 中没有作为参数暴露；官方 API 的搜索深度由专题 (topic) 配置控制。逆向 API 项目通过 OpenAI 兼容接口 (`/v1/chat/completions`) 间接暴露这些模式。

---

## 2. 官方 REST API

### 2.1 认证

```
Authorization: Bearer {api-key}
```

- API Key 格式: `mk-` 开头，例如 `mk-0D30xxxxxxxxxxxxxxxxx`
- 获取方式: 登录 https://metaso.cn ，在专题设置中开启并获取
- 新用户注册赠送 5000 次免费调用

### 2.2 接口: 搜索 (v2 专题搜索)

**搜索第一轮结果中提到的官方接口地址有两个版本: `https://metaso.cn/api/open/search/v2` 和 `https://metaso.cn/api/v1/search`。v2 是专题绑定搜索 (需要 searchTopicId)，v1 是自由搜索 (通过 scope 指定范围)。以下分别记录。**

#### 2.2.1 自由搜索 (v1)

```
POST https://metaso.cn/api/v1/search
Content-Type: application/json
Authorization: Bearer {api-key}
```

**请求参数:**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `q` | string | 是 | 搜索查询 |
| `scope` | string | 是 | 搜索范围: `webpage`, `document`, `scholar`, `image`, `video`, `podcast` |
| `includeSummary` | boolean | 否 | 是否包含 AI 摘要 (默认 false) |
| `includeRowContent` | boolean | 否 | 是否包含原始内容 (默认 false) |
| `size` | string | 否 | 结果数量，如 `"10"` |

**请求示例 (Python httpx):**

```python
import httpx

async with httpx.AsyncClient() as client:
    resp = await client.post(
        "https://metaso.cn/api/v1/search",
        json={
            "q": "人工智能最新进展",
            "scope": "scholar",
            "includeSummary": False,
            "size": "10"
        },
        headers={
            "Authorization": "Bearer mk-xxxxxxxx",
            "Accept": "application/json",
            "Content-Type": "application/json"
        }
    )
    data = resp.json()
```

**响应结构 (根据 MCP 服务端源码推断):**

```json
{
  "summary": "...",        // 仅当 includeSummary=true 时存在
  "webpages": [...],       // scope=webpage 的结果
  "documents": [...],      // scope=document 的结果
  "scholars": [...],       // scope=scholar 的结果
  "images": [...],         // scope=image 的结果
  "videos": [...],         // scope=video 的结果
  "podcasts": [...]        // scope=podcast 的结果
}
```

**各 scope 返回字段:**

- **webpage:** `title`, `link`, `snippet`, `displayDate`
- **document:** `title`, `authors` (list or string), `link`/`url`, `snippet`/`abstract`, `score`, `position`, `source`, `publishDate`
- **scholar:** `title`, `authors`, `link`/`url`, `snippet`/`abstract`, `date`/`year`, `score`, `position`, `venue`/`journal`, `citationCount`, `doi`
- **image:** `title`, `imageUrl`, `imageWidth`/`imageHeight`, `score`, `position`, `sourceUrl`/`link`, `description`
- **video:** `title`, `authors`/`channel`, `link`/`url`, `snippet`, `duration` (秒), `date`/`publishDate`, `score`, `position`, `coverImage`/`thumbnail`, `viewCount`
- **podcast:** `title`, `authors`/`host`, `link`/`url`, `snippet`, `duration` (秒), `date`/`publishDate`, `score`, `position`, `podcastName`/`show`, `audioUrl`

#### 2.2.2 专题搜索 (v2)

```
POST https://metaso.cn/api/open/search/v2
Content-Type: application/json
Authorization: Bearer {api-key}
```

**请求参数:**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `question` | string | 是 | 搜索问题 |
| `searchTopicId` | string | 是 | 专题ID，限定只从上传到专题的文件里搜索 |
| `stream` | boolean | 否 | 是否流式返回 (默认 false) |
| `lang` | string | 否 | 输出语言: `zh` (中文) / `en` (英文)，默认 zh |
| `needHighlight` | boolean | 否 | 是否需要高亮信息 (默认 false) |

**流式响应 SSE 事件类型 (stream=true 时):**

| type | 说明 |
|------|------|
| `balance` | 余额信息 |
| `heartbeat` | 心跳消息 (可忽略) |
| `query` | 搜索基础信息 (包含 sessionId) |
| `set-reference` | 返回 resultId 和来源列表 |
| `append-text` | 回答文本 (逐字流式输出) |
| `answer-link-num-highlights` | 高亮信息 |
| `error` | 异常信息 |
| `[DONE]` | 回答结束 |

### 2.3 接口: 网页全文读取

```
POST https://metaso.cn/api/v1/reader
Content-Type: application/json
Authorization: Bearer {api-key}
```

**请求参数:**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 要读取的网页 URL |

**响应:**
- `Accept: text/plain` 返回 Markdown 格式
- `Accept: application/json` 返回 JSON 格式

自动去除广告和格式噪音，保留图片和段落结构。

### 2.4 专题管理接口

#### 创建专题

```
PUT https://metaso.cn/api/open/topic
Authorization: Bearer {api-key}
```

**参数:** `name` (string, 必填), `description` (string, 可选)

#### 上传文件到专题

```
PUT https://metaso.cn/api/open/file/{dirId}
Content-Type: multipart/form-data
Authorization: Bearer {api-key}
```

**参数:** `file` (文件)

#### 查看文件处理进度

```
GET https://metaso.cn/api/open/file/{fileId}/progress
Authorization: Bearer {api-key}
```

返回 0-100 的进度百分比。

#### 删除专题

```
POST https://metaso.cn/api/open/topic/trash
Authorization: Bearer {api-key}
```

**参数:** `ids` (专题ID数组)

### 2.5 定价

- 每次请求约 0.03 元
- 新用户注册赠送 5000 次免费调用
- 面向个人开发者和中小团队

---

## 3. MCP Server (非官方)

### 3.1 基本信息

- **项目:** HundunOnline/mcp-metaso (GitHub)
- **SDK:** FastMCP
- **环境变量:** `METASO_API_KEY` (必填)
- **日志级别:** `MCP_LOG_LEVEL` (可选，输出到 stderr)

### 3.2 MCP 工具列表

#### 工具: `metaso_search`

**描述:** 多维搜索工具，支持六种搜索范围。

**输入参数:**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `query` | string | 是 | - | 搜索查询词 |
| `scope` | string | 否 | `"webpage"` | 搜索范围 |
| `include_summary` | boolean | 否 | `false` | 是否包含 AI 摘要 |
| `size` | integer | 否 | `10` | 结果数量 (1-20) |

**scope 允许值:** `"webpage"`, `"document"`, `"scholar"`, `"image"`, `"video"`, `"podcast"`

**输出:** 格式化后的 Markdown 文本，每条结果带有序号、标题、链接和元数据。结尾附结果计数摘要。

**底层调用:** `POST {base_url}/search`，映射到 REST API 的 `/api/v1/search`。

#### 工具: `metaso_reader`

**描述:** 网页内容读取工具，提取并转换页面内容。

**输入参数:**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `url` | string | 是 | - | 要读取的网页 URL |
| `output_format` | string | 否 | `"markdown"` | 输出格式: `"markdown"` 或 `"json"` |

**输出:** Markdown 或 JSON 格式的页面内容。

**底层调用:** `POST {base_url}/reader`，映射到 REST API 的 `/api/v1/reader`。

#### 工具: `metaso_chat`

**描述:** 基于搜索增强的 AI 问答 (在部分来源列出，但 MCP 服务端源码中未发现独立实现，可能由 search 的 `include_summary=true` 实现)。

---

## 4. 官方 Python SDK

### 4.1 安装

```bash
pip install metaso-sdk
```

- PyPI: https://pypi.org/project/metaso-sdk/
- GitHub: https://github.com/meta-sota/metaso-sdk
- 文档: https://meta-sota.github.io/metaso-sdk/

### 4.2 核心类和方法

| 类/函数 | 说明 |
|---------|------|
| `search(query, stream=False, topic=None)` | 执行搜索 |
| `Query(question, sessionId=None)` | 构建查询对象 |
| `create_topic(Topic(name=...))` | 创建专题 |
| `upload_directory(topic, path)` | 上传目录到专题 |
| `Topic` | 专题对象 |

### 4.3 使用示例

```python
from metaso_sdk import search, Query

# 基本搜索
result = search(Query(question="人工智能"))

# 流式搜索
for chunk in search(Query(question="人工智能"), stream=True):
    print(chunk)
    # chunk 类型: {'type': 'heartbeat'} 或 {'text': '...', 'type': 'append-text'}

# 续写搜索 (多轮对话)
result = search(Query(question="广播公司", sessionId="8550018047390023680"))

# 专题搜索
from metaso_sdk import create_topic, Topic
topic = create_topic(Topic(name="functional programming"))
result = search(Query(question="monad"), topic=topic)
```

### 4.4 认证

SDK 通过环境变量 `METASO_API_KEY` 读取 API Key。

---

## 5. 总结对比

| 维度 | 官方 REST API | MCP Server | Python SDK |
|------|---------------|------------|------------|
| 认证 | Bearer Token (mk-...) | 环境变量 METASO_API_KEY | 环境变量 METASO_API_KEY |
| 搜索范围 | 6 种 scope | 6 种 scope | 通过 SDK 间接支持 |
| 学术搜索 | 支持 (scope=scholar) | 支持 (scope=scholar) | 支持 |
| 流式输出 | v2 接口支持 SSE | 不支持 (非流式) | 支持 |
| 网页读取 | /api/v1/reader | metaso_reader 工具 | 通过 SDK 间接支持 |
| 专题管理 | 创建/上传/删除 | 不支持 | create_topic/upload_directory |
| 深度模式 | 专题配置控制 | 不支持 | 不支持 |
| 官方维护 | 是 | 否 (社区) | 是 |

---

## 6. 参考文献

- 秘塔官方 API 文档: https://metaso.cn/subject/8547516269457154048
- 官方 Python SDK: https://github.com/meta-sota/metaso-sdk
- 官方 SDK 文档: https://meta-sota.github.io/metaso-sdk/
- MCP Server (非官方): https://github.com/HundunOnline/mcp-metaso
- ModelScope MCP 市场: https://www.modelscope.cn/mcp/servers/metasota/metaso-search
- 逆向 API 项目 (仅供学习): https://github.com/YXYAXA/metaso
- 秘塔官网: https://metaso.cn/
