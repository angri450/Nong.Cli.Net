# 阶段 11 补充审计（第三轮）

日期：2026-06-03
状态：完成 8/10 项

---

## 修复清单

### #1 Main 返回码（已修）
- **问题**：handler 设 `Environment.ExitCode = 1` 不成为进程返回码
- **修复**：`Program.cs` Main 改为 `var code = await builder.InvokeAsync(args); return Environment.ExitCode != 0 ? Environment.ExitCode : code;`
- **验证**：`chart analyze missing.json` EXIT:1

### #2 输出前创建父目录（已修）
- **问题**：`-o out/paper.docx` 如果 out/ 不存在会失败
- **修复**：`CliHelpers.EnsureParentDir(path)` — 写文件前 `Directory.CreateDirectory`
- **覆盖**：word fill/rebuild、inspect write-paper、chart bar、diagram flowchart/network

### #3 word rebuild 同路径守卫（已修）
- **问题**：`nong word rebuild a.docx -o a.docx` → File.Copy 自己覆盖自己
- **修复**：比较 `Path.GetFullPath(file)` 和 `Path.GetFullPath(output)`，相同则返回 E006

### #4 Excel 异常包裹（已修）
- **问题**：excel read sheet/range 不存在可能崩异常
- **修复**：excel read 加 try/catch → E004。sheets/to-groups 有 ValidateXlsx 前置 + try/catch

### #5 --raw 和 --json 行为（已修）
- **问题**：同时用 --raw 和 --json 时 agent 不知道该读什么
- **设计**：--raw 优先级，handler 先判断 raw 分支

### #6 chart JSON 命令字段（已修）
- **问题**：JSON 里 command 字段写 "stats anova"/"stats duncan"，但 stats alias 已删除
- **修复**：统一为 "chart anova"/"chart duncan"/"chart analyze"/"chart bar"

### #7 StatsValidation 全覆盖（已修）
- **问题**：仅 anova 有输入校验，duncan/analyze/bar 没有
- **修复**：anova/duncan/analyze/bar 全部在调用前提取 groups → `StatsValidation.Validate()` → 空组/单重复/NaN 返回 E006

### #8 CheckArtifact 生效（已修）
- **问题**：helper 定义了但生成命令没调用
- **修复**：chart bar、word fill/rebuild 生成后调 `CheckArtifact(path, kind)` → 不存在或 0 bytes 返回 E008

### #9 write-paper spec 结构校验（待做）
- **状态**：当前 spec 缺 heading/body 格式错 → E004（internal_error），不够精确
- **建议**：后续加 spec schema 校验，返回 E006（validation_failed）
- **影响**：低。E004 vs E006 对 agent 来说都是"失败了要修"，差别不大

### #10 Manifest 手写漂移（已知问题）
- **状态**：当前 20 implemented + 24 stub，显式标注
- **建议**：后续让 manifest 从命令注册自动生成，消除漂移
- **影响**：低。短期手动维护可接受

---

## 编译与验证

- Release build: 0 错误
- 退出码: EXIT:1（错误）、EXIT:0（成功）
- commands --json: 20 implemented + 24 stub

## 相关文件

- `Cli/Program.cs` — Main 返回码
- `Cli/Commands/ChartCommands.cs` — JSON 字段 + StatsValidation + EnsureParentDir
- `Cli/Commands/WordCommands.cs` — EnsureParentDir + rebuild 同路径守卫
- `Cli/Commands/InspectCommands.cs` — EnsureParentDir
- `Cli/Commands/DiagramCommands.cs` — EnsureParentDir
- `Cli/Commands/ExcelCommands.cs` — try/catch + --raw + Culture
- `Cli/Common/CliHelpers.cs` — EnsureParentDir + CheckArtifact + WriteError 内部设 ExitCode
- `Cli/Common/StatsValidation.cs` — 统计输入校验 + Duncan 标注
- `Cli/Common/Manifest.cs` — 20 implemented + 24 stub 显式标注
- `Cli/NongCli.csproj` — DebugType none
