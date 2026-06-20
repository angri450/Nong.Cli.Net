# Metaso API Contract

## Credentials

`nong metaso` reads the API key from either:

- `NONG_LIT_METASO_KEY`
- `METASO_API_KEY`

Do not print real key values. Report only whether the needed variable is present.

## Command Surface

- `metaso search`: multi-scope search API. Current scopes are `webpage`, `document`, `scholar`, `image`, `video`, and `podcast`.
- `metaso reader`: fetch one URL and emit either structured JSON or Markdown.
- `metaso chat`: citation-backed RAG answer generation with `fast`, `fast_thinking`, or `ds-r1` models, plus scholar/webpage scope routing.

## Workflow

Typical search and read path:

```powershell
nong metaso search --query "rare earth fertilizer field trial" --scope scholar --size 10 --json
nong metaso reader --url "https://example.com/paper" --format markdown --json
```

Typical chat path:

```powershell
nong metaso chat --query "What are the recent field-trial findings on humic acid fertilizers?" --scope scholar --model fast_thinking --json
```

## Boundaries

Do not claim that Metaso is the same as unrestricted web browsing. Do not fabricate citations or quoted source text that the API did not return. Use `lit` for OpenAlex/Crossref/Unpaywall workflows and `aminer` for AMiner-specific scholar/paper/patent surfaces.
