---
name: aminer
description: AMiner research API workflows through nong aminer. Trigger on AMiner scholar profiling, paper recommendation, patent lookup (NOT general literature search or Metaso web search), org, or venue lookup, paper recommendation, paid AMiner detail commands, scholar profile exploration, or AMiner-specific research questions.
---

# AMiner

Use `nong aminer` for deterministic AMiner API access. Keep `nong lit` for OpenAlex/Crossref/Unpaywall metadata and OA lookup; use `nong aminer` when the task explicitly needs AMiner's scholar, paper, patent, organization, venue, or paid detail surfaces.

Do not scrape the AMiner website, hide paid API costs, or pretend the free endpoints provide the same depth as the paid ones.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and that the `aminer` group is available.

## Implemented Commands

Current `nong commands --json` exposes these 22 implemented AMiner commands:

```text
aminer scholar
aminer paper
aminer rec
aminer patent
aminer org
aminer venue
aminer paper-info
aminer patent-info
aminer paper-pro
aminer paper-detail
aminer paper-qa
aminer paper-citations
aminer deep-research
aminer scholar-detail
aminer scholar-figure
aminer scholar-stat
aminer scholar-papers
aminer scholar-patents
aminer scholar-projects
aminer org-detail
aminer org-patents
aminer patent-detail
```

## Dispatch

For credentials, paid/free boundaries, and command-family routing, read [references/api-contract.md](references/api-contract.md).

1. For free scholar lookup, start with `nong aminer scholar --name <name> --ingest --json`.
2. For free paper title lookup, run `nong aminer paper --title <title> --ingest --json`.
3. For scholar/topic recommendation, run `nong aminer rec --author <name> --topics <topic1> <topic2> --json`.
4. For patent, organization, or venue discovery, use `nong aminer patent`, `nong aminer org`, or `nong aminer venue`.
5. For batch paper lookup by IDs, use `nong aminer paper-info --ids <id1> <id2> --json`.
6. For patent detail by ID, use `nong aminer patent-info <id> --json` (free) or `nong aminer patent-detail <id> --json` (paid).
7. For paid paper exploration, route by intent:
   - `nong aminer paper-pro` for multi-field paper search
   - `nong aminer paper-detail` for full paper details by ID
   - `nong aminer paper-qa` for semantic question-style paper search
   - `nong aminer paper-citations` for paper citation graph
   - `nong aminer deep-research` for long-form streaming answer generation
8. For scholar profiles, route by depth:
   - `nong aminer scholar-detail` for the full profile
   - `nong aminer scholar-figure` for research portrait
   - `nong aminer scholar-stat` for summary metrics
   - `nong aminer scholar-papers`, `nong aminer scholar-patents`, `nong aminer scholar-projects` for portfolios
9. For organization or patent deep dives, use `nong aminer org-detail`, `nong aminer org-patents`, or `nong aminer patent-detail`.
10. Run `nong aminer <command> --help` before paid commands if exact option names matter for the current task.

## Contract

Always pass `--json` when the output feeds another tool or model decision. Treat AMiner output as API search evidence, not as peer review, advisor review, or ground-truth validation.
