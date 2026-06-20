using DiagramCore;
using DiagramCore.Models;

// 蛋白互作网络图
// 展示关键信号蛋白之间的相互作用关系

var graph = new Graph();

// 添加蛋白节点
graph.AddNode("TP53", "TP53\n(肿瘤抑制)");
graph.AddNode("BRCA1", "BRCA1\n(DNA修复)");
graph.AddNode("EGFR", "EGFR\n(生长因子受体)");
graph.AddNode("KRAS", "KRAS\n(GTP酶)");
graph.AddNode("MYC", "MYC\n(转录因子)");
graph.AddNode("AKT", "AKT\n(激酶)");
graph.AddNode("PTEN", "PTEN\n(磷酸酶)");

// 添加互作边
graph.AddEdge("TP53", "BRCA1", "调控");
graph.AddEdge("BRCA1", "EGFR", "抑制");
graph.AddEdge("EGFR", "KRAS", "激活");
graph.AddEdge("KRAS", "MYC", "激活");
graph.AddEdge("MYC", "TP53", "抑制");
graph.AddEdge("EGFR", "AKT", "激活");
graph.AddEdge("AKT", "PTEN", "抑制");
graph.AddEdge("PTEN", "AKT", "去磷酸化");

// 渲染网络图
DiagramBuilder.NetworkGraph(graph, "protein_network.png", width: 1200, height: 1200);

Console.WriteLine("已生成: protein_network.png");
