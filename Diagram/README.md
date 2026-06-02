# Angri450.Nong.Diagram

Scientific diagram generator. Flowcharts, network graphs, phylogenetic trees, and mechanism diagrams — all as publication-quality PNG or SVG.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Diagram)](https://www.nuget.org/packages/Angri450.Nong.Diagram)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Diagram
```

## Quick Start

### Flowchart

```csharp
using DiagramCore;
using DiagramCore.Models;

var graph = new Graph();
graph.AddNode("A", "Sample Collection");
graph.AddNode("B", "DNA Extraction");
graph.AddNode("C", "PCR Amplification");
graph.AddNode("D", "Sequencing");
graph.AddEdge("A", "B");
graph.AddEdge("B", "C");
graph.AddEdge("C", "D");
DiagramBuilder.Flowchart(graph, "flowchart.png", 800, 600);
```

### Phylogenetic Tree

```csharp
var newick = "((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);";
DiagramBuilder.PhylogeneticTree(newick, "tree.png", radial: false, 800, 600);
```

### Network Graph

```csharp
var net = new Graph();
net.AddNode("n1", "Server A");
net.AddNode("n2", "Server B");
net.AddNode("n3", "Database");
net.AddEdge("n1", "n2", weight: 0.8);
net.AddEdge("n2", "n3", weight: 0.5);
net.AddEdge("n1", "n3", weight: 0.3);
DiagramBuilder.NetworkGraph(net, "network.png", 800, 600);
```

## Diagram Types

| Type | Method | Use Case |
|------|--------|----------|
| Flowchart | `Flowchart()` | Process diagrams, pipelines |
| Network | `NetworkGraph()` | System architecture, topology |
| Phylogenetic Tree | `PhylogeneticTree()` | Newick format trees (rectangular/radial) |
| Mechanism | `MechanismDiagram()` | Scientific mechanism illustrations |
| Sankey | `SankeyDiagram()` | Flow and transfer diagrams |

## Node and Edge Customization

```csharp
graph.AddNode("X", "Label", shape: NodeShape.Diamond, color: "#FF6B6B");
graph.AddEdge("A", "B", weight: 0.7, label: "0.7", style: EdgeStyle.Dashed);
```

## Dependencies

- `Angri450.Nong.ThirdParty` — merged foundation (MSAGL + SkiaSharp + all transitive deps)
- `Angri450.Nong.Bioicons` — 40 scientific SVG icons for node decoration

## API Reference

| Class | Description |
|-------|-------------|
| `DiagramBuilder` | Entry point for all diagram types, output to file or stream |
| `Graph` / `Node` / `Edge` | Graph model classes |
| `PhylogeneticTreeParser` | Newick format parser |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
