# 阶段 16 OCR 基础设施可行性报告

日期：2026-06-04
状态：DESIGN ONLY — 研究完成，无代码变更

---

## Goal

设计 Nong 的 OCR/多模态基础设施，区分本地 PP-OCRv5 文字识别、云端 PaddleOCR-VL-1.6 文档解析、纯 .NET ImageAnalyzer 图像结构分析三层能力，使其统一进入 nongmark/v1 和一刀三流。

## Read References

- `log/guidance/2026-06-04-019-stage16-ocr-infrastructure-blueprint.md`
- `测试文件夹/office-skill-research/PaddleOCR-main/PADDLEOCR_NET_INTEGRATION.md`
- `changelog/2026-06-02-cli-ocr-spec.md`
- `MultiModal/PaddleOcrVlClient.cs`, `MultiModal/OcrModels.cs`, `MultiModal/ImageAnalyzer.cs`, `MultiModal/LocalOcrClient.cs`
- `Cli/Commands/OcrCommands.cs`

---

## 1. Architecture Decision

```
                    ┌─────────────────────────────┐
                    │     nong CLI (OcrCommands)    │
                    └─────────────┬───────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
          ▼                       ▼                       ▼
┌─────────────────┐   ┌─────────────────────┐   ┌─────────────────┐
│  PP-OCRv5 Local │   │ PaddleOCR-VL-1.6    │   │  ImageAnalyzer  │
│  (ONNX Runtime) │   │ Cloud API            │   │  (SkiaSharp)    │
│  CPU-only       │   │ Token required       │   │  No token       │
└────────┬────────┘   └──────────┬──────────┘   └────────┬────────┘
         │                       │                        │
         ▼                       ▼                        ▼
┌─────────────────────────────────────────────────────────────┐
│                   nong-ocr/v1 JSON Schema                    │
│  textBlocks / layoutBlocks / imageRegions / assets          │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    nongmark/v1 Bridge                        │
│  文字块→paragraph, 表格→table, 公式→equation, 图片→image     │
└─────────────────────────────────────────────────────────────┘
```

三层定位：
- **本地 PP-OCRv5**：纯文字检测+识别，离线、CPU、<20MB 模型。适合截图、标签、简单扫描件、敏感文档
- **云端 PaddleOCR-VL-1.6**：完整文档解析（版面、表格、公式、图表、印章）。需要 token，保留最强模型
- **ImageAnalyzer**：通用视觉验收工具。纯 .NET 本地图像结构分析（像素分类、内容区域、空白率），永远可无 token 执行。服务 OCR 图片预检、chart/diagram 生成质量检查、pptx 幻灯片内容验证、word assets 图片审计，不是 OCR 的附属功能

---

## 2. PP-OCRv5 ONNX Runtime Feasibility

### 2.1 模型体系

| 组件 | 功能 | 体积 | ONNX 可导出 |
|------|------|------|-----------|
| PP-OCRv5_mobile_det | 文字检测（DB 算法） | 2-5 MB | 是 |
| PP-OCRv5_mobile_rec | 文字识别（CRNN + CTC） | 10-15 MB | 是 |
| ch_ppocr_mobile_cls | 文字方向分类 | ~2 MB | 是 |

总计 < 20MB ONNX 模型。已有工程验证：`paddleocr-js`（PaddleOCR 官方浏览器 SDK）基于 ONNX Runtime Web 跑 PP-OCRv5。

### 2.2 C# 实现路径

参考 `paddleocr-js/packages/core/src/models/det.ts` 和 `rec.ts`：

1. 图像预处理：BGR 转换、均值/标准差归一化、缩放至目标尺寸（长边 960px）
2. 加载 ONNX session（`Microsoft.ML.OnnxRuntime`）
3. 推理：det 模型输出概率图 → DB 后处理（二值化 + findContours）→ 文字框坐标
4. 对每个文字框 crop + resize → rec 模型推理 → CTC 解码 → 字符串
5. 预处理参数和后处理阈值从 `inference.yml` 读取

### 2.3 性能预估（基于 PP-OCRv4 benchmark，v5 类似）

| 操作 | GPU | ARM CPU | x64 CPU (预估) |
|------|-----|---------|---------------|
| 检测（mobile_det） | 5.7ms | 92ms | ~50ms |
| 识别（mobile_rec） | 1.7ms | 33ms | ~20ms |
| 一页 A4 文字 (~30 行) | <1s | ~3s | ~1.5s |

量化后（INT8）可进一步缩小模型和加速。

### 2.4 Risik

- C# 端需手写 DB 后处理，涉及二值化、连通域、minAreaRect — SkiaSharp 可完成
- CTC 解码词典需随模型包分发（`dict.txt`，约 100KB）
- ONNX Runtime 原生库按平台分发（win-x64/linux-x64/osx-arm64），每平台约 10-15MB

---

## 3. Model Distribution Strategy

### 3.1 候选方案

| 方案 | 优点 | 缺点 |
|------|------|------|
| A: 主包内置 ONNX | 装 tool 即用 | NuGet 包 +20MB；平台 native runtime 翻倍 |
| B: 单独模型资源包 `Angri450.Nong.Ocr.PpOcrV5.Models` | 主包轻；模型版本可独立升级 | 用户需额外安装模型包 |
| C: 首次运行自动下载 | 主包最小 | 网速差/无网体验差；需 checksum + 断点续传 |
| D: `--model-dir` 手工指定 | 高级用户/内网部署 | 不能作为默认推广路径 |

### 3.2 推荐：B + D 为主，C 为增强。--model-dir 优先级最高

用户显式指定的路径必须优先于所有自动发现：

```
优先级（从高到低）：
1. --model-dir 参数（用户显式指定，最高优先级）
2. 模型资源包（NuGet 包目录）
3. 用户缓存目录（%LOCALAPPDATA%\Angri450.Nong\models\）
4. 以上均未找到 → E005 + 提示 "nong ocr install-model pp-ocrv5-mobile"
```

### 3.3 模型缓存目录结构

```
%LOCALAPPDATA%\Angri450.Nong\models\pp-ocrv5-mobile\
  manifest.json         # nong-ocr-model/v1
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

### 3.4 ONNX Runtime 依赖

NuGet: `Microsoft.ML.OnnxRuntime`（CPU 包，非 GPU 包）
- 不引入 `Microsoft.ML.OnnxRuntime.Gpu`（额外 200MB+）
- 不引入 `Microsoft.ML.OnnxRuntime.DirectML`
- 已有依赖 SkiaSharp 已在 ThirdParty 中

---

## 4. Cloud PaddleOCR-VL-1.6 Only Decision

### 4.1 不保留多模型选择

- 固定模型：`PaddleOCR-VL-1.6`（内部常量）
- 不暴露 `--model` 参数
- 不引入 PP-StructureV3、PaddleOCR-VL-1.5、PaddleOCR-VL 等旧模型
- 未来如果有更强的 VLM 模型发布（VL-2.0 等），另开阶段同步

### 4.2 当前代码状态

`MultiModal/PaddleOcrVlClient.cs`：
- 已实现 SubmitFile/SubmitUrl/WaitForJob/DownloadResults
- 已固定使用 `PaddleOCR-VL-1.6`
- `DownloadResultsStructuredAsync()` 已解析 JSONL 并下载图片
- `ProcessToWordAsync()` 已调用 `LayoutToWordConverter.Convert()`

### 4.3 输出升级需求

当前 `ProcessAsync()` 只返回 Markdown 文件路径列表。需要升级为：
1. 解析 JSONL → 提取 `ParsingBlock` 列表（已实现）
2. 映射到 nongmark/v1 blocks（新需求）
3. 输出完整三流目录结构（manifest/document/content/structure/format/assets）

---

## 5. PADDLEOCR_ACCESS_TOKEN Migration

### 5.1 当前状态

- `PaddleOcrVlClient` 构造函数从 `PADDLEOCR_TOKEN` 环境变量读取
- `OcrCommands.cs` 中 `ocr cloud` 检查 `PADDLEOCR_TOKEN`
- 官方 SDK 默认使用 `PADDLEOCR_ACCESS_TOKEN`

### 5.2 迁移方案

```
1. 优先读取 PADDLEOCR_ACCESS_TOKEN
2. 兼容读取 PADDLEOCR_TOKEN（deprecated）
3. 若只发现 PADDLEOCR_TOKEN：
   - 正常使用（不阻断功能）
   - issues[] 中追加 warning: "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN"
4. 两个都没有 → E005: "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN."
```

### 5.3 安全规则

- 不允许 `--token` CLI 参数（避免命令行泄露）
- 不允许在 JSON 输出、异常消息、日志中打印 token 值
- 测试只检查 token 是否存在，不输出 token 内容
- 错误消息只提示"设置环境变量"，不粘贴 token 前缀

---

## 6. nongmark/v1 Mapping: PaddleOCR-VL-1.6 JSONL

### 6.1 源字段（从 ParsingBlock）

| 源字段 | 类型 | 说明 |
|--------|------|------|
| `block_label` | string | doc_title, paragraph_title, text, table, formula, equation, image, chart, vision_footnote, ... |
| `block_content` | string | 文本内容/Markdown/LaTeX |
| `block_bbox` | float[4] | [x1,y1,x2,y2] |
| `block_id` | int | 原始 block ID |
| `block_order` | int? | 阅读顺序 |
| `block_polygon_points` | float[][] | 精确多边形坐标 |

### 6.2 映射表

| block_label | nongmark/v1 kind | 特殊处理 |
|-------------|-----------------|---------|
| `doc_title` | `heading` (level=1) | 标题 |
| `paragraph_title` | `heading` (level=2) | 小节标题 |
| `text` | `paragraph` | 正文段落 |
| `table` | `table` | block_content 保留 Markdown table → 解析为 cells |
| `formula` | `equation` | block_content → latex 字段，display=true |
| `equation` | `equation` | 同 formula |
| `image` | `image` | assetId 关联 markdownImages 中的 URL |
| `chart` | `figure` (kind=chart) | 图表，保留 source 信息 |
| `vision_footnote` | `footnote` | 脚注 |
| 未知 label | `rawOpenXmlRef` (kind=unknown) | 保留 sourceLabel，non-destructive |

### 6.3 坐标保留

每个 nongmark/v1 block 的 source 字段：
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
    "confidence": null,
    "originalBlockId": 3
  }
}
```

---

## 7. CLI Command Contract Draft

### 7.1 最终命令清单

```
读: ocr local, ocr cloud, ocr analyze-image
查: ocr check-env, ocr models
管: ocr install-model
写: ocr to-word (暂缓 P1)
```

### 7.2 各命令详细规格

#### `nong ocr check-env --json`

检查内容：
- ONNX Runtime 可用性 → `onnxRuntime: true/false`
- 模型目录存在 → `model.ppOcrv5Mobile: present/missing`
- 模型 checksum → `model.ppOcrv5Mobile.checksum: ok/mismatch/missing`
- ImageAnalyzer 可用性 → `imageAnalyzer: ok`
- Cloud token → `cloudToken: set/missing`（不打印 token 值）
- Python fallback → `pythonFallback: available/unavailable`

输出：
```json
{
  "status": "ok",
  "data": {
    "onnxRuntime": true,
    "model": {"ppOcrv5Mobile": "missing"},
    "imageAnalyzer": "ok",
    "cloudToken": "set",
    "pythonFallback": "unavailable"
  }
}
```
EXIT: 0（环境检查本身不应失败，只报告状态）

#### `nong ocr models --json`

列出已安装模型。输出：
```json
{
  "data": {
    "models": [
      {"id": "pp-ocrv5-mobile", "version": "2026-06-04", "path": "...", "checksum": "ok"}
    ]
  }
}
```
无模型时：空数组，EXIT: 0。

#### `nong ocr install-model pp-ocrv5-mobile --json`

第一版返回 E009（下载逻辑未实现）：
```json
{
  "status": "error",
  "errors": [{"code": "E009", "message": "Model download not yet implemented. Place model files in <cache-dir>."}]
}
```
等实现后：下载 → checksum 验证 → 解压到缓存目录。

#### `nong ocr local <image> -o <dir> --json`

本地 PP-OCRv5 文字识别。选项：`--lang zh|en`，`--model-dir <dir>`，`--format json|txt`，`--analyze-image`。

当前状态：E005（模型缺失）→ 提示 `nong ocr install-model pp-ocrv5-mobile`。
完整实现后：PP-OCRv5 ONNX 推理 → `nong-ocr/v1` JSON。

#### `nong ocr analyze-image <image> -o <out-dir> --json`

通用视觉验收工具。纯 .NET ImageAnalyzer，不需要 token，不需要 Python。立即可实现（代码已在 `MultiModal/ImageAnalyzer.cs`，需暴露 CLI 入口）。

服务范围（跨模块）：
- `ocr` 模块：OCR 前图片预检（空白率、文字区域密度、是否适合 OCR）
- `chart` 模块：生成图表质量检查（空白区域、像素分布）
- `diagram` 模块：网络图/流程图/树图渲染验证
- `pptx` 模块：幻灯片内容区域分析
- `word` 模块：文档图片资产审计（word assets manifest 中的图片分析）

输出：
```
<out-dir>/
  image-analysis.json      # ImageLayout 结构
  image.map.txt            # ASCII 像素地图
```

#### `nong ocr cloud <file> -o <out-dir> --json`

PaddleOCR-VL-1.6 云端文档解析。选项：`--pages "1-5,10"`，`--poll 5`，`--timeout 600`，`--url`。

当前修复项（第一轮小修范围）：
- Token 优先 PADDLEOCR_ACCESS_TOKEN，兼容 PADDLEOCR_TOKEN
- 输出尝试结构化（不只统计 markdown blocks 数量）

`--to-word <docx>` 属于后续 16d 路线图，不属于 16a，不得在 16a 实现或注册。

完整实现后：nongmark/v1 三流输出。

### 7.3 错误码映射

| 场景 | 错误码 |
|------|--------|
| 图片/PDF 不存在 | E001 |
| 格式不支持 | E002 |
| 缺必要参数 | E003 |
| Token 缺失 | E005 |
| 模型缺失 | E005 |
| ONNX Runtime 不可用 | E005 |
| HTTP 400（参数错误） | E006 |
| HTTP 401/403（token 无效） | E005 (dependency_missing / auth_failed) |
| HTTP 429（频率限制） | E005 (dependency_missing, issue=rate_limited) |
| HTTP 503/504（服务不可用） | E005 (dependency_missing, issue=service_unavailable) |
| 选项非法 | E006 |
| 图片读取失败 | E007 |
| JSONL 解析失败 | E007 |
| 输出写入失败 | E008 |
| install-model 下载未实现 | E009 |

关键：云端 HTTP 错误一律不落 E004（internal_error）。它们是外部服务问题（E005）或输入问题（E006/E007），不是 Nong 内部崩溃。

---

## 8. First-Round Implementation Scope (Phase 16a)

不要完整手搓 PP-OCRv5 推理。先做设计 + 5 个小修：

1. **Token 环境变量迁移** — `PADDLEOCR_ACCESS_TOKEN` 优先，`PADDLEOCR_TOKEN` 兼容
2. **新增 `ocr analyze-image`** — 暴露 `ImageAnalyzer` 到 CLI，立即可实现
3. **新增 `ocr check-env`** — 报告 cloud/local/imageAnalyzer/model 状态
4. **`ocr local` E005 文案更新** — 指向 PP-OCRv5 model package，不再指向 pip install
5. **`ocr cloud` 输出升级** — 尝试返回结构化数据（pages/blocks），不只统计 markdown 行数

明确不做：

- 不实现 `--to-word <docx>` — 等阶段 15 NongMark/Word 结构稳定后再接
- 不实现完整 ONNX det/rec 推理
- 不实现自动模型下载
- 不发布 PP-OCRv5 模型 NuGet 包

---

## 9. Open Risks

| 风险 | 等级 | 缓解 |
|------|------|------|
| ONNX Runtime native 包体积（~10-15MB/platform） | Medium | 只引用 CPU 包；多平台由 NuGet 自动解析 |
| PP-OCRv5 C# 后处理（DB 二值化、findContours） | Medium | SkiaSharp 可做轮廓检测；paddleocr-js 有 TypeScript 参考实现 |
| 无网环境模型分发 | Medium | 方案 B + D；方案 C 作为可选增强 |
| CTC 解码词典大小和编码 | Low | dict.txt ~100KB，UTF-8 |
| 本地纯文字 OCR 与云端版面理解的边界混淆 | Low | CLI 命令明确区分 local vs cloud；local 只有 textBlocks |
| nongmark/v1 映射不完全（VL label 枚举可能扩展） | Low | unknown label → kind=unknown + sourceLabel 保留 |
| Python fallback 路线误导用户 | Low | check-env 明确报告 pythonFallback 状态；不推荐为主路线 |

---

## 10. Recommended Implementation Phases

| Phase | 内容 | 产出 |
|-------|------|------|
| 16a (本阶段) | 设计 + 小修 | 可行性报告；token 迁移；analyze-image；check-env |
| 16b | ONNX 原型 | PpOcrV5Client 骨架；预处理/推理/后处理验证 |
| 16c | 模型包化 | Angri450.Nong.Ocr.PpOcrV5.Models NuGet；install-model |
| 16d | CLI 完整 | local/cloud 完整 nongmark/v1 输出；to-word |
| 16e | 测试+文档 | OcrCommandTests；CAPABILITY/AGENT 同步 |

---

## 11. Next Recommended Stage

继续阶段 15 Word 一刀三流开发（当前有 4 个 agent 并行中）。
阶段 16a 的小修（token 迁移 + analyze-image + check-env）可在阶段 15 收尾后立即启动。
阶段 16b-16e 需等待 PP-OCRv5 ONNX 模型就绪后再开。
