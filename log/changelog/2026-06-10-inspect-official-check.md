# 2026-06-10 Inspect official-check

## What changed

新增 `inspect official-check` 命令。对已有公文 DOCX 做 8 项格式合规审核。

| 检查项 | 检测规则 |
|--------|---------|
| 红头标题 | 红色居中文字 |
| 发文字号 | 居中、含〔〕、字号<18pt |
| 公文标题 | 粗体居中、字号>=16pt |
| 主送机关 | 以：结尾的短文字 |
| 正文 | 标题后有实质内容 |
| 结束语 | 特此/此致/以上开头 |
| 落款 | 靠右侧的机关署名 |
| 成文日期 | XXXX年X月X日格式 |

## Files touched

- `Cli/Commands/InspectCommands.cs` — CreateOfficialCheck + OfficialCheckResult 模型
- `Cli/Common/Manifest.cs` — inspect official-check
- `Cli.Tests/CliContractTests.cs` — 2 个测试

## Verification

```text
nong commands --json → 103 commands (was 102)
dotnet test → pending
```
