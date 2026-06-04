# Angri450.Nong.Diagram

科学图表渲染引擎。angri450 基于 MSAGL + SkiaSharp 构建了四种渲染管线 —— 流程图、网络图、系统发育树、机制图，输出为可发表的 PNG 或 SVG。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Diagram)](https://www.nuget.org/packages/Angri450.Nong.Diagram)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Diagram
```

## Quick Start

### 流程图

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

### 系统发育树

```csharp
var newick = "((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);";
DiagramBuilder.PhylogeneticTree(newick, "tree.png", radial: false, 800, 600);
```

### 网络图

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

angri450 实现的五种图表类型：

| Type | Method | Use Case |
|------|--------|----------|
| Flowchart | `Flowchart()` | 流程图、管线图 |
| Network | `NetworkGraph()` | 系统架构、拓扑图 |
| Phylogenetic Tree | `PhylogeneticTree()` | Newick 格式系统发育树（矩形/辐射） |
| Mechanism | `MechanismDiagram()` | 科学机制示意图 |
| Sankey | `SankeyDiagram()` | 桑基图、流向图 |

## Node and Edge Customization

```csharp
graph.AddNode("X", "Label", shape: NodeShape.Diamond, color: "#FF6B6B");
graph.AddEdge("A", "B", weight: 0.7, label: "0.7", style: EdgeStyle.Dashed);
```

## Dependencies

- `Angri450.Nong.ThirdParty` — 合并基础库（MSAGL + SkiaSharp + 全部传递依赖）
- `Angri450.Nong.Bioicons` — 40 个科学 SVG 图标用于节点装饰

## API Reference

| Class | Description |
|-------|-------------|
| `DiagramBuilder` | 所有图表类型入口，输出到文件或流 |
| `Graph` / `Node` / `Edge` | 图模型类 |
| `PhylogeneticTreeParser` | Newick 格式解析器 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0
