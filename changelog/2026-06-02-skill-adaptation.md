# C 线：清理收尾

## Skill 层适配（待 groundpa-toolkit 仓库执行）

word skill 需要以下适配（groundpa-toolkit 仓库，不在 Nong.NET 仓库）：

1. SKILL.md dispatch 优先 .NET CLI（`nong word read`），不再降级 PowerShell
2. 新增 genre skill：参考 `changelog/2026-06-02-cli-inspect-spec.md` 中 inspect 命令
3. dissect-docx.ps1 增加 Program.cs subcommand 路由检测

**状态：已记录，待 skill agent 处理。**
