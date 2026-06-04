# 2026-06-04 阶段 15 开工指令：Word NongMark / 一刀三流

本文件给 ClaudeCode 使用。Gate report 已通过，可以开阶段 15。

## 0. 开工前确认

已复核：

```text
dotnet build Cli/NongCli.csproj -c Release --no-restore  PASS
dotnet test Cli.Tests/Cli.Tests.csproj -c Release --no-build  PASS, 24/24
ocr local test.png --json  E005 + EXIT:1
```

注意一个同步差异：

```text
当前 nong commands --json 实测为 47 implemented，不是 gate report/CAPABILITY 中的 46。
原因：ocr local 已计入 implemented，但运行时诚实返回 E005 dependency_missing。
这不是阶段 15 阻塞项，但阶段 15 收尾时必须同步 CAPABILITY.md / Cli/AGENT.md / release-checklist.md。
```

## 1. 本阶段只做 Word

读取并严格遵守：

```text
log/guidance/2026-06-03-018-stage15-word-yidao-sanliu-blueprint.md
changelog/2026-06-03-018-stage15-gate-report.md
```

不要做：

```text
不要修改 GroundPA-Toolkit。
不要做 OCR 阶段 16。
不要接 PP-OCRv5。
不要改 PaddleOCR token。
不要扩 chart/excel/pptx/inspect。
不要新增 net10/net11 依赖。
```

OCR 相关内容只作为 schema 预留：

```text
image.analysis / image.ocr 字段可以进入 nongmark/v1。
不能声称 PP-OCRv5 已实现。
不能修改 ocr cloud/local 行为。
```

## 2. 阶段 15 核心目标

目标不是简单补命令数，而是恢复 Word 招牌设计：

```text
一刀：nong word dissect <file.docx> -o <out-dir> --json
三流：content / structure / format-assets
核心标记层：nongmark/v1 JSON，不是 Markdown
```

必须产出：

```text
<out-dir>/
  manifest.json
  document.json
  content.md
  content.jsonl
  structure.json
  format.json
  assets/
    manifest.json
```

`document.json` 必须是 canonical source：

```json
{
  "schemaVersion": "nongmark/v1",
  "blocks": []
}
```

`content.md` 只能是 preview，不是 canonical source。

## 3. 推荐实施顺序

### Phase A：NongMark 模型和 word dissect -o

先实现最小可用的一刀三流，不要先补全部 add 命令。

新增建议：

```text
Docx/NongMarkModels.cs
Docx/WordSlice.cs
```

要求：

```text
1. 提取 paragraph / heading / run / table / image / equation。
2. run-level format 必须保留。
3. math/chemEquation/chemicalStructure 必须有结构字段；识别不了就写 null/source/confidence，不要瞎猜。
4. assets manifest 必须存在；图片可调用 ImageHeaderReader 取尺寸。
5. 如果安全可行，可调用 MultiModal/ImageAnalyzer；如果引入循环依赖或项目引用不合适，先在 Known limitations 写明，不能硬改架构。
6. word dissect <file> --json 原摘要模式必须保持兼容。
7. word dissect <file> -o <dir> --json 才进入完整输出目录模式。
```

验收：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15-slice --json
Test-Path tests-output\stage15-slice\manifest.json
Test-Path tests-output\stage15-slice\document.json
Test-Path tests-output\stage15-slice\content.md
Test-Path tests-output\stage15-slice\content.jsonl
Test-Path tests-output\stage15-slice\structure.json
Test-Path tests-output\stage15-slice\format.json
Test-Path tests-output\stage15-slice\assets\manifest.json
```

### Phase B：补回读/查命令

实现：

```text
word outline
word images
word comments
word revisions
word infer-format
```

要求：

```text
输出必须引用 blockId/assetId。
空 comments/revisions 是 ok + 空数组。
infer-format 空输入/无法解析返回 E006。
```

### Phase C：改命令和 merge 升级

实现：

```text
word fix-order
word protect
word embed-font
升级 word merge
```

要求：

```text
word merge 不能只停留在 shallow merge。
优先复用 AdvancedFeatures.AppendDocument。
即便升级后仍有限制，也必须在 issues/Known limitations 说明页眉页脚、编号冲突、样式同名等边界。
所有生成命令 input/output 同路径必须 E006。
所有生成命令必须 CheckArtifact。
```

### Phase D：写命令 add group

实现：

```text
word add paragraph
word add table
word add footnote
word add endnote
word add image
word add toc
word add xref
word add link
word add bookmark
word add comment
word add math
```

要求：

```text
1. 默认不原地覆盖，必须 -o。
2. --after 如果不能精确实现，返回 E006，不得忽略。
3. malformed spec 返回 E006。
4. JSON command 字段必须逐项正确。
5. add math 复用 MathRenderer。
```

## 4. 多 agent 分工

可以并行，但避免多人同时改同一文件。

```text
Agent A: NongMark schema + WordSlice 核心
Agent B: 读/查命令 helper
Agent C: 改命令 + merge
Agent D: add group
Agent E: tests + docs + Manifest/CAPABILITY/AGENT sync
Coordinator: 最后统一接入 WordCommands.cs / Manifest.cs / 文档
```

共享文件由 Coordinator 最后改：

```text
Cli/Commands/WordCommands.cs
Cli/Common/Manifest.cs
CAPABILITY.md
Cli/AGENT.md
release-checklist.md
```

## 5. 必须加测试

新增或更新：

```text
Cli.Tests/WordCommandTests.cs
```

最低覆盖：

```text
word dissect -o 输出 7 个核心路径
document.json schemaVersion = nongmark/v1
content.md 只是 preview
word outline JSON shape
word images 无图/有图稳定返回
word comments/revisions 空数组 ok
word add paragraph/table/math 生成 docx 后 word read 能读到新增文本
malformed JSON spec -> E006 + EXIT:1
missing input -> E001 + EXIT:1
output == input -> E006 + EXIT:1
JSON command 字段正确
```

## 6. 收尾验收

必须跑：

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

至少抽测：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15-slice --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word outline tests-output\05-docx\docx-basic.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word fix-order tests-output\05-docx\docx-basic.docx -o tests-output\stage15-fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\stage15-math.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word validate tests-output\stage15-fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word read tests-output\stage15-math.docx --json
```

## 7. 结果日志

完成后写：

```text
changelog/2026-06-04-020-stage15-word-yidao-sanliu-result.md
```

必须包含：

```text
Goal:
Gate report reference:
Implemented commands:
Word command count before/after:
NongMark schema summary:
One-cut three-stream output files:
Files changed:
Tests added:
Commands run:
Build/test result:
Known limitations:
Open risks:
Next recommended stage:
```

Known limitations 不许空。至少写：

```text
OMML -> LaTeX 逆转换边界
chemEquation / chemicalStructure 只是 schema 预留还是已有识别
ImageAnalyzer 是否已接入，若未接入原因是什么
merge 对复杂域、页眉页脚、编号、样式冲突的边界
--after blockId 是否完整实现
```

## 8. 审计者会重点查

```text
1. document.json 是否真实存在且 schemaVersion = nongmark/v1。
2. 是否保留 run-level format。
3. 是否把 content.md 误当 canonical source。
4. math/chemEquation/chemicalStructure 是否结构化。
5. word merge 是否真的升级，不只是改注释。
6. 所有生成命令是否 CheckArtifact。
7. malformed spec 是否 E006。
8. Manifest/CAPABILITY/AGENT/release-checklist 是否同步。
9. commands --json 数量是否和文档一致。
10. 是否误动 OCR/GroundPA/阶段16内容。
```
