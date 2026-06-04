# 2026-06-03 阶段 10 指导：稳定、打包、发布

## 目标

阶段 10 才开始认真做工程稳定和发布。

前面阶段可以快，阶段 10 必须慢。

## P0：测试

至少补这些 CLI 级测试：

```text
word read
word preview
inspect diagnose
inspect refs
chart analyze
chart bar
excel to-groups
missing file error
unsupported format error
commands --json
```

测试重点不是覆盖率，而是命令契约不漂移。

## P0：打包

先做 framework-dependent dotnet tool：

```powershell
dotnet pack Cli/NongCli.csproj -c Release
dotnet tool install --global --add-source Cli/bin/Release Angri450.Nong.Cli
nong --version
```

暂不做多平台自包含。

## P1：体积控制

建议配置：

```xml
<DebugType>none</DebugType>
<DebugSymbols>false</DebugSymbols>
```

不要把 PDB、测试文件、样例大文件打进 tool 包。

## P1：版本策略

建议：

- 阶段 1-10 内部版本：`3.1.x`
- 首个可用 CLI：`3.2.0`
- skill 同步后：`3.3.0`

## P2：许可证

之前可以暂缓，但发布前必须回头处理：

- ThirdParty 来源清单
- NOTICE
- license compatibility
- README 声明

## 建议 ClaudeCode 任务

```text
目标：阶段 10 稳定、打包、发布准备。

要求：
1. 新增 CLI 契约测试，覆盖已实现命令。
2. dotnet build Cli/NongCli.csproj -c Release 通过。
3. dotnet pack Cli/NongCli.csproj -c Release 通过。
4. 本地 dotnet tool install 测试通过。
5. 检查 nupkg 内容，不打入无关样例、PDB、大型临时文件。
6. 生成 release-checklist.md。
7. 不处理复杂许可证合规，只列出待处理清单。
```

## 验收标准

阶段 10 完成后，`nong` 应该可以作为本机稳定工具使用：

```powershell
nong word read a.docx
nong inspect diagnose paper.txt --json
nong excel to-groups data.xlsx --sheet Sheet1 --group A --value B --json
nong chart bar groups.json -o fig.png --json
```

如果这些命令在新终端里通过全局 `nong` 可调用，阶段 10 就合格。
