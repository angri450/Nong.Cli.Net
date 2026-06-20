# 多 Sheet 读取

## 用户想要什么

一个 Excel 文件有多个 sheet（试验设计、原始数据、统计分析），需要逐个读取。

## 做了什么

```powershell
# 1. 列出所有 sheet
nong excel sheets experiment.xlsx --json

# 输出：["试验设计", "原始数据", "统计分析"]

# 2. 逐个读取需要的 sheet
nong excel read experiment.xlsx --sheet "试验设计" --json
nong excel read experiment.xlsx --sheet "原始数据" --json

# 3. 对"原始数据"做切片（进入 NongPandoc 管线）
nong excel dissect experiment.xlsx --sheet "原始数据" -o raw-data.slice --json

# 4. 读切片包
nong slice inspect raw-data.slice --json
```

## 结果

`excel sheets` 列出所有 sheet 名。`excel read` 逐个读取内容。`excel dissect` 把指定 sheet 切片为 NongPandoc 包，AI 可以结构化的方式读。

## 关键点

- 先 `excel sheets` 看有哪些 sheet，再决定读哪个。不要假设只有一个 sheet。
- `excel read` 是纯文本读取，适合快速看内容。
- `excel dissect` 是结构化切片，输出 NongPandoc 包（content.jsonl、structure.json 等），适合 AI 做深度分析。
- 如果不需要切片，只读关键 sheet 即可——不要整个 workbook 全部切片。
