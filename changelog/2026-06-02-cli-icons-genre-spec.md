# nong icons + nong genre CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 一、nong icons（Bioicons 包）

### 概述

`nong icons` 是科学图标层（Angri450.Nong.Bioicons）的 CLI 入口。覆盖 40 个 SVG 科学图标的搜索、预览和导出。

底层实现：IconProvider（6 个分类：Biology、Chemistry、Medical、LabEquipment、Arrows、Experimental）。

### IconProvider.cs → CLI 命令映射

#### `nong icons list`
列出所有可用图标分类及数量。

输出：
```
Biology (12): dna, cell, protein, enzyme, bacteria, virus, leaf, ...
Chemistry (8): flask, beaker, test-tube, molecule, atom, ...
Medical (6): syringe, pill, stethoscope, heart, ...
LabEquipment (7): microscope, centrifuge, pipette, ...
Arrows (4): arrow-up, arrow-down, arrow-left, arrow-right
Experimental (3): thermometer, ph-meter, spectrophotometer
```

实现：IconProvider.GetCategories() + GetIcons()

#### `nong icons search <keyword>`
搜索图标。

```
nong icons search dna   →  "Biology/dna"
nong icons search flask →  "Chemistry/flask"
```

实现：遍历所有分类的图标名称，模糊匹配

#### `nong icons show <category> <name>`
显示图标 SVG 内容。

```
nong icons show Biology dna   →  输出 SVG 源码
```

实现：IconProvider.GetSvg(category, name)

#### `nong icons export <category> <name> [-o <svg>]`
导出图标为 .svg 文件。

实现：IconProvider.SaveSvg(category, name, outputPath)

---

### 命令总数：4 个

---

## 二、nong genre（Genre 模板库）

### 概述

`nong genre` 是格式模板层（Angri450.Nong.Genre）的 CLI 入口。覆盖 6 个 JSON 格式模板的浏览和查询。

底层实现：GenreTemplate（Load/List）。

### GenreTemplate.cs → CLI 命令映射

#### `nong genre list`
列出所有可用模板。

输出：
```
journal-paper     — 期刊论文（GB/T 7714）
degree-thesis     — 毕业论文
contest-paper     — 竞赛论文
defense-ppt       — 答辩 PPT
official-notice   — 通知公文
business-letter   — 商务信函
```

实现：GenreTemplate.List()

#### `nong genre show <name>`
显示模板完整 JSON 内容。

```
nong genre show degree-thesis   →  输出完整 JSON
```

实现：GenreTemplate.Load(name)

---

### 命令总数：2 个

---

## 第一版实施计划

### icons

| 命令 | 优先级 | 说明 |
|------|--------|------|
| list | P1 | 列出图标 |
| search | P1 | 搜索图标 |
| show | P2 | 显示 SVG |
| export | P2 | 导出 SVG |

### genre

| 命令 | 优先级 | 说明 |
|------|--------|------|
| list | P0 | 列出模板 |
| show | P0 | 查看模板 |
