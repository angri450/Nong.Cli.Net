---
name: inspect
description: Agricultural paper and official-document inspection/generation via nong. Trigger on paper diagnosis, 璁烘枃璇婃柇, paper type classification, structure extraction, reference check, variable plan, evidence chain, gap grade, data requirements, semantic diagnosis, paper drafting from JSON spec, official-document drafting from JSON spec, or inspect diagnose --ingest.
---

# Inspect

Use `nong inspect` for paper-level semantic, structural, evidence, reference, and writing workflows. Inspect consumes text or paper specs; for `.docx` sources, use `word dissect` or `word read` first. For external literature metadata/OA retrieval, use the `literature` skill and `nong lit`; `inspect refs` is an internal reference-list and citation-risk check, not a database search.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the needed command group.
## Implemented Commands

Current `nong commands --json` exposes these 11 implemented Inspect commands:

```powershell
nong inspect diagnose <paper.txt> [--ingest] [--json]
nong inspect refs <paper.txt> --json
nong inspect write-paper <spec.json> -o <out.docx> --json
nong inspect write-official <spec.json> -o <out.docx> --json
nong inspect classify <paper.txt> --json
nong inspect structure <paper.txt> --json
nong inspect varplan <paper.txt> --json
nong inspect evidence <paper.txt> --json
nong inspect data-req <paper.txt> --json
nong inspect gap <paper.txt> --json
nong inspect semantics <paper.txt> --json
```

## Dispatch

For input preparation, focused diagnostics, and writing handoff, read [references/paper-diagnostics.md](references/paper-diagnostics.md).

1. For `.docx` papers, run `nong word dissect <file.docx> --output <slice-dir> --json` and use `<slice-dir>/content.nongmark` as the inspect input. Use `nong word read <file.docx> --json` only for quick plain-text extraction.
2. For full paper quality diagnosis, run `nong inspect diagnose <paper.txt> [--ingest] [--json]`.
3. For references and citation risk, run `nong inspect refs <paper.txt> --json`.
4. For paper type routing, run `nong inspect classify <paper.txt> --json`.
5. For title, abstract, keywords, section, table, and reference extraction, run `nong inspect structure <paper.txt> --json`.
6. For variable operationalization and data collection planning, run `nong inspect varplan <paper.txt> --json`.
7. For evidence chain checks, run `nong inspect evidence <paper.txt> --json`.
8. For data source, sample, measurement, and method-data fit checks, run `nong inspect data-req <paper.txt> --json`.
9. For empirical paper gap grading, run `nong inspect gap <paper.txt> --json`.
10. For research design semantics, causal wording, mechanism claims, and method alignment, run `nong inspect semantics <paper.txt> --json`.
11. For official-document gongwen format compliance audit, run `nong inspect official-check <file.docx> --json`. Checks red header, doc number, title, recipient, body, closing, signature, and date against gongwen formatting rules.
12. For a Word paper draft, prepare a paper spec JSON and run `nong inspect write-paper <spec.json> -o <out.docx> --json`; then use Word commands for DOCX validation and slicing.
13. For an official-document draft, prepare an official spec JSON and run `nong inspect write-official <spec.json> -o <out.docx> --json`; then validate/slice with the Word skill. For formatting an existing DOCX into gongwen style, use `nong word format-gongwen`.

Inspect is not a DOCX parser. Keep Word layout, assets, comments, revisions, validation, and append edits in the `word` skill.

## Paper Spec

Use this minimal shape for `write-paper`:

```json
{
  "title": "Title",
  "abstract": "Abstract text.",
  "keywords": "keyword1; keyword2",
  "sections": [
    {
      "heading": "1 Introduction",
      "level": 1,
      "body": ["Paragraph one.", "Paragraph two."]
    }
  ],
  "references": ["[1] Author. Title. Journal, 2026."]
}
```

`sections[].heading` is required. `level` must be 1-3. Treat `E006 validation_failed` as a spec problem.

## Official-Document Spec

Use this minimal shape for `write-official`:

```json
{
  "redHeader": "Demo Agency File",
  "docNumber": "Demo [2026] 1",
  "title": "Demo Notice",
  "recipient": "All units:",
  "body": ["Paragraph one.", "Paragraph two."],
  "closing": "This is the notice.",
  "signature": "Demo Agency",
  "date": "2026-06-10"
}
```

`title` is required. `body` may be a string or an array of strings. Treat `E006 validation_failed` as a spec problem.

## Contract

Always use `--json` when the result will feed another tool or model decision. Treat `status: "error"` as failed.

Reference and semantic outputs are rule-based writing and method diagnostics. Do not present them as external database verification, advisor review, or peer review. Use `nong lit` when the task requires OpenAlex/Crossref/Unpaywall metadata, DOI lookup, or reference export.

## Template Discovery (nong genre)

```powershell
nong genre list [--json]              # List available writing templates
nong genre show <name> [--json]       # Show template details
```

Templates include: life-sciences-contest, journal-paper, course-paper, degree-thesis. Use `nong inspect write-paper <spec.json> --template <name>` to draft from a template.
