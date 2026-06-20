---
name: literature
description: Literature retrieval and local literature cache workflows through nong lit. Trigger on local literature retrieval, CNKI-like search, DOI lookup (NOT AMiner scholar profiles or Metaso citation-backed search) expressions, literature search planning, DOI lookup, OpenAlex/Crossref/Unpaywall metadata or OA lookup, cache import/query/export, Word template fill from cached papers, or bibliography preparation for agricultural papers.
---

# Literature

Use `nong lit` as the deterministic literature retrieval entrypoint. Nong.Toolkit.Net prepares CNKI-like search expressions, checks provider plans, reads normalized JSON records, maintains the local literature cache, and exports references. It does not use Python literature packages, browser automation, scraping, or paywall bypass.

AMiner and Metaso are separate command groups and separate skills. Keep `nong lit` for OpenAlex/Crossref/Unpaywall and local cache workflows only.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the needed command group.

## Implemented Commands

Current `nong commands --json` exposes these 11 implemented `lit` commands:

```powershell
nong lit parse --query "<expr>" --json
nong lit validate --query "<expr>" --json
nong lit plan --query "<expr>" --sources openalex,crossref,unpaywall --json
nong lit search --query "<expr>" --sources openalex,crossref,unpaywall --limit 50 --profile balanced --ingest -o refs.json --json
nong lit export --input refs.json --format markdown -o refs.md --json
nong lit batch <dir> --sources openalex,crossref,unpaywall --limit 10 --profile balanced --json
nong lit cache-import --input refs.json --json
nong lit cache-query --keyword humic --min-year 2020 --limit 20 --json
nong lit cache-stats --json
nong lit cache-export --limit 20 --max-chars 8000 -o cache.md --json
nong lit word --template template.docx --limit 1 -o filled.docx --json
```

## DSL

The stable contract is the CNKI-like Nong DSL, not any provider's native query language.

Supported fields:

```text
SU TI KY AB FT AU FI F AF JN RF YE FU CLC SN CN IB CF DOI
```

Supported syntax:

```text
FIELD=...
'quoted phrase'
unquoted terms
+  *  -
AND OR NOT
()
YE BETWEEN ('2000','2013')
```

Operator mapping:

- `+` means OR.
- `*` means AND.
- `-` means NOT.
- `AND`, `OR`, and `NOT` keep their usual boolean meanings.

Useful examples:

```powershell
nong lit parse --query "SU=('鑵愭閰?+'鑵愭畺閰?)*('绋€鍦?+'寰偉')" --json
nong lit validate --query "AU=閽变紵闀?AND (AF=娓呭崕澶у OR AF=涓婃捣澶у)" --json
nong lit plan --query "DOI='10.1016/j.chemgeo.2007.05.018'" --sources openalex,crossref,unpaywall --json
```

Unsupported operators such as `%`, `/SEN`, `/NEAR`, `/PREV`, `/AFT`, `/PRG`, and `$N` must fail with `E006 validation_failed`. Do not silently reinterpret them.

## Default Workflow

1. Parse or validate the query before running provider work:

```powershell
nong lit validate --query "<expr>" --json
```

2. Inspect the provider plan:

```powershell
nong lit plan --query "<expr>" --sources openalex,crossref,unpaywall --json
```

Use the plan to explain field coverage, generated rough queries, provider limitations, and credential availability. Credential diagnostics expose environment variable names and booleans only; do not print real values.

3. Search with implemented providers:

```powershell
nong lit search --query "<expr>" --sources openalex,crossref,unpaywall --limit 50 --profile balanced -o refs.json --json
```

Ranking profiles are `balanced`, `classic`, and `recent`.

4. If the results need to live in the local literature cache:

- Preferred direct path: add `--cache` to `nong lit search` so the search result is stored immediately in the unified `nong.db` as a literature-list object with paper relationships.
- Import an existing search result JSON with:

```powershell
nong lit cache-import --input refs.json --json
```

5. Inspect and reuse the local cache:

```powershell
nong lit cache-query --keyword humic --min-year 2020 --limit 20 --json
nong lit cache-stats --json
nong lit cache-export --limit 20 --max-chars 8000 -o cache.md --json
```

6. Export references or fill a Word template from cached papers:

```powershell
nong lit export --input refs.json --format markdown -o refs.md --json
nong lit export --input refs.json --format bibtex -o refs.bib --json
nong lit word --template template.docx --limit 1 -o filled.docx --json
```

Do not treat export success as valid unless the output artifact exists and is non-empty.

## Providers

For provider credentials, query planning, cache workflow, and boundaries, read [references/provider-contract.md](references/provider-contract.md).

Implemented providers:

- OpenAlex: metadata search and DOI lookup; optional `NONG_LIT_OPENALEX_API_KEY` or `NONG_LIT_OPENALEX_KEY`.
- Crossref: metadata search and DOI enrichment; optional `NONG_LIT_MAILTO`.
- Unpaywall: legal open-access lookup by DOI; requires `NONG_LIT_UNPAYWALL_EMAIL` or fallback `NONG_LIT_MAILTO`.

Unpaywall is DOI-only. If selected without a DOI or without the required email variable, report the machine-readable issue instead of pretending the provider searched broadly.

## Boundaries

`nong lit` does not implement Semantic Scholar, PubMed, PMC, arXiv, Wanfang, Sciverse, Tavily, iFlow, Lewen, DBLP, Qinyan, local PDF literature providers, scraping, browser automation, CAPTCHA bypass, institutional login automation, commercial database scraping, paywall bypass, full-text search, or automatic Chinese-English synonym translation.

AMiner and Metaso are separate skills. If the task needs AMiner scholar/paper/patent/org endpoints, route to the `aminer` skill. If the task needs multi-scope web search, reader, or citation-backed RAG answers, route to the `metaso` skill.

If users need English synonyms, include them explicitly in the query. If users need full-text evidence from PDFs, use the `pdf` skill to slice local PDFs first, then reason over `content.nongmark` and cite that local evidence separately from `lit` metadata.

## Error Contract

Always pass `--json` when output feeds another tool or model decision. Treat `status: "error"` as failed.

Common codes:

- `E001 file_not_found`: fix the input or export path.
- `E005 dependency_missing`: provider credential or runtime prerequisite is missing.
- `E006 validation_failed`: query syntax, unsupported field/operator, provider name, rank profile, source selection, or template request is invalid.
- `E007 read_failed`: input literature JSON cannot be parsed.
- `E008 write_failed`: output artifact was not created or is empty.
- `E004 internal_error`: unexpected bug; keep command JSON and stderr/stdout for diagnosis.
