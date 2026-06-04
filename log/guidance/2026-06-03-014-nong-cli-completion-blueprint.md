# 2026-06-03 Nong CLI 后续完善总蓝图

## 目标

把当前 `nong` CLI 从“20 个 implemented + 一批 stub”推进到可长期自用、可推广、可由 skill 层稳定调用的 2.0 工作基座。

当前方向已经确定：

1. `nong` 是统一入口。
2. skill 层只调用 `nong` 已实现命令。
3. 所有未实现命令必须返回 `E009 not_implemented` + `EXIT:1`，不得伪装成功。
4. 所有 agent-facing 命令优先支持 `--json`。
5. 所有生成命令必须创建父目录，生成后检查 artifact。
6. 目标运行环境是 `.NET 8+`，不要依赖 `.NET 11 preview`。

本文件是给 ClaudeCode 多 agent 执行的开发蓝图。不要在主对话里反复讨论细节，按此拆任务并行推进。

---

## 当前基线

### 已实现命令

```text
word read
word preview
word fill
word rebuild

inspect diagnose
inspect refs
inspect write-paper

chart analyze
chart anova
chart duncan
chart bar

excel sheets
excel read
excel to-groups

diagram flowchart
diagram network

genre list
genre show

icons list
icons search
```

### 计划但未实现命令

```text
word extract
word dissect
word stats
word fonts
word styles
word validate
word merge

inspect classify
inspect structure
inspect varplan
inspect evidence
inspect data-req
inspect gap
inspect semantics

chart line
chart scatter
chart pie

diagram tree

pptx read
pptx slides

ocr local
ocr cloud

excel create
```

### 已知约束

1. `pptx read` 当前暂桩，PptxCore 需适配 ShapeCrawler 合并后的实际 API。
2. Duncan 当前使用简化 Q 值近似，正式论文需人工复核。CLI 需要在输出中明确标注 method note。
3. 生成命令输出目录不存在时必须自动创建。
4. `word rebuild` 输入输出不能同一路径。
5. 所有 stub 返回 `E009 + EXIT:1`。
6. `skill-manager` global tool 路线废弃，迁入 `nong skill ...`。

---

## 总体架构原则

### 1. CLI contract 第一

每个命令必须有一致 JSON 结构：

```json
{
  "status": "ok",
  "command": "word stats",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": {},
  "metrics": {},
  "errors": [],
  "meta": {
    "durationMs": 0,
    "version": "3.1.0"
  }
}
```

错误：

```json
{
  "status": "error",
  "command": "word stats",
  "errors": [
    {
      "code": "E006",
      "name": "validation_failed",
      "message": "..."
    }
  ]
}
```

### 2. 错误码约定

```text
E001 file_not_found
E002 unsupported_format
E003 missing_argument
E004 internal_error
E005 dependency_missing
E006 validation_failed
E007 read_failed
E008 write_failed
E009 not_implemented
```

新增错误码必须同时更新：

```text
Cli/Common/ErrorCodes.cs
Cli/AGENT.md
README.md
GroundPA-Toolkit 对应 skill
```

### 3. 不允许 silent fallback

如果底层库能力缺失，返回清晰错误，不要悄悄输出半成品。

### 4. 不允许扩了代码忘了 Manifest

每实现一个命令必须同步：

```text
Cli/Common/Manifest.cs
Cli/AGENT.md
Cli/README.md
GroundPA-Toolkit 对应 SKILL.md
changelog
```

### 5. 生成命令统一 artifact gate

所有生成命令必须：

1. `EnsureParentDir(output)`
2. 执行生成
3. `CheckArtifact(output, kind)`
4. JSON 输出 `artifacts`

适用：

```text
word fill
word rebuild
word merge
inspect write-paper
chart bar
chart line/scatter/pie
diagram flowchart/network/tree
pptx read? no artifact unless explicit output
ocr cloud/local
excel create
skill package
```

### 6. 正文数据不进 stderr

机器可读数据走 stdout JSON。stderr 只放人类调试信息。

---

## 多 agent 分工

建议 ClaudeCode 一次开 5 个子 agent，并行读代码和实现，但最后由主 agent 统一合并。

### Agent A：Word commands

范围：

```text
word extract
word dissect
word stats
word fonts
word styles
word validate
word merge
```

主文件：

```text
Cli/Commands/WordCommands.cs
Docx/
ThirdParty OpenXml
```

输出：

```text
WordCommands.cs patch
word command tests
AGENT.md word section update
```

### Agent B：Inspect commands

范围：

```text
inspect classify
inspect structure
inspect varplan
inspect evidence
inspect data-req
inspect gap
inspect semantics
```

主文件：

```text
Cli/Commands/InspectCommands.cs
Inspect/
```

输出：

```text
InspectCommands.cs patch
paper text sample tests
JSON schema examples
```

### Agent C：Chart + Excel

范围：

```text
chart line
chart scatter
chart pie
excel create
Duncan method note
```

主文件：

```text
Cli/Commands/ChartCommands.cs
Cli/Commands/ExcelCommands.cs
Chart/
Excel/
```

输出：

```text
chart command patch
excel create patch
sample groups JSON
chart artifact tests
```

### Agent D：Diagram + PPTX + OCR

范围：

```text
diagram tree
pptx read
pptx slides
ocr cloud
ocr local
```

主文件：

```text
Cli/Commands/DiagramCommands.cs
Cli/Commands/PptxCommands.cs
Cli/Commands/OcrCommands.cs
Diagram/
Pptx/
MultiModal/
```

输出：

```text
diagram tree patch
pptx read/slides patch
ocr design/implementation patch
dependency check behavior
```

### Agent E：Contract / manifest / tests / docs

范围：

```text
Manifest sync
AGENT.md
README.md
GroundPA-Toolkit skill sync
release checklist
CLI contract tests
```

输出：

```text
contract diff
commands --json validation
release checklist
changelog
```

---

## 开发阶段路线

## 阶段 12：迁移 SkillManager 到 nong skill

指导文件：

```text
log/guidance/2026-06-03-013-skill-manager-into-nong-cli.md
```

实现：

```text
nong skill validate
nong skill scan
nong skill inventory
nong skill package
```

目的：

1. 废弃旧 `skill-manager` global tool。
2. 解决 `.NET 11 preview` 依赖问题。
3. 让 GroundPA-Toolkit 2.0.0 可以由 `nong skill ...` 验证和打包。

验收：

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
nong skill validate C:\Users\Administrator\Documents\Github\GroundPA-Toolkit\word --json
nong skill scan C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill inventory C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong commands --json
```

完成后 implemented 命令数：

```text
20 + 4 = 24
```

---

## 阶段 13：Word P1 完整化

### `word stats`

用途：低 token 获取 docx 统计信息。

输入：

```text
nong word stats <file.docx> --json
```

输出 data：

```json
{
  "paragraphs": 29,
  "tables": 2,
  "images": 3,
  "characters": 12345,
  "footnotes": 1,
  "endnotes": 0,
  "sections": 1,
  "styles": 12,
  "fonts": 4
}
```

实现建议：

1. 优先复用 `WordTextReader.Read` 和 `WordPreview.Preview`。
2. 图片数量从 document relationships / drawing elements 统计。
3. 不要全文输出，只输出计数。

验收：

```powershell
nong word stats sample.docx --json
```

必须 `status: ok`，metrics 与 data 一致。

### `word fonts`

用途：列出文档实际使用字体。

输出：

```json
{
  "fonts": [
    {
      "name": "Times New Roman",
      "count": 42,
      "source": "runFonts.ascii"
    }
  ]
}
```

实现建议：

1. 扫描 `w:rFonts`。
2. 区分 ascii/eastAsia/hAnsi/cs。
3. 空字体不报错，返回空数组。

### `word styles`

用途：列出 style definitions 和正文引用情况。

输出：

```json
{
  "styles": [
    {
      "id": "Heading1",
      "name": "heading 1",
      "type": "paragraph",
      "used": true,
      "useCount": 3
    }
  ]
}
```

实现建议：

1. 读取 StyleDefinitionsPart。
2. 统计 `w:pStyle` / `w:rStyle` 引用。

### `word validate`

用途：OOXML 基础校验。

输出：

```json
{
  "valid": true,
  "errors": [],
  "warnings": []
}
```

实现建议：

1. 使用 OpenXmlValidator。
2. 最多返回前 100 条错误，避免 token 爆炸。
3. 有错误时 `status` 仍可为 `ok`，因为命令成功运行；把文档错误放 `issues`。只有读取失败才 `status:error`。

### `word extract`

用途：提取嵌入图片。

命令：

```text
nong word extract <file.docx> -o <outDir> --json
```

输出：

```json
{
  "count": 3,
  "images": [
    {
      "file": "image1.png",
      "contentType": "image/png",
      "bytes": 12345
    }
  ]
}
```

artifacts：

```json
{
  "dir": "C:/.../images"
}
```

实现建议：

1. 遍历 image parts。
2. 保留扩展名。
3. 输出目录自动创建。
4. 无图片时 `status: ok`，count=0。

### `word dissect`

用途：生成低 token 文档结构指纹。

命令：

```text
nong word dissect <file.docx> --json
```

输出：

```json
{
  "fingerprint": {
    "paragraphs": 29,
    "tables": 2,
    "styles": ["Normal", "Heading1"],
    "fonts": ["宋体", "Times New Roman"],
    "hasImages": true,
    "hasFootnotes": true
  },
  "warnings": []
}
```

注意：不要输出全文。

### `word merge`

用途：合并 docx。

命令：

```text
nong word merge <first.docx> <second.docx> -o <out.docx> --json
```

实现建议：

1. 第一版只支持顺序合并正文 body。
2. 样式冲突先不做复杂处理，issues 中提示。
3. 输入输出不能同路径。
4. 生成后 `CheckArtifact`。

阶段 13 验收：

```powershell
nong word stats sample.docx --json
nong word fonts sample.docx --json
nong word styles sample.docx --json
nong word validate sample.docx --json
nong word extract sample.docx -o out/images --json
nong word dissect sample.docx --json
nong word merge a.docx b.docx -o out/merged.docx --json
nong commands --json
```

完成后新增 implemented：

```text
7
```

总 implemented：

```text
24 + 7 = 31
```

---

## 阶段 14：Inspect P1 拆分命令

目标：把 `inspect diagnose` 的内部能力拆成更省 token 的单命令。

### `inspect classify`

```text
nong inspect classify <paper.txt> --json
```

输出：

```json
{
  "topType": "问卷调查型论文",
  "match": 80,
  "candidates": [
    {
      "type": "...",
      "match": 80,
      "recommendedData": "...",
      "recommendedMethods": "..."
    }
  ]
}
```

实现：`PaperTypeClassifier.Classify(text)`。

### `inspect structure`

```text
nong inspect structure <paper.txt> --json
```

输出：

```json
{
  "title": "...",
  "keywords": [],
  "sections": [
    {
      "level": 1,
      "title": "1 Introduction",
      "startLine": 12
    }
  ],
  "hasReferences": true,
  "hasAppendix": false
}
```

实现：`PaperStructureExtractor.BuildPaperStructure(text)`。

### `inspect evidence`

```text
nong inspect evidence <paper.txt> --json
```

输出 evidence chain only。

实现：

1. build structure
2. `PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections)`

### `inspect data-req`

```text
nong inspect data-req <paper.txt> --json
```

输出 data requirement only。

实现：`PaperDiagnostics.DiagnoseDataRequirements(text)`。

### `inspect gap`

```text
nong inspect gap <paper.txt> --json
```

实现：

1. classify
2. structure
3. evidence
4. data-req
5. `PaperDiagnostics.DiagnoseGapGrade(...)`

输出：

```json
{
  "level": "D",
  "description": "...",
  "canContinue": false
}
```

### `inspect semantics`

```text
nong inspect semantics <paper.txt> --json
```

如果底层没有独立语义诊断 API，第一版从 `DiagnosePaperQuality` 中筛选语义/表达/逻辑类问题。

不要编造不存在的 AI 语义能力。

### `inspect varplan`

```text
nong inspect varplan <paper.txt> --json
```

如果 `VariablePlanGenerator` 已存在，则调用。若 API 不稳定，保持 stub，不要伪实现。

阶段 14 验收：

```powershell
nong inspect classify paper.txt --json
nong inspect structure paper.txt --json
nong inspect evidence paper.txt --json
nong inspect data-req paper.txt --json
nong inspect gap paper.txt --json
nong inspect semantics paper.txt --json
nong inspect varplan paper.txt --json
nong commands --json
```

完成后新增 implemented：

```text
7
```

总 implemented：

```text
31 + 7 = 38
```

---

## 阶段 15：Chart + Excel P1

### Duncan 方法说明修正

所有包含 Duncan 的输出必须加：

```json
{
  "methodNote": "Duncan MRT uses simplified Q critical approximation; verify manually for formal publication."
}
```

适用：

```text
chart analyze
chart duncan
chart bar
```

### `chart line`

```text
nong chart line <spec.json> -o <out.png> --json
```

spec 建议：

```json
{
  "title": "Growth curve",
  "xLabel": "Day",
  "yLabel": "Height",
  "series": [
    {
      "name": "Control",
      "x": [0, 7, 14],
      "y": [1.2, 2.4, 3.8]
    }
  ]
}
```

第一版只支持数值 x/y。

### `chart scatter`

```text
nong chart scatter <spec.json> -o <out.png> --json
```

spec：

```json
{
  "title": "Correlation",
  "xLabel": "Soil N",
  "yLabel": "Yield",
  "points": [
    { "x": 1.2, "y": 3.4, "group": "A" }
  ],
  "trendline": true
}
```

第一版 trendline 可选；如果 ChartCore 没有，先忽略并在 issues 提示。

### `chart pie`

```text
nong chart pie <spec.json> -o <out.png> --json
```

spec：

```json
{
  "title": "Composition",
  "values": [
    { "label": "A", "value": 30 },
    { "label": "B", "value": 70 }
  ]
}
```

### `excel create`

```text
nong excel create <spec.json> -o <out.xlsx> --json
```

spec：

```json
{
  "sheets": [
    {
      "name": "Data",
      "rows": [
        ["Treatment", "Yield"],
        ["A", 1.2],
        ["B", 2.3]
      ]
    }
  ]
}
```

第一版只做简单 workbook + sheets + rows，不做样式、公式、条件格式。

阶段 15 验收：

```powershell
nong chart line line-spec.json -o out/line.png --json
nong chart scatter scatter-spec.json -o out/scatter.png --json
nong chart pie pie-spec.json -o out/pie.png --json
nong excel create workbook-spec.json -o out/data.xlsx --json
nong excel sheets out/data.xlsx --json
```

完成后新增 implemented：

```text
4
```

总 implemented：

```text
38 + 4 = 42
```

---

## 阶段 16：Diagram tree

### `diagram tree`

```text
nong diagram tree <tree.newick> -o <out.png> --json
```

输入：

```text
((A:0.1,B:0.2):0.3,C:0.4);
```

输出：

```json
{
  "leaves": 3,
  "format": "newick"
}
```

实现：

1. 如果 DiagramCore 已有 Newick parser/tree renderer，直接调用。
2. 如果没有，不要临时写复杂 parser；先保持 stub，并在 changelog 写清楚缺口。
3. 生成后 `CheckArtifact`.

阶段 16 验收：

```powershell
nong diagram tree sample.newick -o out/tree.png --json
```

完成后新增 implemented：

```text
1
```

总 implemented：

```text
42 + 1 = 43
```

---

## 阶段 17：PPTX 读取能力

目标：只实现读取，不做生成。

### 关键约束

`PptxCore` 需要适配 ShapeCrawler 合并后的实际 API。不要假设旧 API 仍可用。

### `pptx read`

```text
nong pptx read <file.pptx> --json
```

输出：

```json
{
  "slides": [
    {
      "index": 1,
      "title": "...",
      "texts": ["..."],
      "notes": []
    }
  ],
  "text": "..."
}
```

### `pptx slides`

```text
nong pptx slides <file.pptx> --json
```

输出结构清单：

```json
{
  "slideCount": 10,
  "slides": [
    {
      "index": 1,
      "shapeCount": 8,
      "textBoxCount": 3,
      "tableCount": 1,
      "imageCount": 2
    }
  ]
}
```

实现建议：

1. 先研究 `Pptx/` 当前 API。
2. 不做写入。
3. 读取失败返回 `E007 read_failed`。
4. 格式不支持返回 `E002`。

阶段 17 验收：

```powershell
nong pptx read sample.pptx --json
nong pptx slides sample.pptx --json
```

完成后新增 implemented：

```text
2
```

总 implemented：

```text
43 + 2 = 45
```

---

## 阶段 18：OCR cloud/local

目标：OCR 进入 .NET/nong 体系，不走 Python skill。

### `ocr cloud`

```text
nong ocr cloud <input> -o <outDir> --json
```

输入：

```text
image/pdf path
```

环境：

```text
PADDLEOCR_TOKEN
```

输出：

```json
{
  "pages": 3,
  "markdownFiles": ["..."],
  "text": "..."
}
```

artifacts：

```json
{
  "dir": "...",
  "markdown": "...",
  "docx": "..."
}
```

规则：

1. token 缺失返回 `E005 dependency_missing` 或 `E006 validation_failed`，不要进入内部错误。
2. 网络/API 失败返回 `E007 read_failed`，message 说明 API 错误。
3. 不要打印 token。
4. 不要把 API 原始大响应完整塞进 JSON。

### `ocr local`

```text
nong ocr local <image> --json
```

路线选择：

1. 如果已有 .NET local OCR 能力，直接接。
2. 如果仍依赖 Python PaddleOCR，不作为 implemented；保持 stub。
3. 不为了“去 stub”临时引入 Python 环境复杂依赖。

输出：

```json
{
  "blocks": [
    {
      "text": "...",
      "confidence": 0.98,
      "box": [0, 0, 100, 40]
    }
  ]
}
```

阶段 18 验收：

```powershell
nong ocr cloud sample.pdf -o out/ocr --json
nong ocr local sample.png --json
```

如果 local 仍无纯 .NET 实现，不要强行通过验收；保留 stub。

完成后预计新增 implemented：

```text
ocr cloud = 1
ocr local = 0 or 1
```

---

## 阶段 19：GroundPA-Toolkit skill 同步

每个 CLI 阶段完成后同步 skill 层。

位置：

```text
C:\Users\Administrator\Documents\Github\GroundPA-Toolkit
```

规则：

1. 只把 `nong commands --json` 中 implemented 的命令写入 skill。
2. stub 不进入 description，不进入 README 能力表。
3. 如果 PPTX/OCR 实现了，再恢复对应 `SKILL.md`。
4. 每次同步后跑：

```powershell
claude plugin validate .
nong skill validate <skill-dir> --json
nong skill scan . --json
```

### 当前 2.0.0 skill 暴露面

```text
word
inspect
excel
chart
diagram
genre
icons
```

后续新增：

```text
skill       # 阶段 12 完成后可新增或更新 skill-manager skill
pptx        # 阶段 17 完成后恢复
multimodal  # 阶段 18 cloud/local 至少一个真实实现后恢复
```

---

## 阶段 20：测试体系与发布门禁

### 建议新增 CLI contract tests

如果当前没有测试项目，新增：

```text
Cli.Tests/
```

覆盖：

1. 所有 implemented 命令在 `commands --json` 中存在。
2. 所有 stub 命令返回 `E009 + EXIT:1`。
3. JSON error 时进程退出码非 0。
4. 生成命令 artifact 存在且非 0 bytes。
5. malformed JSON spec 返回 `E006`，不是 `E004`。
6. command 字段必须等于实际命令名。

### 每阶段固定验收

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
nong commands --json
nong commands --all --json
```

如果有测试：

```powershell
dotnet test
```

### 发布前总验收

1. `nong commands --json` implemented 数量符合阶段目标。
2. `nong commands --all --json` stub 状态准确。
3. GroundPA-Toolkit plugin validate 通过。
4. GroundPA-Toolkit skill 清单不包含 stub。
5. changelog 写清楚新增命令、剩余 stub、已知限制。

---

## 最终目标状态

理想 implemented：

```text
word read / preview / fill / rebuild / extract / dissect / stats / fonts / styles / validate / merge

inspect diagnose / refs / write-paper / classify / structure / varplan / evidence / data-req / gap / semantics

chart analyze / anova / duncan / bar / line / scatter / pie

excel sheets / read / to-groups / create

diagram flowchart / network / tree

pptx read / slides

ocr cloud
ocr local  # 仅在纯 .NET 或可控依赖实现后

genre list / show

icons list / search

skill validate / scan / inventory / package
```

预计 implemented 数量：

```text
20 current
+ 4 skill
+ 7 word
+ 7 inspect
+ 4 chart/excel
+ 1 diagram tree
+ 2 pptx
+ 1-2 ocr
= 46-47 implemented
```

如果 `ocr local` 仍依赖 Python 且不可控，则保持 stub，总数 46。

---

## 给 ClaudeCode 的执行提示

1. 先并行审查底层包 API，不要凭记忆写调用。
2. 每个 agent 只负责自己的命令组。
3. 每个命令先写 JSON contract，再接底层 API。
4. 不要把暂时不可实现的功能伪装成 implemented。
5. 不要为了清 stub 引入不可控依赖。
6. 每个阶段结束必须更新 Manifest、AGENT.md、README、changelog。
7. 每个阶段结束都要跑实际命令验收，不只 build。
8. 如果底层 API 缺口明显，写入 changelog 并保持 stub。

---

## 开发优先级总结

建议顺序：

```text
12 skill-manager migration
13 word P1
14 inspect P1
15 chart/excel P1
16 diagram tree
17 pptx read/slides
18 ocr cloud/local
19 GroundPA skill sync
20 tests/release gate
```

理由：

1. `skill` 命令先做，方便后续自动验证 GroundPA。
2. Word/Inspect 是农学生论文工作流核心。
3. Chart/Excel 是实验数据核心。
4. Diagram/PPTX/OCR 重要但依赖更复杂，放后面。
5. 测试和发布门禁贯穿每阶段，但最后统一收口。
