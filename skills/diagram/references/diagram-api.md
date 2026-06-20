# Diagram CLI Reference

Nong.Toolkit.Net uses the implemented `nong diagram` CLI commands. Do not call DiagramCore directly for normal skill routing.

## Flowchart

```powershell
nong diagram flowchart flow.json -o flow.png --json
```

Spec:

```json
{
  "type": "flowchart",
  "title": "Workflow",
  "nodes": [
    { "id": "sample", "label": "Sample collection" },
    { "id": "dna", "label": "DNA extraction" },
    { "id": "pcr", "label": "PCR" }
  ],
  "edges": [
    { "from": "sample", "to": "dna" },
    { "from": "dna", "to": "pcr" }
  ]
}
```

Node fields include `id`, `label`, optional `shape`, and optional `color`. Edge fields include `from`, `to`, and optional `label`.

## Network

```powershell
nong diagram network network.json -o network.png --json
```

Spec:

```json
{
  "title": "Interaction network",
  "nodes": [
    { "id": "A", "label": "Gene A" },
    { "id": "B", "label": "Gene B" },
    { "id": "C", "label": "Gene C" }
  ],
  "edges": [
    { "from": "A", "to": "B", "label": "activates" },
    { "from": "B", "to": "C", "label": "binds" }
  ]
}
```

`network` accepts the graph model directly. Keep IDs stable and unique.

## Tree

Tree from Newick text:

```powershell
nong diagram tree tree.nwk -o tree.png --json
```

`tree.nwk`:

```text
((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);
```

Tree from JSON:

```powershell
nong diagram tree tree.json -o tree.png --json
```

`tree.json`:

```json
{
  "type": "tree",
  "title": "Primate tree",
  "newick": "((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);",
  "radial": false
}
```

Newick input must be valid Newick text and should end with `;`.

## Output and QA

PNG-generating commands return the generated path in `artifacts.png` when `--json` is used. Optional structural QA:

```powershell
nong ocr analyze-image flow.png -o flow.analysis --json
```

This checks dimensions, whitespace, blankness, and content regions. It is not OCR, text recognition, or semantic diagram interpretation.

## Boundaries

The current implemented diagram surface is `flowchart`, `network`, and `tree`. Bioicons listing/search belongs to the `icons` skill. Do not promise additional diagram types or arbitrary diagram editing through this skill.
