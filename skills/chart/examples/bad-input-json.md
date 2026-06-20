# 输入 JSON 格式错误

## 用户想要什么

一个柱状图，但输入的 JSON 格式有问题。

## 出了什么问题

```powershell
nong chart bar bad-data.json --json
```

```json
{
  "status": "error",
  "errors": [{
    "code": "E002",
    "message": "validation_failed: missing required field 'groups'"
  }]
}
```

用户给的 JSON：
```json
{
  "treatment": "CK",
  "yield": [5.2, 5.1, 5.3]
}
```

## 怎么修的

告诉用户正确的格式：

```json
{
  "groups": [
    {"name": "CK", "values": [5.2, 5.1, 5.3, 5.0, 5.2]},
    {"name": "N1", "values": [6.1, 6.3, 5.9, 6.2, 6.0]}
  ]
}
```

用户修正 JSON 后重新运行：

```powershell
nong chart bar fixed-data.json -o result.png --json
# 成功
```

## 关键点

- Chart 命令的 JSON 输入格式要求 `groups` 数组，每个元素有 `name`（字符串）和 `values`（数字数组）。
- 错误码 E002 表示 validation_failed——检查 JSON 结构和必填字段。
- 不要把单个处理组的数据直接传给 `chart bar`——即使是单组也要用 `groups` 数组包裹。
- 如果数据在 Excel 里，先用 `excel to-groups` 转成 groups JSON。
