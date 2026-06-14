# 2026-06-03 阶段 13 指导：Nong CLI stub 全量补全蓝图

## 目标

当前 `nong` CLI 已经形成稳定基础：

- 统一 `JsonOutput` schema
- 错误码体系已收口
- stub 返回 `E009 not_implemented` + 非零退出码
- 生成命令支持自动创建输出目录
- `word rebuild` 已有同路径守卫
- `chart` 统计输入已有基础校验
- Nong.Toolkit.Net 2.0.0 skill 层已经切到 Nong CLI-first

下一阶段目标：系统性消灭 stub，补全 `nong` 的工具调用基础能力，让它成为农学生工作流的主入口。

不要再做零散修补。按本蓝图分阶段、多 agent 并行推进。

## 当前 stub 清单

```text
word extract / dissect / stats / fonts / styles / validate / merge
inspect classify / structure / varplan / evidence / data-req / gap / semantics
chart line / scatter / pie
diagram tree
pptx read / slides
ocr local / cloud
excel create
```

## 总原则

### 1. 只实现真实能力

每个命令必须可运行、可验收、可返回结构化 JSON。不能为了移除 stub 而写假实现。

### 2. 所有命令必须遵守 agent contract

JSON 输出固定：

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

错误处理：

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

新增错误码前必须确认已有错误码不能表达。

### 3. 生成 artifact 必须校验

所有生成文件命令必须：

1. `EnsureParentDir(output)`
2. 写文件
3. `CheckArtifact(output, kind)`
4. JSON 中写 `artifacts`

### 4. 所有输入 spec 必须先校验再执行

JSON spec 错误返回 `E006 validation_failed`，不要落入 `E004 internal_error`。

### 5. Manifest / AGENT / README / skill 层必须同步

每实现一个命令：

1. `Cli/Common/Manifest.cs` 状态从 `stub` 改为 implemented。
2. `Cli/AGENT.md` 增加输入格式、示例、输出字段。
3. Nong.Toolkit.Net 对应 skill 如需暴露能力，同步更新。
4. release checklist 更新。

### 6. 不引入 .NET 11 preview

目标框架保持 net8.0。推广环境按 `.NET 8+` 设计。

### 7. 多 agent 并行，但最后必须统一审计

ClaudeCode 可开多 agent：

```text
Agent A: Word commands
Agent B: Inspect commands
Agent C: Chart + Excel
Agent D: Diagram + PPTX + OCR
Agent E: Contract tests + docs + manifest + skill sync
```

每个 agent 只改自己负责的命令。最后由主 agent 汇总 build、验收和文档同步。

## 阶段总览

```text
Phase 0: skill-manager 迁入 nong skill P0
Phase 1: CLI contract test harness
Phase 2: Word stub 补全
Phase 3: Inspect stub 补全
Phase 4: Chart + Excel 补全
Phase 5: Diagram tree
Phase 6: PPTX read/slides
Phase 7: OCR local/cloud
Phase 8: GroundPA skill sync
Phase 9: release audit
```

建议顺序：先 Phase 0/1，再并行 Phase 2-7，最后 Phase 8/9。

---

# Phase 0：skill-manager 迁入 nong skill

参考：

```text
log/guidance/2026-06-03-013-skill-manager-into-nong-cli.md
```

目标命令：

```text
nong skill validate <dir> --json
nong skill scan <dir> --json
nong skill inventory <dir> --json
nong skill package <dir> --json
```

完成后 implemented 命令数从 20 变 24。

验收：

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
nong skill validate C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net\word --json
nong skill scan C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json
nong skill inventory C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json
nong commands --json
```

---

# Phase 1：CLI contract test harness

## 目标

在继续扩功能前，建立专门的 CLI contract 测试，防止快速开发再次造成：

- JSON command 字段漂移
- error status 但 exit code 为 0
- stub 返回 ok
- manifest 与真实命令不一致
- artifact 未生成却返回 ok

## 推荐新增项目

```text
Cli.Tests/
  NongCliContractTests.cs
  TestAssets/
```

目标框架：`net8.0`

可用测试框架：xUnit 或 MSTest。若仓库已有偏好，沿用已有偏好。

## 必测项

### 1. 所有 stub 返回 E009

读取 `nong commands --all --json`，找 `status == "stub"`，逐个调用最小命令。若需要参数，允许用固定 dummy 参数，但预期必须：

```json
{
  "status": "error",
  "errors": [{ "code": "E009" }]
}
```

退出码必须非 0。

### 2. commands 默认只列 implemented

```powershell
nong commands --json
```

不应出现 `status: stub`。

### 3. commands --all 列出 implemented + stub

```powershell
nong commands --all --json
```

每个命令必须有 `name/group/status`。

### 4. 错误命令非零退出

示例：

```powershell
nong chart analyze missing.json --json
```

预期 `E001` + exit != 0。

### 5. malformed JSON spec 返回 E006

覆盖：

```text
inspect write-paper
diagram flowchart
diagram network
chart analyze/anova/duncan/bar data file
```

可先从已实现命令开始，后续新命令加入。

## 验收

```powershell
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
```

---

# Phase 2：Word stub 补全

负责人：Agent A

目标命令：

```text
word extract
word dissect
word stats
word fonts
word styles
word validate
word merge
```

## 2.1 `nong word stats <file.docx> --json`

用途：极低 token 成本获取 Word 文档统计。

输入：`.docx`

输出建议：

```json
{
  "data": {
    "paragraphs": 29,
    "tables": 2,
    "images": 3,
    "footnotes": 1,
    "endnotes": 0,
    "characters": 3210,
    "wordsApprox": 1200,
    "sections": 1
  },
  "metrics": {
    "paragraphs": 29,
    "tables": 2,
    "images": 3
  }
}
```

实现来源：

- 复用 `DocxCore.WordTextReader`
- 复用 `WordPreview` 统计能力
- 必要时直接 OpenXML 计数

验收：

```powershell
nong word stats sample.docx --json
```

必须返回 `status: ok`，无 artifact。

## 2.2 `nong word fonts <file.docx> --json`

用途：列出正文、样式、runs 中出现的字体。

输出建议：

```json
{
  "data": {
    "fonts": [
      { "name": "宋体", "count": 120, "source": "run" },
      { "name": "Times New Roman", "count": 80, "source": "run" }
    ],
    "eastAsiaFonts": [],
    "asciiFonts": [],
    "warnings": []
  }
}
```

实现要点：

- 读取 `RunProperties.RunFonts`
- 区分 `Ascii`, `HighAnsi`, `EastAsia`, `ComplexScript`
- 样式字体可单独 source=`style`

## 2.3 `nong word styles <file.docx> --json`

用途：列出 style definitions，帮助 agent 判断模板结构。

输出建议：

```json
{
  "data": {
    "styles": [
      {
        "id": "Heading1",
        "name": "heading 1",
        "type": "paragraph",
        "basedOn": "Normal",
        "isDefault": false
      }
    ],
    "count": 12
  }
}
```

## 2.4 `nong word validate <file.docx> --json`

用途：OOXML 与基础质量校验。

输出规则：

- 文件可读、OOXML validator 无 error：`status: ok`
- validator 有 error：`status: error` + `E006`
- warnings 放 `issues`

输出建议：

```json
{
  "data": {
    "valid": true,
    "errors": [],
    "warnings": [],
    "statistics": {}
  },
  "issues": []
}
```

实现来源：

- `DocumentFormat.OpenXml.Validation.OpenXmlValidator`
- `WordPreview` 的 warnings/errors

## 2.5 `nong word extract <file.docx> -o <out-dir> --json`

用途：提取图片等嵌入资源。

参数：

```text
file.docx
-o/--output <dir> required
--images true by default
```

输出：

```json
{
  "artifacts": {
    "dir": "...",
    "images": [".../image1.png", ".../image2.jpeg"]
  },
  "metrics": {
    "images": 2
  }
}
```

实现要点：

- 读取 `MainDocumentPart.ImageParts`
- 保留原扩展名或按 content type 推断
- 输出目录自动创建
- 0 张图片也可 `status: ok`，summary 写明 0 images

## 2.6 `nong word dissect <file.docx> --json`

用途：格式指纹 / 文档结构 DNA。

输出建议：

```json
{
  "data": {
    "stats": {},
    "fonts": {},
    "styles": {},
    "tables": {},
    "numbering": {},
    "sections": {},
    "warnings": []
  }
}
```

实现方式：

- 聚合 `stats/fonts/styles`
- 加 tables 基础格式
- 加 numbering count
- 不要试图一次做复杂模板还原

## 2.7 `nong word merge <file1.docx> <file2.docx> ... -o <out.docx> --json`

用途：合并多个 Word 文档。

参数：

```text
files[] min 2
-o required
```

实现建议：

- 优先使用 OpenXML AltChunk 或已有 DocxCore merge 能力
- 若没有可靠 merge API，先做“正文段落追加”的保守实现
- 不要破坏输入文件
- 输出必须 `CheckArtifact`

验收：

```powershell
nong word merge a.docx b.docx -o merged.docx --json
nong word validate merged.docx --json
```

---

# Phase 3：Inspect stub 补全

负责人：Agent B

目标命令：

```text
inspect classify
inspect structure
inspect varplan
inspect evidence
inspect data-req
inspect gap
inspect semantics
```

## 总体原则

这些命令不是新算法，优先拆分 `inspect diagnose` 已经调用的内部能力：

```text
PaperTypeClassifier
PaperStructureExtractor
VariablePlanGenerator
PaperDiagnostics.DiagnoseEvidenceChain
PaperDiagnostics.DiagnoseDataRequirements
PaperDiagnostics.DiagnoseGapGrade
PaperDiagnostics.DiagnosePaperQuality / semantic-related methods
ReferenceAnalyzer
```

目的：让 agent 可以按需调用小命令，节省 token。

## 3.1 `nong inspect classify <paper.txt> --json`

输出：

```json
{
  "data": {
    "topType": "问卷调查型论文",
    "match": 80,
    "candidates": [
      {
        "type": "问卷调查型论文",
        "match": 80,
        "recommendedData": "...",
        "recommendedMethods": "..."
      }
    ]
  }
}
```

## 3.2 `nong inspect structure <paper.txt> --json`

输出：

```json
{
  "data": {
    "title": "...",
    "keywords": [],
    "sections": [
      { "level": 1, "heading": "1 引言", "startLine": 10, "endLine": 20 }
    ],
    "hasReferences": true,
    "referenceStartLine": 120,
    "hasAppendix": false
  },
  "metrics": {
    "sectionCount": 6
  }
}
```

## 3.3 `nong inspect evidence <paper.txt> --json`

输出 evidence chain 表。

字段和 `inspect diagnose` 中 `data.evidence` 保持一致。

## 3.4 `nong inspect data-req <paper.txt> --json`

输出 data requirements 表。

字段和 `inspect diagnose` 中 `data.dataRequirements` 保持一致。

## 3.5 `nong inspect gap <paper.txt> --json`

实现：

1. classify
2. structure
3. evidence
4. data-req
5. gap grade

输出：

```json
{
  "data": {
    "grade": "C",
    "description": "...",
    "canContinue": "..."
  }
}
```

## 3.6 `nong inspect varplan <paper.txt> --json`

输出变量操作化方案。

若现有 `VariablePlanGenerator` 返回文本，则先结构化为：

```json
{
  "data": {
    "variables": [],
    "measurement": [],
    "dataNeeded": [],
    "methods": [],
    "raw": "..."
  }
}
```

## 3.7 `nong inspect semantics <paper.txt> --json`

用途：论文语义/逻辑风险诊断。

如果现有 Inspect 包没有独立 semantic API，则使用 `PaperDiagnostics.DiagnosePaperQuality` 的语义相关输出，先保守实现：

```json
{
  "data": {
    "issues": [
      {
        "category": "...",
        "issue": "...",
        "fixRequirement": "...",
        "priority": "高"
      }
    ]
  },
  "metrics": {
    "issueCount": 12
  }
}
```

## Inspect 验收

准备 `paper-test.txt`，逐个跑：

```powershell
nong inspect classify paper-test.txt --json
nong inspect structure paper-test.txt --json
nong inspect evidence paper-test.txt --json
nong inspect data-req paper-test.txt --json
nong inspect gap paper-test.txt --json
nong inspect varplan paper-test.txt --json
nong inspect semantics paper-test.txt --json
```

全部 `status: ok`。

---

# Phase 4：Chart + Excel 补全

负责人：Agent C

目标命令：

```text
chart line
chart scatter
chart pie
excel create
```

## 4.1 `nong excel create <spec.json> -o <out.xlsx> --json`

用途：从简单 JSON spec 创建 Excel。

不要一开始做复杂公式/样式。P0 支持：

```json
{
  "sheets": [
    {
      "name": "Data",
      "headers": ["Treatment", "Yield"],
      "rows": [
        ["A", 1.2],
        ["A", 1.3],
        ["B", 2.1]
      ]
    }
  ]
}
```

验收：

```powershell
nong excel create spec.json -o data.xlsx --json
nong excel sheets data.xlsx --json
nong excel read data.xlsx --json
```

## 4.2 `nong chart line <spec.json> -o <out.png> --json`

输入 spec：

```json
{
  "title": "Growth curve",
  "xLabel": "Days",
  "yLabel": "Height",
  "series": [
    {
      "name": "Control",
      "x": [0, 7, 14],
      "y": [1.0, 2.0, 3.2]
    }
  ]
}
```

校验：

- series array 非空
- x/y 长度一致
- 数值不能 NaN/Infinity

## 4.3 `nong chart scatter <spec.json> -o <out.png> --json`

输入 spec：

```json
{
  "title": "Correlation",
  "xLabel": "Soil pH",
  "yLabel": "Yield",
  "points": [
    { "x": 6.1, "y": 12.3, "group": "A" }
  ],
  "trendline": true
}
```

P0 可选 trendline；若实现不了，先忽略并在 `issues` 提示。

## 4.4 `nong chart pie <spec.json> -o <out.png> --json`

输入 spec：

```json
{
  "title": "Composition",
  "values": [
    { "label": "A", "value": 30 },
    { "label": "B", "value": 70 }
  ]
}
```

校验：

- values 至少 2 项
- value > 0

## Chart/Excel 验收

```powershell
nong excel create excel-spec.json -o data.xlsx --json
nong chart line line-spec.json -o line.png --json
nong chart scatter scatter-spec.json -o scatter.png --json
nong chart pie pie-spec.json -o pie.png --json
```

所有图片生成后必须 `CheckArtifact`。

---

# Phase 5：Diagram tree

负责人：Agent D

目标命令：

```text
diagram tree
```

## `nong diagram tree <newick.txt|spec.json> -o <out.png> --json`

用途：系统发育树 / Newick tree。

建议支持两种输入：

### Newick 文本

```text
((A:0.1,B:0.2):0.3,C:0.4);
```

### JSON spec

```json
{
  "newick": "((A:0.1,B:0.2):0.3,C:0.4);",
  "title": "Phylogenetic tree"
}
```

实现来源：

- 如果 `DiagramCore` 已有 `NewickTree.Parse` / `TreeRenderer`，复用。
- 如果没有，先检查 `diagram/examples/primate_tree.cs` 对应 API。

验收：

```powershell
nong diagram tree tree.nwk -o tree.png --json
```

---

# Phase 6：PPTX read/slides

负责人：Agent D

关键约束：

```text
pptx read 暂桩，PptxCore 需适配 ShapeCrawler 合并
```

目标命令：

```text
pptx read
pptx slides
```

## 原则

PPTX 先只做读取，不做生成，不做编辑。

不要恢复旧 GroundPA `pptx` skill，直到这两个命令真实 implemented 并验收通过。

## 6.1 `nong pptx read <file.pptx> --json`

用途：抽取全部 slide 文本。

输出：

```json
{
  "data": {
    "text": "...",
    "slides": [
      {
        "index": 1,
        "title": "...",
        "texts": ["...", "..."]
      }
    ]
  },
  "metrics": {
    "slides": 10,
    "textBlocks": 42,
    "characters": 3200
  }
}
```

## 6.2 `nong pptx slides <file.pptx> --json`

用途：列 slide 结构，不只是文本。

输出：

```json
{
  "data": {
    "slides": [
      {
        "index": 1,
        "shapeCount": 8,
        "textCount": 3,
        "pictureCount": 1,
        "tableCount": 0,
        "chartCount": 0,
        "title": "..."
      }
    ]
  }
}
```

实现路线：

1. 先确认 PptxCore 当前是否编译。
2. 适配 ShapeCrawler 合并后的 API。
3. 若 ShapeCrawler 复杂，先用 OpenXML SDK 直接遍历 `PresentationDocument.PresentationPart.SlideParts` 提取 text。
4. `slides` 结构统计可先用 OpenXML element 类型计数。

验收：

```powershell
nong pptx read sample.pptx --json
nong pptx slides sample.pptx --json
```

---

# Phase 7：OCR local/cloud

负责人：Agent D

目标命令：

```text
ocr local
ocr cloud
```

关键决策：

不要让 skill 层直接调用 Python OCR。OCR 应该是 `nong ocr ...` 的 .NET CLI 能力。

## 7.1 `nong ocr cloud <file> -o <out-dir> --json`

用途：云端 OCR，输出 Markdown/文本/可选 Word。

参数建议：

```text
file
-o/--output <dir> required
--format markdown|text|docx default markdown
--token-env PADDLEOCR_TOKEN default
```

输出：

```json
{
  "artifacts": {
    "dir": "...",
    "markdown": ".../result.md",
    "text": ".../result.txt",
    "docx": ".../result.docx"
  },
  "data": {
    "pages": 3,
    "blocks": 120
  }
}
```

错误：

- token 缺失：`E005 dependency_missing`
- API 失败：`E004 internal_error` 或新增 `E010 external_service_failed`，只有确有必要才新增
- 输出失败：`E008 write_failed`

## 7.2 `nong ocr local <image> -o <out.json|out.txt> --json`

用途：本地 OCR。

现实约束：

本地 OCR 若依赖 Python/PaddleOCR，则推广环境成本高。P0 可先实现环境检查 + 调用包装，但必须如实返回依赖缺失。

参数建议：

```text
image
-o/--output <file> optional
--lang ch|en default ch
```

依赖缺失：

```json
{
  "status": "error",
  "command": "ocr local",
  "errors": [
    {
      "code": "E005",
      "name": "dependency_missing",
      "message": "Local OCR dependency is missing: PaddleOCR runtime not found."
    }
  ]
}
```

重要：

如果不能保证本地 OCR 在普通 net8 环境可用，不要把它改成 implemented。可以先只实现 `ocr cloud`，`ocr local` 继续 E009。

## OCR 验收

云端 OCR 需要 token，不可在无 token 环境假装通过。

验收分两类：

```powershell
nong ocr cloud sample.pdf -o ocr-out --json
```

有 token：`status: ok` + artifacts。

无 token：`E005 dependency_missing` + exit != 0。

---

# Phase 8：GroundPA skill sync

负责人：Agent E

每完成一个 phase，更新 Nong.Toolkit.Net。

当前 2.0.0 已暴露：

```text
word inspect excel chart diagram genre icons
```

## 同步规则

### Word

当实现：

```text
word extract/stats/fonts/styles/validate/merge/dissect
```

更新 `Nong.Toolkit.Net/word/SKILL.md`。

### Inspect

当实现：

```text
inspect classify/structure/varplan/evidence/data-req/gap/semantics
```

更新 `Nong.Toolkit.Net/inspect/SKILL.md`。

### Chart

当实现：

```text
chart line/scatter/pie
```

更新 `Nong.Toolkit.Net/chart/SKILL.md`。

### Excel

当实现：

```text
excel create
```

更新 `Nong.Toolkit.Net/excel/SKILL.md`。

### Diagram

当实现：

```text
diagram tree
```

更新 `Nong.Toolkit.Net/diagram/SKILL.md`。

### PPTX/OCR

只有当以下命令真实 implemented 且验收通过，才恢复对应 skill：

```text
pptx read
pptx slides
ocr cloud
ocr local
```

恢复时：

1. 新建/恢复 `pptx/SKILL.md` 或 `multimodal/SKILL.md`
2. 加入 `.claude-plugin/plugin.json`
3. 加入 `skills.sh.json`
4. 更新 README
5. `claude plugin validate .`

## GroundPA 验收

```powershell
claude plugin validate .
```

以及：

```powershell
nong skill scan C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json
nong skill inventory C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json
```

---

# Phase 9：Release audit

负责人：主 agent + Agent E

## 全局验收命令

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
nong commands --json
nong commands --all --json
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
```

## 命令级验收

所有 implemented 命令至少有：

1. happy path
2. file missing path
3. malformed JSON/spec path
4. JSON command 字段正确
5. exit code 正确

## Manifest 验收

```text
commands --json
```

只列 implemented。

```text
commands --all --json
```

列 implemented + stub，stub 必须明确 status。

## release checklist

新增或更新：

```text
changelog/2026-06-03-xxx-stage13-stub-completion.md
```

记录：

- 实现了哪些 stub
- 哪些仍保留 E009
- 哪些命令因为外部依赖只能部分验收
- Duncan 简化 Q 值近似仍需提示正式论文复核

---

# 多 agent 分工细则

## Agent A：Word

只改：

```text
Cli/Commands/WordCommands.cs
Docx/ 或 DocxCore 相关辅助 API
Cli/Common 如必要
```

交付：

```text
word stats/fonts/styles/validate/extract/dissect/merge
```

不要改 Inspect/Chart/PPTX/OCR。

## Agent B：Inspect

只改：

```text
Cli/Commands/InspectCommands.cs
Inspect/ 相关 API
```

交付：

```text
inspect classify/structure/evidence/data-req/gap/varplan/semantics
```

不要新增论文算法，先拆已有诊断管线。

## Agent C：Chart + Excel

只改：

```text
Cli/Commands/ChartCommands.cs
Cli/Commands/ExcelCommands.cs
Chart/ ChartCore
Excel/ 如需要
```

交付：

```text
chart line/scatter/pie
excel create
```

Duncan 简化 Q 值近似保留说明，不在本阶段改统计核心。

## Agent D：Diagram + PPTX + OCR

只改：

```text
Cli/Commands/DiagramCommands.cs
Cli/Commands/PptxCommands.cs
Cli/Commands/OcrCommands.cs
Diagram/
Pptx/
MultiModal/
```

交付：

```text
diagram tree
pptx read/slides
ocr cloud
ocr local only if dependency path is honest and testable
```

OCR local 如果依赖不可控，可继续 E009。

## Agent E：Contract + docs + skill sync

只改：

```text
Cli/Common/Manifest.cs
Cli/AGENT.md
Cli/README.md
Cli.Tests/
Nong.Toolkit.Net skill files
changelog/
```

职责：

1. command 字段统一
2. Manifest 不漂移
3. stub 状态正确
4. GroundPA 只暴露 implemented
5. release checklist 完整

---

# 最终目标

当本蓝图完成后，`nong` 应具备：

```text
Word: read/preview/fill/rebuild/extract/dissect/stats/fonts/styles/validate/merge
Inspect: diagnose/refs/write-paper/classify/structure/varplan/evidence/data-req/gap/semantics
Chart: analyze/anova/duncan/bar/line/scatter/pie
Excel: sheets/read/to-groups/create
Diagram: flowchart/network/tree
PPTX: read/slides
OCR: cloud/local or honest dependency error
Genre: list/show
Icons: list/search
Skill: validate/scan/inventory/package
```

预计 implemented 命令数：

```text
当前 20
+ skill 4
+ word 7
+ inspect 7
+ chart 3
+ excel 1
+ diagram 1
+ pptx 2
+ ocr 1-2
= 46-47 implemented
```

如果 `ocr local` 不能可靠实现，则保持 stub：

```text
45-46 implemented + ocr local E009
```

这不是失败。诚实边界优先于数量。
