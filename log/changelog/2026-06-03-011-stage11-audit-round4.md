# 阶段 11 补充审计（第四轮）

日期：2026-06-03
状态：5 项全部修复

---

## 修复

### #1 inspect write-paper spec 校验
- **问题**：`{"sections":[{}]}` 抛异常变 E004，并留下 2378 bytes 半成品 docx
- **修复**：新增 `ValidatePaperSpec(json)` — 校验 title/abstract/keywords 为 string、sections[].heading 必填且为 string、level 1-3、body[] 为 string 数组、references[] 为 string 数组。不合法返回 E006 含字段路径（如 `sections[0].heading is required and must be a string`）。catch 块中 `try { File.Delete(output); } catch { }` 清理半成品
- **文件**：`Cli/Commands/InspectCommands.cs`

### #2 stub 命令返回成功
- **问题**：`nong word extract --json` 返回 status:ok、EXIT:0，误导模型以为功能执行成功
- **修复**：新增 E009 not_implemented。`CliHelpers.SetNotImplemented` 改为调用 WriteError → status:error、code:E009、EXIT:1
- **文件**：`Cli/Common/ErrorCodes.cs`、`Cli/Common/CliHelpers.cs`
- **验证**：`nong word extract --json` EXIT:1

### #3 inspect diagnose/refs JSON 命令字段
- **问题**：JSON 输出 `"command": "paper diagnose"` 和 `"command": "refs check"`，但命令入口是 `inspect diagnose` / `inspect refs`
- **修复**：统一为 `"inspect diagnose"` / `"inspect refs"`
- **文件**：`Cli/Commands/InspectCommands.cs`

### #4 CheckArtifact 覆盖 word fill/rebuild
- **问题**：日志说已覆盖，实际只有 chart bar 调了 CheckArtifact
- **修复**：word fill、word rebuild 生成后调用 `CheckArtifact(output, "DOCX")`，不存在或 0 bytes 返回 E008
- **文件**：`Cli/Commands/WordCommands.cs`

### #5 chart 校验错误 command 字段
- **问题**：StatsValidation 错误返回 `"command": "chart"`，不够精确
- **修复**：anova → `"chart anova"`、duncan → `"chart duncan"`、analyze → `"chart analyze"`、bar → `"chart bar"`
- **文件**：`Cli/Commands/ChartCommands.cs`

---

## 编译与验证

- Release build: 0 错误
- stub 命令: EXIT:1 + E009
- inspect diagnose JSON: command="inspect diagnose"

## 阶段 11 四轮累计

共修复 30 项 CLI 契约问题。
