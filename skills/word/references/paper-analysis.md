# Paper Analysis from Word Sources

Word commands handle DOCX reading, slicing, validation, and editing. Paper-level diagnosis belongs to `nong inspect`.

## DOCX to Inspect

For a complex paper document:

```powershell
nong word dissect paper.docx --output paper.slice --json
nong inspect diagnose paper.slice/preview/content.txt --json
```

For quick text-only analysis:

```powershell
nong word read paper.docx --json
```

Save the extracted text to a `.txt` or use `paper.slice/preview/content.txt`, then run inspect commands. Keep `paper.slice/content.nongmark` as the structured NongMark evidence stream when sectioning, tables, references, or figure placeholders matter.

## Inspect Commands

```powershell
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
nong inspect write-paper paper-spec.json -o paper.docx --json
nong inspect classify paper.txt --json
nong inspect structure paper.txt --json
nong inspect varplan paper.txt --json
nong inspect evidence paper.txt --json
nong inspect data-req paper.txt --json
nong inspect gap paper.txt --json
nong inspect semantics paper.txt --json
```

Command roles:

| Command | Role |
|---------|------|
| `diagnose` | Full paper quality diagnosis. |
| `refs` | Reference and citation risk checks. |
| `write-paper` | Generate a Word paper draft from a JSON spec. |
| `classify` | Classify paper type across the supported paper categories. |
| `structure` | Extract title, abstract, keywords, sections, tables, and references. |
| `varplan` | Generate a variable operationalization plan. |
| `evidence` | Diagnose research question, variables, methods, conclusions, and contribution claims. |
| `data-req` | Diagnose data source, sample, measurement, method-data fit, and robustness requirements. |
| `gap` | Grade the distance between the current paper and a defensible empirical paper. |
| `semantics` | Diagnose research design semantics such as causal wording, mechanism claims, and method alignment. |

## Boundaries

Inspect is rule-based paper analysis. It does not verify databases, fabricate citation checks, or replace advisor or peer review. Reference outputs that require external lookup should remain marked for human verification.

Use Word commands again after paper-level work when the result needs DOCX validation, slicing, merge, or append edits.
