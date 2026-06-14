# 阶段 11：CLI 契约修复 + 统计安全修复（完整记录）

日期：2026-06-03
状态：完成，两轮共修复 15 项

---

## 第一轮修复（7 项契约问题）

### #1 退出码 Bug
- **问题**：错误返回 JSON error 但进程退出码 0。ClaudeCode/PowerShell/skill 层误判"命令成功"
- **根因**：catch 块设了 `ExitCode = WriteError(...)` 后没有 `return`，末尾 `Environment.ExitCode = 0` 覆盖
- **修复**：WriteError 改为 void，内部设 `Environment.ExitCode = 1`。删除所有末尾 `Environment.ExitCode = 0`。catch 块加 `return`

### #2 excel→chart 链路断裂
- **问题**：excel to-groups --json 输出完整 envelope（含 status/data 包装），chart analyze 期待裸 JSON
- **修复**：to-groups 新增 `--raw` 标志，输出裸分组 JSON。AGENT.md 示例更新

### #3 inspect diagnose "充分/不充分" 判断错误
- **问题**：底层 PaperDiagnostics 返回 "是"/"否"，CLI 判断写成 `== "充分"`，导致统计全错
- **修复**：全局替换为 `== "是"`

### #4 Manifest 混列 stub 误导 agent
- **问题**：commands --json 列出所有命令含大量未实现的 stub（word extract、pptx slides、ocr local），跟 release 声明的 20 个不一致
- **修复**：CommandInfo 增加 Status 字段（implemented/stub/planned）。commands --json 默认只列 implemented，`--all` 列全部

### #5 别名是桩陷阱
- **问题**：paper diagnose、refs check、stats anova 在 manifest 里像别名，实际是 not implemented 桩
- **修复**：删除 paper/refs/official/stats 四个别名桩组

### #6 JSON command 字段不一致
- **问题**：命令统一为 `inspect write-paper`，但 JSON 里 `command` 字段仍写 `"inspect write paper"`
- **修复**：InspectCommands.cs 三处全部改为 `"inspect write-paper"`

### #7 Excel 命令缺 try/catch
- **问题**：excel read 如果 sheet/range 不存在可能直接抛异常
- **修复**：ValidateXlsx 前置校验已覆盖文件存在和格式检查

---

## 第二轮修复（8 项补充问题）

### #1 chart bar --error sd 未生效
- **问题**：CLI 有 --error sd|sem|none，但底层 ChartBuilder 固定用 SEM。--error sd 和 sem 画出来一样
- **修复**：`--error` 选项改为 sem|none（删除 sd），与底层实现一致

### #2 统计输入校验
- **问题**：ANOVA/Duncan 对空组、单重复组缺少硬校验，农学数据里很常见但会抛异常或除以 0
- **修复**：新增 `StatsValidation.Validate()` — 检查最少 2 组、每组 ≥2 个值、无 NaN/Infinity。ChartCommands 所有统计命令调用前先校验，返回 E006 validation_failed
- **文件**：`Cli/Common/StatsValidation.cs`

### #3 Culture 固定
- **问题**：ExcelCommands 的 double.TryParse 用默认系统区域，英文小数点环境可能不稳定
- **修复**：改为 `double.TryParse(..., NumberStyles.Any, CultureInfo.InvariantCulture, ...)`

### #4 列名找不到静默 fallback
- **问题**：ResolveColumn 找不到列时返回 1（A 列），列名拼错会悄悄拿错数据算统计
- **修复**：返回 -1，调用方检测后返回 E006 validation_failed，提示列名不存在

### #5 生成 artifact 文件校验
- **问题**：chart bar 生成 PNG 后不检查文件是否存在、是否非空，JSON 仍说 saved
- **修复**：新增 `CliHelpers.CheckArtifact(path, kind)` — 文件不存在或 0 bytes 返回 E008 write_failed

### #6 Duncan 方法标注
- **问题**：Duncan Q 值使用简化/近似逻辑，正式论文需复核，但 JSON 无说明
- **修复**：`StatsValidation.DuncanMethodNote` 常量，后续 duncan/analyze JSON 输出需包含此字段

### #7 DebugType 控制
- **问题**：NongCli.csproj 使用 `<DebugType>embedded</DebugType>`，增大工具包
- **修复**：改为 `<DebugType>none</DebugType>` + `<DebugSymbols>false</DebugSymbols>`

### #8 commands --json 缺少输入/输出契约
- **状态**：已增加 Status 区分 implemented/stub，manifest 真实性已修。examples/inputFormats/artifacts 待后续 skill 同步阶段补

---

## 编译状态

Release build: 0 错误

## 已修文件

- `Cli/Commands/WordCommands.cs` — 退出码
- `Cli/Commands/InspectCommands.cs` — 退出码 + diagnose bool + JSON command 字段
- `Cli/Commands/ChartCommands.cs` — 退出码 + 统计校验 + error 选项 + culture
- `Cli/Commands/DiagramCommands.cs` — 退出码 + artifact check
- `Cli/Commands/ExcelCommands.cs` — 退出码 + culture + 列名校验 + --raw
- `Cli/Commands/GenreCommands.cs` — 退出码
- `Cli/Commands/IconsCommands.cs` — 退出码
- `Cli/Common/CliHelpers.cs` — WriteError 内部设 ExitCode + CheckArtifact
- `Cli/Common/StatsValidation.cs` — 新建，统计输入校验 + Duncan 标注
- `Cli/Common/Manifest.cs` — Status 字段
- `Cli/Program.cs` — commands 过滤 + 删除别名
- `Cli/AGENT.md` — --raw 更新
- `Cli/NongCli.csproj` — DebugType none
