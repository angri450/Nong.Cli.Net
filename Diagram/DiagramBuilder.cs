using DiagramCore.Models;
using DiagramCore.Renderers;

namespace DiagramCore;

/// <summary>
/// 统一科学图表生成 API
/// </summary>
public static class DiagramBuilder
{
    /// <summary>
    /// 生成流程图
    /// </summary>
    public static void Flowchart(Graph graph, string outputPath, int width = 800, int height = 600)
    {
        var renderer = new FlowchartRenderer(graph);
        renderer.Render(outputPath, width, height);
    }

    /// <summary>
    /// 生成网络图（力导向布局）
    /// </summary>
    public static void NetworkGraph(Graph graph, string outputPath, int width = 800, int height = 600)
    {
        var renderer = new NetworkGraphRenderer(graph);
        renderer.Render(outputPath, width, height);
    }

    /// <summary>
    /// 生成系统发育树（Newick 格式）
    /// </summary>
    public static void PhylogeneticTree(string newick, string outputPath, bool radial = false, int width = 800, int height = 600)
    {
        var tree = NewickTree.Parse(newick);
        var renderer = new TreeRenderer(tree, radial);
        renderer.Render(outputPath, width, height);
    }

    /// <summary>
    /// 生成系统发育树（已解析的树对象）
    /// </summary>
    public static void PhylogeneticTree(NewickTree tree, string outputPath, bool radial = false, int width = 800, int height = 600)
    {
        var renderer = new TreeRenderer(tree, radial);
        renderer.Render(outputPath, width, height);
    }

    /// <summary>
    /// 从 DSL JSON 生成图表
    /// </summary>
    public static void FromDsl(string json, string outputPath, int width = 800, int height = 600)
    {
        var dsl = System.Text.Json.JsonSerializer.Deserialize<DiagramDsl>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dsl == null)
            throw new ArgumentException("Invalid DSL JSON");

        switch (dsl.Type?.ToLower())
        {
            case "flowchart":
                var flowGraph = BuildGraphFromDsl(dsl);
                Flowchart(flowGraph, outputPath, width, height);
                break;

            case "network":
                var netGraph = BuildGraphFromDsl(dsl);
                NetworkGraph(netGraph, outputPath, width, height);
                break;

            case "tree":
                if (string.IsNullOrEmpty(dsl.Newick))
                    throw new ArgumentException("Tree type requires 'newick' field");
                PhylogeneticTree(dsl.Newick, outputPath, dsl.Radial, width, height);
                break;

            default:
                throw new ArgumentException($"Unknown diagram type: {dsl.Type}");
        }
    }

    /// <summary>
    /// 生成 Bioicons 图标总览表，按类别展示所有可用图标
    /// </summary>
    public static void BioIconSheet(string outputPath, int width = 1200, int height = 800)
    {
        BioIconRenderer.RenderIconSheet(outputPath, width, height);
    }

    private static Graph BuildGraphFromDsl(DiagramDsl dsl)
    {
        var graph = new Graph { Title = dsl.Title ?? "" };

        if (dsl.Nodes != null)
        {
            foreach (var node in dsl.Nodes)
            {
                graph.AddNode(node.Id, node.Label);
                var n = graph.GetNode(node.Id)!;
                if (!string.IsNullOrEmpty(node.Shape)) n.Shape = node.Shape;
                if (!string.IsNullOrEmpty(node.Color)) n.FillColor = node.Color;
            }
        }

        if (dsl.Edges != null)
        {
            foreach (var edge in dsl.Edges)
            {
                graph.AddEdge(edge.From, edge.To, edge.Label ?? "");
            }
        }

        return graph;
    }
}

public class DiagramDsl
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Newick { get; set; }
    public bool Radial { get; set; }
    public List<DslNode>? Nodes { get; set; }
    public List<DslEdge>? Edges { get; set; }
}

public class DslNode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Shape { get; set; }
    public string? Color { get; set; }
}

public class DslEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Label { get; set; }
}
