# nong excel CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong excel` 是 Excel 层（Angri450.Nong.Excel）的 CLI 入口。覆盖读、查、写三类操作。底层基于 ClosedXML（via ThirdParty）。

底层实现：ExcelBuilder、AdvancedBuilder、ExcelPreview、FormulaValidator、StylePresets。

---

## Excel 包 5 个文件 → CLI 命令映射

### 1. ExcelBuilder.cs（基础工作表构建器 + SheetBuilder 链式 API）

对应命令（写）：

#### `nong excel read <file> [--sheet <name|N>] [--range <A1:B10>]`
读取 Excel 文件内容。

输入：.xlsx 文件路径
输出：CSV 或 JSON（`--json`），指定 sheet 和范围

实现：SheetBuilder 读取 → 逐行输出

#### `nong excel sheets <file>`
列出所有工作表名称 + 行列数。

实现：XLWorkbook.Worksheets 遍历

#### `nong excel create <file> [--sheet <name>]`
创建空白 .xlsx 文件（含一个 sheet）。

实现：ExcelBuilder.Sheet(wb, name)

---

### 2. AdvancedBuilder.cs（高级功能：透视表、图片、迷你图、条件格式等）

对应命令（写）：

#### `nong excel add pivot <file> --sheet <name> --source <range> --rows <cols> --values <cols> [-o <file>]`
添加透视表。

#### `nong excel add picture <file> --sheet <name> --src <image> [--cell <A1>]`
插入图片。

#### `nong excel add sparkline <file> --sheet <name> --type <line|column|winloss> --source <range> --cell <A1>`
插入迷你图。

#### `nong excel add filter <file> --sheet <name> --range <A1:B10>`
添加自动筛选器。

#### `nong excel add conditional <file> --sheet <name> --range <A1:B10> --rule <type> --value <v>`
添加条件格式。规则类型：contains / equals / greater / less / between / blank / error / duplicate / unique / top / bottom。

#### `nong excel add databar <file> --sheet <name> --range <A1:A10> [--color <hex>]`
数据条。

#### `nong excel add colorscale <file> --sheet <name> --range <A1:A10> [--low <red> --mid <yellow> --high <green>]`
色阶。

#### `nong excel add iconset <file> --sheet <name> --range <A1:A10>`
图标集。

#### `nong excel sort <file> --sheet <name> --range <A1:B10> --by <col> --order <asc|desc>`
排序。

#### `nong excel protect <file> --sheet <name> [--password <pw>]`
保护工作表。

---

### 3. ExcelPreview.cs（预览诊断器）

对应命令（查）：

#### `nong excel preview <file> [--sheet <name>]`
预览工作表内容和结构。

输出：
```
Sheet: 实验数据 (30行 × 5列)
列: A(菌株) B(OD600) C(pH) D(温度) E(时间)

  A       B      C     D    E
  枯草1   2.34   7.0   37   24h
  枯草2   1.89   6.8   37   24h
  ...
  警告: 列 D 包含零宽列
```

实现：ExcelPreview.Preview()

#### `nong excel validate <file>`
公式审计。

输出：所有公式的错误检查结果（计算错误、未保护除法、未保护 VLOOKUP）。

实现：FormulaValidator.Audit()

---

### 4. FormulaValidator.cs（公式验证器）

对应命令：

#### `nong excel audit <file>`
等价于 `nong excel validate`，专门审计公式。

#### `nong excel eval <file> [-o <file>]`
保存含公式预计算结果的工作簿。

实现：FormulaValidator.SaveWithEvaluation()

---

### 5. StylePresets.cs（样式预设）

内部使用，不暴露命令。`Mono` 和 `Finance` 两套主题在 create 时自动应用。

---

## 命令总数：17 个

| 类别 | 数量 | 命令 |
|------|------|------|
| 读 | 3 | read, sheets, preview |
| 查 | 3 | validate, audit, eval |
| 写 | 11 | create, add pivot/picture/sparkline/filter/conditional/databar/colorscale/iconset, sort, protect |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| read | P0 | 读取 Excel，最高频 |
| sheets | P0 | 列 sheet，跟 read 配合 |
| create | P0 | 创建空白文件 |
| preview | P1 | 预览诊断 |
| validate | P1 | 公式审计 |
| 其余 12 个 | P2 | 后续迭代 |
