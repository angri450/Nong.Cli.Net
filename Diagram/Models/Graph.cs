using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramCore.Models;

public class GraphNode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 120;
    public double Height { get; set; } = 40;
    public string Shape { get; set; } = "rectangle"; // rectangle, ellipse, diamond, circle, icon:category:name
    public string FillColor { get; set; } = "#E8F4F8";
    public string StrokeColor { get; set; } = "#2C5F7D";
    public string TextColor { get; set; } = "#1A1A1A";
    public string? IconCategory { get; set; }
    public string? IconName { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class GraphEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Label { get; set; } = "";
    public string Style { get; set; } = "solid"; // solid, dashed, dotted
    public string Color { get; set; } = "#666666";
    public bool HasArrow { get; set; } = true;
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class Graph
{
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphEdge> Edges { get; set; } = new();
    public string Title { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new();

    public GraphNode? GetNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    public void AddNode(string id, string label = "")
    {
        if (GetNode(id) == null)
        {
            Nodes.Add(new GraphNode { Id = id, Label = string.IsNullOrEmpty(label) ? id : label });
        }
    }

    public void AddEdge(string from, string to, string label = "")
    {
        AddNode(from);
        AddNode(to);
        Edges.Add(new GraphEdge { From = from, To = to, Label = label });
    }

    public double Width => Nodes.Count > 0 ? Nodes.Max(n => n.X + n.Width) : 0;
    public double Height => Nodes.Count > 0 ? Nodes.Max(n => n.Y + n.Height) : 0;
}
