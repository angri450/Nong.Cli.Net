# 2026-06-04 阶段 16 指导：OCR 基础设施与一刀三流合龙

本文件给 ClaudeCode 使用。目标是研究并设计 Nong 的 OCR/多模态基础设施，不设计 skill，不修改 GroundPA。参考资料：

```text
C:\Users\Administrator\Documents\Github\测试文件夹\office-skill-research\PaddleOCR-main\PADDLEOCR_NET_INTEGRATION.md
Angri450.Nong\log\guidance\2026-06-03-018-stage15-word-yidao-sanliu-blueprint.md
Angri450.Nong\changelog\2026-06-02-cli-ocr-spec.md
Angri450.Nong\MultiModal\
Angri450.Nong\Cli\Commands\OcrCommands.cs
```

## 0. 产品结论

PaddleOCR 的 Agent Skills 部分不重要。本项目不是在设计 skill，而是在设计 Nong 的基础设施。

本阶段结论：

```text
本地：PP-OCRv5，面向纯文字识别，优先离线、轻量、可包化、可缓存。
云端：PaddleOCR-VL-1.6，面向最强文档解析和版面理解，只保留这个模型。
图片结构：MultiModal/ImageAnalyzer.cs，纯 .NET 本地分析，永远不需要 token。
中间表示：必须对齐 nongmark/v1、一刀三流、读/查/改/写。
```

不要再引入 PP-StructureV3、PaddleOCR-VL-1.5、旧 PaddleOCR-VL 等云端模型。等 Paddle 团队发布更强的大模型后，后续另开阶段同步。

## 1. 本地 PP-OCRv5 的路线

### 1.1 目标

用户不应该为了用本地文字识别而手工装 Python、PaddlePaddle、PaddleOCR、模型文件。推广环境里硬盘、网速、网络连通性都不稳定，所以默认目标是：

```text
dotnet tool install Angri450.Nong.Cli
nong ocr local image.png -o out --json
```

或者在 .NET 包里：

```csharp
var ocr = new PpOcrV5Client();
var result = await ocr.RecognizeAsync("image.png");
```

用户视角应尽量接近“引用包即可用”。如果模型体积和 NuGet 限制不允许把模型直接塞进主包，则采用模型资源包或首次运行下载缓存，但必须有清晰策略，不得让用户自己翻 Paddle 文档。

### 1.2 技术路线

PP-OCRv5 不是大模型，是传统 OCR pipeline：

```text
PP-OCRv5_mobile_det  # 文本检测，约 2-5 MB
PP-OCRv5_mobile_rec  # 文本识别，约 10-15 MB
可选 cls             # 方向分类器
```

Nong 的本地方案必须严肃考虑：

```text
1. .NET 8+
2. CPU-first
3. Windows 优先，但不能写死 Windows
4. 不要求 CUDA
5. 不依赖 Python 作为最终路线
6. 模型小、首次使用简单、错误可解释
```

推荐路线：

```text
Microsoft.ML.OnnxRuntime + PP-OCRv5 ONNX 模型 + C# 预处理/后处理
```

不推荐最终路线：

```text
Python 子进程 + pip install paddleocr
```

现有 `MultiModal/LocalOcrClient.cs` 可以作为兼容/临时 fallback，但不能作为 2.0 基础设施目标。

### 1.3 包化策略

ClaudeCode 必须先写设计和体积评估，再动代码。

候选方案：

```text
方案 A：主包内置 mobile det/rec ONNX
优点：最简单，用户装 CLI 就能用。
缺点：NuGet 包变大；不同平台 native runtime 可能增加体积。

方案 B：单独模型资源包 Angri450.Nong.Ocr.PpOcrV5.Models
优点：主包轻；模型版本可独立升级。
缺点：用户/CLI 需要自动解析模型包路径。

方案 C：首次运行自动下载到用户缓存目录
优点：主包最小。
缺点：网速差/无网环境体验差；需要 checksum、断点/失败处理。

方案 D：允许用户手工指定模型目录
优点：适合高级用户和内网部署。
缺点：不能作为默认推广体验。
```

推荐默认：

```text
短期：B + D
中期：B 为主，C 作为可选自动修复
```

即：

```text
1. 发布一个模型资源包，包含 det/rec/字典/yml/checksum。
2. CLI 优先查模型资源包/随包目录。
3. 查不到时再查用户缓存。
4. 仍查不到时返回 E005，并给出 `nong ocr install-model pp-ocrv5-mobile` 指令。
5. 支持 `--model-dir <dir>` 给高级用户。
```

模型缓存目录建议：

```text
Windows: %LOCALAPPDATA%\Angri450.Nong\models\pp-ocrv5-mobile\
Linux/macOS: ~/.cache/angri450.nong/models/pp-ocrv5-mobile/
```

模型文件结构建议：

```text
pp-ocrv5-mobile/
  manifest.json
  det/
    inference.onnx
    inference.yml
  rec/
    inference.onnx
    inference.yml
    dict.txt
  cls/
    inference.onnx
    inference.yml
  checksums.sha256
```

`manifest.json`：

```json
{
  "schemaVersion": "nong-ocr-model/v1",
  "modelId": "pp-ocrv5-mobile",
  "engine": "onnxruntime",
  "version": "2026-06-04",
  "tasks": ["textDetection", "textRecognition"],
  "cloud": false,
  "sizeBytes": 0,
  "files": [
    {"path": "det/inference.onnx", "sha256": "..."},
    {"path": "rec/inference.onnx", "sha256": "..."},
    {"path": "rec/dict.txt", "sha256": "..."}
  ]
}
```

## 2. 云端 PaddleOCR-VL-1.6 路线

### 2.1 只保留最强模型

云端只考虑：

```text
PaddleOCR-VL-1.6
```

原因：

```text
1. 这是当前最强文档解析模型。
2. 云端调用成本高、需要 token，应只用于本地 PP-OCRv5 解决不了的版面理解场景。
3. 多模型选择会增加 agent 判断成本和维护成本。
```

不要暴露 `--model` 让 agent 乱选。内部常量固定为 `PaddleOCR-VL-1.6`。

### 2.2 认证变量迁移

参考 PaddleOCR 官方方案，默认环境变量是：

```text
PADDLEOCR_ACCESS_TOKEN
```

现有 Nong 代码使用：

```text
PADDLEOCR_TOKEN
```

阶段 16 要做兼容迁移：

```text
1. 优先读取 PADDLEOCR_ACCESS_TOKEN。
2. 兼容读取 PADDLEOCR_TOKEN。
3. 若只发现 PADDLEOCR_TOKEN，返回 warning/issue 提醒迁移。
4. 文档统一推荐 PADDLEOCR_ACCESS_TOKEN。
5. 不允许在日志、JSON、异常中打印 token 值。
```

缺 token：

```text
status:error
errors[0].code:E005
EXIT:1
message: "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN."
```

### 2.3 云端输出要合龙一刀三流

当前云端结果是 Markdown + 图片，这不够。必须转换为 Nong 的结构：

```text
PaddleOCR-VL JSONL
  -> OcrDocumentResult
  -> nongmark/v1 document.json
  -> content/structure/format-assets 三流
  -> 可选 Word 重建
```

云端结果中的 `markdown.text` 只能作为预览/辅助，不是 canonical source。

必须保留：

```text
page
block_label
block_content
block_bbox
block_polygon_points
block_order
markdown image mapping
layout visualization image
visualization image
```

映射到 nongmark/v1：

```text
doc_title / paragraph_title -> heading
text -> paragraph
table -> table
formula / equation -> equation
image / chart -> image/figure
vision_footnote -> footnote
unknown label -> block(kind="unknown", sourceLabel=...)
```

坐标必须保留到 `source.layout`：

```json
{
  "id": "p0001",
  "kind": "paragraph",
  "runs": [{"id": "r0001", "text": "示例", "format": {}}],
  "source": {
    "engine": "PaddleOCR-VL-1.6",
    "page": 1,
    "label": "text",
    "bbox": [10, 20, 200, 40],
    "polygon": null,
    "confidence": null
  }
}
```

## 3. CLI 命令设计

### 3.1 本地文字识别

```powershell
nong ocr local <image> -o <out-dir> --json
```

用途：

```text
本地 PP-OCRv5，纯文字检测+识别。
```

支持：

```text
--lang zh|en|auto       # 第一版可只支持 zh/en，默认 zh
--model-dir <dir>       # 高级用户指定模型目录
--format json|txt       # 默认 json
--analyze-image         # 同时跑 ImageAnalyzer
```

输出目录：

```text
<out-dir>/
  ocr.json              # canonical OCR result
  text.txt              # 纯文本预览
  image-analysis.json   # 如果 --analyze-image
```

JSON schema：

```json
{
  "schemaVersion": "nong-ocr/v1",
  "engine": "PP-OCRv5",
  "mode": "local",
  "modelId": "pp-ocrv5-mobile",
  "pages": [
    {
      "page": 1,
      "width": 1200,
      "height": 800,
      "textBlocks": [
        {
          "id": "ocr0001",
          "text": "第一行文字",
          "confidence": 0.98,
          "polygon": [[10,20],[200,20],[200,40],[10,40]],
          "bbox": [10,20,200,40]
        }
      ]
    }
  ]
}
```

### 3.2 本地模型状态

```powershell
nong ocr check-env --json
nong ocr models --json
nong ocr install-model pp-ocrv5-mobile --json
```

`check-env` 不再只检查 Python。它要检查：

```text
ONNX Runtime 可用性
模型目录是否存在
模型 checksum 是否匹配
是否存在 legacy Python fallback
```

`install-model` 第一版可以只做：

```text
如果自动下载暂未实现，返回 E009 或 E005？
```

建议：

```text
如果命令已注册但下载逻辑未实现，返回 E009。
如果下载逻辑已实现但网络失败，返回 E005 或 E007，按错误原因区分。
```

### 3.3 本地图像结构分析

```powershell
nong ocr analyze-image <image> -o <out-dir> --json
```

用途：

```text
纯 .NET ImageAnalyzer；不做文字 OCR，不需要 token。
```

输出：

```text
image-analysis.json
image.map.txt
```

### 3.4 云端文档解析

```powershell
nong ocr cloud <file-or-url> -o <out-dir> --json
```

用途：

```text
PaddleOCR-VL-1.6 文档解析，处理 PDF/图片，输出 nongmark/v1 + 预览 Markdown + 图片资产。
```

支持：

```text
--pages "1-5,10"
--poll 5
--timeout 600
--url                 # 如果输入是 URL，也可自动识别
--to-word <docx>      # 可选，调用 LayoutToWordConverter 或 Word writer
```

不要支持：

```text
--model
--token
```

输出目录：

```text
<out-dir>/
  manifest.json
  document.json        # nongmark/v1 canonical
  content.md           # 预览
  content.jsonl
  structure.json
  assets/
    manifest.json
    ...
  raw/
    result.jsonl       # 可选保存，默认可保存用于审计；不得含 token
```

### 3.5 OCR 到 Word

```powershell
nong ocr to-word <file> -o <docx> --json
```

要求：

```text
1. 默认使用云端 PaddleOCR-VL-1.6，因为它有版面和表格理解。
2. 只本地 PP-OCRv5 的纯文字结果不能声称 layout-preserving。
3. 如果只用本地文字识别生成 Word，命令必须叫清楚，例如后续 `ocr local-to-word` 或 `--mode plain-text`。
```

本阶段可以先不实现 `to-word`，但要把边界写清楚。

## 4. 读/查/改/写对齐

OCR 也要对齐 Nong 的读/查/改/写，不是单独的工具岛。

### 读

```text
ocr local
ocr cloud
ocr analyze-image
```

读的结果必须低 token、结构化：

```text
textBlocks
layoutBlocks
imageRegions
assets
```

### 查

```text
ocr check-env
ocr models
ocr validate-result
```

查的问题包括：

```text
模型缺失
checksum 不一致
token 缺失
云端 job 失败
空白图片
低置信度文本块
```

### 改

```text
ocr normalize
ocr crop
ocr deskew
```

阶段 16 不要求实现这些命令，但 schema 要允许后续把图片预处理结果接入。

### 写

```text
ocr to-word
ocr export
```

写必须从 `nong-ocr/v1` 或 `nongmark/v1` 走，不要从 Markdown 字符串直接硬转。

## 5. 与一刀三流合龙

OCR 输出要能直接进入 Word 一刀三流：

```text
扫描件/PDF/image
  -> nong ocr cloud/local
  -> nong-ocr/v1
  -> nongmark/v1 document.json
  -> content / structure / format-assets
  -> word rebuild / add / merge / to-word
```

本地 PP-OCRv5 的限制：

```text
只有文字框和识别文本，不能可靠生成复杂 Word 版面。
适合：截图文字、标签、简单扫描件、敏感文档离线识别。
```

云端 PaddleOCR-VL-1.6 的定位：

```text
复杂 PDF、论文截图、表格、公式、图表、印章、版面解析。
```

调度建议：

```text
1. 默认敏感/纯文字/单图：ocr local。
2. 需要表格/公式/图表/版面：ocr cloud。
3. 没有 token 或用户离线：不得自动上云，返回 E005 或建议 local。
4. PP-OCRv5 能本地解决的，不要引导上云。
```

## 6. 代码落点

新增/调整建议：

```text
MultiModal/PpOcrV5/
  PpOcrV5Client.cs
  PpOcrV5Options.cs
  PpOcrV5ModelResolver.cs
  PpOcrV5Detector.cs
  PpOcrV5Recognizer.cs
  PpOcrV5PostProcessor.cs
  ModelManifest.cs

MultiModal/OcrDocument.cs       # nong-ocr/v1 + nongmark bridge models
MultiModal/OcrModelManager.cs   # models/check-env/install-model
MultiModal/PaddleOcrVlClient.cs # token env rename + structured output
Cli/Commands/OcrCommands.cs
Cli/Common/Manifest.cs
Cli/AGENT.md
CAPABILITY.md
release-checklist.md
```

NuGet/项目引用：

```text
Microsoft.ML.OnnxRuntime
YamlDotNet 或现有 YAML 解析能力
SkiaSharp 已在 ThirdParty/现有依赖中使用
```

注意：

```text
不要把 onnxruntime GPU 包默认塞进去。
不要把大模型权重塞进主 CLI 包。
不要引入 net10/net11 目标。
不要让模型下载在 build/test 阶段自动发生。
```

## 7. 多 agent 分工

### Coordinator

职责：

```text
1. 不实现 skill。
2. 先写体积/包化/命令契约设计。
3. 决定本阶段是否只做设计，还是实现 check-env/analyze-image/cloud structured 修复。
4. 统一更新文档和 CAPABILITY。
5. 写 changelog/2026-06-04-019-stage16-ocr-infrastructure-result.md。
```

### Agent A：PP-OCRv5 ONNX 可行性

输入：

```text
PADDLEOCR_NET_INTEGRATION.md
paddleocr-js packages/core/src/models/det.ts
paddleocr-js packages/core/src/models/rec.ts
deploy/paddle2onnx/readme.md
```

输出：

```text
det/rec/cls 的 C# 预处理、后处理、字典、模型文件需求。
ONNX Runtime 包大小和 native runtime 风险。
模型包化建议。
```

### Agent B：云端 VL 合龙 NongMark

输入：

```text
MultiModal/PaddleOcrVlClient.cs
MultiModal/OcrModels.cs
阶段15 nongmark/v1 指导
```

输出：

```text
PaddleOCR-VL JSONL -> nongmark/v1 映射表。
cloud 输出目录结构。
token env 迁移方案。
错误码映射。
```

### Agent C：CLI 命令契约

输出：

```text
ocr local/check-env/models/install-model/analyze-image/cloud/to-word 命令草案。
每个命令的 input/options/output/errors/exit code。
```

### Agent D：测试与安全

输出：

```text
无模型环境下 check-env 行为。
无 token 下 cloud E005。
token 不泄露测试。
模型缺失 E005。
malformed result JSONL E006/E007。
artifact 检查。
```

## 8. 本阶段推荐实际开发范围

为了不把阶段 15 Word NongMark 和阶段 16 OCR 同时炸开，建议阶段 16 先做“设计 + 小修”，不要直接手搓完整 PP-OCRv5 后处理。

推荐范围：

```text
1. 写 OCR 基础设施设计文档。
2. 修正云端 token 环境变量：PADDLEOCR_ACCESS_TOKEN 优先，PADDLEOCR_TOKEN 兼容。
3. 新增 `ocr analyze-image`，接入现有 ImageAnalyzer。
4. 新增 `ocr check-env`，报告 cloud/local/imageAnalyzer/model 状态。
5. `ocr local` 保持 E005 或改成模型缺失 E005，但文案指向 PP-OCRv5 model package，不再指向 pip install。
6. `ocr cloud` 输出 structured result 路线，至少不再只统计 markdown blocks。
```

暂缓：

```text
1. 完整 ONNX det/rec 推理。
2. 自动下载模型。
3. PP-OCRv5 模型 NuGet 包发布。
4. OCR to Word 高保真重建。
```

如果用户明确要求火力全开实现 PP-OCRv5，则必须先完成 Agent A 的可行性报告和模型体积/分发决策。

## 9. 错误码契约

```text
E001 file_not_found       输入图片/PDF/模型文件不存在
E002 unsupported_format   输入格式不支持
E003 missing_argument     缺必要参数
E004 internal_error       未预期异常
E005 dependency_missing   token 缺失、模型缺失、ONNX Runtime 不可用
E006 validation_failed    选项非法、模型 manifest/checksum 不合法
E007 read_failed          图片/PDF/JSONL 读取失败
E008 write_failed         输出文件/目录写入失败
E009 not_implemented      已注册但未实现的命令
```

关键：

```text
模型缺失不是 E009，是 E005。
PP-OCRv5 推理代码不存在才是 E009。
云端 token 缺失是 E005。
云端 HTTP 401/403 是 E005 或 E007？建议 E005，message 不含 token。
云端 429 是 E005 dependency/rate limit 或新增 issue severity warning；不要 E004。
```

## 10. 验收命令

设计阶段至少跑：

```powershell
cd C:\Users\Administrator\Documents\Github\Angri450.Nong
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release

dotnet .\Cli\bin\Release\net8.0\nong.dll ocr check-env --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr analyze-image tests-output\sample.png -o tests-output\ocr-image --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr local tests-output\sample.png -o tests-output\ocr-local --json
dotnet .\Cli\bin\Release\net8.0\nong.dll ocr cloud tests-output\sample.png -o tests-output\ocr-cloud --json
```

预期：

```text
check-env: EXIT 0，报告 imageAnalyzer ok；local model 可能 missing；cloud token set/missing 不打印值。
analyze-image: EXIT 0，输出 image-analysis.json 和 map。
local: 如果 PP-OCRv5 未实现/模型缺失，必须 E005 或 E009，契约明确。
cloud: 无 token 时 E005 + EXIT 1；不得打印 token。
```

## 11. 结果日志

完成后写：

```text
changelog/2026-06-04-019-stage16-ocr-infrastructure-result.md
```

必须包含：

```text
Goal:
Read references:
Architecture decision:
Local PP-OCRv5 packaging decision:
Cloud PaddleOCR-VL-1.6 decision:
Commands designed/implemented:
Files changed:
Build/test result:
Secret scan result:
Known limitations:
Next recommended stage:
```

Known limitations 必须评估：

```text
ONNX Runtime native 包体积
PP-OCRv5 模型分发方式
无网环境体验
首次运行下载是否可接受
云端 token 环境变量迁移
本地纯文字 OCR 与云端版面理解的边界
nongmark/v1 合龙程度
```

## 12. 审计检查点

审计时重点查：

```text
1. 是否误把 PaddleOCR Skills 当成 GroundPA skill 任务。
2. 是否引入了非 PaddleOCR-VL-1.6 的云端模型选择。
3. 是否继续推荐用户 pip install paddleocr 作为主路线。
4. 是否把 token 写进代码、日志、JSON、测试输出。
5. 是否优先读取 PADDLEOCR_ACCESS_TOKEN 并兼容 PADDLEOCR_TOKEN。
6. ocr local 模型缺失时是否返回 E005 + EXIT:1。
7. ocr analyze-image 是否真正调用 ImageAnalyzer。
8. cloud 输出是否准备映射 nongmark/v1，而不是只返回 markdown。
9. commands --json / Manifest 是否同步。
10. CAPABILITY.md / Cli/AGENT.md / release-checklist.md 是否同步。
```

本阶段成功标准：

```text
Nong 明确区分本地 PP-OCRv5 文字识别、云端 PaddleOCR-VL-1.6 文档解析、纯 .NET ImageAnalyzer 图像结构分析；
三者都能通过统一 JSON 契约进入 nongmark/v1 和一刀三流，而不是成为三个孤立功能。
```
