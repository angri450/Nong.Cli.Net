# nong pptx CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong pptx` 是 PPT 层（Angri450.Nong.Pptx）的 CLI 入口。覆盖读、查、写三类操作。底层基于 ShapeCrawler + 内部布局系统。

底层实现：PresentationBuilder、SlideBuilder、SlidePreview、SlideValidator、ThemePreset、RawAccessor、LayoutSystem。

---

## Pptx 包 9 个文件 → CLI 命令映射

### 1. SlideBuilder.cs（入口）

对应命令（写）：

#### `nong pptx create <file> [--theme <name>]`
创建空白 PPT 文件。主题：professional / academic / modern / minimal / warm / cool。

实现：SlideBuilder.Create().Theme(ThemePreset.xxx).Save()

---

### 2. PresentationBuilder.cs（演示文稿构建器）

对应命令（写）：

#### `nong pptx add title-slide <file> --title <text> [--subtitle <text>] [--author <text>]`
添加封面页。

实现：PresentationBuilder.AddTitleSlide()

#### `nong pptx add slide <file> --title <text> --bullets <json> [--layout <name>]`
添加内容页。`--bullets` JSON 数组 `["要点1", "要点2"]`。

10 种布局模式：TwoColumns / Cards / BigNumber / Quote / SingleFocus / Symmetric / Asymmetric / ThreeColumn / PrimarySecondary / HeroTop

实现：PresentationBuilder.AddContentSlide()

#### `nong pptx add table-slide <file> --title <text> --data <json>`
添加表格页。JSON 格式：`{"headers":["A","B"],"rows":[["1","2"]]}`

实现：PresentationBuilder.AddTableSlide()

#### `nong pptx add chart-slide <file> --title <text> --chart <pie|bar> --data <json>`
添加图表页。

实现：PresentationBuilder.AddChartSlide()

---

### 3. SlideBuilders.cs（四种幻灯片构建器）

被 PresentationBuilder 内部调用，不暴露独立命令。

---

### 4. SlideHelper.cs（单页操作 API + 10 种布局模式）

被 PresentationBuilder 内部调用，不暴露独立命令。

---

### 5. SlidePreview.cs（预览诊断器 + ShapeMap）

对应命令（读+查）：

#### `nong pptx read <file>`
提取所有幻灯片文本内容。

输出：
```
=== 幻灯片 1（封面）===
标题: 枯草芽孢杆菌发酵工艺优化
副标题: XX大学 硕士学位论文
作者: 张三

=== 幻灯片 2 ===
标题: 研究背景
  - 枯草芽孢杆菌是重要的工业微生物
  - 其代谢产物在农业领域有广泛应用
  ...
```

实现：SlidePreview.Preview()

#### `nong pptx slides <file>`
列出幻灯片结构（类型+标题+形状数）。

实现：SlidePreview.Preview() 摘要部分

#### `nong pptx shape-map <file>`
输出幻灯片完整形状映射（JSON），供程序化处理。

实现：SlidePreview.ShapeMap()

---

### 6. SlideValidator.cs（PPT 结构验证器）

对应命令（查）：

#### `nong pptx validate <file>`
验证 PPT 文件结构完整性：文件大小、幻灯片数量、尺寸、内容可见性。

实现：SlideValidator.Validate()

---

### 7. ThemePreset.cs（10 套主题预设）

内部使用，被 create 命令的 `--theme` 参数驱动。不暴露独立命令。

---

### 8. RawAccessor.cs（底层 OOXML 访问器）

对应命令（改）：

#### `nong pptx raw <file> list-parts`
列出所有 OOXML 部件。

#### `nong pptx raw <file> get-part <path>`
获取指定部件 XML。

#### `nong pptx raw <file> set-part <path> --xml <xml>`
设置指定部件 XML。

实现：RawAccessor 的 ListParts / GetPart / SetPart

---

### 9. LayoutSystem.cs（布局常量）

内部使用，不暴露命令。

---

## 命令总数：13 个

| 类别 | 数量 | 命令 |
|------|------|------|
| 读 | 3 | read, slides, shape-map |
| 查 | 1 | validate |
| 写 | 6 | create, add title-slide, add slide, add table-slide, add chart-slide, add notes |
| 改 | 3 | raw list-parts, raw get-part, raw set-part |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| read | P0 | 提取文本 |
| slides | P0 | 列出结构 |
| create | P1 | 创建 |
| add slide | P1 | 添加内容页 |
| validate | P1 | 验证 |
| 其余 8 个 | P2 | 后续迭代 |
