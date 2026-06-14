# OpenAlex REST API Reference

Base URL: `https://api.openalex.org`

---

## 1. Authentication

### Getting a Key

- Register at `openalex.org` (free, ~30 seconds).
- Copy your key from `openalex.org/settings/api`.
- Append `api_key=YOUR_KEY` to every request.

```
GET https://api.openalex.org/works?api_key=YOUR_KEY
```

### Pricing Model

Freemium: $1/day free credit, resets at midnight UTC. Singleton lookups (by ID/DOI) are always free and unlimited.

| Operation | Cost per 1,000 calls |
|---|---|
| Get singleton (by ID/DOI) | Free |
| List + filter | $0.10 |
| Search (full-text keyword) | $1 |
| Semantic search (AI) | $1 |
| Content download (cached PDF) | $10 |

### Daily Free Budget ($1)

| Activity | Limit |
|---|---|
| Singleton lookups | Unlimited |
| List + filter calls | 10,000 calls / ~1,000,000 results |
| Search calls | 1,000 calls / ~100,000 results |
| Content downloads | 100 downloads |

### Rate Limit Headers

Every response includes:

| Header | Meaning |
|---|---|
| `X-RateLimit-Limit` | Total daily limit (USD) |
| `X-RateLimit-Remaining` | Remaining budget today |
| `X-RateLimit-Credits-Used` | Cost of the current request |
| `X-RateLimit-Reset` | Seconds until midnight UTC reset |

### Quota Endpoint

```
GET https://api.openalex.org/rate-limit?api_key=YOUR_KEY
```

Returns:

- `daily_budget_usd`
- `daily_used_usd`
- `daily_remaining_usd`
- `prepaid_balance_usd` / `prepaid_remaining_usd`
- `resets_in_seconds`
- `endpoint_costs_usd` — breakdown by operation type

### Hard Limits

| Constraint | Limit |
|---|---|
| Requests per second | 100 |
| `per_page` maximum | 100 |
| `sample` maximum | 10,000 |
| Basic paging limit | 10,000 results (use cursor for beyond) |
| OR values per filter | 100 |
| 429 error | Daily budget exhausted or >100 req/s |

### Prepaid Balance

Buy credit to cover usage beyond the $1/day budget. Prepaid credit does not expire daily.

### Usage Dashboard

`openalex.org/settings/usage`

---

## 2. Entity Endpoints

21 entity types, each at its own `/entity` path:

| Entity | Endpoint | Description |
|---|---|---|
| Works | `/works` | Scholarly articles, books, datasets |
| Authors | `/authors` | Disambiguated researchers |
| Sources | `/sources` | Journals, repositories, conferences |
| Institutions | `/institutions` | Universities, research orgs |
| Topics | `/topics` | Research area classifications |
| Keywords | `/keywords` | Short phrases from works |
| Publishers | `/publishers` | Publishing organizations |
| Funders | `/funders` | Funding agencies |
| Awards | `/awards` | Research grants |
| Domains | `/domains` | Top-level topic hierarchy |
| Fields | `/fields` | Second-level topic hierarchy |
| Subfields | `/subfields` | Third-level topic hierarchy |
| SDGs | `/sdgs` | UN Sustainable Development Goals |
| Countries | `/countries` | Geographic entities |
| Continents | `/continents` | Geographic entities |
| Languages | `/languages` | Language classifications |
| Work Types | `/work-types` | Enumeration of work types |
| Source Types | `/source-types` | Enumeration of source types |
| Institution Types | `/institution-types` | Enumeration of institution types |
| Licenses | `/licenses` | Enumeration of licenses |
| Concepts | `/concepts` | Legacy taxonomy (deprecated) |

### Core Operations Per Entity

| Operation | Pattern | Example |
|---|---|---|
| List | `GET /{entities}` | `GET /works` |
| Get single | `GET /{entities}/{id}` | `GET /works/W2741809807` |
| Filter | `GET /{entities}?filter=` | `GET /works?filter=publication_year:2024` |
| Search | `GET /{entities}?search=` | `GET /works?search=machine+learning` |
| Aggregate | `GET /{entities}?group_by=` | `GET /works?group_by=type` |

### External ID Lookups

```
GET /works/doi:10.7717/peerj.4375
GET /works/https://doi.org/10.7717/peerj.4375
GET /works/pmid:29456894
GET /authors/https://orcid.org/0000-0001-6187-6610
GET /institutions/https://ror.org/0161xgx34
```

---

## 3. Query Parameters

| Parameter | Description |
|---|---|
| `api_key` | API key (recommended) |
| `filter` | Filter by field values (comma-separated `field:value`) |
| `search` | Full-text search |
| `search.exact` | Exact search (no stemming, supports wildcards) |
| `search.semantic` | AI embedding-based conceptual search |
| `sort` | Sort by field (append `:desc` for descending) |
| `per_page` | Results per page (1-100, default 25) |
| `page` | Page number (starts at 1) |
| `cursor` | Cursor token for deep pagination (>10,000 results) |
| `sample` | Random sample of N results (max 10,000) |
| `select` | Comma-separated list of fields to return |
| `group_by` | Aggregate results by field |

---

## 4. Common Response Structure

```json
{
  "meta": {
    "count": 286750097,
    "db_response_time_ms": 152,
    "page": 1,
    "per_page": 25,
    "groups_count": null,
    "cost_usd": 0.0001
  },
  "results": [ /* entity objects */ ],
  "group_by": [ /* populated only when group_by is used */ ]
}
```

### Pagination

- **Page-based:** Use `page` and `per_page`. Capped at 10,000 results.
- **Cursor-based:** Use `cursor` for deep pagination through large result sets.

---

## 5. Filter Syntax

Format: `filter=field:value` with comma-separated pairs.

### Operators

| Operator | Syntax | Example |
|---|---|---|
| Exact match (default) | `field:value` | `type:article` |
| Inequality | `field:>N` or `field:<N` | `cited_by_count:>100` |
| Negation (NOT) | `field:!value` | `is_oa:!true` |
| AND (across attributes) | `f1:v1,f2:v2` | `cited_by_count:>1,is_oa:true` |
| AND (within attribute) | `f:v1,f:v2` or `f:v1+v2` | `institutions.country_code:fr+gb` |
| OR (within attribute) | `f:v1\|v2` | `type:article\|book` |
| Date range | `from_date:YYYY-MM-DD,to_date:YYYY-MM-DD` | `from_publication_date:2022-01-01,to_publication_date:2022-01-26` |

- OR with `|` capped at 100 values per filter.
- OR is only valid within a single filter attribute (cross-attribute OR returns an error).
- `+` syntax does not work for search, boolean, or numeric filters.
- Filters are case-insensitive.
- Negate any filter by prepending `!` to the value.

---

## 6. Search Syntax

Search costs $1/1,000 calls. Default search stems words and removes stop words. `search.exact` disables stemming and enables wildcards/fuzzy. `search.semantic` uses AI embeddings.

### Searchable Fields by Entity

| Entity | Fields searched |
|---|---|
| Works | `title`, `abstract`, `fulltext` |
| Authors | `display_name`, `display_name_alternatives` |
| Sources | `display_name`, `alternate_titles`, `abbreviated_title` |
| Institutions | `display_name`, `display_name_alternatives`, `display_name_acronyms` |
| Topics/Keywords | `display_name`, `description` |

### Boolean Operators

Uppercase `AND`, `OR`, `NOT`. Default between words is `AND`. Parentheses for grouping.

```
search=(elmo AND "sesame street") NOT (cookie OR monster)
```

### Phrase Search

Double-quoted strings for exact word sequence matching.

```
search="climate change"
```

### Proximity Search

`~N` after a quoted phrase matches words within N positions.

```
search="climate change"~5
```

### Wildcards

Require `search.exact`:

- `*` matches zero or more characters (trailing only): `machin*` matches "machine", "machines"
- `?` matches exactly one character: `wom?n` matches "woman", "women"
- Minimum 3 characters before wildcard
- Leading wildcards (`*ology`) are unsupported

### Fuzzy Search

`~N` (N = 0, 1, 2) allows up to N character edits. Requires 3+ characters before `~`. Use `search.exact`.

```
search.exact=machin~1
```

### Author Byline Search (`raw_author_name.search`)

Filter-based only, no `search` parameter equivalent. Matches against author names as published.

```raw
filter=raw_author_name.search:"john smith"
```

Critical: unquoted tokens match across ALL authors on a work. Wrap names in quotes to scope to a single byline.

### URL Limit

Approximately 4 KB (~4,094 bytes). Split large OR lists into chunks and union results client-side.

---

## 7. Sort

Format: `sort=field` (ascending) or `sort=field:desc` (descending). Multi-field: comma-separated.

### Sortable Fields

| Field | Notes |
|---|---|
| `display_name` | Alphabetical |
| `cited_by_count` | Citation count |
| `works_count` | Number of works |
| `publication_date` | Publication date (works only) |
| `relevance_score` | Requires an active search filter |

### Examples

```
GET /works?sort=cited_by_count:desc
GET /works?sort=publication_date
GET /works?search=bioplastics&sort=publication_year:desc,relevance_score:desc
```

---

## 8. Group By

Format: `group_by=field` with `:include_unknown` to expose the unknown bucket.

### Response Format

```json
{
  "meta": {
    "count": 286750097,
    "groups_count": 5
  },
  "group_by": [
    {
      "key": "https://openalex.org/T12345",
      "key_display_name": "Machine Learning",
      "count": 4250000
    }
  ]
}
```

- `key` is the OpenAlex ID URL or raw value; `key_display_name` is human-readable.
- Non-entity fields: both `key` and `key_display_name` hold the raw value.
- Unknown bucket: `key` is `"unknown"` for strings, `-111` or `-111.0` for numerics, `false` for booleans.
- Max 200 groups per page. Use cursor paging for more.
- Results sorted by key, not by count.

### Example

```
GET /works?filter=publication_year:2023&group_by=type
GET /works?filter=author.id:A5023888391&group_by=open_access.is_oa
GET /works?group_by=authorships.countries:include_unknown
```

---

## 9. Select Fields

Format: `select=field1,field2,field3`

Limit returned fields to reduce response size. Only the listed fields appear in each result object.

```
GET /works?select=id,doi,title,publication_year,cited_by_count
```

Nested field selection is supported using dot notation.

```
GET /works?select=id,title,primary_location.source.display_name
```

---

## 10. Works Response Schema

### Each Work Object

#### Identifiers

| Field | Type | Example |
|---|---|---|
| `id` | string | `"https://openalex.org/W2741809807"` |
| `doi` | string | `"https://doi.org/10.7717/peerj.4375"` |
| `ids.openalex` | string | Same as `id` |
| `ids.doi` | string | Normalized DOI URI |
| `ids.mag` | string | Microsoft Academic Graph ID |
| `ids.pmid` | string | PubMed ID URI |
| `ids.pmcid` | string | PubMed Central ID URI |

#### Basic Metadata

| Field | Type | Description |
|---|---|---|
| `title` | string | Full title |
| `display_name` | string | Display title (same as title) |
| `publication_year` | integer | e.g. `2016` |
| `publication_date` | string | ISO date, `"2016-06-01"` |
| `language` | string | ISO 639-1 code, `"en"` |
| `type` | string | `"article"`, `"book"`, `"book-chapter"`, `"dataset"`, `"thesis"`, etc. |
| `is_retracted` | boolean | |
| `is_paratext` | boolean | |
| `is_xpac` | boolean | |
| `relevance_score` | float | Only when search is used |
| `updated_date` | string | e.g. `"2026-05-21T06:26:12.895304"` |
| `created_date` | string | e.g. `"2025-10-10"` |

#### Abstract

| Field | Type | Description |
|---|---|---|
| `abstract_inverted_index` | object | Inverted index: word -> array of positions. `null` when absent. |

To reconstruct: place each word at its listed position(s). Tokens may be stemmed (lowercased, no punctuation). Example:

```json
{
  "Deeper": [0],
  "neural": [1],
  "networks": [2, 19, 57]
}
```

#### Authorships

Array of objects, each containing:

| Field | Type | Description |
|---|---|---|
| `author_position` | string | `"first"`, `"middle"`, `"last"` |
| `author.id` | string | OpenAlex author URI |
| `author.display_name` | string | Author name |
| `author.orcid` | string or null | ORCID URI |
| `institutions` | array | Institution objects |
| `countries` | array | ISO country codes, e.g. `["DE"]` |
| `is_corresponding` | boolean | |
| `raw_author_name` | string | Name as published |
| `raw_affiliation_strings` | array | Original affiliation text |
| `raw_orcid` | string or null | |
| `affiliations` | array | Structured affiliation objects |

Each institution in `institutions`:

| Field | Type |
|---|---|
| `id` | string (URI) |
| `display_name` | string |
| `ror` | string (ROR URI) |
| `country_code` | string |
| `type` | string (`"education"`, `"company"`, `"government"`, etc.) |
| `lineage` | array of strings |

Each entry in `affiliations`:

| Field | Type |
|---|---|
| `raw_affiliation_string` | string |
| `institution_ids` | array of strings |

#### Citation Metrics

| Field | Type | Description |
|---|---|---|
| `cited_by_count` | integer | Total citation count |
| `fwci` | float | Field-Weighted Citation Index |
| `citation_normalized_percentile.value` | float | |
| `citation_normalized_percentile.is_in_top_1_percent` | boolean | |
| `citation_normalized_percentile.is_in_top_10_percent` | boolean | |
| `cited_by_percentile_year.min` | integer | |
| `cited_by_percentile_year.max` | integer | |
| `counts_by_year` | array | `[{year: int, cited_by_count: int}]` |

#### Open Access

| Field | Type | Description |
|---|---|---|
| `open_access.is_oa` | boolean | |
| `open_access.oa_status` | string | `"gold"`, `"hybrid"`, `"green"`, `"bronze"`, `"closed"` |
| `open_access.oa_url` | string or null | |
| `open_access.any_repository_has_fulltext` | boolean | |

#### Primary Location & Locations

`primary_location` and `locations` (array) share the same structure:

| Field | Type |
|---|---|
| `id` | string |
| `is_oa` | boolean |
| `landing_page_url` | string |
| `pdf_url` | string or null |
| `license` | string or null |
| `license_id` | string or null |
| `version` | string |
| `is_accepted` | boolean |
| `is_published` | boolean |
| `raw_source_name` | string |
| `raw_type` | string |
| `source` | object |

`source` sub-object:

| Field | Type |
|---|---|
| `id` | string (URI) |
| `display_name` | string |
| `issn_l` | string |
| `issn` | array of strings |
| `is_oa` | boolean |
| `is_in_doaj` | boolean |
| `is_core` | boolean |
| `host_organization` | string (URI) |
| `host_organization_name` | string |
| `host_organization_lineage` | array of strings |
| `host_organization_lineage_names` | array of strings |
| `type` | string |

`best_oa_location`: same structure as `primary_location` or `null`.

#### Topics

`topics` array and `primary_topic`:

| Field | Type |
|---|---|
| `id` | string (URI) |
| `display_name` | string |
| `score` | float |
| `subfield.id` | string |
| `subfield.display_name` | string |
| `field.id` | string |
| `field.display_name` | string |
| `domain.id` | string |
| `domain.display_name` | string |

#### Keywords

Array:

| Field | Type |
|---|---|
| `id` | string (URI) |
| `display_name` | string |
| `score` | float |

#### Concepts (Legacy, Deprecated)

Array:

| Field | Type |
|---|---|
| `id` | string (URI) |
| `wikidata` | string (URI) |
| `display_name` | string |
| `level` | integer (0-3) |
| `score` | float |

#### MeSH Terms (PubMed articles)

Array:

| Field | Type |
|---|---|
| `descriptor_ui` | string |
| `descriptor_name` | string |
| `qualifier_ui` | string or null |
| `qualifier_name` | string or null |
| `is_major_topic` | boolean |

#### SDGs

`sustainable_development_goals` array:

| Field | Type |
|---|---|
| `display_name` | string |
| `id` | string (URI) |
| `score` | float |

#### Bibliographic Info

| Field | Type |
|---|---|
| `biblio.volume` | string |
| `biblio.issue` | string |
| `biblio.first_page` | string |
| `biblio.last_page` | string |

#### Funders & Awards

| Field | Type |
|---|---|
| `funders` | array of `{id, display_name, ror}` |
| `awards` | array of award objects |

#### Content Access

| Field | Type |
|---|---|
| `has_fulltext` | boolean |
| `has_content.grobid_xml` | boolean |
| `has_content.pdf` | boolean |
| `content_urls.pdf` | string or null |
| `content_urls.grobid_xml` | string or null |

#### APC

| Field | Type |
|---|---|
| `apc_list.value` | integer or null |
| `apc_list.currency` | string or null |
| `apc_list.value_usd` | integer or null |
| `apc_paid.value` | integer or null |
| `apc_paid.currency` | string or null |
| `apc_paid.value_usd` | integer or null |

#### Indexing

| Field | Type |
|---|---|
| `indexed_in` | array of strings: `"crossref"`, `"pubmed"`, `"doaj"` |

#### Relationships

| Field | Type |
|---|---|
| `referenced_works` | array of strings (OpenAlex URIs) |
| `referenced_works_count` | integer |
| `related_works` | array of strings (OpenAlex URIs) |

---

## 11. Works Filter Fields Reference

Quick reference of commonly used filter fields on `/works`:

### Core Filters

| Filter | Values / Notes |
|---|---|
| `publication_year` | e.g. `2023` |
| `publication_date` | `YYYY-MM-DD` |
| `from_publication_date` | Lower bound date |
| `to_publication_date` | Upper bound date |
| `type` | `article`, `book`, `book-chapter`, `dataset`, `thesis`, `paratext`, `other`, etc. |
| `language` | ISO 639-1 code |
| `doi` | DOI URL or prefix |
| `doi_starts_with` | DOI prefix matching |
| `has_doi` | boolean |
| `is_retracted` | boolean |
| `is_paratext` | boolean (deprecated, use `type:paratext`) |
| `cited_by_count` | integer, supports `>`, `<` |
| `fwci` | float, Field-Weighted Citation Impact |
| `referenced_works_count` | integer |
| `has_abstract` | boolean |
| `has_fulltext` | boolean |
| `has_references` | boolean |
| `has_pmid` | boolean |
| `has_pmcid` | boolean |
| `has_pdf_url` | boolean |
| `has_orcid` | boolean |
| `has_embeddings` | boolean |
| `indexed_in` | `crossref`, `pubmed`, `doaj` |
| `created_date` | `YYYY-MM-DD` |
| `updated_date` | `YYYY-MM-DD` |
| `from_created_date` | Lower bound |
| `to_created_date` | Upper bound |
| `to_updated_date` | Upper bound |
| `version` | Version string |

### Open Access Filters

| Filter | Values |
|---|---|
| `open_access.is_oa` | boolean |
| `open_access.oa_status` | `gold`, `hybrid`, `green`, `bronze`, `closed` |
| `open_access.any_repository_has_fulltext` | boolean |
| `is_oa` | boolean (shorthand) |
| `oa_status` | same as `open_access.oa_status` |
| `has_oa_accepted_or_published_version` | boolean |
| `has_oa_submitted_version` | boolean |

### Author & Institution Filters

| Filter | Description |
|---|---|
| `author.id` | Author OpenAlex ID |
| `author.orcid` | Author ORCID |
| `authorships.author.id` | Author ID within authorships |
| `authorships.author.orcid` | Author ORCID within authorships |
| `authorships.institutions.id` | Institution OpenAlex ID |
| `authorships.institutions.ror` | Institution ROR ID |
| `authorships.institutions.country_code` | Country code |
| `authorships.institutions.type` | Institution type |
| `authorships.institutions.continent` | Continent |
| `authorships.institutions.is_global_south` | boolean |
| `authorships.countries` | Country codes |
| `authorships.is_corresponding` | boolean |
| `authorships.affiliations.institution_ids` | Institution IDs from affiliations |
| `corresponding_author_ids` | Corresponding author IDs |
| `corresponding_institution_ids` | Corresponding institution IDs |
| `institutions.id` | Institution ID (legacy) |
| `institutions.country_code` | Country code (legacy) |
| `institutions.continent` | Continent (legacy) |
| `institutions.type` | Institution type (legacy) |
| `institutions.ror` | ROR ID (legacy) |
| `institutions.is_global_south` | boolean (legacy) |
| `institution.id` | Institution ID (alternate) |
| `countries_distinct_count` | Number of distinct countries |
| `institutions_distinct_count` | Number of distinct institutions |
| `has_raw_affiliation_strings` | boolean |

### Source / Location Filters

| Filter | Prefix: `primary_location.` or `locations.` or `best_oa_location.` |
|---|---|
| `*.is_oa` | boolean |
| `*.is_accepted` | boolean |
| `*.is_published` | boolean |
| `*.license` | License string |
| `*.license_id` | License ID |
| `*.version` | Version |
| `*.landing_page_url` | URL |
| `*.raw_type` | Raw type string |
| `*.source.id` | Source OpenAlex ID |
| `*.source.type` | Source type |
| `*.source.is_oa` | boolean |
| `*.source.is_in_doaj` | boolean |
| `*.source.is_core` | boolean |
| `*.source.issn` | ISSN |
| `*.source.has_issn` | boolean |
| `*.source.host_organization` | Host org ID |
| `*.source.host_organization_lineage` | Host org lineage |
| `*.source.host_institution_lineage` | Host institution lineage |
| `*.source.publisher_lineage` | Publisher lineage |
| `journal` | Journal/source name |

### Topic & Concept Filters

| Filter | Description |
|---|---|
| `primary_topic.id` | Topic ID |
| `primary_topic.domain.id` | Domain ID |
| `primary_topic.field.id` | Field ID |
| `primary_topic.subfield.id` | Subfield ID |
| `topics.id` | Topic ID (any) |
| `topics.domain.id` | Domain ID (any) |
| `topics.field.id` | Field ID (any) |
| `topics.subfield.id` | Subfield ID (any) |
| `concepts.id` | Concept ID (legacy) |
| `concepts.wikidata` | Wikidata ID (legacy) |

### Relationship Filters

| Filter | Description |
|---|---|
| `cited_by` | Works that cite the given work ID |
| `cites` | Works cited by the given work ID |
| `referenced_works` | Works referenced by the result work |
| `related_to` | Works related to given work ID |

### SDG, Funder, Award Filters

| Filter | Description |
|---|---|
| `sustainable_development_goals.id` | SDG ID |
| `sustainable_development_goals.score` | SDG relevance score |
| `funders.id` | Funder ID |
| `awards.id` | Award ID |
| `awards.funder_id` | Award funder ID |
| `awards.funder_display_name` | Funder name |
| `awards.funder_award_id` | Funder's award identifier |
| `awards.doi` | Award DOI |

### Content & APC Filters

| Filter | Description |
|---|---|
| `has_content.grobid_xml` | boolean |
| `has_content.pdf` | boolean |
| `apc_list.value` | APC list value |
| `apc_list.value_usd` | APC value in USD |
| `apc_list.currency` | APC currency |
| `apc_list.provenance` | APC data provenance |
| `apc_paid.value` | Paid APC value |
| `apc_paid.value_usd` | Paid APC value in USD |
| `apc_paid.currency` | Paid APC currency |
| `apc_paid.provenance` | Paid APC provenance |

### ID Filters

| Filter | Description |
|---|---|
| `ids.openalex` | OpenAlex ID |
| `ids.pmid` | PubMed ID |
| `ids.pmcid` | PubMed Central ID |
| `ids.mag` | Microsoft Academic Graph ID |
| `openalex` | OpenAlex ID (alternate) |
| `openalex_id` | OpenAlex ID (alternate) |
| `pmid` | PubMed ID (shorthand) |
| `pmcid` | PubMed Central ID (shorthand) |
| `mag` | Microsoft Academic Graph ID (shorthand) |

### Citation & Percentile Filters

| Filter | Description |
|---|---|
| `citation_normalized_percentile.value` | Percentile value |
| `citation_normalized_percentile.is_in_top_1_percent` | boolean |
| `citation_normalized_percentile.is_in_top_10_percent` | boolean |
| `cited_by_percentile_year.min` | Min percentile |
| `cited_by_percentile_year.max` | Max percentile |

### Other Filters

| Filter | Description |
|---|---|
| `authors_count` | Number of authors |
| `concepts_count` | Number of concepts |
| `topics_count` | Number of topics |
| `locations_count` | Number of locations |
| `best_open_version` | Best open access version type |
| `datasets` | Dataset works |
| `fulltext_origin` | Source of fulltext |
| `has_old_authors` | Legacy author data flag |
| `is_corresponding` | Corresponding author flag |
| `is_xpac` | XPAC status |
| `repository` | Repository name |
| `mag_only` | MAG-only works |
| `raw_affiliation_strings` | Raw affiliation text |

### Deprecated Filter-Based Search Fields

These should be replaced with the `search` parameter:

- `title.search` / `title.search.no_stem`
- `abstract.search` / `abstract.search.no_stem`
- `display_name.search` / `display_name.search.no_stem`
- `title_and_abstract.search` / `title_and_abstract.search.no_stem`
- `fulltext.search`
- `keyword.search`
- `default.search`
- `semantic.search`
- `raw_affiliation_strings.search`
- `raw_author_name.search` (still useful — see Search section)

---

## 12. Autocomplete Endpoint

```
GET https://api.openalex.org/autocomplete/{entity_type}?q=QUERY&api_key=KEY
```

### Path Parameters

| Parameter | Values |
|---|---|
| `entity_type` | `works`, `authors`, `sources`, `institutions`, `topics`, `keywords`, `publishers`, `funders` |

### Query Parameters

| Parameter | Required | Description |
|---|---|---|
| `q` | Yes | Search query string |
| `filter` | No | Standard `field:value` filter syntax |
| `api_key` | Yes | API key |

### Response Schema

```json
{
  "meta": {
    "count": 628080396,
    "db_response_time_ms": 40
  },
  "results": [
    {
      "id": "https://openalex.org/A5122736977",
      "short_id": "authors/A5122736977",
      "display_name": "Bjorn Lindahl",
      "hint": "Pacific Northwest National Laboratory, USA",
      "cited_by_count": 87,
      "works_count": 73728,
      "entity_type": "author",
      "external_id": "https://orcid.org/0000-0001-9529-6550",
      "filter_key": "authorships.author.id"
    }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `id` | string | OpenAlex entity URI |
| `display_name` | string | Entity name |
| `hint` | string or null | Contextual info (e.g., affiliation) |
| `cited_by_count` | integer | Citation count |
| `works_count` | integer | Number of works |
| `entity_type` | string | Entity type |
| `external_id` | string or null | External ID (ORCID, DOI, etc.) |
| `filter_key` | string | Filter parameter to use in list queries |

Returns up to 10 results. Designed for search-as-you-type UIs.

### Error Responses

| Status | Description |
|---|---|
| 400 | Bad request - invalid parameters |
| 429 | Rate limit exceeded |

---

## 13. OpenAPI Specification

Downloadable spec at:

```
https://developers.openalex.org/api-reference/openapi.json
```

OpenAPI 3.1 format. Usable for code generation and tooling integration.

---

## 14. Cost Optimization Best Practices

1. Set `per_page=100` to maximize results per call.
2. Batch ID lookups with OR syntax (up to 100 IDs per filter).
3. Use `select=` to return only needed fields.
4. Implement exponential backoff on 429 responses.
5. Use cursor paging for large result sets (>10,000).
6. Singleton lookups (by ID/DOI) are always free -- prefer them.
7. Check `X-RateLimit-Remaining` header before batch operations.
8. Prepaid balance activates automatically after daily budget exhausts.

---

## 15. Citation

Priem, J., Piwowar, H., & Orr, R. (2022). OpenAlex: A fully-open index of scholarly works, authors, venues, institutions, and concepts. arXiv:2205.01833.
