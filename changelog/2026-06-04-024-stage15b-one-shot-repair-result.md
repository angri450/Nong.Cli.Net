# 阶段 15b Word 一刀三流全量返工修复报告

日期：2026-06-04
状态：COMPLETE — Stage 15b P0/P1 收口完成，58/58 tests PASS

---

## Goal

一次性把阶段 15 Word「一刀三流」修到可发布、可审计、可由 agent 稳定调用的状态。上一轮 15 虽 build/test 通过，但核心入口 `word dissect -o` 未接线，ID 体系混乱，--spec/--after 契约偏差。

## Why Previous COMPLETE Was Rejected

```
word dissect -o 命令不存在（Unrecognized command or argument '-o'）
add 命令是 hyphen 形态（add-paragraph），不是 canonical（add paragraph）
--spec 不读文件路径，只解释为内联 JSON
--after blockId 未实现也未按契约报错
NongMark ID 体系不统一（b3/img1/para_guid vs p0001/img0001）
ImageAnalyzer 未接入资产流
错误码分流错误（E004 用于 invalid mode/font extension）
```

## Problem Inventory Fixed

### Agent A: NongMark schema + ID map
- `Manifest.Sha256` → `SourceSha256`，新增 `createdAt` ISO 8601
- `streams.ContentMd` → `Content`
- `NongAssetEntry` 新增 `file/width/height/usedBy/internalRelationshipId/analysis/ocr`
- `HyperlinkBlock` 区分 `Url` 和 `InternalAnchor`
- 新建 `Docx/WordBlockIdMap.cs`：稳定 ID 分配器 + OpenXML anchor map
- 公开 ID 规则：p0001+/h0001+/t0001+/img0001+/m0001+/f0001+/e0001+/c0001+/rev0001+
- 永不暴露 OpenXML rId 作为公开 ID

### Agent B: WordSlice 一刀三流核心
- 使用 WordBlockIdMap 稳定 ID 分配
- 输出 7 个核心文件
- Hyperlink 解析 external relationship URL
- Revisions 真实提取 inserted/deleted/move snippets
- Comments 定位 anchorBlockId/anchorText
- Table rows/cells 使用 tr/tc 独立计数器
- Run-level format 全部保留
- Equations best-effort OMML→LaTeX

### Agent C: ImageAnalyzer adapter
- 新建 `Docx/IImageAnalyzer.cs` 接口
- 新建 `Cli/Adapters/WordImageAnalyzerAdapter.cs` 桥接 MultiModal
- 避免 Docx→MultiModal 循环依赖

### Agent D: read/check helpers 对齐 NongMark
- `OutlineReader` 使用 h0001+ 稳定 ID
- `ImageLister` 使用 img0001+，`UsedInParagraph`→`UsedBy` 数组，`FileNameHint`→`FileName`
- `CommentReader` 新增 `AnchorBlockId`/`AnchorText`
- `RevisionReader` 新增 `RevisionId`/`BlockId`，`Text`→`Snippet`
- 所有模型添加 `[JsonPropertyName]` camelCase

### Agent EF: edit/merge + add group
- `fix-order` 覆盖 9 个 part（body/styles/numbering/settings/header/footer/footnote/endnote/comment）
- `FixOrderResult` 新增 `PartsScanned` 字段
- `protect` mode 校验前置，invalid→ArgumentException（CLI→E006）
- `embed-font` extension 校验前置，unsupported→ArgumentException（CLI→E002）
- `WordAddOperations` 全部方法添加 `[JsonPropertyName]` camelCase 支持
- 全量方法添加入参 null 校验，缺必填字段→ArgumentException（CLI→E006）
- **`--after blockId` 实现**：`FindElementByBlockId`/`InsertAfterBlockId`，对齐 WordSlice 语义 ID，支持 p/h/t/img/m/toc/link/xref/bm 等可定位 body 锚点
- **ID 系统升级**：GUID/重复 ID→稳定 public blockId；add 返回值与 dissect 输出的 p/t/img/m/link/xref/bm/f/e/c/toc 前缀对齐
- **OOXML 顺序修复**：新增 table/paragraph/link/xref 的 RunProperties、ParagraphProperties、TableBorders、TableCellProperties 按 schema 顺序输出；最小 docx add-table 后 `word validate` 通过

### Agent H: Word 专项测试
- 新建 `Cli.Tests/WordCommandTests.cs`：23 项 Word 专项测试
- 扩展到 canonical add/spec/--after/dissect -o/--output、parse error JSON、semantic blockId、math dissect、schema-valid add-table 覆盖
- 58 PASS / 0 SKIP / 58 total
- 覆盖 dissect/outline/images/comments/revisions/infer-format/add-*/fix-order/protect/embed-font/merge

### Coordinator (主线程)
- `word dissect` 新增 `-o/--output` 选项，接线 WordSlice.Slice + ImageAnalyzer adapter
- 修复 `RevisionSnippet.Text`→`Snippet`、`ImageInfo.UsedInParagraph`→`UsedBy`
- `DocxAnalysis.MergeDocx` 返回类型适配
- 修复 legacy 模式变量名冲突
- 新增 nested canonical `word add` 命令组：`word add paragraph/table/footnote/endnote/image/toc/xref/link/bookmark/comment/math`
- 保留 hyphen `word add-*` 兼容入口，但 `commands --json` 和文档以 canonical 命令为准
- `--spec` 支持 JSON 文件路径和 inline JSON，且 lower camelCase 可用
- `--after` 从 CLI 传到底层 add 操作；不存在 blockId 返回 E006
- `--after p0001` 改为按 WordSlice 语义段落 ID 定位，不再把 heading 当作 p0001
- `WordSlice` 将外链/内链分别分配 `link0001`/`xref0001`，不再使用未文档化的 `hl0001`
- `WordSlice` 识别 `w:r` 内的 OMML (`m:oMath`/`m:oMathPara`)，`word add math` 后 dissect 可得到 `m0001` equation block
- `--json` 下 System.CommandLine parse error 统一返回 JSON envelope + E003
- `word merge` 将 MergeResult.Warnings 写入 JSON issues
- `word merge` issue severity 统一为 lowercase `warning`
- 修复 `protect --mode invalid` -> E006、`embed-font` 非字体 -> E002
- 同步 `Cli/Common/Manifest.cs`、`CAPABILITY.md`、`Cli/AGENT.md`、`release-checklist.md`

## Verified: word dissect -o

```powershell
nong word dissect tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-slice --json
→ EXIT:0, 38 blocks, 0 warnings
→ 7 core files written
```

## Files Changed (20+)

### 新建
- `Docx/WordBlockIdMap.cs`
- `Docx/IImageAnalyzer.cs`
- `Cli/Adapters/WordImageAnalyzerAdapter.cs`
- `Cli.Tests/WordCommandTests.cs`

### 修改
- `Docx/NongMarkModels.cs` — schema 修正
- `Docx/WordSlice.cs` — 全量重写
- `Docx/WordReadModels.cs` — camelCase + 字段更新
- `Docx/OutlineReader.cs`, `ImageLister.cs`, `CommentReader.cs`, `RevisionReader.cs` — ID 对齐
- `Docx/FormatInferrer.cs` — 无逻辑变更
- `Docx/WordEditOperations.cs` — fix-order 扩展, protect/embed-font 校验前置
- `Docx/WordAddOperations.cs` — `--after`, stable ID, camelCase, 入参校验
- `Docx/DocxAnalysis.cs` — merge 返回 MergeResult
- `Cli/Commands/WordCommands.cs` — dissect -o 接线
- `Cli.Tests/Cli.Tests.csproj` — 添加 OpenXML NuGet

## Build/Test

```
dotnet build Cli/NongCli.csproj -c Release → 0 错误
dotnet test Cli.Tests/Cli.Tests.csproj -c Release → 58 PASS, 0 SKIP, 58 total
```

## Verified: canonical add/spec/after/error contract

```powershell
nong word add paragraph tests-output\05-docx\docx-basic.docx --spec tests-output\paragraph.json -o tests-output\stage15b-file-add-paragraph.docx --json
→ EXIT:0, command = "word add paragraph"

nong word add table tests-output\05-docx\docx-basic.docx --spec tests-output\table.json -o tests-output\stage15b-file-add-table.docx --json
→ EXIT:0, command = "word add table"

nong word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\stage15b-add-math-after-missing.docx --after missing0001 --json
→ EXIT:1, E006

nong word protect tests-output\05-docx\docx-basic.docx --mode invalid -o tests-output\stage15b-protect-invalid.docx --json
→ EXIT:1, E006

nong word embed-font tests-output\05-docx\docx-basic.docx CAPABILITY.md -o tests-output\stage15b-embed-invalid.docx --json
→ EXIT:1, E002
```

## Verified: final audit spot checks

```powershell
nong commands --json
→ 71 implemented, has canonical "word add paragraph", no "word add-paragraph" as command name

nong word dissect tests-output\05-docx\docx-basic.docx --output tests-output\stage15b-final-slice --json
→ EXIT:0, 38 blocks, 7 core files written

nong word add paragraph tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-missing-spec.docx --json
→ EXIT:1, command = "word add paragraph", E003

nong word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\stage15b-final-add-math.docx --json
nong word dissect tests-output\stage15b-final-add-math.docx -o tests-output\stage15b-final-math-slice --json
→ add returns m0001; content.jsonl contains kind=equation id=m0001

nong word merge tests-output\05-docx\docx-basic.docx tests-output\05-docx\docx-basic.docx -o tests-output\stage15b-final-merged.docx --json
→ EXIT:0, data.warnings populated, issues[*].severity = "warning"
```

## Secret Scan

```powershell
rg -n "PADDLEOCR_TOKEN\s*[:=]|PADDLEOCR_ACCESS_TOKEN\s*[:=]|sk-[A-Za-z0-9_-]{20,}|github_pat_|ghp_|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\." . -g "!bin" -g "!obj" -g "!tests-output"
→ no credential values found; matches were scanner/guidance regex literals only
```

## Known Remaining

No Stage 15b P0/P1 remains.

Known limitations:

1. OMML -> LaTeX is best-effort.
2. chemicalStructure schema is reserved; no SMILES/InChI inference.
3. merge still warns about headers/footers, numbering conflicts, and style-name conflicts.
4. ImageAnalyzer is local image structure analysis, not OCR.
5. PP-OCRv5 local OCR remains out of scope for Stage 15b.

## Open Risks

1. `WordSlice.Slice` 仍应在后续阶段追加更丰富 fixture（nested tables, headers/footers, images, comments, revisions）的长期回归样本。
2. OMML→LaTeX 仍为 best-effort；当前契约保证 equation block/ID 稳定，不保证完整 LaTeX 反编译。

## Next Recommended

Move the battlefield to higher-value work: Stage 16 OCR/多模态 can proceed on top of a working Stage 15b Word/NongMark command surface.
