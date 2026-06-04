# 2026-06-03 阶段 5 指导：Excel 数据入口

## 目标

阶段 5 只解决一个问题：让模型不再写 PowerShell 临时读取 Excel。

科研工作流里，Excel 是数据入口。阶段 4 已经能从 JSON/CSV 生成统计和图，阶段 5 要把 `.xlsx` 接进来。

## P0 命令

```powershell
nong excel sheets data.xlsx --json
nong excel read data.xlsx --sheet Sheet1 --range A1:D20 --json
nong excel to-groups data.xlsx --sheet Sheet1 --group A --value B --json
```

其中 `to-groups` 是关键：它要把 Excel 转成 `chart analyze/bar` 能吃的分组数据。

## 实现边界

1. `excel sheets`：列出 sheet 名称、行列范围。
2. `excel read`：读取指定 sheet/range，输出二维数组。
3. `excel to-groups`：按组列和值列输出：

```json
{
  "CK": [1.2, 1.3, 1.1],
  "T1": [2.0, 2.2, 2.1]
}
```

4. 暂时不做复杂 Excel 生成、样式、公式、透视表。

## JSON 要求

`excel read --json`：

```json
{
  "status": "ok",
  "command": "excel read",
  "data": {
    "sheet": "Sheet1",
    "range": "A1:D20",
    "rows": []
  },
  "metrics": {
    "rows": 20,
    "columns": 4
  }
}
```

`excel to-groups --json`：

```json
{
  "status": "ok",
  "command": "excel to-groups",
  "data": {
    "groups": {}
  },
  "metrics": {
    "groups": 3,
    "observations": 18
  }
}
```

## 建议 ClaudeCode 任务

```text
目标：实现阶段 5 Excel 数据入口。

只做：
1. nong excel sheets <file>
2. nong excel read <file> --sheet <name> [--range <A1:D20>]
3. nong excel to-groups <file> --sheet <name> --group <col> --value <col>

要求：
- 新建 Cli/Commands/ExcelCommands.cs。
- Program.cs 用 ExcelCommands 替换 excel stub。
- 优先复用 Excel 包里已有 ExcelPreview / ClosedXML 能力。
- to-groups 输出必须兼容 chart analyze/chart bar 的输入结构。
- 错误码沿用 E001/E002/E004。
- Release build 0 错误。
```

## 验收标准

阶段 5 完成后，应能做到：

```powershell
nong excel to-groups data.xlsx --sheet Sheet1 --group A --value B --json > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
```

这条链路跑通，阶段 5 就合格。
