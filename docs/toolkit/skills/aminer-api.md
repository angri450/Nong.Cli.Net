# AMiner — Chinese Academic Platform

## When to use

- Chinese academic paper, scholar, patent, institution, or journal search
- Scholar profile research (publications, citations, projects)
- Patent landscape analysis
- Institution name disambiguation
- Journal/conference analytics

## Auth

JWT token from https://open.aminer.cn → `NONG_LIT_AMINER_KEY` env var.

---

## Pricing Overview

**Free-first principle**: use free search endpoints to discover IDs, then selectively pay for deep details.

| Tier | Count | Price range | Examples |
|------|-------|-------------|----------|
| Free | 7 endpoints | ¥0 | scholar search, paper search, patent search, org search, venue search, paper info, patent info |
| Low | 7 endpoints | ¥0.01–0.05 | paper pro, paper detail, paper QA, org detail, patent detail, org normalize |
| Medium | 4 endpoints | ¥0.10–0.30 | paper citations, org patents, venue papers, venue analytics |
| High | 5 endpoints | ¥0.50–3.00 | scholar portrait, scholar detail, scholar papers, scholar patents, scholar projects |

---

## Free Endpoints (7 total)

### `aminer scholar` — Scholar Search
Search scholars by name, field, or affiliation. Returns scholar ID, name, metrics (paper count, citations, h-index), research interests, organizations.
```bash
nong aminer scholar -q "张钹" --size 10 --json
```
Price: **FREE**

### `aminer paper` — Paper Search
Search papers by keyword, title, or topic.
```bash
nong aminer paper -q "深度学习 自然语言处理" --size 20 --order citation --json
```
Price: **FREE**

### `aminer patent` — Patent Search
Search patents by keyword, inventor, or applicant.
```bash
nong aminer patent -q "沸石 肥料" --size 10 --json
```
Price: **FREE**

### `aminer org` — Organization Search
Search institutions/organizations by name. Returns ID, papers/members count.
```bash
nong aminer org -q "清华大学" --json
```
Price: **FREE**

### `aminer venue` — Venue Search
Search journals and conferences by name. Returns ID, impact factor, citation count.
```bash
nong aminer venue -q "中国科学" --json
```
Price: **FREE**

### `aminer paper-info` — Paper Info (Batch)
Get basic paper details by IDs (space-separated, batch).
```bash
nong aminer paper-info --ids "53e9a331b7602d9701e7b0d1" "abc123" --json
```
Price: **FREE**

### `aminer patent-info` — Patent Info
Get basic patent details by ID.
```bash
nong aminer patent-info --id "CN1234567A" --json
```
Price: **FREE**

---

## Paid Endpoints — Papers (5 total)

### `aminer paper-pro` — Multi-Field Paper Search
Filter by author, institution, venue, year range — all in one query.
```bash
nong aminer paper-pro --keyword "neural network" --author "Hinton" --venue "Nature" --year-from 2020 --year-to 2025 --json
```
Price: **¥0.01/次**

### `aminer paper-detail` — Full Paper Details
Complete paper metadata: title, abstract (CN+EN), authors+orgs, venue, doi, pdf_url, keywords, concepts, citation count.
```bash
nong aminer paper-detail --id "53e9a331b7602d9701e7b0d1" --json
```
Price: **¥0.01/次**

### `aminer paper-qa` — Semantic Paper Search
Natural language query with semantic understanding. Supports SCI-only filter, author/org filter.
```bash
nong aminer paper-qa -q "最新的大模型幻觉检测方法有哪些" --sci --size 20 --json
```
Price: **¥0.05/次** (cheapest paid paper endpoint)

### `aminer paper-citations` — Citation Graph
Get a paper's citing/cited papers list.
```bash
nong aminer paper-citations --id "53e9a331b7602d9701e7b0d1" --relation cited --size 50 --json
```
Price: **¥0.10/次**

### `aminer paper-keywords` — Multi-Keyword Batch Search
Search papers by multiple keywords simultaneously. Useful for broad-topic scanning.
```bash
nong aminer paper-keywords --keywords "graph neural network" "attention mechanism" "transformer" --size 30 --json
```
Price: **¥0.10/次**

---

## Paid Endpoints — Scholars (5 total)

### `aminer scholar-detail` — Full Scholar Details
Complete profile: bio, position, affiliation, education, work experience, contact, metrics, research fields.
```bash
nong aminer scholar-detail --id "53e9a331b7602d9701e7b0d1" --json
```
Price: **¥1.00/次** (most expensive scholar endpoint)

### `aminer scholar-portrait` — Research Portrait
Research interests, fields, academic trajectory. Lighter than full detail.
```bash
nong aminer scholar-portrait --id "53e9a331b7602d9701e7b0d1" --json
```
Price: **¥0.50/次** (cheaper alternative to scholar-detail)

### `aminer scholar-papers` — Scholar's Paper List
All papers by a scholar, sortable by citation count or year.
```bash
nong aminer scholar-papers --id "53e9a331b7602d9701e7b0d1" --order citation --size 50 --json
```
Price: **¥1.50/次**

### `aminer scholar-patents` — Scholar's Patent List
All patents associated with a scholar.
```bash
nong aminer scholar-patents --id "53e9a331b7602d9701e7b0d1" --size 20 --json
```
Price: **¥1.50/次**

### `aminer scholar-projects` — Research Projects / Funding
Research projects, grants, and funding history. Funders, amounts, duration, team members.
```bash
nong aminer scholar-projects --id "53e9a331b7602d9701e7b0d1" --json
```
Price: **¥3.00/次** (most expensive AMiner endpoint)

---

## Paid Endpoints — Orgs / Venues / Patents (6 total)

### `aminer org-detail` — Full Organization Details
Aliases, founding info, description, country, type, logo, metrics.
```bash
nong aminer org-detail --id "xxx" --json
```
Price: **¥0.01/次**

### `aminer org-patents` — Organization's Patent Portfolio
All patents filed by an organization.
```bash
nong aminer org-patents --id "xxx" --size 50 --json
```
Price: **¥0.10/次**

### `aminer org-normalize` — Organization Name Normalization
Disambiguate institution names (e.g. "清华" vs "清华大学" vs "Tsinghua University").
```bash
nong aminer org-normalize -q "清华" --json
```
Price: **¥0.05/次**

### `aminer patent-detail` — Full Patent Details
Complete patent metadata: abstract, claims, IPC/CPC classification, filing/grant dates, legal status.
```bash
nong aminer patent-detail --id "CN1234567A" --json
```
Price: **¥0.01/次**

### `aminer venue-papers` — Venue Paper List
All papers published in a journal/conference.
```bash
nong aminer venue-papers --id "xxx" --order year --size 50 --json
```
Price: **¥0.10/次**

### `aminer venue-analytics` — Venue Analysis Report
Year-by-year paper/citation counts, top authors, top keywords, publication trends.
```bash
nong aminer venue-analytics --id "xxx" --json
```
Price: **¥0.30/次**

---

## Recommended Workflows

### Cost-efficient literature review
```
1. aminer scholar -q "[researcher name]" --json                      # free → get scholar ID
2. aminer scholar-papers --id "[scholar_id]" --size 50 --json       # ¥1.50 → full paper list
3. aminer paper-info --ids "[id1]" "[id2]" "[id3]" --json           # free → batch basic info
4. aminer paper-detail --id "[most_interesting_paper]" --json       # ¥0.01 → deep dive into one paper
```
Total cost: **¥1.51** for a complete scholar literature review.

### Patent landscape
```
1. aminer patent -q "[technology keyword]" --size 20 --json          # free → patent search
2. aminer patent-detail --id "[relevant_patent]" --json              # ¥0.01 → full details
3. aminer org-patents --id "[assignee_org_id]" --size 50 --json     # ¥0.10 → full portfolio
```
Total cost: **¥0.11** for a complete patent landscape.

### Journal evaluation
```
1. aminer venue -q "[journal name]" --json                           # free → find venue ID
2. aminer venue-analytics --id "[venue_id]" --json                  # ¥0.30 → full analytics
3. aminer venue-papers --id "[venue_id]" --order citation --size 20 --json  # ¥0.10 → top papers
```
Total cost: **¥0.40** for a complete journal evaluation.

---

## Field Reference

### Scholar fields (free search)
`id, name, name_zh, avatar, homepage, paper_count, citation_count, h_index, pubs, interests(tags), orgs`

### Scholar fields (paid detail/portrait — additional)
`bio, position, affiliation, email, phone, education, work_experience, research_fields`

### Paper fields (free search)
`id, title, title_zh, year, citation_count, doi, authors, keywords, keywords_zh, venue(name, name_zh, type)`

### Paper fields (paid detail — additional)
`abstract, abstract_zh, pdf_url, landing_page, volume, issue, pages, publisher, lang, concepts(fos)`

### Patent fields
`id, title, title_zh, abstract, year, patent_no, patent_type, applicant, inventors, country, status, filing_date, grant_date, ipc, cpc`

### Venue fields
`id, name, name_zh, type, homepage, issn, publisher, country, rank, impact_factor, paper_count, citation_count, h_index`

### Project fields
`id, title, title_zh, abstract, year, funder, amount, duration, status, pi, members`

---

## Quick Price Reference Card

```
┌──────────────────────────────────────────────┐
│              AMiner Pricing                  │
├──────────────┬─────────┬─────────────────────┤
│ ¥0.00        │ 7 APIs  │ all search + info   │
│ ¥0.01        │ 4 APIs  │ paper/patent/org detail, paper pro │
│ ¥0.05        │ 2 APIs  │ paper QA, org normalize            │
│ ¥0.10        │ 4 APIs  │ paper citations, org patents, venue papers, paper keywords │
│ ¥0.20        │ 1 API   │ conditional paper detail           │
│ ¥0.30        │ 1 API   │ venue analytics                   │
│ ¥0.50        │ 1 API   │ scholar portrait                  │
│ ¥1.00        │ 1 API   │ scholar detail                    │
│ ¥1.50        │ 2 APIs  │ scholar papers, scholar patents   │
│ ¥3.00        │ 1 API   │ scholar projects                  │
├──────────────┼─────────┼─────────────────────────────────────┤
│ Total        │ 28 APIs │ 6 models                          │
└──────────────┴─────────┴─────────────────────────────────────┘
```
