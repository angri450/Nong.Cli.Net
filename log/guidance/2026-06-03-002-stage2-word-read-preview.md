# 2026-06-03 阶段 2 指导：word read / word preview

## 结论

阶段 1 可以通过，CLI 骨架方向正确：

- `Cli/NongCli.csproj` 已建立为 `dotnet tool` 工程。
- `nong commands --json` 可输出结构化命令清单。
- `Docx` 和 `Inspect` 已作为首批依赖接入。
- Release build 可通过。

但进入阶段 2 前，建议先做一个小整理，避免后续命令膨胀后失控。

## 阶段 1 发现的问题

1. `Program.cs` 已经承担了入口、全部命令注册、空实现 handler、alias 注册。阶段 2 一旦继续往里写业务逻辑，会很快变成难维护的大文件。
2. `Manifest.All()` 是手写清单，真实命令树也是手写注册，二者容易漂移。当前 `inspect refs resolve/generate` 在命令树里存在，但 manifest 没有列出。
3. JSON 输出属性目前是 PascalCase，例如 `Status`、`Command`、`DurationMs`。如果目标是给模型省 token，建议统一成 camel/snake case，至少要固定下来，不要阶段 2 后再改。
4. `--version` 当前实际输出来自 System.CommandLine 默认版本机制，结果包含提交 hash：`3.1.0+...`。这不一定是坏事，但和 changelog 中的 `nong v3.1.0` 不一致。
5. `NongCli.csproj` 使用 `<DebugType>embedded</DebugType>`，如果后续关心工具包体积，可以改成 `none` 并关闭符号。

## 阶段 2 原则

阶段 2 只做两个真实命令：

- `nong word read <file>`
- `nong word preview <file>`

不要在阶段 2 同时实现 `extract/dissect/rebuild/fill/stats/fonts/styles/validate/merge`。现在最重要的是证明 CLI 的输入、错误、JSON、stdout 规则稳定。

## 推荐实现边界

### word preview

直接调用已有 API：

```csharp
DocxCore.WordPreview.Preview(file)
```

CLI 只负责：

- 接收 `<file>` 参数。
- 检查文件存在和扩展名。
- 把 `PreviewResult` 映射为 `JsonOutput`。
- 非 JSON 时输出简洁诊断报告。

JSON 的 `data` 建议包含：

```json
{
  "text": "...",
  "warnings": [],
  "errors": [],
  "info": [],
  "statistics": {}
}
```

`metrics` 建议放高频数字：

```json
{
  "paragraphs": 12,
  "tables": 1,
  "images": 0,
  "ooxml_errors": 0,
  "ooxml_warnings": 0
}
```

### word read

不要直接复用 `WordPreview.Preview().Text` 作为最终实现，因为 `WordPreview` 目前会截断到 3000 字，且更偏诊断。

建议在 `Docx` 层新增一个很薄的稳定 API：

```csharp
namespace DocxCore;

public static class WordTextReader
{
    public static WordTextResult Read(string docxPath);
}

public sealed record WordTextResult(
    string Text,
    List<string> Paragraphs,
    List<string> Tables,
    List<string> Footnotes,
    List<string> Endnotes
);
```

实现可以先覆盖正文段落和表格文本，脚注/尾注可留空但字段必须保留。不要把复杂诊断塞进 `word read`。

JSON 的 `data` 建议包含：

```json
{
  "text": "...",
  "paragraphs": [],
  "tables": [],
  "footnotes": [],
  "endnotes": []
}
```

`metrics` 建议包含：

```json
{
  "characters": 1234,
  "paragraphs": 12,
  "tables": 1,
  "footnotes": 0,
  "endnotes": 0
}
```

非 JSON 输出只打印纯文本，方便 PowerShell 管道使用：

```powershell
nong word read paper.docx > paper.txt
```

## 建议 ClaudeCode 任务

把下面内容交给 ClaudeCode：

```text
你在 C:\Users\Administrator\Documents\Github\Nong.Cli.Net 工作。

目标：实现阶段 2，只做 nong word read 和 nong word preview。

要求：
1. 先把 CLI 命令注册从 Program.cs 拆到 Cli/Commands/WordCommands.cs，保留 Program.cs 作为入口和总注册。
2. 新建或调整公共 helper，使真实命令可以复用 JsonOutput、ErrorCodes、文件检查、耗时统计。
3. 实现 nong word preview <file>：
   - 调用 DocxCore.WordPreview.Preview(file)。
   - --json 输出稳定结构。
   - 非 --json 输出简洁报告。
   - 文件不存在返回 error JSON，错误码 E001，退出码非 0。
4. 实现 nong word read <file>：
   - 不要使用会截断的 WordPreview.Text 作为最终结果。
   - 如果 Docx 层没有完整读取 API，则在 Docx 中新增 WordTextReader。
   - 至少读取正文段落和表格文本。
   - --json 输出 text/paragraphs/tables/footnotes/endnotes。
   - 非 --json 只输出纯文本，方便重定向。
5. 保持其它命令仍然是 not implemented，不要顺手实现其它命令。
6. 修正 manifest 与真实命令树明显不一致的问题，至少把 inspect refs resolve/generate 是否列入 manifest 固定下来。
7. 保持 dotnet build Cli/NongCli.csproj -c Release 通过。

验收命令：
dotnet build Cli/NongCli.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word read <一个真实docx>
dotnet .\Cli\bin\Release\net8.0\nong.dll word read <一个真实docx> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word preview <一个真实docx>
dotnet .\Cli\bin\Release\net8.0\nong.dll word preview <一个真实docx> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word read missing.docx --json
```

## 我方验收标准

阶段 2 完成后，只看三件事：

1. CLI 是否真的能替代模型写 PowerShell 读取 docx。
2. JSON 是否足够稳定，适合 skill 层长期记忆。
3. 错误输出是否机器可读，模型不需要猜失败原因。

如果这三点过了，再进入 `paper diagnose` 或 `stats anova/duncan`，不要急着铺满所有 Office 命令。
