using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramCore.Layout;

/// <summary>
/// Sugiyama 层级布局算法（用于流程图）
/// 基于拓扑排序和层级分配
/// </summary>
public class SugiyamaLayout
{
    private const double LayerSpacing = 100;
    private const double NodeSpacing = 80;

    public void Layout(Models.Graph graph, double width = 800)
    {
        if (graph.Nodes.Count == 0) return;

        // 1. 拓扑排序，分配层级
        var levels = AssignLevels(graph);

        // 2. 每层内排序（减少交叉）
        foreach (var level in levels.Values)
        {
            level.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        // 3. 分配坐标
        double maxWidth = levels.Values.Max(l => l.Count);
        double startX = (width - maxWidth * NodeSpacing) / 2;

        foreach (var (level, nodes) in levels)
        {
            double y = 50 + level * LayerSpacing;
            double totalWidth = nodes.Count * NodeSpacing;
            double x = startX + (maxWidth * NodeSpacing - totalWidth) / 2;

            foreach (var node in nodes)
            {
                node.X = x + nodes.IndexOf(node) * NodeSpacing;
                node.Y = y;
            }
        }
    }

    private Dictionary<int, List<Models.GraphNode>> AssignLevels(Models.Graph graph)
    {
        var levels = new Dictionary<int, List<Models.GraphNode>>();
        var nodeLevels = new Dictionary<string, int>();
        var inDegree = new Dictionary<string, int>();

        // 计算入度
        foreach (var node in graph.Nodes)
        {
            inDegree[node.Id] = 0;
        }
        foreach (var edge in graph.Edges)
        {
            inDegree.TryGetValue(edge.To, out var deg);
            inDegree[edge.To] = deg + 1;
        }

        // BFS 拓扑排序
        var queue = new Queue<string>();
        foreach (var node in graph.Nodes.Where(n => inDegree[n.Id] == 0))
        {
            queue.Enqueue(node.Id);
            nodeLevels[node.Id] = 0;
        }

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var level = nodeLevels[id];

            if (!levels.ContainsKey(level))
                levels[level] = new List<Models.GraphNode>();
            levels[level].Add(graph.GetNode(id)!);

            foreach (var edge in graph.Edges.Where(e => e.From == id))
            {
                nodeLevels.TryGetValue(edge.To, out var currentLevel);
                nodeLevels[edge.To] = Math.Max(currentLevel, level + 1);

                inDegree[edge.To]--;
                if (inDegree[edge.To] == 0)
                    queue.Enqueue(edge.To);
            }
        }

        // 处理环（剩余节点）
        foreach (var node in graph.Nodes)
        {
            if (!nodeLevels.ContainsKey(node.Id))
            {
                var level = levels.Keys.Max() + 1;
                nodeLevels[node.Id] = level;
                if (!levels.ContainsKey(level))
                    levels[level] = new List<Models.GraphNode>();
                levels[level].Add(node);
            }
        }

        return levels;
    }
}
