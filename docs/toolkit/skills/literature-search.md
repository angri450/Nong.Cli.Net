# Literature Search (文献搜索)

## When to use

Use this skill when the user asks for:
- Literature search across any academic databases
- Scholar/author lookup
- Patent search
- Research direction literature review
- Paper metadata fetching
- Web page reading/parsing for academic purposes
- RAG-based research Q&A
- Chinese academic resource search (AMiner, Metaso)

## How this skill works

This skill orchestrates THREE independent tool groups. Pick the right tool for each task — you can mix them in a single conversation.

```
                    User asks: "搜沸石缓释肥料"
                           |
           ┌───────────────┼───────────────┐
           |               |               |
     Foreign papers    Chinese papers    AI-powered
     (lit search)      (aminer)          (metaso)
     OpenAlex          scholar           search
     Crossref          paper             fetch
     Unpaywall         patent            chat/RAG
                       org
                       venue
```

## Tool Group A: `lit` — Foreign Literature Pipeline

**Best for**: Structured English paper search with CNKI-like DSL syntax. Local cache with dedup. Word export.

### Commands

| Command | Purpose | Example |
|---------|---------|---------|
| `nong lit search` | Execute foreign literature search | `nong lit search --query "zeolite slow release fertilizer" --limit 10 --json` |
| `nong lit parse` | Check DSL syntax | `nong lit parse --query "SU='沸石'*'缓释'" --json` |
| `nong lit plan` | See generated rough queries | `nong lit plan --query "SU='沸石'*'缓释'" --json` |
| `nong lit batch` | Batch search a directory of DSL files | `nong lit batch 搜索文献/ -o 报告.md --limit 10` |
| `nong lit export` | Convert results to Markdown/BibTeX | `nong lit export --input result.json --format markdown -o refs.md` |
| `nong lit cache-import` | Import search results into local cache (LiteDB) | `nong lit cache-import --input result.json` |
| `nong lit cache-query` | Query local cache by year/citations/keywords | `nong lit cache-query --min-year 2020 --min-citations 10 --limit 20` |
| `nong lit cache-stats` | Show cache statistics | `nong lit cache-stats` |
| `nong lit cache-export` | Export cache as markdown (Claude context) | `nong lit cache-export --limit 5 --max-chars 8000` |
| `nong lit cache-to-word` | Export cache as Word template JSON | `nong lit cache-to-word --limit 1 -o data.json` |
| `nong lit cache-search-word` | DSL-filter cache → auto-fill Word template | `nong lit cache-search-word --dsl "SU='zeolite'*'fertilizer'" --template report.docx` |

### Parameters

- `--query` / `-q`: Search query (plain keywords or DSL)
- `--limit`: Result count (default 5)
- `--sources`: Providers (default: openalex,crossref,unpaywall)
- `--mode`: `strict` (precise) or `recall` (lenient, better for Chinese)
- `--profile`: `balanced` | `classic` | `recent`
- `--json`: Output structured JSON

### DSL Quick Reference

```
SU='沸石'*'缓释'                               # AND
SU='沸石'+'缓释'                               # OR
AU = '钱伟长' AND (AF='清华' OR AF='北大')     # Complex boolean
AB='转基因/NEAR 5水稻'                         # Proximity
YE BETWEEN(2020,2025)                          # Year range
FT='大数据 $5'                                 # Word frequency
```

### Supported Fields

SU(Topic) TI(Title) AB(Abstract) KY(Keywords) FT(FullText) AU(Author) AF(Institution) YE(Year) CF(CitationCount) JN(Journal) DOI

### When to use `lit`

- When the user wants English academic papers
- When the user writes a CNKI-like DSL query
- When the user wants structured, filterable results from foreign databases
- When doing systematic literature reviews with precise criteria

---

## Tool Group B: `aminer` — Chinese Academic Platform

**Best for**: Chinese scholar/paper/patent search, institution lookup, journal discovery.

### Commands

| Command | Purpose | Example |
|---------|---------|---------|
| `nong aminer scholar` | Search scholars by name/field | `nong aminer scholar -q "张钹" --size 10 --json` |
| `nong aminer paper` | Search papers by keyword | `nong aminer paper -q "深度学习 自然语言处理" --size 20 --json` |
| `nong aminer patent` | Search patents by keyword | `nong aminer patent -q "沸石 缓释 肥料" --size 10 --json` |
| `nong aminer org` | Search organizations | `nong aminer org -q "清华大学" --json` |
| `nong aminer venue` | Search journals/venues | `nong aminer venue -q "中国科学" --json` |
| `nong aminer paper-info` | Get paper details by IDs | `nong aminer paper-info --ids 53e9a331b7602d9701e7b0d1 --json` |
| `nong aminer patent-info` | Get patent details by ID | `nong aminer patent-info --id "CN1234567A" --json` |

### Parameters

- `--query` / `-q`: Search keyword (required)
- `--size` / `--page` / `--offset`: Pagination (default 10)
- `--order`: Sort order for papers (`citation` | `year`)
- `--ids`: Paper IDs for paper-info
- `--id`: Patent ID for patent-info

### When to use `aminer`

- When the user wants Chinese academic papers, scholars, or patents
- When looking up a specific scholar's profile and publications
- When searching Chinese institutions or journals
- When checking Chinese patent information
- When the user speaks Chinese and wants Chinese-language academic resources

### Auth

Requires `NONG_LIT_AMINER_KEY` env var (JWT from https://open.aminer.cn). Free tier covers all 7 endpoints.

---

## Tool Group C: `metaso` — AI-Powered Search Engine

**Best for**: Broad Chinese web+academic search, page reading, AI RAG answers.

### Commands

| Command | Purpose | Example |
|---------|---------|---------|
| `nong metaso search` | Multi-scope search | `nong metaso search -q "沸石" --scope scholar --summary --json` |
| `nong metaso reader` | Fetch web page (JSON/Markdown) | `nong metaso reader --url "https://..." --format markdown -o article.md` |
| `nong metaso chat` | RAG: AI research with citations | `nong metaso chat -q "沸石在农业中的应用" --model fast_thinking --stream` |

### Search Scopes

| Scope | Meaning | Returns |
|-------|---------|---------|
| `scholar` | Academic papers | Title, authors, snippet, year |
| `webpage` | Web pages | Title, link, snippet, source domain |
| `document` | Documents (PDF, DOC, etc.) | Title, link, snippet |
| `image` | Images | Title, link, thumbnail URL |
| `video` | Videos | Title, link, thumbnail, source |
| `podcast` | Podcast episodes | Title, link, description |

### Search Parameters

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `--query` / `-q` | string | (required) | Search query |
| `--scope` | string | `scholar` | Search scope |
| `--size` | int | 10 | Result count (max 50) |
| `--summary` | bool | false | Include AI-generated summary of results |
| `--concise` | bool | true | Return concise snippet (shorter, faster) |

### Reader Parameters

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `--url` | string | (required) | URL to fetch |
| `--format` | string | `json` | `json` (structured with title/content) or `markdown` (clean MD text) |
| `-o` | string | — | Save content to file |

**Word integration**: Reader output feeds directly into DocxTemplate `cellReplace`. Fetch a page as Markdown, pipe into a Word template cell:

```powershell
# Fetch page content as clean Markdown
nong metaso reader --url "https://example.com/paper" --format markdown -o article.md

# Then use the Markdown as fill data in Word cellReplace
nong word fill --template report.docx --json '{ "article_content": "<read from article.md>" }'
```

### Chat Models

| Model | Search Scope | Description | Use Case |
|-------|-------------|-------------|----------|
| `fast` | scholar/webpage | Fast reasoning, concise answer | Quick fact-check, simple Q&A |
| `fast_thinking` | scholar/webpage | Fast reasoning + strong info synthesis | Research Q&A with citations (RECOMMENDED) |
| `ds-r1` | scholar/webpage | DeepSeek-R1 reasoning model | Complex reasoning, multi-step analysis |
| `fast-scholar` | scholar | Fast academic search | Quick literature scan |
| `fast_thinking-scholar` | scholar | Fast reasoning + academic synthesis | Detailed paper analysis |
| `ds-r1-scholar` | scholar | DeepSeek-R1 academic reasoning | Complex academic deep research |

### Chat Parameters

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `--query` / `-q` | string | (required) | Research question |
| `--model` | string | `fast_thinking` | Model (see table above) |
| `--scope` | string | `scholar` | Search scope: `scholar` or `webpage` |
| `--stream` | bool | false | Enable real-time SSE streaming output |
| `--concise` | bool | true | Return concise original-text match snippets |
| `-o` | string | — | Save answer to file |

### When to use `metaso`

- When you need the broadest Chinese search coverage (metaso indexes more Chinese sources than AMiner)
- When you want AI to synthesize an answer from search results (RAG)
- When you need to read/fetch the full text content of a web page
- When doing preliminary research before structured DSL queries
- When the user asks open-ended questions about a research topic

### Auth

Requires `NONG_LIT_METASO_KEY` env var (from https://metaso.cn). Free tier: 5,000 points.

---

## Orchestration Guide

### Pattern 1: Initial broad scan → precise search

```
1. metaso chat -q "what are the recent advances in [topic]?" --model concise-scholar
   → Get AI overview and discover key terms

2. lit search -q "SU='[key1]'*'[key2]'" --limit 10 --json
   → Structured search in foreign databases

3. aminer paper -q "[Chinese keywords]" --size 10 --json
   → Complement with Chinese papers
```

### Pattern 2: Scholar investigation

```
1. aminer scholar -q "[scholar name]" --json
   → Find scholar profile

2. aminer paper -q "[scholar name]" --order citation --size 20 --json
   → Get their papers sorted by citations

3. metaso reader --url "https://www.aminer.cn/profile/[id]" --json
   → Fetch full profile page if needed
```

### Pattern 3: Patent landscape

```
1. aminer patent -q "[technology key]" --size 20 --json
   → Chinese patent search

2. aminer patent-info --id "[patent_id]" --json
   → Get specific patent details
```

### Pattern 4: Literature review workflow

```
1. lit batch 搜索文献/ -o report.md --limit 10
   → Batch DSL search across multiple topics

2. For each paper found:
   metaso reader --url "[landing_page_url]" --format markdown -o paper.md
   → Fetch paper page for full abstract/context

3. metaso chat -q "summarize the research gap between [paper1] and [paper2]"
   → AI synthesis across papers
```

### Pattern 5: AI deep research

```
1. metaso chat -q "[complex research question]" --model research-scholar
   → AI does multi-iteration search and synthesis

2. Extract references from the answer, then:
   lit search --query "[paper title]" --json
   → Verify and find metadata for referenced papers
```

### Pattern 6: Literature → Word report

```
1. lit search --query "SU='zeolite'*'fertilizer'" --limit 100 -o result.json --json
   → Broad search, save results

2. lit cache-import --input result.json
   → Store in local cache (DOI dedup)

3. lit cache-search-word --dsl "SU='zeolite'*'fertilizer' AND YE BETWEEN(2020,2025)" --template report.docx
   → Filter cache by DSL, auto-fill Word template with cellReplace + tableRows
   → Output: report_filled.docx (ready to distribute)

4. For individual paper deep-dive:
   lit cache-to-word --limit 1 -o paper_detail.json
   nong word fill --template paper_template.docx --json paper_detail.json -o paper.docx
```

See full Word bridge reference: `docs/toolkit/skills/literature-word-bridge.md`

---

## Decision Tree

```
What does the user need?
|
├─ English academic papers with precise criteria
│  └─ Use: lit search (with DSL) or lit batch
|
├─ Chinese scholars / papers / patents
│  └─ Use: aminer scholar | paper | patent
|
├─ Chinese academic papers (broadest coverage)
│  └─ Use: metaso search --scope scholar
|
├─ General web search (Chinese or English)
│  └─ Use: metaso search --scope web
|
├─ AI-synthesized answer based on search results
│  └─ Use: metaso chat
|
├─ Get text content of a specific URL
│  └─ Use: metaso reader
|
├─ Multiple research directions → one report
│  └─ Use: lit batch
|
├─ Cache and manage search results locally
│  └─ Use: lit cache-import | cache-query | cache-stats
│
├─ Export to Word (cellReplace + tableRows)
│  └─ Use: lit cache-to-word | lit cache-search-word
│
└─ Export citations for paper writing
   └─ Use: lit search → lit export --format markdown
```

---

## Key Differences Between Tools

| Dimension | lit | aminer | metaso |
|-----------|-----|--------|--------|
| Language focus | English | Chinese + English | Chinese + English |
| Query syntax | DSL + plain text | Keyword only | Natural language |
| Search depth | Structured filters | Domain-specific DB | Full web index |
| Result format | PaperRecord list | Typed entities | Search snippets / AI text |
| AI synthesis | No | No | Yes (RAG chat) |
| Offline filtering | Yes (Full DSL) | No (API-only) | No (API-only) |
| Best for | Systematic review | Chinese academia | Broad exploration |
| Free tier | Unlimited (no key needed for OpenAlex/Crossref) | Unlimited free | 5000 pts |

---

## API Keys Setup

```powershell
# One-time setup in PowerShell:
$env:NONG_LIT_OPENALEX_API_KEY = "your-openalex-key"
$env:NONG_LIT_AMINER_KEY        = "your-aminer-jwt"   # From https://open.aminer.cn
$env:NONG_LIT_METASO_KEY        = "mk-..."             # From https://metaso.cn
$env:NONG_LIT_MAILTO            = "your@email.com"   # For Crossref polite pool
```

---

## Code Locations

```
Literature/          — Foreign paper pipeline (OpenAlex, Crossref, Unpaywall)
  Pipeline/LiteratureSearchPipeline.cs
  Dsl/                — CNKI DSL parser
  Data/               — LiteDB cache layer
    LiteratureCache.cs
  Export/
    LiteratureWordBridge.cs  — Word template bridge

Aminer/              — AMiner REST API client (28 endpoints)
  AminerClient.cs

Metaso/              — Metaso AI Search client (search + reader + chat)
  MetasoClient.cs

LiteDB/              — Embedded NoSQL database (238 .cs files, compiled into ThirdParty.dll)

Cli/Commands/
  LitCommands.cs     — lit parse|plan|search|export|batch|cache-*|cache-*-word
  AminerCommands.cs  — aminer scholar|paper|patent|org|venue|paper-*|scholar-*
  MetasoCommands.cs  — metaso search|reader|chat

docs/toolkit/skills/
  literature-search.md      — Multi-tool search guide (this doc)
  aminER-api.md              — AMiner full reference (28 APIs + pricing)
  literature-word-bridge.md  — Lit → Word bridge guide
```

---

*Nong.Cli.Net v4.3.0 — Multi-Tool Literature Search*
