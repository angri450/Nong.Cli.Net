# 2026-06-03 阶段 12 指导：废弃 skill-manager global tool，迁入 nong skill

## 背景

当前 `SkillManager/SkillManager.Cli.csproj` 源码目标框架是 `net8.0`，但用户本机全局安装的旧版：

```text
angri450.nong.skill.manager  1.0.3  skill-manager
```

运行时依赖 `.NET 11 preview`，在大多数推广环境中不可用。推广环境应以 `.NET 8+` 为基线，不应要求用户安装 .NET 11 preview。

## 决策

直接废弃独立 `skill-manager` global tool 路线。

从本阶段开始，skill 生命周期能力迁入 `nong` 主 CLI：

```text
nong skill validate <dir> --json
nong skill scan <dir> --json
nong skill inventory <dir> --json
nong skill package <dir> --json
```

旧命令：

```text
skill-manager validate .
skill-manager scan .
skill-manager package .
```

不再作为推广路径，不再写入 GroundPA-Toolkit 2.0.0 的安装说明。

## 原则

1. **目标框架固定 net8.0。**
   不引入 net10/net11 preview 依赖，不写 `global.json` 锁 SDK。

2. **统一 CLI 契约。**
   所有 `nong skill ... --json` 输出必须复用现有 `JsonOutput` schema：

   ```json
   {
     "status": "ok",
     "command": "skill validate",
     "summary": "...",
     "data": {},
     "issues": [],
     "artifacts": {},
     "metrics": {},
     "errors": [],
     "meta": { "durationMs": 0, "version": "3.1.0" }
   }
   ```

3. **不要从零重写核心逻辑。**
   复用现有 `SkillManager/Tools` 与 `SkillManager/Models`：

   ```text
   SkillValidator.cs
   SecurityScanner.cs
   Packager.cs
   InventoryRunner.cs
   Scaffolder.cs
   EvalRunner.cs
   EvalViewer.cs
   DescriptionOptimizer.cs
   LoopRunner.cs
   BlindAnonymizer.cs
   Models/*.cs
   ```

4. **先做 P0，不扩 eval 体系。**
   本阶段只迁移稳定、确定性、对 GroundPA-Toolkit 2.0.0 立即有用的命令。

5. **旧 global tool 只保留源码，不作为产品入口。**
   可以暂时保留 `SkillManager/` 项目，方便复用代码；但 README、skill、agent contract 不再推荐安装 `Angri450.Nong.Skill.Manager`。

## 推荐实现方案

### 方案 A：短期最小改动

在 `Cli/NongCli.csproj` 中直接引用 `SkillManager` 项目：

```xml
<ProjectReference Include="..\SkillManager\SkillManager.Cli.csproj" />
```

然后新增：

```text
Cli/Commands/SkillCommands.cs
```

将 `SkillManager.Cli.Tools` 中的类作为库调用。

优点：最快。

风险：`SkillManager.Cli.csproj` 是 `Exe` + `PackAsTool`，作为库引用不够干净。

### 方案 B：推荐收口方案

把 `SkillManager` 拆成核心库 + 可选旧壳：

```text
SkillManagerCore/
  SkillManagerCore.csproj       # net8.0 classlib
  Tools/
  Models/
  assets/viewer.html

SkillManager/
  SkillManager.Cli.csproj       # 可选旧壳，后续可删除
  Program.cs

Cli/
  Commands/SkillCommands.cs     # nong skill ...
```

依赖方向：

```text
Cli -> SkillManagerCore
SkillManager.Cli -> SkillManagerCore   # 仅兼容/本地调试用
```

优点：架构干净，后续不会让 `nong` 依赖一个工具壳。

建议 ClaudeCode 采用方案 B。如果时间不够，可以先用方案 A 跑通，再立刻改 B。

## P0 命令规范

### `nong skill validate <dir> --json`

用途：验证单个 skill 目录。

输入：含 `SKILL.md` 的目录。

成功：

```json
{
  "status": "ok",
  "command": "skill validate",
  "summary": "Skill valid: word",
  "data": {
    "valid": true,
    "skill": "word",
    "lines": 50,
    "errors": [],
    "warnings": []
  }
}
```

失败但工具正常运行：

```json
{
  "status": "error",
  "command": "skill validate",
  "errors": [
    {
      "code": "E006",
      "name": "validation_failed",
      "message": "SKILL.md not found in skill directory."
    }
  ]
}
```

### `nong skill scan <dir> --json`

用途：安全扫描 skill 或插件目录。

成功条件：

- 无 Critical/High：`status: ok`
- 有 Critical/High：`status: error`，退出码非 0
- Medium/Low 只进入 `issues`，不进入 `errors`

数据结构建议：

```json
{
  "data": {
    "critical": 0,
    "high": 0,
    "medium": 14,
    "low": 1,
    "findings": [
      {
        "severity": "Medium",
        "rule": "HOME_PATH_REFERENCE",
        "file": "word/references/workspace-setup.md",
        "line": 7,
        "detail": "Home directory reference detected"
      }
    ]
  }
}
```

### `nong skill inventory <dir> --json`

用途：列出 skill 目录内容，用于审计和发布前检查。

如果输入是插件根目录，应支持汇总所有含 `SKILL.md` 的子目录。

建议输出：

```json
{
  "data": {
    "root": "...",
    "skills": ["word", "inspect", "excel"],
    "skillCount": 17,
    "hasPluginManifest": true,
    "hasMarketplaceManifest": true
  }
}
```

### `nong skill package <dir> --json`

用途：打包单个 skill 或插件根目录。

流程：

1. validate
2. scan
3. 如果 scan 有 Critical/High，返回 error，不打包
4. 生成 zip
5. `artifacts.zip` 返回 zip 绝对路径

成功：

```json
{
  "status": "ok",
  "command": "skill package",
  "summary": "Package created.",
  "artifacts": {
    "zip": "C:/.../groundpa-toolkit-2.0.0.zip"
  }
}
```

## P1 暂缓

以下命令先不要做，避免再次跑太快：

```text
nong skill scaffold
nong skill eval
nong skill eval serve
nong skill optimize-description
nong skill run-loop
nong skill compare
```

这些等 P0 稳定、GroundPA-Toolkit 2.0.0 能打包发布后再迁移。

## 代码修改建议

### 1. 新建 SkillManagerCore

把以下目录从 `SkillManager/` 移到 `SkillManagerCore/`：

```text
Tools/
Models/
assets/
```

命名空间可暂时保留 `SkillManager.Cli.*`，降低迁移风险；后续再统一成 `Nong.SkillManager.*`。

`SkillManagerCore.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="assets\viewer.html" />
  </ItemGroup>
</Project>
```

### 2. Cli 引用 SkillManagerCore

`Cli/NongCli.csproj` 加：

```xml
<ProjectReference Include="..\SkillManagerCore\SkillManagerCore.csproj" />
```

### 3. 注册 SkillCommands

`Program.cs`：

```csharp
root.AddCommand(SkillCommands.Create(jsonOpt));
```

### 4. Manifest 增加 implemented 命令

`Cli/Common/Manifest.cs` 增加：

```text
skill validate
skill scan
skill inventory
skill package
```

状态为 implemented。

### 5. 更新 AGENT.md

加入：

```text
nong skill validate <dir> --json
nong skill scan <dir> --json
nong skill inventory <dir> --json
nong skill package <dir> --json
```

并明确：

```text
Do not use the old skill-manager global tool. Use nong skill instead.
```

## 验收清单

必须通过：

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
nong skill validate C:\Users\Administrator\Documents\Github\GroundPA-Toolkit\word --json
nong skill scan C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill inventory C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong commands --json
```

预期：

1. Release build 0 错误。
2. `skill validate` 对 `word` 返回 `status: ok`。
3. `skill scan` 对 GroundPA-Toolkit 当前状态返回 `status: ok`，因为无 High/Critical。
4. `skill inventory` 能识别 17 个 skill。
5. `nong commands --json` 包含 24 个 implemented 命令：
   - 原 20 个
   - 新增 4 个 `skill ...`

## 不要做

1. 不要要求用户安装 .NET 11 preview。
2. 不要继续推广 `dotnet tool install --global Angri450.Nong.Skill.Manager`。
3. 不要把旧 `skill-manager.exe` 的行为当成验收标准。
4. 不要在本阶段迁移 eval viewer / optimizer / loop runner。
5. 不要把 PPTX/OCR skill 重新加回 GroundPA-Toolkit，除非 `nong pptx` / `nong ocr` 已经真实 implemented。

## 给 ClaudeCode 的任务描述

目标：将 SkillManager P0 能力迁入 Nong CLI。

请按方案 B 实现：

1. 新建 `SkillManagerCore` net8.0 classlib。
2. 将 `SkillManager/Tools`、`SkillManager/Models`、`SkillManager/assets` 迁入 core。
3. `Cli` 引用 `SkillManagerCore`。
4. 新增 `Cli/Commands/SkillCommands.cs`，实现：
   - `nong skill validate <dir> --json`
   - `nong skill scan <dir> --json`
   - `nong skill inventory <dir> --json`
   - `nong skill package <dir> --json`
5. 更新 Manifest 和 AGENT.md。
6. Release build。
7. 用 GroundPA-Toolkit 当前目录跑 validate/scan/inventory 验收。

只做 P0，不做 eval/scaffold/serve/optimizer。
