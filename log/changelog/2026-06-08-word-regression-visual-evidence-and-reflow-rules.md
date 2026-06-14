# 2026-06-08 Word regression visual evidence and reflow rules

## 时间

2026-06-08

## 影响包

- `Angri450.Nong.Docx`
- `Angri450.Nong.Pandoc`
- `Angri450.Nong.Cli.Tests`

## 变更类型

- Word 可视排版质量回归
- `nong-pandoc/package/v1` slice 证据增强
- Word 能力矩阵同步

## 详细说明

- 同步 `Docx/WORD_CAPABILITY_MATRIX.md`，把化学式下标、文档网格冲突修复、重复表头、`format-audit` 证据和 `word table-reflow` 显式规则更新为真实状态。
- 将三份沸石工作站真实文档纳入 `Cli.Tests/TestAssets/WordRegression/academic-format/`：
  - `zeolite-workstation-handwritten.docx`
  - `zeolite-workstation-original.docx`
  - `zeolite-workstation-beautified.docx`
- 更新 `Cli.Tests/TestAssets/WordRegression/MANIFEST.md`，记录 SHA256、分类、预期路由和必查证据。
- `WordFormatAuditor` 增加从已打开 `WordprocessingDocument` 审计的重载，并补 `chemistry` 审计字段，用 run 级字符展开判断 `N2O`、`H2O2`、`O2-` 等公式数字是否真实 `w:vertAlign=subscript`。
- `word dissect` 的 `format.json.visualEvidence` 接入 `format-audit` 证据，新增/填充：
  - `headings`
  - `body`
  - `fonts`
  - `lineSpacing`
  - `tables`
  - `latinNames`
  - `chemistry`
  - `audit`
- 补真实文档回归测试链路：`academic-format -> validate -> format-audit -> dissect`。
- 真实文档额外检查三线表顶线/栏目线/底线线宽、无表格阴影、表格内无首行缩进、化学式数字下标、`settings.xml` 中 `m:mathPr` 周边顺序，以及 slice visual evidence。
- 明确 `word table-reflow` 的长表、宽表、跨页/续表规则：长表按行拆并重复表头，宽表按列组拆并重复左侧关键列，续表前段底线 0.75 pt、续页/续表顶线 1.5 pt、最终底线 1.5 pt。

## 样本 SHA256

| Asset | SHA256 |
|---|---|
| `academic-format/zeolite-workstation-handwritten.docx` | `6B072D5876F1A7CC2EB340A6F492A1DD9144C3F5C063BB908ED6BEDFE71B9A81` |
| `academic-format/zeolite-workstation-original.docx` | `13C3C924BAC235BECD8732D9277ED408B840F8C812375A9E9FB655DEBDE425C9` |
| `academic-format/zeolite-workstation-beautified.docx` | `8A43052E8DD49F83AB9F0F6B9065705DCBD66D752444F34CA3BBED14736FEA8F` |

## 验证

- `dotnet build .\Cli\NongCli.csproj -c Release`
  - 通过，0 warning，0 error。
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordFormatAudit|FullyQualifiedName~WordCommandTests.WordAcademicFormat|FullyQualifiedName~WordCommandTests.WordTableReflow|FullyQualifiedName~WordCommandTests.WordDissect"`
  - 通过，17 passed。
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests.Commands_Json|FullyQualifiedName~CliContractTests.WordRepairPlan|FullyQualifiedName~CliContractTests.SliceInspect_Strict|FullyQualifiedName~CliContractTests.SliceFormats_IncludeUnifiedVisualEvidence|FullyQualifiedName~CliContractTests.SliceBlocks|FullyQualifiedName~CliContractTests.SliceBlock|FullyQualifiedName~CliContractTests.SliceAssets"`
  - 通过，7 passed。

## 剩余风险

- `word table-reflow` 的跨页能力当前是显式 row-threshold 续表规则，不是自动分页/渲染引擎。后续若要按 Word 实际分页断点自动拆表，需要新增渲染或布局估算证据。
- 真实 Word/WPS 肉眼验收仍建议保留，自动证据覆盖的是 OOXML 可验证的排版质量。
