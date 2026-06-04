# 2026-06-04 阶段 15b 指导：Word 一刀三流全量返工修复

本文件给 ClaudeCode 使用。目标不是最小补丁，而是一次性把阶段 15 Word「一刀三流」修到可发布、可审计、可由 agent 稳定调用的状态。

当前结论：

```text
阶段 15 不能视为完成。
build/test 通过不代表阶段 15 通过。
核心入口 word dissect -o 未接线，NongMark schema/ID/CLI/docs/tests 均存在契约偏差。
本轮按 Stage 15b 全量返工处理。
```

## 0. 开工前必须先读

必须读取并遵守：

```text
log/guidance/2026-06-03-018-stage15-word-yidao-sanliu-blueprint.md
log/guidance/2026-06-04-020-stage15-kickoff-execution.md
changelog/2026-06-04-022-overnight-stage16-release-result.md
```

注意：`changelog/2026-06-04-022-overnight-stage16-release-result.md` 写了 COMPLETE，但其 Known Limitations 明确承认：

```text
word dissect -o 三流模式未接线
WordSlice.cs 未人工审核 API 是否完整可用
--after blockId 未实现
```

所以不要沿用 COMPLETE 判断。

## 1. 当前已审计出的硬问题

### 1.1 P0：一刀三流主入口不可用

实测：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\audit-stage15-slice --json
```

当前返回：

```text
Unrecognized command or argument '-o'
```

代码证据：

```text
Cli/Commands/WordCommands.cs: CreateDissect 只注册 <file>
Cli/Commands/WordCommands.cs: 仍调用 DocxAnalysis.Dissect(file)
Docx/WordSlice.cs: WordSlice.Slice(...) 存在但未被 CLI 调用
```

必须修成：

```powershell
nong word dissect <file.docx> --json
```

继续返回旧格式指纹摘要。

```powershell
nong word dissect <file.docx> -o <out-dir> --json
nong word dissect <file.docx> --output <out-dir> --json
```

进入完整一刀三流模式，写出：

```text
manifest.json
document.json
content.md
content.jsonl
structure.json
format.json
assets/manifest.json
```

### 1.2 P0：add 命令形态错误

阶段 15 要求 canonical 命令是：

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

当前实现是：

```text
word add-paragraph
word add-table
word add-footnote
...
```

这会导致按指导调用的 agent 直接失败。必须新增 nested `word add` 命令组，并把 manifest/docs/tests 统一到 canonical 形态。

允许保留 hyphen 旧入口作为兼容 alias 或 deprecated compatibility command，但：

```text
1. CAPABILITY.md / Cli/AGENT.md / release-checklist.md 必须写 canonical word add paragraph 形态。
2. commands --json 中的 Word 写命令必须以 canonical 形态为准。
3. JSON command 字段必须是 canonical command，例如 "word add table"。
4. 如果保留 hyphen 入口，必须有测试证明它不破坏 canonical 输出。
```

### 1.3 P0：`--spec` 契约错误

阶段 15 验收命令要求：

```powershell
nong word add paragraph input.docx --spec tests-output\paragraph.json -o out.docx --json
nong word add table input.docx --spec tests-output\table.json -o out.docx --json
```

当前代码把 `--spec` 当内联 JSON 字符串解析，不读取文件路径。

实测：

```powershell
nong word add-paragraph tests-output\05-docx\docx-basic.docx --spec tests-output\paragraph.json -o tests-output\audit-add-para-file.docx --json
```

返回 E006 `Invalid JSON spec`。

还存在大小写问题：

```text
--spec '{"text":"hello"}' 当前不能绑定到 ParagraphSpec.Text
--spec '{"headers":["A"],"rows":[["1"]]}' 当前 add-table 触发 E004 NullReference
--spec '{"Text":"hello"}' 才能成功
```

必须修成：

```text
1. --spec 默认解释为 JSON 文件路径。
2. 为兼容可以支持内联 JSON，但文件路径是 canonical。
3. JSON 必须 case-insensitive，lower camelCase 示例必须可用。
4. malformed JSON -> E006 + EXIT:1。
5. spec 文件不存在 -> E001 + EXIT:1。
6. 必填字段缺失 -> E006 + EXIT:1，不得落 E004。
```

### 1.4 P0：`--after <blockId>` 未实现也未按契约报错

阶段 15 原文说如果暂时无法实现，必须 E006，不得忽略。

本轮不做最小修复，要求实现 body-level 精确插入：

```text
word add paragraph/table/image/toc/xref/link/bookmark/comment/math
```

必须支持插入到 `p0001/h0001/t0001/img0001/m0001/...` 之后。

规则：

```text
1. 使用 WordSlice/ID map 定位 body 中可插入锚点。
2. --after 指向不存在 ID -> E006。
3. --after 指向非 body 可插入对象（footnote/comment/revision 等）-> E006，message 说明该 ID 不能作为 body 插入锚点。
4. 不传 --after 时追加到文档末尾，但必须避开 final sectPr，插入在 sectPr 之前。
5. 所有 add 命令输出 data.blockId 使用新增对象的稳定 ID 前缀，不用 GUID。
```

脚注/尾注：

```text
word add footnote / endnote 可把 reference 插入到 --after 目标段落之后或目标段落末尾。
```

### 1.5 P0：NongMark ID 体系没有成为共享契约

阶段 15 要求稳定 ID：

```text
p0001, h0001, t0001, f0001, e0001, img0001, c0001, r0001, m0001
```

当前问题：

```text
OutlineReader 输出 b3 / b5
ImageLister 输出 img1
WordAddOperations 输出 para_<guid> / table_<guid> / math_<guid>
WordSlice assets manifest 把 public id 写成 rId
```

必须修成：

```text
1. 建立一个共享 WordBlockIdMap / WordSliceIndex / WordIdAllocator。
2. ID 按文档出现顺序生成，同一 docx 多次 dissect 结果稳定。
3. OpenXML rId 只能出现在 internalRelationshipId/internal 字段，不得作为公开 id。
4. outline/images/comments/revisions 必须引用同一套 blockId/assetId。
5. add 命令返回的 blockId 必须是确定性新增 ID，例如根据当前最大 p/t/m/img 后递增。
```

### 1.6 P0：NongMark schema 与阶段 15 schema 不一致

当前 `Docx/NongMarkModels.cs` 和 `Docx/WordSlice.cs` 存在以下偏差：

```text
manifest.json 用 sha256，不是 sourceSha256
manifest.json 缺 createdAt
streams 用 contentMd，不是 content
assets/manifest.json item 只有 id/path/contentType/size
assets item 缺 file/width/height/usedBy/internalRelationshipId/analysis/ocr
assets item id 当前用 rId
image.analysis 默认 "not available"，没有接 MultiModal/ImageAnalyzer
hyperlink block 里 Url 当前可能只是 relationship id，不是真实 URL
WordSlice 定义 RevisionBlock，但未实际提取 inserted/deleted/move revisions
table row/cell ID 复用 rCounter，容易和 run ID 计数混杂
```

必须按阶段 15 schema 修正：

```json
{
  "schemaVersion": "nongmark/v1",
  "source": "paper.docx",
  "sourceSha256": "...",
  "createdAt": "2026-06-04T00:00:00+08:00",
  "streams": {
    "document": "document.json",
    "content": "content.md",
    "contentJsonl": "content.jsonl",
    "structure": "structure.json",
    "format": "format.json",
    "assets": "assets/manifest.json"
  },
  "metrics": {
    "paragraphs": 0,
    "headings": 0,
    "tables": 0,
    "images": 0,
    "footnotes": 0,
    "endnotes": 0,
    "comments": 0,
    "revisions": 0,
    "math": 0,
    "chemEquations": 0,
    "chemicalStructures": 0
  },
  "warnings": []
}
```

assets manifest item 必须至少包含：

```json
{
  "id": "img0001",
  "file": "image_001.png",
  "contentType": "image/png",
  "width": 1200,
  "height": 800,
  "usedBy": ["p0007"],
  "internalRelationshipId": "rId5",
  "analysis": {
    "engine": "ImageAnalyzer",
    "netLocal": true,
    "regions": []
  },
  "ocr": {
    "status": "notRun",
    "engine": null,
    "textBlocks": []
  }
}
```

### 1.7 P0：ImageAnalyzer 未接入一刀三流资产流

当前 `WordSlice.Slice(..., IImageAnalyzer? imageAnalyzer = null)` 设计了接口，但 CLI 没有传入实现。由于 `MultiModal` 已引用 `Docx`，不能让 `Docx` 反向引用 `MultiModal`。

必须做：

```text
1. 在 CLI 层或单独 adapter 层创建 ImageAnalyzerAdapter : DocxCore.IImageAnalyzer。
2. adapter 调用 MultiModalCore.ImageAnalyzer。
3. 把 ImageLayout 映射到 DocxCore.ImageAnalysis。
4. assets/manifest.json 也必须写入 analysis，而不只在 document image block 中写。
5. 如果某图片无法解码，analysis.status/engine/issue 诚实记录，不能吞掉为 "not available"。
```

### 1.8 P0：写命令参数不符合规格

当前 SimpleAdd 把很多命令压成一个 `--text`：

```text
add-toc      强制要求 --text
add-xref     用 --text "ref:label"
add-link     用 --text "url|text"
add-bookmark 用 --text
add-comment  缺 --author
```

必须改成阶段 15 规格：

```text
word add footnote <file.docx> --text <text> -o <out.docx> [--after <blockId>]
word add endnote <file.docx> --text <text> -o <out.docx> [--after <blockId>]
word add image <file.docx> --src <image> [--caption <text>] -o <out.docx> [--after <blockId>]
word add toc <file.docx> -o <out.docx> [--after <blockId>]
word add xref <file.docx> --to <bookmark> --text <display> -o <out.docx> [--after <blockId>]
word add link <file.docx> --url <url> --text <display> -o <out.docx> [--after <blockId>]
word add bookmark <file.docx> --name <name> -o <out.docx> [--after <blockId>]
word add comment <file.docx> --text <text> [--author <name>] -o <out.docx> [--after <blockId>]
word add math <file.docx> --latex <latex> [--display] -o <out.docx> [--after <blockId>]
```

### 1.9 P0：错误码分流错误

实测：

```text
word protect --mode invalid -> E004 internal_error
word embed-font CAPABILITY.md -> E004 internal_error
add-table lower camelCase rows -> E004 NullReference
```

必须修：

```text
invalid mode -> E006
unsupported font extension -> E002
unsupported image extension -> E002
missing input/spec/font/image -> E001
malformed JSON spec -> E006
missing required spec fields -> E006
output == input -> E006
artifact missing/corrupt after write -> E007 or existing artifact error contract
unexpected OpenXML/package failure -> E004
```

### 1.10 P1：fix-order 覆盖面不足

当前 `WordEditOperations.FixOrder` 只处理 body：

```text
ElementOrder.RectifyTree(body)
ElementOrder.FixOrphanBorders(body)
```

阶段 15 要求复制 input 到 output 后，对 main document/styles/numbering/settings 等可处理 part 执行顺序修复。

必须扩展：

```text
1. MainDocumentPart.Document
2. StyleDefinitionsPart.Styles
3. NumberingDefinitionsPart.Numbering
4. DocumentSettingsPart.Settings
5. Header/Footer parts（如可安全处理）
6. Footnotes/Endnotes/Comments parts（如可安全处理）
```

返回 metrics：

```json
{
  "fixedElements": 0,
  "partsScanned": 0,
  "orphanBordersFixed": 0
}
```

### 1.11 P1：merge warnings 丢失

`WordEditOperations.MergeDocuments` 返回 warnings，但 `word merge` JSON 没把 warnings 放进 `issues`，也没有 metrics。

必须修：

```text
1. CLI 直接使用 WordEditOperations.MergeDocuments 或保留 DocxAnalysis.MergeDocx 但返回 MergeResult。
2. output.issues 写入页眉页脚、编号冲突、样式同名冲突等 warnings。
3. metrics.sourceFiles = N。
4. artifacts.docx 必须存在并通过 CheckArtifact。
5. merge 后抽测 word validate。
```

### 1.12 P1：read/check 命令没有围绕 blockId + NongMark

当前：

```text
word outline 输出 b3/b5
word images 输出 img1 且只 UsedInParagraph bool
comments/revisions 没有统一 blockId/anchor ID
```

必须修：

```text
1. outline 输出 h/p blockId，和 word dissect -o 的 structure.outline 一致。
2. images 输出 assetId = img0001，usedBy = ["p0007"]，internalRelationshipId 单独保留。
3. comments 输出 commentId + anchorBlockId/anchorText，拿不到时 null。
4. revisions 输出 revisionId + blockId + type + snippet，不输出整篇。
5. infer-format 空输入/不可解析仍 E006，解析结果用 camelCase。
```

### 1.13 P1：文档和命令数口径混乱

当前：

```text
CAPABILITY.md 写 Word 30 个命令，但阶段 15 蓝图写 29 leaf commands。
Cli/AGENT.md 顶部仍写 46 implemented，后面又写 71 implemented。
release-checklist.md 写 71 total。
Stage 16 result 写 68 implemented，但当前 commands --json 是 71。
```

逻辑结论：

```text
阶段 15 历史规格的 29 leaf commands 不包含 legacy word extract。
阶段 15 又要求保留现有 11 个命令，其中包含 word extract。
所以最终 Word 命令数应明确写为：
Word = 30 implemented leaf commands
= 29 Stage 15 canonical Word leaf commands + retained legacy word extract.
```

不要为了凑 29 删除 `word extract`。文档中必须说明这个计数修正。

### 1.14 P1：阶段 16 污染阶段 15 收尾

当前工作区已经有阶段 16 OCR 改动：

```text
ocr check-env
ocr analyze-image
ocr models
ocr install-model
ocr to-word
PADDLEOCR_ACCESS_TOKEN 迁移
```

本轮目标是阶段 15b，不继续扩大 OCR。

要求：

```text
1. 不新增 OCR 功能。
2. 不回滚用户已有改动，除非明确需要且得到用户指令。
3. Manifest/CAPABILITY/AGENT 必须 truthfully reflect 当前 commands --json。
4. Stage 15b 结果日志必须把 OCR 标成 existing out-of-scope state，不得把 OCR 变化算作 Stage 15b 成果。
5. 如果 OCR command 注册为 implemented 但运行返回 E009/E005，要在 docs 中诚实说明。
```

### 1.15 P0：Word 专项测试缺失

阶段 15 要求新增或更新：

```text
Cli.Tests/WordCommandTests.cs
```

当前该文件不存在。现有 34 个测试没有覆盖：

```text
word dissect -o
document.json schemaVersion
content.jsonl 一行一 block
stable ID
assets manifest
ImageAnalyzer analysis
word add paragraph/table/math canonical command
--spec file path
--after blockId
malformed spec E006
output == input E006
JSON command 字段逐项正确
```

必须新增 `Cli.Tests/WordCommandTests.cs`，不要把这些只塞进泛化 contract tests。

## 2. 多 agent 一步到位分工

使用多 agent 并行，但要避免共享文件冲突。

### Coordinator

职责：

```text
1. 建立最终 Word command contract。
2. 锁定共享文件编辑窗口。
3. 合并各 agent helper API。
4. 最后统一改 Cli/Commands/WordCommands.cs、Cli/Common/Manifest.cs、CAPABILITY.md、Cli/AGENT.md、release-checklist.md。
5. 写 changelog/2026-06-04-024-stage15b-one-shot-repair-result.md。
```

Coordinator 专属共享文件：

```text
Cli/Commands/WordCommands.cs
Cli/Common/Manifest.cs
CAPABILITY.md
Cli/AGENT.md
release-checklist.md
README.md
README.zh-CN.md
```

### Agent A：NongMark schema + ID map

改：

```text
Docx/NongMarkModels.cs
新增 Docx/WordBlockIdMap.cs 或 Docx/WordSliceIndex.cs
```

任务：

```text
1. 修正 manifest schema：sourceSha256、createdAt、streams.content。
2. 修正 assets manifest schema：file/width/height/usedBy/internalRelationshipId/analysis/ocr。
3. 建立稳定 ID 分配器和 OpenXML anchor map。
4. 明确 public id 与 internal rId 分离。
5. 为 paragraph/heading/table/image/math/footnote/endnote/comment/revision/hyperlink/bookmark/toc/field/rawOpenXmlRef 建立统一 ID 规则。
6. table row/cell 如果保留 ID，要用 tr/tc 独立 counter，不得复用 rCounter。
```

验收：

```text
同一 docx 连续两次生成 ID 完全一致。
任何公开 id 不得等于 rId\d+。
```

### Agent B：WordSlice 一刀三流核心

改：

```text
Docx/WordSlice.cs
必要时新增 Docx/WordSliceOptions.cs
```

任务：

```text
1. 使用 Agent A 的 ID map。
2. 输出 7 个核心路径。
3. content.md 只做 preview，document.json/content.jsonl 为 canonical。
4. content.jsonl 一行一个 canonical block。
5. 提取 paragraph/heading/run/table/image/equation/footnote/endnote/hyperlink/bookmark/comment/revision/toc/field/rawOpenXmlRef。
6. hyperlink 必须解析 external relationship target URL；internal anchor 单独字段。
7. revisions 必须真实提取 inserted/deleted/move snippets。
8. comments 尽量定位 anchorBlockId/anchorText。
9. images 必须写 assets/manifest.json，即使 0 张图也 items: []。
10. 不输出整包 raw XML；rawOpenXmlRef 只写 part/element/reason/short locator。
```

限制：

```text
OMML -> LaTeX 可保留 best-effort，但不能把公式混成普通文本。
chemEquation/chemicalStructure 可 schema 预留，能识别简单文本化学方程式才填 confidence/source。
```

### Agent C：ImageAnalyzer adapter

改：

```text
Cli/Commands 或新增 Cli/Adapters/WordImageAnalyzerAdapter.cs
```

任务：

```text
1. 实现 DocxCore.IImageAnalyzer。
2. 调用 MultiModalCore.ImageAnalyzer。
3. 映射 WhitespaceRatio、Content bounds、Regions。
4. 写 analysis.engine = "ImageAnalyzer"，netLocal = true。
5. 生成 asciiMapFile 时写到 assets/image_001.map.txt，并在 analysis 中引用。
6. 不接 PP-OCRv5，不做 OCR。
7. image.ocr 固定 status = "notRun"，engine = null，textBlocks = []。
```

注意：

```text
Docx 项目不能引用 MultiModal，避免循环依赖。
CLI 已引用 Docx 和 MultiModal，可以在 CLI 层做 adapter。
```

### Agent D：read/check helper 对齐 NongMark

改：

```text
Docx/OutlineReader.cs
Docx/ImageLister.cs
Docx/CommentReader.cs
Docx/RevisionReader.cs
Docx/WordReadModels.cs
Docx/FormatInferrer.cs
```

任务：

```text
1. outline/images/comments/revisions 使用同一 ID map 或从 WordSliceIndex 读取。
2. outline blockId 使用 p/h 稳定 ID，不再 b3。
3. images 使用 img0001，usedBy 数组，不再只有 UsedInParagraph bool。
4. comments 输出 anchorBlockId/anchorText。
5. revisions 输出 revisionId/blockId/type/snippet/author/date。
6. infer-format 保持 conservative；空/不可解析 E006。
```

### Agent E：edit/merge 强化

改：

```text
Docx/WordEditOperations.cs
Docx/DocxAnalysis.cs 如需改返回类型
```

任务：

```text
1. fix-order 覆盖 body/styles/numbering/settings/header/footer/footnote/endnote/comment 等可处理 part。
2. protect mode 校验前置，invalid mode 不抛到 E004。
3. embed-font 校验 .ttf/.otf，unsupported -> E002。
4. merge 直接返回 MergeResult，CLI 可拿到 warnings。
5. merge 使用 AdvancedFeatures.AppendDocument。
6. merge/fix-order/protect/embed-font 全部 CheckArtifact。
7. 所有 output 不得等于 input。
```

### Agent F：add group + --after

改：

```text
Docx/WordAddOperations.cs
新增 Docx/WordInsertionPlanner.cs 如需要
```

任务：

```text
1. 实现 canonical nested add group 所需底层 API。
2. 支持 --after body blockId 精确插入。
3. add paragraph/table 支持 spec 文件和 inline JSON，case-insensitive。
4. add xref/link/bookmark/comment 参数拆成 --to/--url/--name/--author。
5. add toc 不强制 --text。
6. add image unsupported extension -> E002。
7. malformed spec / required field missing -> E006。
8. 添加后返回新增 blockId，不用 GUID。
9. 生成后 validate/read 能看见新增内容。
```

### Agent G：CLI 接线 + JSON contract

由 Coordinator 合并前准备 patch，但最终由 Coordinator 改共享文件：

```text
Cli/Commands/WordCommands.cs
```

任务：

```text
1. word dissect 增加 -o/--output。
2. word add 建 nested command group。
3. 可选保留 hyphen compatibility entry，但 docs/manifest canonical。
4. 所有 JsonOutput.Ok command 字段逐项正确。
5. 所有 WriteError command 字段逐项正确。
6. 所有新命令 status/error schema 一致。
7. non-json 输出可简洁，但 json 是验收主路径。
```

### Agent H：Word 专项测试

改：

```text
Cli.Tests/WordCommandTests.cs
Cli.Tests/TestAssets/...
```

任务：

新增测试覆盖：

```text
1. word dissect -o 输出 7 个核心路径。
2. manifest.schemaVersion = nongmark/v1。
3. manifest.sourceSha256 和 createdAt 存在。
4. manifest.streams.content = content.md。
5. document.json schemaVersion = nongmark/v1，blocks 非空。
6. content.jsonl 每行可解析，且每行有 id/kind。
7. 连续两次 dissect 同一 docx，block ID 序列一致。
8. 无图 docx 仍有 assets/manifest.json items: []。
9. 有图 docx 的 assets manifest 包含 img0001、file、width、height、usedBy、internalRelationshipId、analysis、ocr.status=notRun。
10. ImageAnalyzer analysis.engine = ImageAnalyzer。
11. word outline 返回 h/p 稳定 blockId。
12. word images 返回 img0001 + usedBy。
13. comments/revisions 空文档返回 ok + []。
14. word add paragraph/table/math canonical 命令生成 docx，word read 可读到新增文本或 math fallback。
15. --spec file path 可用。
16. lower camelCase spec 可用。
17. malformed JSON spec -> E006。
18. missing spec file -> E001。
19. output == input -> E006。
20. --after p0001 插入成功，读顺序符合预期。
21. --after missing -> E006。
22. add image unsupported extension -> E002。
23. protect invalid mode -> E006。
24. embed-font invalid extension -> E002。
25. merge 输出 docx，issues 包含 warnings，validate 通过。
26. commands --json 不含 stub，不把兼容 hyphen 当 canonical 重复计数。
27. JSON command 字段逐项等于 canonical command。
```

测试夹具建议：

```text
使用 OpenXML 在测试中创建临时 docx，不依赖 tests-output 固定文件。
创建 minimal docx、heading docx、image docx、comment docx、revision docx、math docx。
```

### Agent I：文档和结果日志

由 Coordinator 最后合并：

```text
CAPABILITY.md
Cli/AGENT.md
release-checklist.md
changelog/2026-06-04-024-stage15b-one-shot-repair-result.md
```

必须写清：

```text
1. Word 一刀三流是主工作流。
2. canonical layer 是 nongmark/v1 JSON，不是 Markdown。
3. Word = 30 implemented leaf commands = 29 Stage 15 canonical + retained legacy word extract。
4. command examples 使用 word add paragraph，不使用 add-paragraph。
5. current total commands 以实际 commands --json 为准。
6. OCR 当前状态是 existing out-of-scope，不计入 Stage 15b 成果。
7. Known limitations 不得包含 P0 未完成项：
   - 不得写 word dissect -o 未接线
   - 不得写 --after 未实现
   - 不得写 spec 文件路径不支持
```

允许的 Known limitations：

```text
OMML -> LaTeX 逆转换 best-effort。
chemicalStructure 不自动推 SMILES/InChI。
merge 对复杂页眉页脚/编号/样式冲突仍有边界，但必须有 warnings。
ImageAnalyzer 是图像结构分析，不等同 OCR。
PP-OCRv5 本地接入仍不属于 Stage 15b。
```

## 3. 必须执行的验收命令

开工前：

```powershell
cd C:\Users\Administrator\Documents\Github\Angri450.Nong
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

修复后必须跑：

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
```

一刀三流：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-slice --json

Test-Path tests-output\stage15b-slice\manifest.json
Test-Path tests-output\stage15b-slice\document.json
Test-Path tests-output\stage15b-slice\content.md
Test-Path tests-output\stage15b-slice\content.jsonl
Test-Path tests-output\stage15b-slice\structure.json
Test-Path tests-output\stage15b-slice\format.json
Test-Path tests-output\stage15b-slice\assets\manifest.json
```

读/查：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word outline tests-output\05-docx\docx-basic.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word images tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-images --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word comments tests-output\05-docx\docx-basic.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word revisions tests-output\05-docx\docx-basic.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word infer-format "黑体 四号 居中 固定行距28磅" --json
```

改：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word fix-order tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word protect tests-output\05-docx\docx-basic.docx --mode readonly -o tests-output\stage15b-protected.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word merge tests-output\05-docx\docx-basic.docx tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-merged.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word validate tests-output\stage15b-fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word validate tests-output\stage15b-merged.docx --json
```

写：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word add paragraph tests-output\05-docx\docx-basic.docx --spec tests-output\paragraph.json -o tests-output\stage15b-add-paragraph.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add table tests-output\05-docx\docx-basic.docx --spec tests-output\table.json -o tests-output\stage15b-add-table.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\stage15b-add-math.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word read tests-output\stage15b-add-paragraph.docx --json
```

错误路径：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word add paragraph tests-output\05-docx\docx-basic.docx --spec tests-output\missing.json -o tests-output\x.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add table tests-output\05-docx\docx-basic.docx --spec tests-output\bad.json -o tests-output\x.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\x.docx --after missing0001 --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word protect tests-output\05-docx\docx-basic.docx --mode invalid -o tests-output\x.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word embed-font tests-output\05-docx\docx-basic.docx CAPABILITY.md -o tests-output\x.docx --json
```

必须做 secret scan：

```powershell
rg -n "PADDLEOCR_TOKEN\s*[:=]|PADDLEOCR_ACCESS_TOKEN\s*[:=]|sk-[A-Za-z0-9_-]{20,}|github_pat_|ghp_|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\." . -g "!bin" -g "!obj" -g "!tests-output"
```

结果日志不得粘贴任何密钥值。

## 4. 结果日志必须写

写：

```text
changelog/2026-06-04-024-stage15b-one-shot-repair-result.md
```

必须包含：

```text
Goal:
Why Stage 15 previous COMPLETE was rejected:
Problem inventory fixed:
Implemented command surface:
Word command count before/after:
NongMark schema changes:
One-cut three-stream output files:
Stable ID rules:
ImageAnalyzer integration:
--after implementation:
Files changed:
Tests added:
Commands run:
Build/test result:
Secret scan result:
Known limitations:
Open risks:
Next recommended stage:
```

`Open risks` 不得包含本指导列出的 P0。

## 5. 完成标准

只有满足以下条件才算 Stage 15b 完成：

```text
1. word dissect -o 真实写出 7 个核心文件。
2. document.json/content.jsonl 是 nongmark/v1 canonical source。
3. content.md 只是 preview。
4. manifest/assets/structure/format schema 和阶段 15 对齐。
5. ImageAnalyzer 结果进入 assets manifest。
6. outline/images/comments/revisions 引用稳定 blockId/assetId。
7. word add paragraph/table/... canonical 命令可用。
8. --spec 文件路径可用，lower camelCase JSON 可用。
9. --after body blockId 可用。
10. 生成类命令全部 CheckArtifact。
11. 错误码按 E001/E002/E006/E007/E004 分流。
12. WordCommandTests 覆盖阶段 15 主路径和错误路径。
13. CAPABILITY.md / Cli/AGENT.md / release-checklist.md 同步且无 46/68/71 自相矛盾。
14. build/test/验收命令全部通过。
15. 结果日志明确说明历史 29 与 retained extract 导致 Word 30 implemented 的口径。
```

本阶段不是“命令数变多”，而是 agent 可以稳定执行：

```powershell
nong word dissect paper.docx -o paper.slice --json
```

然后只读 `document.json`、`content.jsonl`、`structure.json`、`format.json`、`assets/manifest.json` 就能判断下一步，再用 `word add ...`、`fix-order`、`merge` 做确定性修改。
