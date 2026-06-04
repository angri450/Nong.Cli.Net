# 阶段 3：科研核心接入结果

日期：2026-06-03
状态：完成

---

## 改动清单

### 新增

| 文件 | 用途 |
|------|------|
| `Cli/Commands/InspectCommands.cs` | `inspect diagnose`（完整诊断管线）+ `inspect refs`（参考文献检查） |
| `Cli/Commands/ChartCommands.cs` | `chart anova`（ANOVA）+ `chart duncan`（Duncan MRT） |

### 修改

| 文件 | 改动 |
|------|------|
| `Cli/NongCli.csproj` | 新增 `ChartCore.csproj` ProjectReference |
| `Cli/Program.cs` | 用 InspectCommands/ChartCommands 替换 inspect/chart 桩组 |
| `Cli/Common/CliHelpers.cs` | 新增 `ValidateTextFile` 方法 |

---

## 验收结果

| 命令 | 结果 |
|------|------|
| `nong inspect diagnose paper-test.txt` | 完整诊断报告（类型/证据链/数据需求/缺口/质量/引文） |
| `nong inspect diagnose --json` | JSON 含 paperType/evidence/dataReqs/gap/quality/references |
| `nong inspect refs paper-test.txt` | 5 条参考文献 + 2 条风险 |
| `nong chart anova anova-test.json` | F=301.27, P=0.000000, 3 组 |
| `nong chart duncan anova-test.json` | a/b/c 显著性分组 |
| `nong chart anova missing.json --json` | E001 错误 JSON |
| `dotnet build -c Release` | 0 错误 |
