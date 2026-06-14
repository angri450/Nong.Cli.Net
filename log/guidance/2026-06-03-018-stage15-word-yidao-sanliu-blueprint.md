# 2026-06-03 阶段 15 指导：恢复 Word「一刀三流」招牌设计

本文件给 ClaudeCode 使用。目标是继续扩充 `Angri450.Nong`，但本阶段只抓 Word，把 Nong 的 Word 能力重新做成低 token、可组合、可审计的核心招牌。

## 0. 开工前状态门槛

阶段 14 的修复必须先完成并通过审计，不能把新功能堆在漂移的契约上。

开工前必须先跑：

```powershell
cd C:\Users\Administrator\Documents\Github\Nong.Cli.Net
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

阶段 14 必须确认：

```text
word validate: OpenXmlValidator 的 error 不得被归为 warning
ocr cloud --format: help 中声明的格式必须真实生效，或删除未实现格式
ocr local: manifest/status/错误码契约必须一致
pptx read: slide 顺序必须按 SlideIdList
word merge: 不得继续无提示浅合并复杂 docx
文档: CAPABILITY.md / Cli/AGENT.md / release-checklist.md 同步
```

如果上述任意项失败，先修阶段 14，不进入阶段 15。

## 1. 产品判断

现在 `CAPABILITY.md` 显示 Word 只有 11 个命令：

```text
read / preview / fill / rebuild / stats / fonts / styles / validate / extract / dissect / merge
```

历史规格 `changelog/2026-06-02-cli-word-spec.md` 设计的是 Word 读、查、改、写四类能力，标称 28 个命令。注意历史文档本身有一个计数问题：写类列出了 11 个 leaf command，但总数写成 10 个，所以实际是 28 个命令族或 29 个 leaf command。不要为了凑数字删掉 `add math`。本阶段按「Word 29 leaf commands」落地，文档中说明历史计数修正。

这不是普通 Office .NET 库的补功能。本阶段的核心产品形态是：

```text
一刀：一个稳定命令把 docx 切成可引用、可诊断、可重建的中间表示。
三流：内容流、结构流、格式/资产流。
```

这套设计服务 AI agent：

```text
模型不用再写 PowerShell 解 zip/xml。
模型不用一次读完整 docx。
模型可以拿 blockId 精确讨论、修复、追加内容。
模型可以用极少 token 判断文档内容、结构、格式和资产问题。
```

### 1.1 重要升级：Markdown 不是核心中间层

历史上 `content.md` 很有用，但现在项目已经合并/研究了 Pandoc 超级 Markdown、数学公式、化学方程式、化学结构式、多模态图片理解等能力，Word 的读/查/改/写不能再以 Markdown 为中心。

本阶段必须明确：

```text
Markdown / HTML / Pandoc AST 只能作为 import/export/preview 适配层。
Nong 内部核心中间表示必须是 JSON-first 的语义标记层。
```

建议命名：

```text
nongmark/v1
```

不要理解成另一个 Markdown 方言。它是面向 agent 调用的 JSON 标记方法：

```text
文本、文本对应格式、公式、化学内容、图片、表格、脚注、引用、批注、修订、资产识别结果全部结构化。
```

核心要求：

```text
1. content.md 只保留为人类预览和低 token 摘要，不是 canonical source。
2. content.jsonl / document.json / structure.json 才是 canonical source。
3. 所有读/查/改/写命令都要围绕 blockId + JSON patch/spec 设计。
4. 不允许把复杂内容退化成一段 Markdown 字符串后丢失类型信息。
5. Pandoc 可用于导入/导出 Markdown/HTML/LaTeX，但不能替代 nongmark/v1。
```

### 1.2 nongmark/v1 必须表达的对象

最小对象类型：

```text
paragraph
heading
run
table
image
figure
equation
chemEquation
chemicalStructure
footnote
endnote
citation
reference
hyperlink
bookmark
comment
revision
toc
field
rawOpenXmlRef
```

数学公式不能混在普通文本里：

```json
{
  "id": "m0001",
  "kind": "equation",
  "display": true,
  "latex": "E=mc^2",
  "ommlPresent": true,
  "source": "omml|latex|pandoc|manual",
  "textFallback": "E=mc^2"
}
```

化学方程式：

```json
{
  "id": "ce0001",
  "kind": "chemEquation",
  "text": "6CO2 + 6H2O -> C6H12O6 + 6O2",
  "normalized": "6 CO2 + 6 H2O -> C6H12O6 + 6 O2",
  "species": ["CO2", "H2O", "C6H12O6", "O2"],
  "source": "text|ocr|manual|pandoc",
  "confidence": null
}
```

化学结构式：

```json
{
  "id": "cs0001",
  "kind": "chemicalStructure",
  "representation": {
    "smiles": null,
    "inchi": null,
    "molfile": null,
    "imageAssetId": "img0003"
  },
  "source": "image|manual|pandoc",
  "confidence": null
}
```

文字和格式必须一一对应，不能只输出纯文本：

```json
{
  "id": "r0001",
  "kind": "run",
  "text": "氮肥处理显著提高产量",
  "format": {
    "styleId": "Normal",
    "fontEastAsia": "宋体",
    "fontAscii": "Times New Roman",
    "fontSizePt": 10.5,
    "bold": false,
    "italic": false,
    "underline": false,
    "color": null,
    "superscript": false,
    "subscript": false
  }
}
```

图片也必须结构化，不只保存文件路径：

```json
{
  "id": "img0001",
  "kind": "image",
  "file": "assets/image_001.png",
  "contentType": "image/png",
  "width": 1200,
  "height": 800,
  "usedBy": ["p0007"],
  "analysis": {
    "engine": "ImageAnalyzer",
    "netLocal": true,
    "whitespaceRatio": 0.62,
    "regions": [
      {"x": 10, "y": 20, "width": 200, "height": 60, "type": "Text"}
    ],
    "asciiMapFile": "assets/image_001.map.txt"
  },
  "ocr": {
    "engine": null,
    "status": "notRun|ok|dependencyMissing|error",
    "textBlocks": []
  }
}
```

### 1.3 OCR 和图片识别边界

当前已有：

```text
MultiModal/ImageAnalyzer.cs
```

这是纯 .NET + SkiaSharp 的本地图像结构分析，不依赖 Python、云端 token 或 PP-OCR。`word dissect -o` 提取图片时必须优先调用它，产出图片布局/区域识别结果。

当前 OCR 状态：

```text
ocr cloud: PaddleOCR-VL 云端，读取 PADDLEOCR_TOKEN 环境变量。
ocr local: 当前 CLI 仍是 E005，但 MultiModal/LocalOcrClient.cs 已有 Python 子进程版 PaddleOCR 客户端。
用户正在研究 PP-OCRv5 本地接入，PP-OCRv5 先只定义为文字识别，不负责版面理解。
```

阶段 15 不要求把 PP-OCRv5 接进 CLI，但必须为后续预留 schema：

```text
image.analysis: 本地 .NET 图像结构分析，永远可无 token 执行。
image.ocr: 文字识别结果，可来自 local PP-OCRv5 或 cloud PaddleOCR-VL。
layout: 云端 VL 可提供版面理解；本地 PP-OCRv5 只能提供文字框和置信度。
```

不要把 PP-OCRv5 说成已经实现。未接入 CLI 前仍然是 planned。

### 1.4 云端 OCR 密钥安全要求

现有代码应使用：

```text
PADDLEOCR_TOKEN 环境变量
```

安全规则：

```text
1. 不允许把 token 写进 repo、CAPABILITY.md、changelog、log、tests-output。
2. 不允许新增 --token 参数，除非明确把命令行泄露风险写进 help，并且默认推荐环境变量。
3. 不允许在 JSON 输出、异常消息、日志中打印 token。
4. ocr cloud 发现 token 缺失时返回 E005，message 只提示设置 PADDLEOCR_TOKEN。
5. 测试只能检查 token 是否存在，不能输出 token 内容。
6. 结果日志必须写明是否做了 secret scan，以及命中情况，不得粘贴密钥值。
```

开工前安全扫描：

```powershell
rg -n "PADDLEOCR_TOKEN\s*[:=]|sk-[A-Za-z0-9_-]{20,}|github_pat_|ghp_|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\." . -g "!bin" -g "!obj" -g "!tests-output"
```

如果发现真实密钥：

```text
1. 停止功能开发。
2. 不在日志中复制密钥值。
3. 记录文件路径和规则名。
4. 建议用户立即吊销/轮换对应 key。
5. 清理仓库历史另开安全任务，不混在阶段 15。
```

## 2. 「一刀三流」定义

### 2.1 一刀

扩展现有命令，不另起一个破坏性入口：

```powershell
nong word dissect <file.docx> -o <out-dir> --json
```

兼容要求：

```text
nong word dissect <file.docx> --json
```

必须继续可用，返回现有格式指纹摘要。新增 `-o/--output` 后进入完整「一刀三流」模式，写出目录型中间表示。

### 2.2 三流目录结构

`word dissect -o <out-dir>` 必须生成：

```text
<out-dir>/
  manifest.json          # 总清单，版本、源文件、hash、流文件、block 计数
  document.json          # nongmark/v1 canonical document model
  content.md             # 人类预览/低 token 摘要，不是 canonical source
  content.jsonl          # 一行一个 canonical block，适合流式/增量读取
  structure.json         # 标题树、段落、表格、脚注尾注、链接、书签、批注、修订、公式索引
  format.json            # 样式、字体、编号、页面、节、表格边框、格式指纹
  assets/
    manifest.json        # 图片/媒体/嵌入对象清单
    image_001.png
    image_002.jpg
```

如果文档没有图片，`assets/manifest.json` 仍然要存在，`items: []`。

### 2.3 blockId 规则

所有可引用对象必须有稳定 ID：

```text
p0001, p0002, ...
h0001, h0002, ...
t0001, t0002, ...
f0001, f0002, ...
e0001, e0002, ...
img0001, img0002, ...
c0001, c0002, ...
r0001, r0002, ...
m0001, m0002, ...
```

规则：

```text
ID 按文档出现顺序生成。
同一个 docx 多次 dissect，ID 必须稳定。
不要用 OpenXML rId 作为公开 ID，因为 rId 不够稳定。
结构流里保留原始 rId 只能作为 internal 字段。
```

### 2.4 三流 schema

`manifest.json`：

```json
{
  "schemaVersion": "nongmark/v1",
  "source": "paper.docx",
  "sourceSha256": "...",
  "createdAt": "2026-06-03T00:00:00+08:00",
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

`content.jsonl` 每行：

```json
{"id":"p0001","kind":"paragraph","runs":[{"id":"r0001","text":"...","format":{"styleId":"Normal"}}],"headingLevel":null}
{"id":"h0001","kind":"heading","runs":[{"id":"r0002","text":"材料与方法","format":{"styleId":"Heading1"}}],"headingLevel":1}
{"id":"t0001","kind":"table","caption":null,"rows":3,"cols":4,"cells":[["A","B"]]}
{"id":"m0001","kind":"equation","latex":"E=mc^2","display":true,"ommlPresent":true}
```

`structure.json`：

```json
{
  "outline": [
    {"id":"h0001","level":1,"text":"引言","blockId":"p0001"}
  ],
  "blocks": [
    {"id":"p0001","kind":"paragraph","textPreview":"...","styleId":"Normal","order":1}
  ],
  "tables": [
    {"id":"t0001","order":12,"rows":3,"cols":4,"cells":[["A","B"]]}
  ],
  "footnotes": [],
  "endnotes": [],
  "hyperlinks": [],
  "bookmarks": [],
  "comments": [],
  "revisions": [],
  "math": [],
  "chemEquations": [],
  "chemicalStructures": []
}
```

`format.json`：

```json
{
  "styles": [],
  "fonts": [],
  "numbering": {},
  "sections": [],
  "tables": [],
  "page": {},
  "warnings": []
}
```

`assets/manifest.json`：

```json
{
  "items": [
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
  ]
}
```

## 3. 本阶段命令目标

### 3.1 保持现有 11 个命令

现有命令不得退化：

```text
word read
word preview
word fill
word rebuild
word stats
word fonts
word styles
word validate
word extract
word dissect
word merge
```

其中：

```text
word dissect: 增加 -o/--output 完整三流模式
word merge: 必须改成关系安全的合并，至少复用 AdvancedFeatures.AppendDocument，并明确 warnings
```

### 3.2 补回历史缺口

本阶段补齐 Word 原设计中缺失的命令。

读：

```text
word outline <file.docx>
word images <file.docx> [-o <dir>]
word comments <file.docx>
word revisions <file.docx>
```

查：

```text
word infer-format <text>
```

改：

```text
word fix-order <file.docx> -o <out.docx>
word protect <file.docx> --mode readonly|comments|tracked|forms [-p <password>] -o <out.docx>
word embed-font <file.docx> <font-file.ttf> [--name <fontName>] -o <out.docx>
```

写：

```text
word add paragraph <file.docx> --spec <spec.json> [-o <out.docx>] [--after <blockId>]
word add table <file.docx> --spec <spec.json> [-o <out.docx>] [--after <blockId>]
word add footnote <file.docx> --text <text> [-o <out.docx>] [--after <blockId>]
word add endnote <file.docx> --text <text> [-o <out.docx>] [--after <blockId>]
word add image <file.docx> --src <image> [--caption <text>] [-o <out.docx>] [--after <blockId>]
word add toc <file.docx> [-o <out.docx>] [--after <blockId>]
word add xref <file.docx> --to <bookmark> --text <display> [-o <out.docx>] [--after <blockId>]
word add link <file.docx> --url <url> --text <display> [-o <out.docx>] [--after <blockId>]
word add bookmark <file.docx> --name <name> [-o <out.docx>] [--after <blockId>]
word add comment <file.docx> --text <text> [--author <name>] [-o <out.docx>] [--after <blockId>]
word add math <file.docx> --latex <latex> [--display] [-o <out.docx>] [--after <blockId>]
```

说明：

```text
-o 建议必填。为了保护用户文件，CLI 不应默认原地改 docx。
如果为了兼容历史规格允许省略 -o，必须默认生成 <input>.nong.docx，不能覆盖原文件。
--after <blockId> 可以先做 P1：如果暂时实现不了精确插入，必须返回 E006，不要假装成功。
```

## 4. 代码落点

优先新增 helper，不要把所有逻辑塞进 `WordCommands.cs`。

建议文件：

```text
Docx/WordSlice.cs              # 一刀三流核心模型和导出
Docx/NongMarkModels.cs         # nongmark/v1 JSON-first 语义标记模型
Docx/WordReadModels.cs         # outline/images/comments/revisions 结果模型
Docx/WordEditOperations.cs     # fix-order/protect/embed-font/add commands 的底层操作
Cli/Commands/WordCommands.cs   # 只做 CLI 参数、错误处理、JSON 包装
Cli/Common/Manifest.cs         # 命令发现
Cli/AGENT.md                   # agent 使用契约
CAPABILITY.md                  # 能力表
release-checklist.md           # 发布检查
Cli.Tests/WordCommandTests.cs  # Word 专项契约测试
```

可复用现有类：

```text
Docx/WordTextReader.cs
Docx/DocxAnalysis.cs
Docx/DocumentWriter.cs
Docx/AdvancedFeatures.cs
Docx/ElementOrder.cs
Docx/ImageEmbedder.cs
Docx/MathRenderer.cs
Docx/StyleRebuilder.cs
Docx/WordPreview.cs
Docx/DocxTemplate.cs
MultiModal/ImageAnalyzer.cs
```

禁止：

```text
不要用 PowerShell 正则解析 document.xml。
不要把 OpenXML raw XML 大段塞进 JSON。
不要把 Markdown 当 canonical source。
不要把公式、化学方程式、化学结构式退化成普通字符串。
不要忽略 run-level 格式。
不要在日志或 JSON 中打印任何 OCR token/API key。
不要为了通过 build 把命令登记成 implemented 但实际返回 E009。
不要修改 Nong.Toolkit.Net。本阶段只做 Angri450.Nong。
不要新增 net10/net11 依赖。目标仍是 net8.0。
不要在没有 artifact 检查的情况下返回 ok。
```

## 5. 多 agent 并行方案

为减少主对话上下文，ClaudeCode 使用多 agent，但必须避免多人同时改同一个共享文件。

### Coordinator

职责：

```text
1. 先确认阶段 14 gate 通过。
2. 建立最终命令清单。
3. 统一合并各 agent 产出的 helper API。
4. 最后集中修改 WordCommands.cs、Manifest.cs、CAPABILITY.md、Cli/AGENT.md。
5. 写 changelog/2026-06-03-018-stage15-word-yidao-sanliu-result.md。
```

Coordinator 才能改：

```text
Cli/Commands/WordCommands.cs
Cli/Common/Manifest.cs
CAPABILITY.md
Cli/AGENT.md
release-checklist.md
```

### Agent A：历史规格恢复

输入：

```text
CAPABILITY.md
changelog/2026-06-02-cli-word-spec.md
changelog/2026-06-02-cli-master-summary.md
log/guidance/2026-06-03-014-full-stub-completion-blueprint.md
```

输出：

```text
docs 或 changelog 中的 Word 命令对照表：
历史设计 -> 当前实现 -> 本阶段实现 -> 仍保留 planned
```

必须指出：

```text
历史 28 计数和 add math 清单不一致，本阶段按 29 leaf commands 处理。
```

### Agent B：一刀三流核心

实现：

```text
Docx/WordSlice.cs
Docx/NongMarkModels.cs
```

职责：

```text
1. 从 docx 生成 manifest/document/content/structure/format/assets。
2. 生成稳定 blockId。
3. 设计并实现 nongmark/v1 JSON-first 模型。
4. 提取图片并记录 contentType、文件名、尺寸、usedBy。
5. 调用 MultiModal/ImageAnalyzer.cs 分析每张图片，写入 assets manifest。
6. 提取 outline、paragraph、run-level format、table、footnote、endnote、hyperlink、bookmark、comment、revision、math 的索引。
7. 为化学方程式和化学结构式预留 schema；能识别简单文本化学方程式时填充 chemEquation，不能识别时不要乱猜。
8. 输出 JSON 使用 camelCase。
9. 输出文本限制为低 token，不要复制整包 XML。
```

必须处理的对象：

```text
paragraph / heading / run
table / image / figure
equation / chemEquation / chemicalStructure
footnote / endnote / hyperlink / bookmark / comment / revision
```

不要求本阶段完成：

```text
OMML -> 高保真 LaTeX 逆转换
图片化学结构式 -> SMILES/InChI 自动识别
PP-OCRv5 CLI 接入
Pandoc AST 全量互转
```

但 schema 必须预留这些字段，不能让后续再推翻结构。
```

验收：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect sample.docx -o tests-output\word-slice --json
Test-Path tests-output\word-slice\manifest.json
Test-Path tests-output\word-slice\document.json
Test-Path tests-output\word-slice\content.md
Test-Path tests-output\word-slice\content.jsonl
Test-Path tests-output\word-slice\structure.json
Test-Path tests-output\word-slice\format.json
Test-Path tests-output\word-slice\assets\manifest.json
```

### Agent C：读和查命令

实现 helper：

```text
Docx/WordReadModels.cs
```

实现命令底层：

```text
outline
images
comments
revisions
infer-format
```

要求：

```text
outline: 识别 Heading1/Heading2/Heading3，也兼容 outlineLvl。
images: 不只数 ImageParts，要尽量记录文档中引用位置。-o 存在时提取文件。
comments: 读取 WordprocessingCommentsPart；可选 anchor 文本，拿不到就留 null。
revisions: 统计 ins/del/moveFrom/moveTo，输出 snippet，不输出整篇。
infer-format: 中文格式描述到 OpenXML 参数，先做常见规则即可，但 malformed/空输入返回 E006。
```

### Agent D：改命令和 merge 强化

实现 helper：

```text
Docx/WordEditOperations.cs
```

职责：

```text
fix-order: 复制 input 到 output，再对 main document/styles/numbering/settings 等可处理 part 执行 ElementOrder.RectifyTree 和 FixOrphanBorders。
protect: 复制 input 到 output，调用 AdvancedFeatures.ProtectDocument。
embed-font: 校验 font 文件存在且后缀 .ttf/.otf，复制 input 到 output，调用 AdvancedFeatures.EmbedFont。
merge: 替换当前浅合并，优先使用 AdvancedFeatures.AppendDocument；保留 warnings 说明仍不能保证所有复杂对象完美合并。
```

命令输出必须包含：

```json
{
  "artifacts": {"docx": "..."},
  "metrics": {"fixedElements": 0},
  "issues": []
}
```

### Agent E：写命令 add group

实现：

```text
word add paragraph/table/footnote/endnote/image/toc/xref/link/bookmark/comment/math
```

底层优先复用：

```text
DocumentWriter
AdvancedFeatures.InsertComment
ImageEmbedder
MathRenderer
TocAndChartBuilder
```

统一规则：

```text
1. 先复制 input 到 output，再打开 output 修改。
2. output 不得等于 input。
3. 生成后必须 CheckArtifact。
4. malformed JSON spec 返回 E006。
5. 缺文件返回 E001。
6. 不支持的图片格式返回 E002。
7. --after 未实现时返回 E006，不能忽略。
8. JSON command 字段必须等于真实命令，如 "word add table"。
```

Spec 最小定义：

`add paragraph`：

```json
{
  "text": "正文",
  "style": "Normal",
  "bold": false,
  "italic": false
}
```

`add table`：

```json
{
  "caption": "表1 处理均值",
  "headers": ["处理", "株高"],
  "rows": [["A", "12.3"], ["B", "15.1"]]
}
```

### Agent F：测试和审计

新增或更新：

```text
Cli.Tests/WordCommandTests.cs
```

测试覆盖：

```text
commands --json 只列 implemented
commands --all --json 列完整状态
word dissect -o 输出 6 个核心文件
word outline JSON shape
word images 无图和有图都能稳定返回
word comments/revisions 无批注修订时返回空数组 EXIT:0
word fix-order 输出 docx 且 validate 通过
word protect 输出 docx
word merge 输出 docx 且 validate 通过
word add paragraph/table/math 输出 docx 且 word read 可读到新增文本
malformed JSON spec -> E006 + EXIT:1
missing input -> E001 + EXIT:1
output == input -> E006 + EXIT:1
JSON command 字段逐项正确
```

字体嵌入 happy path 可以使用：

```text
C:\Windows\Fonts\arial.ttf
C:\Windows\Fonts\simhei.ttf
```

如果测试机不存在这些字体，记录为 skipped with reason，但必须测试 missing font -> E001。

## 6. CLI JSON 约束

所有新命令必须符合现有 schema：

```json
{
  "status": "ok",
  "command": "word outline",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": {},
  "metrics": {},
  "errors": [],
  "meta": {"durationMs": 0, "version": "3.1.0"}
}
```

错误必须：

```text
status: error
errors[0].code: E001/E002/E003/E004/E005/E006/E007/E008/E009
EXIT:1
```

生成类命令必须：

```text
1. EnsureParentDir(output)
2. input/output 同路径守卫
3. 执行写入
4. CheckArtifact(output, "DOCX" 或具体类型)
5. 再返回 status:ok
```

读/查命令新增约束：

```text
1. word read 可以继续输出纯文本，但 --json 必须逐步升级为 run-aware，不只给 text。
2. word dissect -o 必须是 nongmark/v1 的 canonical 输出。
3. word outline/images/comments/revisions 的 data 必须引用 blockId/assetId。
4. 数学、化学、图片识别结果必须保留 type/source/confidence/status。
5. 不确定就返回 null/confidence，不要编造识别结果。
```

## 7. CAPABILITY.md 更新目标

阶段完成后，`CAPABILITY.md` 的 Word 部分应变成：

```text
word: 29 leaf commands

read:
  read, outline, stats, fonts, styles, images, comments, revisions
check:
  preview, validate, dissect, infer-format
modify:
  rebuild, fix-order, fill, merge, protect, embed-font
write:
  add paragraph, add table, add footnote, add endnote, add image,
  add toc, add xref, add link, add bookmark, add comment, add math
```

核心工作流增加：

```powershell
nong word dissect paper.docx -o paper.slice --json
Get-Content paper.slice\content.md
Get-Content paper.slice\document.json
Get-Content paper.slice\structure.json
Get-Content paper.slice\format.json
```

说明文字必须明确：

```text
Word 一刀三流是 Nong 的招牌工作流。
一刀是 word dissect -o。
三流是 content / structure / format-assets。
canonical 标记层是 nongmark/v1 JSON，不是 Markdown。
```

## 8. 验收命令

ClaudeCode 完成后必须在结果日志粘贴这些命令的结果摘要。

```powershell
cd C:\Users\Administrator\Documents\Github\Nong.Cli.Net
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release

dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json

dotnet .\Cli\bin\Release\net8.0\nong.dll word dissect tests-output\sample.docx -o tests-output\word-slice --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word outline tests-output\sample.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word images tests-output\sample.docx -o tests-output\word-images --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word comments tests-output\sample.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word revisions tests-output\sample.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word infer-format "黑体 四号 居中 固定行距28磅" --json

dotnet .\Cli\bin\Release\net8.0\nong.dll word fix-order tests-output\sample.docx -o tests-output\fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word protect tests-output\sample.docx --mode readonly -o tests-output\protected.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word merge tests-output\a.docx tests-output\b.docx -o tests-output\merged.docx --json

dotnet .\Cli\bin\Release\net8.0\nong.dll word add paragraph tests-output\sample.docx --spec tests-output\paragraph.json -o tests-output\add-paragraph.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add table tests-output\sample.docx --spec tests-output\table.json -o tests-output\add-table.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word add math tests-output\sample.docx --latex "E=mc^2" -o tests-output\add-math.docx --json
```

必须额外检查：

```powershell
Test-Path tests-output\word-slice\manifest.json
Test-Path tests-output\word-slice\document.json
Test-Path tests-output\word-slice\content.md
Test-Path tests-output\word-slice\content.jsonl
Test-Path tests-output\word-slice\structure.json
Test-Path tests-output\word-slice\format.json
Test-Path tests-output\word-slice\assets\manifest.json

dotnet .\Cli\bin\Release\net8.0\nong.dll word validate tests-output\fixed.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word validate tests-output\merged.docx --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word read tests-output\add-paragraph.docx --json
```

## 9. 结果日志

完成后写：

```text
changelog/2026-06-03-018-stage15-word-yidao-sanliu-result.md
```

必须包含：

```text
Goal:
Stage14 gate result:
Implemented commands:
Word command count before/after:
One-cut three-stream output files:
Files changed:
Tests added:
Commands run:
Build/test result:
Known limitations:
Open risks:
Secret scan result:
Next recommended stage:
```

Known limitations 不许空着。至少评估：

```text
merge 对复杂域、页眉页脚、编号冲突、样式同名冲突的处理边界
comments/revisions anchor 能否精确定位
math OMML 与 LaTeX 的互转边界
chemEquation / chemicalStructure 当前能识别到什么程度，哪些只是 schema 预留
ImageAnalyzer 只能做本地图像结构分析，不等同 OCR
PP-OCRv5 本地接入仍是 planned，当前 CLI 不得谎称支持
PADDLEOCR_TOKEN 是否只从环境变量读取，是否确认未写入仓库
--after blockId 是否完整实现
embed-font 在不同 Office/WPS 中是否可见
```

## 10. 给审计者的检查点

审计时不要只看 ClaudeCode 的结果日志。重点查：

```text
1. word dissect -o 是否真的写出 manifest/document/content/structure/format/assets。
2. document.json 是否声明 schemaVersion = nongmark/v1。
3. content.jsonl 是否一行一个 block，且 id 稳定。
4. content.md 是否只是 preview，不是唯一 source。
5. run-level format 是否保留。
6. math/chemEquation/chemicalStructure 是否结构化，不是全部混成普通文本。
7. assets manifest 是否包含 ImageAnalyzer 本地分析结果。
8. OCR token 是否没有出现在代码、日志、JSON、测试输出中。
9. Manifest.cs 是否把所有新增命令登记为 implemented。
10. WordCommands.cs 的 JsonOutput.Ok / WriteError command 名是否逐项正确。
11. 所有生成命令是否检查 input/output 同路径。
12. 所有生成命令是否 CheckArtifact。
13. malformed JSON spec 是否 E006，不是 E004。
14. output dir 不存在是否自动创建。
15. commands --json 是否不含 stub。
16. CAPABILITY.md / Cli/AGENT.md / release-checklist.md 是否同步。
```

本阶段完成标准不是「命令数变多」，而是：

```text
一个 agent 可以用 nong word dissect -o 把任意 docx 切开，
只读 content/structure/format/assets 清单就能判断下一步，
再用 word add / rebuild / fix-order / merge 等命令做确定性修改。
```
