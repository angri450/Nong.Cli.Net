---
name: diagram
description: Scientific diagram CLI via nong. Trigger on flowchart, network, tree diagrams (NOT statistical bar/pie/scatter charts — see chart skill). Flowchart,, workflow diagram, process diagram, network graph, relationship graph, mechanism network, phylogenetic tree, or Newick rendering.
---

# Diagram

Use `nong` for implemented scientific diagram rendering. Nong.Toolkit.Net routes to the CLI; do not recreate diagram rendering logic in scripts or temporary projects.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `diagram` command group.

**Modular:** `nong diagram` routes to the standalone `Angri450.Nong.Tool.Diagram` dotnet tool. First use auto-installs. Command surface unchanged.
## Implemented Commands

```powershell
nong diagram flowchart <spec.json> -o <out.png> [--json]
nong diagram network <spec.json> -o <out.png> [--json]
nong diagram tree <tree.nwk|tree.txt|spec.json> -o <out.png> [--json]
```

## Dispatch

1. For process, workflow, protocol, or pipeline diagrams, prepare a flowchart JSON spec and run `nong diagram flowchart`.
2. For relationship, mechanism, interaction, or network diagrams, prepare a network JSON spec and run `nong diagram network`.
3. For phylogenetic trees, use Newick text (`.nwk` or `.txt`) or a tree JSON spec and run `nong diagram tree`.
4. Read `artifacts.png` from JSON output for the generated image path.
5. For Bioicons listing/search, use the `icons` skill.

## Contracts

Flowchart JSON:

```json
{
  "type": "flowchart",
  "title": "Workflow",
  "nodes": [
    { "id": "sample", "label": "Sample" },
    { "id": "dna", "label": "DNA extraction" }
  ],
  "edges": [
    { "from": "sample", "to": "dna" }
  ]
}
```

Network JSON:

```json
{
  "nodes": [
    { "id": "A", "label": "Gene A" },
    { "id": "B", "label": "Gene B" }
  ],
  "edges": [
    { "from": "A", "to": "B", "label": "activates" }
  ],
  "title": "Interaction network"
}
```

Tree Newick:

```text
((A:0.1,B:0.2):0.3,C:0.4);
```

Tree JSON:

```json
{
  "type": "tree",
  "title": "Phylogeny",
  "newick": "((A:0.1,B:0.2):0.3,C:0.4);",
  "radial": false
}
```

Always use `--json` for generated diagrams. Treat `status: "error"` as failed and fix the spec before retrying.

## Optional Visual QA

After generating a PNG, you may suggest:

```powershell
nong ocr analyze-image tree.png -o tree.analysis --json
```

This is structural image QA for dimensions, blankness, whitespace, and content regions. It is not OCR, text recognition, or semantic understanding of the diagram.
