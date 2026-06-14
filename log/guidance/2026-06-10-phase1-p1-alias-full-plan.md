# 2026-06-10 CLI P1 alias 施工详细方案

## 背景

2026-06-10 命令命名审计（`log/reports/toolkit-cli-command-naming-audit.html`）识别出 5 个 P1 级别命令，名字或描述会让 AI 和用户误解能力边界，但命令本身逻辑没问题。修复方式：补 alias，不删不改现有命令。

## 目标

在 Nong.Cli.Net 4.0.0 的 5 个 P1 命令上补更准确的 alias，同步更新 Manifest、Toolkit 教学口径和回归测试。

## 不改什么

- 不删任何现有命令（alias 只是新增入口，老命令继续兼容）
- 不改命令逻辑、参数、输出格式
- 不改包依赖关系
- 不改 SkillManagerCore

## 具体改动

### 1. `word preview` → 补 alias `word diagnose`

**当前问题**：`preview` 会被理解成视觉预览或页面截图，实际是 7 步结构诊断（内容、结构、格式、表格、图片、字体、样式）。

**改法**：在 `Cli/Commands/WordCommands.cs` 的 `preview` 命令定义中加 `[Alias("diagnose")]`（或者 System.CommandLine 的等效 alias 注册）。

**不改**：不把 preview 重命名为 diagnose，preview 保持兼容。

**Toolkit 教学**：word skill 优先教 `word diagnose`，提到 `word preview` 时说明是兼容入口。

### 2. `word rebuild` → 补 alias `word clean-styles`

**当前问题**：`rebuild` 听起来像重建全文，实际是清理 OOXML 样式污染（重复样式 ID、错序样式、脏样式引用）。

**改法**：在 `Cli/Commands/WordCommands.cs` 的 `rebuild` 命令定义中补 alias `clean-styles`。

**不改**：`rebuild` 保留。实际功能就是对文档内部样式做清理，无法重建任意文档。

**Toolkit 教学**：word skill 优先教 `word clean-styles`。

### 3. `inspect refs` → 补 alias `inspect references`

**当前问题**：`refs` 容易被理解成外部文献检索（去数据库查论文），实际是检查论文内部引用列表的风险（缺失、格式不一致、年份矛盾）。

**改法**：在 `Cli/Commands/InspectCommands.cs` 的 `refs` 命令定义中补 alias `references`。

**不改**：`refs` 保留。真正的目的不是检索文献，而是检查引用列表质量。

**Toolkit 教学**：inspect skill 区分 `inspect references`（内部引用检查）和 `lit search`（外部文献检索）。

### 4. `inspect varplan` → 补 alias `inspect variables`

**当前问题**：`varplan` 是内部缩写（variable plan），用户不一定知道是变量操作化与数据采集计划。

**改法**：在 `Cli/Commands/InspectCommands.cs` 的 `varplan` 命令定义中补 alias `variables`。

**不改**：`varplan` 保留。这个命令输出的是变量操作化定义、测量方法和数据采集计划。

**Toolkit 教学**：inspect skill 教 `inspect variables`。

### 5. `inspect data-req` → 补 alias `inspect data-requirements`

**当前问题**：`data-req` 缩写可猜但不适合教学和 discoverability。

**改法**：在 `Cli/Commands/InspectCommands.cs` 的 `data-req` 命令定义中补 alias `data-requirements`。

**不改**：`data-req` 保留。这个命令输出的是每个变量的数据需求和采集条件。

**Toolkit 教学**：inspect skill 教 `inspect data-requirements`。

## 改动文件清单

| 文件 | 改动 |
|------|------|
| `Cli/Commands/WordCommands.cs` | word preview 加 diagnose alias，word rebuild 加 clean-styles alias |
| `Cli/Commands/InspectCommands.cs` | inspect refs 加 references alias，varplan 加 variables alias，data-req 加 data-requirements alias |
| `Cli/Common/Manifest.cs` | 把 alias 注册到 CommandInfo 的 Aliases 字段 |
| `Cli.Tests/` | 新增 alias 回归测试：验证 alias 命令行为与原命令完全一致 |
| `log/changelog/2026-06-10-p1-alias.md` | 变更记录 |

## 验证步骤

```powershell
# 1. 编译
dotnet build Cli/NongCli.csproj -c Release

# 2. 确认 alias 出现在命令面
.\Cli\bin\Release\net8.0\nong.exe commands --json | findstr "diagnose"
.\Cli\bin\Release\net8.0\nong.exe commands --json | findstr "clean-styles"
.\Cli\bin\Release\net8.0\nong.exe commands --json | findstr "references"
.\Cli\bin\Release\net8.0\nong.exe commands --json | findstr "variables"
.\Cli\bin\Release\net8.0\nong.exe commands --json | findstr "data-requirements"

# 3. 跑 alias 冒烟（用本地 build）
.\Cli\bin\Release\net8.0\nong.exe word diagnose paper.docx --json
.\Cli\bin\Release\net8.0\nong.exe word clean-styles paper.docx --json

# 4. 全量测试
dotnet test -c Release

# 5. 确认 alias 行为与原命令一致（exit code、输出格式完全相同）
```

## 风险

- 极低。alias 是 System.CommandLine 原生能力，不改变命令解析逻辑。
- Manifest 更新后 `nong commands --json` 会多出 alias 条目，Toolkit 需要知晓但不需立即动作。
- 如果有人硬编码了命令名字符串匹配（比如 Toolkit skill 里写了 `nong word preview`），改为 `nong word diagnose` 后原 `preview` 仍然可用，不构成阻断。

## 和 ToolKit 的同步

Toolkit 改动在 Nong.Toolkit.Net 仓库的 `log/guidance/2026-06-10-phase2-c-class-skill-rewrite.md` 中规划。要点：
- word SKILL.md：教学口径优先用 `word diagnose` 和 `word clean-styles`
- inspect SKILL.md：教学口径优先用 `inspect references`、`inspect variables`、`inspect data-requirements`
- 共享 preflight 不变（仍是 `nong commands --json` 检测 CLI 存在）

## 时间估算

1-2 小时。Alias 注册 20 分钟 + Manifest 更新 10 分钟 + 测试 30 分钟 + Toolkit 教学同步 30 分钟。

## 状态

plan — **DONE**。2026-06-10 已完工。5 个 alias 全部实现，Manifest 已同步，6 个回归测试通过，125 全量测试 0 失败。
