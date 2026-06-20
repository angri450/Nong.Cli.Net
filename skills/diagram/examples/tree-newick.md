# Newick 系统发育树

## 用户想要什么

从一条 Newick 格式的进化树数据生成系统发育树图。

## 做了什么

```powershell
# 准备 Newick 数据
echo "(A:0.1,(B:0.2,(C:0.15,D:0.12):0.08):0.05);" > tree.nwk

# 生成树图
nong diagram tree tree.nwk -o phylogeny.png --json
```

## 结果

输出 `phylogeny.png`：标准的系统发育树，节点标注物种名，枝长按比例渲染。

## 关键点

- `diagram tree` 的输入必须是 Newick 格式（`.nwk` 或 `.newick`）。
- 枝长是可选字段。如果 Newick 中没有枝长（如 `(A,B,(C,D))`），则渲染为等距树。
- 树图渲染方向为从左到右。不支持圆形树或径向树。
- 如果用户在 Excel 里存了距离矩阵，需要先手工转换成 Newick 格式——当前不支持从矩阵直接生成树。
