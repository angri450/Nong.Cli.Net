using System;
using System.Collections.Generic;

namespace DiagramCore.Layout;

/// <summary>
/// 树形布局算法（用于系统发育树）
/// 径向布局或矩形布局
/// </summary>
public class TreeLayout
{
    private const double LeafSpacing = 30;
    private const double BranchLengthScale = 50;

    public void LayoutRadial(Models.NewickTree root, double centerX, double centerY, double radius)
    {
        var leaves = root.GetLeaves();
        double angleStep = 360.0 / leaves.Count;

        int leafIndex = 0;
        LayoutNodeRadial(root, centerX, centerY, radius, ref leafIndex, 0, 360);
    }

    private void LayoutNodeRadial(Models.NewickTree node, double centerX, double centerY, double radius,
        ref int leafIndex, double startAngle, double endAngle)
    {
        if (node.IsLeaf)
        {
            double angle = (startAngle + endAngle) / 2;
            double rad = angle * Math.PI / 180;
            node.X = centerX + radius * Math.Cos(rad);
            node.Y = centerY + radius * Math.Sin(rad);
            leafIndex++;
        }
        else
        {
            double childAngleSpan = (endAngle - startAngle) / node.Children.Count;
            for (int i = 0; i < node.Children.Count; i++)
            {
                double childStart = startAngle + i * childAngleSpan;
                double childEnd = childStart + childAngleSpan;
                LayoutNodeRadial(node.Children[i], centerX, centerY, radius, ref leafIndex, childStart, childEnd);
            }

            // 父节点在子节点中心
            node.X = node.Children.Average(c => c.X);
            node.Y = node.Children.Average(c => c.Y);
        }
    }

    public void LayoutRectangular(Models.NewickTree root, double startX, double startY)
    {
        var leaves = root.GetLeaves();
        double currentY = startY;

        LayoutNodeRectangular(root, startX, ref currentY);
    }

    private double LayoutNodeRectangular(Models.NewickTree node, double x, ref double currentY)
    {
        node.X = x;

        if (node.IsLeaf)
        {
            node.Y = currentY;
            currentY += LeafSpacing;
            return node.Y;
        }
        else
        {
            double childX = x + node.BranchLength * BranchLengthScale;
            double firstChildY = double.MaxValue;
            double lastChildY = double.MinValue;

            foreach (var child in node.Children)
            {
                double childY = LayoutNodeRectangular(child, childX, ref currentY);
                firstChildY = Math.Min(firstChildY, childY);
                lastChildY = Math.Max(lastChildY, childY);
            }

            node.Y = (firstChildY + lastChildY) / 2;
            return node.Y;
        }
    }
}
