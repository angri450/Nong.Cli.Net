# Diagram 布局优化：自适应节点 + 自动裁剪画布

**时间**: 2026-06-01
**影响包**: Angri450.Nong.Diagram (3.0.4)
**类型**: Enhancement

## 变更

三个渲染器全部从固定画布改为自动裁剪到内容边界：

| 渲染器 | 修复前 | 修复后 |
|--------|--------|--------|
| NetworkGraphRenderer | 固定画布，20px 硬编码节点半径 | 文本驱动半径 + 力导向 + 自动裁剪 |
| FlowchartRenderer | 固定 800x600 | Sugiyama 布局后自动裁剪 |
| TreeRenderer | 固定 800x600 | 布局后自动裁剪 |

## NetworkGraphRenderer 详细变更

1. 节点半径根据文本内容自适应计算（14px 字体 × 字符数 + 内边距）
2. 支持多行文本（`\n` 分隔）
3. 自动裁剪画布：布局后计算节点边界框，输出精确匹配内容

## ForceDirectedLayout 重写

- 节点尺寸感知（最小间距 = 半径之和 + 边距）
- 环形初始化代替随机散布
- 中心引力防止漂移
- 斥力自适应：距离越近力度越大

## 技能须知

- `DiagramBuilder.NetworkGraph()` / `Flowchart()` / `PhylogeneticTree()` 的 `width`/`height` 参数**不再决定最终输出尺寸**——仅用于布局计算空间。输出会自动裁剪。
- 可以为所有调用传入 900x600 之类的安全默认值。
- 节点标签支持 `\n` 多行文本。
