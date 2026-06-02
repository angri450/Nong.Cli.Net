using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramCore.Layout;

/// <summary>
/// 力导向图布局算法 (Force-Directed Layout)
/// 基于 Fruchterman-Reingold 算法，支持节点尺寸感知
/// </summary>
public class ForceDirectedLayout
{
    private const int MaxIterations = 500;
    private const double InitialTemperature = 150;
    private const double CoolingFactor = 0.97;
    private const double AttractiveForceConstant = 0.12;
    private const double RepulsiveForceConstant = 8000;
    private const double MinNodeDistance = 10;
    private const double EdgePadding = 10;

    /// <summary>计算节点渲染半径（基于文本内容），供渲染器使用</summary>
    public Dictionary<string, double> NodeRadii { get; } = new();

    /// <param name="graph">图数据（节点标签将用于计算尺寸）</param>
    /// <param name="nodeRadii">可选预计算的节点半径，为空则自动估算</param>
    public void Layout(Models.Graph graph, double width = 800, double height = 600,
        Dictionary<string, double>? nodeRadii = null)
    {
        if (graph.Nodes.Count == 0) return;

        // 计算或使用传入的节点半径
        foreach (var node in graph.Nodes)
        {
            if (nodeRadii?.TryGetValue(node.Id, out var r) == true)
                NodeRadii[node.Id] = r;
            else
                NodeRadii[node.Id] = EstimateRadius(node.Label);
        }

        var nodes = graph.Nodes;
        var edges = graph.Edges;

        // 环形初始化，避免随机造成的重叠
        var random = new Random(42);
        double centerX = width / 2;
        double centerY = height / 2;
        double initRadius = Math.Min(width, height) * 0.15;
        for (int i = 0; i < nodes.Count; i++)
        {
            double angle = (2 * Math.PI * i / nodes.Count) + random.NextDouble() * 0.1;
            nodes[i].X = centerX + initRadius * Math.Cos(angle);
            nodes[i].Y = centerY + initRadius * Math.Sin(angle);
        }

        double temperature = InitialTemperature;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            var displacements = new Dictionary<string, (double dx, double dy)>();
            foreach (var node in nodes)
                displacements[node.Id] = (0, 0);

            // 斥力（所有节点对之间，考虑节点半径）
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var dx = nodes[i].X - nodes[j].X;
                    var dy = nodes[i].Y - nodes[j].Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                    double minSeparation = NodeRadii[nodes[i].Id] + NodeRadii[nodes[j].Id] + MinNodeDistance;
                    double force = RepulsiveForceConstant / (dist * dist);

                    // 如果距离小于最小间距，加大斥力
                    if (dist < minSeparation)
                        force *= (minSeparation / dist) * 2;

                    var fx = (dx / dist) * force;
                    var fy = (dy / dist) * force;

                    var disp1 = displacements[nodes[i].Id];
                    displacements[nodes[i].Id] = (disp1.dx + fx, disp1.dy + fy);
                    var disp2 = displacements[nodes[j].Id];
                    displacements[nodes[j].Id] = (disp2.dx - fx, disp2.dy - fy);
                }
            }

            // 引力（沿边）
            foreach (var edge in edges)
            {
                var from = graph.GetNode(edge.From);
                var to = graph.GetNode(edge.To);
                if (from == null || to == null) continue;

                var dx = to.X - from.X;
                var dy = to.Y - from.Y;
                var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                var force = dist * AttractiveForceConstant;
                var fx = (dx / dist) * force;
                var fy = (dy / dist) * force;

                var dispFrom = displacements[from.Id];
                displacements[from.Id] = (dispFrom.dx + fx, dispFrom.dy + fy);
                var dispTo = displacements[to.Id];
                displacements[to.Id] = (dispTo.dx - fx, dispTo.dy - fy);
            }

            // 中心引力（防止漂移）
            foreach (var node in nodes)
            {
                double dxCenter = centerX - node.X;
                double dyCenter = centerY - node.Y;
                double distToCenter = Math.Max(Math.Sqrt(dxCenter * dxCenter + dyCenter * dyCenter), 0.01);
                double centerForce = distToCenter * 0.001;
                var disp = displacements[node.Id];
                displacements[node.Id] = (disp.dx + dxCenter / distToCenter * centerForce,
                                          disp.dy + dyCenter / distToCenter * centerForce);
            }

            // 应用位移
            foreach (var node in nodes)
            {
                var (dx, dy) = displacements[node.Id];
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                {
                    var scale = Math.Min(dist, temperature) / dist;
                    node.X += dx * scale;
                    node.Y += dy * scale;
                }

                double r = NodeRadii[node.Id];
                node.X = Math.Max(r + EdgePadding, Math.Min(width - r - EdgePadding, node.X));
                node.Y = Math.Max(r + EdgePadding, Math.Min(height - r - EdgePadding, node.Y));
            }

            temperature *= CoolingFactor;
        }

        // 更新节点宽高以匹配实际渲染尺寸
        foreach (var node in nodes)
        {
            double r = NodeRadii[node.Id];
            node.Width = r * 2;
            node.Height = r * 2;
        }
    }

    private static double EstimateRadius(string label)
    {
        if (string.IsNullOrEmpty(label)) return 30;
        // 按换行拆开，取最长一行的字符数估算
        var lines = label.Split('\n');
        int maxChars = lines.Max(l => l.Length);
        // 中文字符约 14px 宽，12px 字体 + padding
        double textWidth = maxChars * 14 + 20;
        double textHeight = lines.Length * 16 + 16;
        // 圆的半径要能包住最宽的行
        return Math.Max(28, Math.Max(textWidth / 2, textHeight / 2) + 8);
    }
}
