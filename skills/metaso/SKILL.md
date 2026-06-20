---
name: metaso
description: Metaso search workflows through nong metaso. Trigger on Metaso citation-backed web/scholar search, page reading (NOT AMiner profiling or local literature cache), page reader/fetch, or Metaso RAG chat requests where the user wants API-backed search rather than ad hoc browsing. Use --ingest to save results into nong.db for nong search.
---

# Metaso

Use `nong metaso` when the task needs Metaso's remote search API, page reader, or citation-backed chat workflow. Keep normal browsing and local file reasoning separate; `metaso` is specifically the deterministic API path.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and that the `metaso` group is available.

## Implemented Commands

Current `nong commands --json` exposes these 3 implemented Metaso commands:

```powershell
nong metaso search --query "<query>" --scope scholar --size 10 --ingest --json
nong metaso reader --url "https://example.com" --format markdown --ingest --json
nong metaso chat --query "<question>" --scope scholar --model fast_thinking --ingest --json
```

## Dispatch

For credentials, models, scopes, and boundaries, read [references/api-contract.md](references/api-contract.md).

1. For search across scholar/web/document/image/video/podcast scopes, run `nong metaso search`.
2. For fetching one URL as structured JSON or clean Markdown, run `nong metaso reader`.
3. For citation-backed research answers with Metaso's remote model path, run `nong metaso chat`.
4. Use `-o` when the answer or fetched content should be preserved as a reusable artifact.
5. Do not present Metaso output as local browsing or as a substitute for checking a specific PDF or DOCX file already on disk.

## Contract

Always pass `--json` when the output feeds another tool or model decision. Treat Metaso results as API search evidence with citations, not as a guarantee of completeness or correctness beyond the returned source set.
