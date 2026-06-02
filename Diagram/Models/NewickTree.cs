using System;
using System.Collections.Generic;
using System.Text;

namespace DiagramCore.Models;

/// <summary>
/// Newick 格式系统发育树解析器
/// 格式: (A:0.1,B:0.2,(C:0.3,D:0.4):0.5);
/// </summary>
public class NewickTree
{
    public string Name { get; set; } = "";
    public double BranchLength { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public List<NewickTree> Children { get; set; } = new();
    public bool IsLeaf => Children.Count == 0;

    public static NewickTree Parse(string newick)
    {
        newick = newick.Trim();
        if (newick.EndsWith(";"))
            newick = newick[..^1];

        int pos = 0;
        var root = ParseNode(newick, ref pos);
        return root;
    }

    private static NewickTree ParseNode(string text, ref int pos)
    {
        var node = new NewickTree();

        if (pos < text.Length && text[pos] == '(')
        {
            pos++; // skip '('
            node.Children.Add(ParseNode(text, ref pos));

            while (pos < text.Length && text[pos] == ',')
            {
                pos++; // skip ','
                node.Children.Add(ParseNode(text, ref pos));
            }

            if (pos < text.Length && text[pos] == ')')
                pos++; // skip ')'
        }

        // 解析名称和分支长度
        var sb = new StringBuilder();
        while (pos < text.Length && text[pos] != ',' && text[pos] != ')' && text[pos] != ';')
        {
            if (text[pos] == ':')
            {
                node.Name = sb.ToString();
                sb.Clear();
                pos++;
            }
            else
            {
                sb.Append(text[pos]);
                pos++;
            }
        }

        if (sb.Length > 0)
        {
            if (double.TryParse(sb.ToString(), out var length))
                node.BranchLength = length;
            else if (string.IsNullOrEmpty(node.Name))
                node.Name = sb.ToString();
        }

        return node;
    }

    public int Depth()
    {
        if (IsLeaf) return 0;
        return 1 + Children.Max(c => c.Depth());
    }

    public int LeafCount()
    {
        if (IsLeaf) return 1;
        return Children.Sum(c => c.LeafCount());
    }

    public List<NewickTree> GetLeaves()
    {
        var leaves = new List<NewickTree>();
        CollectLeaves(this, leaves);
        return leaves;
    }

    private void CollectLeaves(NewickTree node, List<NewickTree> leaves)
    {
        if (node.IsLeaf)
            leaves.Add(node);
        else
            foreach (var child in node.Children)
                CollectLeaves(child, leaves);
    }
}
