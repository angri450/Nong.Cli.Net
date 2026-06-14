# progress-report skill 创建问题诊断

## 时间
2026-06-09

## 触发场景

用户要求创建 progress-report skill 后，skill-manager 指导创建了一个独立 .NET 工具项目 `tools/ProgressReport/`，用户质疑为什么不在 `nong` CLI 里实现。

## 问题清单

### 问题 1：独立 .NET 工具项目违反了 Nong"一个二进制"原则

**严重程度**: HIGH

**现象**: 在 `tools/ProgressReport/` 下创建了独立的 .NET 项目（含 csproj + Program.cs），通过 `dotnet run --project tools/ProgressReport` 调用。

**正确做法**: Nong.NET 的 README 明确写"93 个命令。一个二进制文件。跨平台运行"。所有功能应该作为 `nong` 的子命令进入 `Cli/Commands/`，例如 `nong progress report --json`。参考已有的 `Cli/Commands/WordCommands.cs`、`Cli/Commands/ChartCommands.cs` 模式。

**为什么错了**:
- skill-manager 的规则是"确定性工作走 .NET 工具"——这里理解错了。规则说的是不要在 prompt 里做确定性工作，应该用代码实现。但实现方式应该走现有 CLI 管线，而不是另建项目。
- 项目里已经有了 14 个命令模块在 `Cli/Commands/` 下，模式非常清晰，不应该创建一个孤立的 `tools/` 项目。

**影响**: 如果有用户安装 `nong` CLI，他们拿不到 report 功能。这个功能成了游离于主二进制之外的孤儿。

### 问题 2：nong 工具链状态误判

**严重程度**: MEDIUM

**现象**: 运行 `nong commands --json` 返回 exit code 154，报错 `nong.dll` 不存在。Agent 未排查错误根因，直接假设"没有 progress/report 命令"。

**实际根因**: `C:\Users\Administrator\.dotnet\tools\.store\angri450.nong.cli\3.2.4\...\nong.dll` 缺失。这是一个已安装但损坏的旧版本（3.2.4）。项目当前版本是 4.0.0，本地 DLL 应该在项目 build 输出目录，不应该依赖过期的全局 tool 安装。

**为什么错了**:
- 退出码 154 不等于"没有这个功能"，需要 `nong --help` 或直接看 `Cli/Commands/` 源码才能确认
- 应该先修复环境再判断，而不是基于一个错误输出做决策

**影响**: 误判导致走了独立项目这条错路。如果早发现 `nong` 有 14 个命令模块在 `Cli/Commands/` 下，就能按现有模式添加 `ProgressCommands.cs`。

### 问题 3：SKILL.md 调用链路不合理

**严重程度**: MEDIUM

**现象**: SKILL.md 写的调用方式是：
```powershell
dotnet run --project tools/ProgressReport -- --project-root .
```

**正确做法**: 应该写成：
```powershell
nong progress report --json
```

Agent 触发 skill 后，skill 应该指导 Agent 调用 `nong` 命令，而不是要求 Agent 编译并运行一个独立项目。

**影响**:
- Agent 每次用这个 skill 都要编译一次 `tools/ProgressReport`
- 与项目其他 skill（Word、Chart、Diagram 等都走 `nong word ...`、`nong chart ...`）模式不一致
- 用户手动使用时不方便，只能通过 Agent

## 需要修复

1. 撤回 `tools/ProgressReport/`，不推入主线
2. 在 `Cli/Commands/` 下新增 `ProgressCommands.cs`，实现 `nong progress report` 命令
3. 把 markdown 解析和 HTML 生成逻辑放到对应的库项目（可能是 `Cli/` 本身或新建一个小项目，然后 Cli 引用它）
4. 重新编译 `nong` CLI，让 `nong commands --json` 可见新命令
5. 更新 SKILL.md 调用方式为 `nong progress report`
6. 修复 nong 全局工具安装问题（当前 3.2.4 版本已损坏，需重装 4.0.0）

## 根本原因

Agent 受到 skill-manager 中"Deterministic work goes into .NET tools"这句规则的过度影响，忽略了 Nong.NET 项目自己的架构约束："一个二进制文件"。项目自己的 CLAUDE.md 明确写了这个原则，Agent 没有用它来纠正 skill-manager 的通用规则。
