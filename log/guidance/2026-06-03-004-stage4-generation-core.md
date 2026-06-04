# 2026-06-03 阶段 4 指导：生成核心

## 结论

阶段 3 可以验收。

已完成的闭环是：

- `nong word read` 负责把 docx 变成文本。
- `nong inspect diagnose` 负责论文诊断。
- `nong inspect refs` / `nong refs check` 负责参考文献风险。
- `nong chart anova` / `nong chart duncan` 负责基础农学统计。

下一阶段不要铺满 Excel、PPT、Diagram、OCR。阶段 4 应该只做“科研结果可交付物”：把分组数据变成统计报告、图、表。

## 阶段 4 优先级

### P0：`nong chart analyze <data>`

用途：一次输出 ANOVA + Duncan + 分组描述统计。

虽然阶段 3 已有 `anova` 和 `duncan`，但模型最省 token 的调用应该是：

```powershell
nong chart analyze data.json --json
```

JSON 应包含：

```json
{
  "anova": {},
  "duncan": {},
  "groups": []
}
```

非 JSON 输出应是可直接贴进日志或论文草稿的统计报告。

### P0：`nong chart bar <data> -o <png>`

用途：生成带误差棒和显著性字母的柱状图。

这是农学最核心的交付物之一。建议默认行为：

- 自动计算 mean、SD、SEM。
- 默认误差棒用 `SEM`，允许 `--error sd|sem|none`。
- 默认自动跑 Duncan 并标字母，允许 `--no-significance`。
- `--json` 输出 artifact 路径、统计结果、图片尺寸。

建议命令：

```powershell
nong chart bar data.json -o fig1.png --title "发酵产量" --ylabel "OD600" --json
```

JSON 里 `artifacts` 必须包含：

```json
{
  "png": "fig1.png"
}
```

### P1：`nong excel sheets <file>` + `nong excel read <file>`

用途：减少模型写 PowerShell 读 Excel。

只做读取，不做复杂生成：

```powershell
nong excel sheets data.xlsx --json
nong excel read data.xlsx --sheet Sheet1 --range A1:D20 --json
```

这两个命令对 agent 很有价值，但不要抢在 `chart bar` 前面。

## 暂缓

阶段 4 暂缓以下内容：

- `pptx`：等图表和表格稳定后再做。
- `diagram`：很有用，但不是当前科研数据闭环的瓶颈。
- `ocr`：依赖重、失败面大，不适合现在接入。
- `official format`：另开公文阶段处理。
- 复杂图表全集：先不要实现 18 种图。

## 建议 ClaudeCode 任务

```text
你在 C:\Users\Administrator\Documents\Github\Angri450.Nong 工作。

目标：实现阶段 4 的生成核心，只做 chart analyze 和 chart bar。

要求：
1. 保持阶段 1-3 已有命令不退化。
2. 在 Cli/Commands/ChartCommands.cs 中实现：
   - nong chart analyze <data>
   - nong chart bar <data> -o <png> [--title <text>] [--ylabel <text>] [--error sd|sem|none] [--no-significance]
3. data 支持现有 DataLoader 能读取的 JSON 和 CSV。xlsx 可以先不做，除非已有实现很顺。
4. chart analyze 调用 ChartCore.StatsEngine.FullAnalysis。
5. chart bar 使用 ChartCore.ChartBuilder 已有能力，不要在 CLI 里重写绘图逻辑。
6. chart bar 默认计算 Duncan 显著性字母并绘制；如果底层 API 不支持直接传入字母，则先完成普通柱状图 + JSON 中返回显著性结果，并在日志说明绘图标字母待补。
7. 所有命令支持 --json，错误使用现有 E001/E002/E004。
8. 输出文件路径写入 artifacts.png。
9. Release build 必须 0 错误。

验收命令：
dotnet build Cli/NongCli.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll chart analyze anova-test.json
dotnet .\Cli\bin\Release\net8.0\nong.dll chart analyze anova-test.json --json
dotnet .\Cli\bin\Release\net8.0\nong.dll chart bar anova-test.json -o fig1.png --title "发酵产量" --ylabel "OD600"
dotnet .\Cli\bin\Release\net8.0\nong.dll chart bar anova-test.json -o fig1.png --json
dotnet .\Cli\bin\Release\net8.0\nong.dll chart bar missing.json -o fig1.png --json
```

## 验收标准

阶段 4 只看两件事：

1. 模型能不能用一条命令把农学分组数据变成统计结论。
2. 模型能不能用一条命令把农学分组数据变成可插入论文的图片。

如果这两件事完成，再进入 Excel 读取阶段；否则不要扩 PPT、OCR、Diagram。
