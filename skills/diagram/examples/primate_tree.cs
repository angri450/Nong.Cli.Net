using DiagramCore;

// 灵长类动物系统发育树
// 展示人类与其他灵长类动物的进化关系

var newick = @"
(
    (
        (Human:0.1, Chimp:0.12):0.05,
        Gorilla:0.15
    ):0.08,
    (
        Orangutan:0.2,
        (Gibbon:0.25, Macaque:0.3):0.1
    ):0.1
);";

// 矩形布局
DiagramBuilder.PhylogeneticTree(
    newick,
    "primate_tree_rectangular.png",
    radial: false,
    width: 1000,
    height: 600
);

// 径向布局
DiagramBuilder.PhylogeneticTree(
    newick,
    "primate_tree_radial.png",
    radial: true,
    width: 1000,
    height: 1000
);

Console.WriteLine("已生成: primate_tree_rectangular.png");
Console.WriteLine("已生成: primate_tree_radial.png");
