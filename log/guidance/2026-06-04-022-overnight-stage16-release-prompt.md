# 2026-06-04 通宵执行提示词：Stage 16a OCR 基础设施 + 发布闸门

本文件给 ClaudeCode 主对话使用。用户要睡觉，允许多 agent 并行，但必须守住发布闸门。不要把“跑得快”理解成“跳过验证”。

## 0. 总目标

今晚目标：

```text
1. 等 Stage 15 Word NongMark / 一刀三流收尾。
2. 并行推进 Stage 16a OCR 基础设施小修。
3. 两阶段都通过后，准备并执行 GitHub / Gitee / NuGet 发布。
4. 写完整结果日志，方便明早 Codex 审计和 GroundPA skill 全面更新。
```

必读：

```text
log/guidance/2026-06-03-018-stage15-word-yidao-sanliu-blueprint.md
log/guidance/2026-06-04-020-stage15-kickoff-execution.md
log/guidance/2026-06-04-019-stage16-ocr-infrastructure-blueprint.md
changelog/2026-06-04-021-stage16-ocr-feasibility-report.md
AGENT.md
PROJECT_OVERVIEW.md
```

## 1. 并发边界

Stage 15 仍在收尾时，Stage 16 agent 可以工作，但不能覆盖 Stage 15 共享文件。

Stage 16 agent 允许先改：

```text
MultiModal/
Cli/Commands/OcrCommands.cs
Cli.Tests/OcrCommandTests.cs
```

共享文件必须最后由 Release Coordinator 统一改：

```text
Cli/Common/Manifest.cs
Cli/AGENT.md
CAPABILITY.md
release-checklist.md
README.md
README.zh-CN.md
Cli/NongCli.csproj
```

禁止：

```text
不要修改 Nong.Toolkit.Net。
不要改 skill。
不要把 tests-output/、临时 png/xlsx/docx/json 加入提交。
不要新增 net10/net11。
不要引入 OnnxRuntime.Gpu 或 DirectML。
不要实现完整 PP-OCRv5 det/rec 推理。
不要自动下载模型。
不要注册未实现命令为 ok。
不要在日志、JSON、异常中打印任何 token。
```

## 2. Stage 16a 实施范围

只做 5 项：

```text
1. PaddleOCR token 迁移：PADDLEOCR_ACCESS_TOKEN 优先，PADDLEOCR_TOKEN 兼容并给 issue warning。
2. 新增 ocr analyze-image：暴露 MultiModal/ImageAnalyzer.cs。
3. 新增 ocr check-env：报告 imageAnalyzer / cloud token / local model / legacy python fallback 状态。
4. ocr local E005 文案更新：指向 PP-OCRv5 model package，不再推荐 pip install 作为主路线。
5. ocr cloud 输出小升级：返回结构化 pages/blocks/artifacts，不再只统计 Markdown 行；不做完整 nongmark 三流。
```

明确不做：

```text
--to-word 不属于 16a；不得在当前小修实现或注册。
ocr models / install-model 可以保留设计，不实现则不要注册；若注册必须 E009 + EXIT:1。
PP-OCRv5 ONNX det/rec 推理不做。
模型资源包不发布。
```

## 3. 多 agent 分工

### Agent 16A：Token + Cloud Error Contract

修改：

```text
MultiModal/PaddleOcrVlClient.cs
Cli/Commands/OcrCommands.cs
```

要求：

```text
PADDLEOCR_ACCESS_TOKEN 优先。
PADDLEOCR_TOKEN 兼容。
只发现旧变量时 JSON issues 加 warning。
缺 token -> E005 + EXIT:1。
401/403 -> E005 auth_failed，不含 token。
429 -> E005 + issue rate_limited。
503/504 -> E005 + issue service_unavailable。
400 -> E006。
其他未预期异常才 E004。
```

### Agent 16B：ImageAnalyzer CLI

修改：

```text
Cli/Commands/OcrCommands.cs
Cli.Tests/OcrCommandTests.cs
```

实现：

```powershell
nong ocr analyze-image <image> -o <out-dir> --json
```

输出：

```text
<out-dir>/image-analysis.json
<out-dir>/image.map.txt
```

JSON 必须包含：

```text
width/height
whitespaceRatio
contentBox
regions
artifacts.analysisJson
artifacts.asciiMap
issues: large_whitespace / mostly_blank 等可选
```

定位：

```text
ImageAnalyzer 是通用视觉验收工具，不是 OCR 附属。
后续 chart/diagram/pptx/word assets 都会用它。
```

### Agent 16C：check-env + local E005

实现：

```powershell
nong ocr check-env --json
```

要求：

```text
EXIT:0。
只报告状态，不因为 token/model 缺失失败。
data.imageAnalyzer = ok/error
data.cloudToken = set/missing/deprecated
data.localModel.ppOcrV5Mobile = present/missing/checksumMismatch/unknown
data.pythonFallback = available/unavailable
```

更新：

```text
ocr local 缺模型/未实现 -> E005。
message 指向 PP-OCRv5 model package / future install-model，不再主推 pip install。
```

### Agent 16D：Tests + Docs Sync

新增/更新：

```text
Cli.Tests/OcrCommandTests.cs
CAPABILITY.md
Cli/AGENT.md
release-checklist.md
```

测试：

```text
ocr check-env --json -> EXIT:0 + schema
ocr analyze-image missing.png --json -> E001 + EXIT:1
ocr analyze-image valid.png -o out --json -> artifacts exist
ocr local valid.png --json without model -> E005 + EXIT:1
ocr cloud valid.png --json without token -> E005 + EXIT:1
token value never appears in stdout/stderr
```

### Release Coordinator

职责：

```text
1. 等 Stage 15 Coordinator 完成。
2. 合并 Stage 16a agent 产物。
3. 统一修改 Manifest/CAPABILITY/AGENT/release-checklist/README。
4. 统一跑 build/test/behavior checks。
5. 更新版本号。
6. 打 tag、推 GitHub/Gitee、发布 NuGet。
7. 写最终结果日志。
```

## 4. Stage 15 必须先通过

发布前必须看到 Stage 15 结果日志：

```text
changelog/2026-06-04-020-stage15-word-yidao-sanliu-result.md
```

并验证：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15-slice --json
Test-Path tests-output\stage15-slice\document.json
Test-Path tests-output\stage15-slice\manifest.json
Test-Path tests-output\stage15-slice\content.jsonl
```

如果 Stage 15 未完成，不允许发布。可以完成 Stage 16a 代码，但停在“未发布”。

## 5. 版本与发布策略

建议版本：

```text
Angri450.Nong.Cli: 3.2.0
相关包如果项目统一版本，也升到 3.2.0。
Nong.Toolkit.Net 不在今晚发布；明早由 skill 管理 agent 更新。
```

发布前检查：

```powershell
git status --short
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

行为抽测：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15-slice --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr check-env --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr analyze-image <valid-image> -o tests-output\ocr-image --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr cloud <valid-image> -o tests-output\ocr-cloud --json
```

无 token 时 `ocr cloud` 预期：

```text
E005 + EXIT:1
不得打印 token。
```

## 6. Git / Gitee / NuGet 闸门

只有全部通过才允许：

```text
build PASS
tests PASS
behavior checks PASS
CAPABILITY/AGENT/release-checklist 同步
无 tests-output 临时文件入提交
无 token 泄露
package 成功
```

发布顺序：

```powershell
git status --short
git add <真实源码/文档/测试/日志文件>
git commit -m "Release Nong CLI 3.2.0"
git tag v3.2.0
git push origin main --tags
git push gitee main --tags
dotnet pack .\Cli\NongCli.csproj -c Release -o .\nupkg
dotnet nuget push .\nupkg\Angri450.Nong.Cli.3.2.0.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

如果任一远端或 NuGet 推送失败：

```text
不要反复盲重试。
记录错误。
能推一个远端就记录哪个成功哪个失败。
不要泄露 NUGET_API_KEY。
```

## 7. 结果日志

必须写：

```text
changelog/2026-06-04-022-overnight-stage16-release-result.md
```

包含：

```text
Goal:
Stage15 result:
Stage16a result:
Files changed:
Commands implemented:
Command count before/after:
Build/test result:
Behavior checks:
Security/token scan:
Package result:
GitHub push result:
Gitee push result:
NuGet push result:
Known limitations:
Tomorrow GroundPA update notes:
Open risks:
```

## 8. 明早给 Codex 的摘要

结果日志最后附：

```text
For Codex tomorrow:
- Current version/tag
- Latest commit hash
- Command count
- Implemented new commands
- Commands that return E005/E009
- Files to audit first
- GroundPA skills that must be updated
```
