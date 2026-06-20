# Literature Provider Contract

## Providers

Nong.Cli.Net 4.3.0 implements:

- OpenAlex: metadata search and DOI lookup.
- Crossref: metadata search and DOI enrichment.
- Unpaywall: legal open-access lookup by DOI.

Unpaywall requires `NONG_LIT_UNPAYWALL_EMAIL` or `NONG_LIT_MAILTO`. Crossref may use `NONG_LIT_MAILTO`. OpenAlex may use `NONG_LIT_OPENALEX_API_KEY` or `NONG_LIT_OPENALEX_KEY`.

## Workflow

Validate first:

```powershell
nong lit validate --query "<expr>" --json
```

Inspect provider translation before searching:

```powershell
nong lit plan --query "<expr>" --sources openalex,crossref,unpaywall --json
```

Search, cache, and export:

```powershell
nong lit search --query "<expr>" --sources openalex,crossref,unpaywall --limit 50 --profile balanced --cache -o refs.json --json
nong lit cache-import --input refs.json --json
nong lit cache-query --keyword humic --min-year 2020 --limit 20 --json
nong lit cache-export --limit 20 --max-chars 8000 -o cache.md --json
nong lit export --input refs.json --format markdown -o refs.md --json
nong lit export --input refs.json --format bibtex -o refs.bib --json
```

If the search is already going through `nong lit search`, prefer `--cache` for direct storage into the unified `nong.db`. Use `cache-import` for existing JSON result files that were produced without `--cache`.

## Boundaries

Do not claim commercial database scraping, full-text retrieval, paywall bypass, browser automation, CAPTCHA bypass, Wanfang, PubMed, Semantic Scholar, PMC, arXiv, or automatic Chinese-English synonym expansion. If synonyms matter, put them explicitly into the query.

AMiner and Metaso are separate command groups. Use `nong aminer ...` or `nong metaso ...` when the task leaves the OpenAlex/Crossref/Unpaywall lane.
