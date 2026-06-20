using DiagramCore;
using DiagramCore.Models;

// 16S rRNA 微生物组分析流程图
// 展示从样品采集到生物信息分析的完整流程

var graph = new Graph();

// 添加节点
graph.AddNode("sample", "样品采集");
graph.AddNode("dna", "DNA 提取");
graph.AddNode("pcr", "PCR 扩增\n(16S V3-V4)");
graph.AddNode("seq", "Illumina 测序");
graph.AddNode("qc", "质量控制");
graph.AddNode("otu", "OTU 聚类");
graph.AddNode("taxonomy", "物种注释");
graph.AddNode("analysis", "多样性分析");

// 添加边
graph.AddEdge("sample", "dna");
graph.AddEdge("dna", "pcr");
graph.AddEdge("pcr", "seq");
graph.AddEdge("seq", "qc");
graph.AddEdge("qc", "otu");
graph.AddEdge("otu", "taxonomy");
graph.AddEdge("taxonomy", "analysis");

// 渲染流程图
DiagramBuilder.Flowchart(graph, "16S_workflow.png", width: 1000, height: 800);

Console.WriteLine("已生成: 16S_workflow.png");
