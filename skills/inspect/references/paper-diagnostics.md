# Paper Diagnostics Reference

## Input Preparation

`nong inspect` consumes text or a JSON paper spec. For DOCX sources, use the `word` skill first:

```powershell
nong word dissect <paper.docx> --output <paper.slice> --json
```

Use `content.nongmark` or a plain text extraction as the inspect input depending on the task. Use the `slice` skill when block-level evidence matters.

## Diagnostic Commands

```powershell
nong inspect diagnose <paper.txt> --json
nong inspect classify <paper.txt> --json
nong inspect structure <paper.txt> --json
nong inspect refs <paper.txt> --json
nong inspect varplan <paper.txt> --json
nong inspect evidence <paper.txt> --json
nong inspect data-req <paper.txt> --json
nong inspect gap <paper.txt> --json
nong inspect semantics <paper.txt> --json
```

Use `diagnose` for the broad pass, then run focused commands when the user asks about a specific dimension.

## Writing

For paper drafting:

```powershell
nong inspect write-paper <spec.json> -o <out.docx> --json
```

For official-document drafting:

```powershell
nong inspect write-official <spec.json> -o <out.docx> --json
```

Use `title` plus optional `redHeader`, `docNumber`, `recipient`, `body`, `closing`, `signature`, and `date`. `body` may be a string or an array of strings.

After writing, validate with the `word` skill and inspect the DOCX or slice before claiming the deliverable is complete. If an existing DOCX needs gongwen formatting, use `nong word format-gongwen`.

## Boundaries

Inspect diagnostics are rule-based writing and method checks. They are not external database verification, advisor review, or peer review. Use the `literature` skill for OpenAlex/Crossref/Unpaywall metadata work.
