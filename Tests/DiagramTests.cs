using DiagramCore;
using DiagramCore.Layout;
using DiagramCore.Models;
using Xunit;

namespace Tests;

public class DiagramTests : IDisposable
{
    private readonly string _tempDir;

    public DiagramTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "diagram-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ---- Graph ----

    [Fact]
    public void Graph_AddNode_AddsNode()
    {
        var graph = new Graph();
        graph.AddNode("A", "Node A");
        graph.AddNode("B", "Node B");

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal("Node A", graph.GetNode("A")!.Label);
    }

    [Fact]
    public void Graph_AddEdge_CreatesNodesIfNeeded()
    {
        var graph = new Graph();
        graph.AddEdge("X", "Y", "link");

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
        Assert.Equal("link", graph.Edges[0].Label);
    }

    [Fact]
    public void Graph_AddNode_DuplicateIgnored()
    {
        var graph = new Graph();
        graph.AddNode("A");
        graph.AddNode("A");

        Assert.Single(graph.Nodes);
    }

    [Fact]
    public void Graph_GetNode_ReturnsNull_WhenNotFound()
    {
        var graph = new Graph();
        Assert.Null(graph.GetNode("nonexistent"));
    }

    // ---- NewickTree ----

    [Fact]
    public void NewickTree_Parse_SimpleTree()
    {
        var tree = NewickTree.Parse("(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);");

        Assert.NotNull(tree);
        Assert.Equal(3, tree.Children.Count);
        Assert.Equal("A", tree.Children[0].Name);
        Assert.Equal(0.1, tree.Children[0].BranchLength, 4);
        Assert.Equal("B", tree.Children[1].Name);
        Assert.Equal(0.2, tree.Children[1].BranchLength, 4);

        var inner = tree.Children[2];
        Assert.Equal(2, inner.Children.Count);
        Assert.Equal("C", inner.Children[0].Name);
        Assert.Equal("D", inner.Children[1].Name);
    }

    [Fact]
    public void NewickTree_Parse_ComplexTree()
    {
        var tree = NewickTree.Parse("((A:0.1,B:0.2):0.3,(C:0.4,(D:0.5,E:0.6):0.7):0.8);");

        Assert.NotNull(tree);
        Assert.Equal(2, tree.Children.Count);
        Assert.Equal(5, tree.LeafCount());
    }

    [Fact]
    public void NewickTree_LeafCount_CorrectCount()
    {
        var tree = NewickTree.Parse("(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);");
        Assert.Equal(4, tree.LeafCount());
    }

    [Fact]
    public void NewickTree_Depth_CorrectDepth()
    {
        var tree = NewickTree.Parse("(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);");
        // Root -> inner node -> C/D  = depth 2
        Assert.Equal(2, tree.Depth());
    }

    [Fact]
    public void NewickTree_GetLeaves_ReturnsAllLeaves()
    {
        var tree = NewickTree.Parse("(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);");
        var leaves = tree.GetLeaves();

        Assert.Equal(4, leaves.Count);
        var names = leaves.Select(l => l.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "A", "B", "C", "D" }, names);
    }

    [Fact]
    public void NewickTree_SingleLeaf()
    {
        var tree = NewickTree.Parse("A:0.5;");
        Assert.True(tree.IsLeaf);
        Assert.Equal("A", tree.Name);
        Assert.Equal(1, tree.LeafCount());
        Assert.Equal(0, tree.Depth());
    }

    // ---- SugiyamaLayout ----

    [Fact]
    public void SugiyamaLayout_Layout_ProducesValidCoordinates()
    {
        var graph = new Graph();
        graph.AddNode("Start", "Start");
        graph.AddNode("Process", "Process");
        graph.AddNode("End", "End");
        graph.AddEdge("Start", "Process");
        graph.AddEdge("Process", "End");

        var layout = new SugiyamaLayout();
        layout.Layout(graph, 800);

        foreach (var node in graph.Nodes)
        {
            Assert.True(node.X >= 0, $"Node {node.Id} X should be >= 0");
            Assert.True(node.Y >= 0, $"Node {node.Id} Y should be >= 0");
        }

        // Start should be above End (lower Y value)
        var start = graph.GetNode("Start")!;
        var end = graph.GetNode("End")!;
        Assert.True(start.Y < end.Y, "Start should be above End in hierarchy");
    }

    [Fact]
    public void SugiyamaLayout_EmptyGraph_DoesNotThrow()
    {
        var graph = new Graph();
        var layout = new SugiyamaLayout();
        layout.Layout(graph, 800);
        // Should not throw
    }

    // ---- ForceDirectedLayout ----

    [Fact]
    public void ForceDirectedLayout_Layout_ProducesValidCoordinates()
    {
        var graph = new Graph();
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");
        graph.AddEdge("A", "B");
        graph.AddEdge("B", "C");

        var layout = new ForceDirectedLayout();
        layout.Layout(graph, 400, 300);

        foreach (var node in graph.Nodes)
        {
            Assert.True(node.X >= 0 && node.X <= 400, $"Node {node.Id} X out of bounds");
            Assert.True(node.Y >= 0 && node.Y <= 300, $"Node {node.Id} Y out of bounds");
        }
    }

    // ---- FlowchartRenderer ----

    [Fact]
    public void FlowchartRenderer_Render_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var graph = new Graph { Title = "Test Flow" };
        graph.AddNode("S", "Start");
        graph.AddNode("P", "Process");
        graph.AddNode("E", "End");
        graph.AddEdge("S", "P");
        graph.AddEdge("P", "E");

        var outPath = Path.Combine(_tempDir, "flow.png");
        DiagramBuilder.Flowchart(graph, outPath);

        Assert.True(File.Exists(outPath), "PNG file should exist");
        Assert.True(new FileInfo(outPath).Length > 0, "PNG file should be non-empty");
    }

    // ---- NetworkGraphRenderer ----

    [Fact]
    public void NetworkGraphRenderer_Render_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var graph = new Graph { Title = "Net" };
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");
        graph.AddEdge("A", "B");
        graph.AddEdge("B", "C");
        graph.AddEdge("C", "A");

        var outPath = Path.Combine(_tempDir, "network.png");
        DiagramBuilder.NetworkGraph(graph, outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    // ---- TreeRenderer ----

    [Fact]
    public void TreeRenderer_Render_Rectangular_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var newick = "(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);";

        var outPath = Path.Combine(_tempDir, "tree-rect.png");
        DiagramBuilder.PhylogeneticTree(newick, outPath, radial: false);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void TreeRenderer_Render_Radial_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var newick = "(A:0.1,B:0.2,(C:0.3,D:0.4):0.5);";

        var outPath = Path.Combine(_tempDir, "tree-radial.png");
        DiagramBuilder.PhylogeneticTree(newick, outPath, radial: true);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void TreeRenderer_Render_WithTreeObject_Works()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var tree = NewickTree.Parse("(X:0.1,Y:0.2,Z:0.3);");

        var outPath = Path.Combine(_tempDir, "tree-obj.png");
        DiagramBuilder.PhylogeneticTree(tree, outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    // ---- DiagramBuilder.FromDsl ----

    [Fact]
    public void DiagramBuilder_FromDsl_Flowchart_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var json = """
        {
            "type": "flowchart",
            "title": "DSL Test",
            "nodes": [
                {"id": "a", "label": "Step A"},
                {"id": "b", "label": "Step B"},
                {"id": "c", "label": "Step C"}
            ],
            "edges": [
                {"from": "a", "to": "b"},
                {"from": "b", "to": "c"}
            ]
        }
        """;

        var outPath = Path.Combine(_tempDir, "dsl-flow.png");
        DiagramBuilder.FromDsl(json, outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void DiagramBuilder_FromDsl_Network_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var json = """
        {
            "type": "network",
            "title": "Net DSL",
            "nodes": [
                {"id": "x", "label": "X"},
                {"id": "y", "label": "Y"}
            ],
            "edges": [
                {"from": "x", "to": "y"}
            ]
        }
        """;

        var outPath = Path.Combine(_tempDir, "dsl-net.png");
        DiagramBuilder.FromDsl(json, outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void DiagramBuilder_FromDsl_Tree_ProducesPng()
    {
        if (!SkiaCheck.IsAvailable()) return;
        var json = """
        {
            "type": "tree",
            "newick": "(A:0.1,B:0.2,C:0.3);",
            "radial": false
        }
        """;

        var outPath = Path.Combine(_tempDir, "dsl-tree.png");
        DiagramBuilder.FromDsl(json, outPath);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void DiagramBuilder_FromDsl_InvalidType_Throws()
    {
        var json = """{"type":"unknown"}""";
        Assert.Throws<ArgumentException>(() =>
            DiagramBuilder.FromDsl(json, Path.Combine(_tempDir, "bad.png")));
    }
}
