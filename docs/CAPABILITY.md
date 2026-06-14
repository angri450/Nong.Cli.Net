# nong CLI 当前能力表 v4.1.2

日期：2026-06-13
当前来源：`nong commands --json` / `nong commands --format openai-tools`
命令数：126 implemented
版本线：`CliVersion.Current == 4.1.2`

`PROJECT_STATE.md` 是当前真相源。本文件只记录当前能力口径；旧 4.0.0 / 93 命令能力表已经是历史信息。

---

## 架构

4.1.x 采用模块化工具架构：

- 主 `nong` 是轻路由器 + 纯 .NET 轻模块。
- 重模块拆成独立 dotnet tool。
- 用户命令入口保持稳定，例如 `nong chart bar ...`。

| 用户命令面 | Tool command | PackageId |
|---|---|---|
| `nong` | `nong` | `Angri450.Nong.Cli` |
| `nong chart ...` | `nong-chart` | `Angri450.Nong.Tool.Chart` |
| `nong diagram ...` | `nong-diagram` | `Angri450.Nong.Tool.Diagram` |
| `nong pdf ...` | `nong-pdf` | `Angri450.Nong.Tool.Pdf` |
| `nong pptx ...` | `nong-pptx` | `Angri450.Nong.Tool.Pptx` |
| `nong ocr ...` | `nong-ocr` | `Angri450.Nong.Tool.Ocr` |
| `nong word images ...` / `nong word crop ...` | `nong-imaging` | `Angri450.Nong.Tool.Imaging` |

主 CLI 内嵌轻模块：`word`、`excel`、`inspect`、`lit`、`genre`、`icons`、`slice`、`skill`、`progress`。

---

## 命令组

| 组 | 数量 | 当前能力 |
|---|---:|---|
| `word` | 51 | DOCX 生成、读取、修复、可见格式审计、表格/图片紧缩、add 系列、compare、render-preview |
| `inspect` | 12 | 论文/公文诊断、结构/证据/缺口分析、论文和公文生成 |
| `excel` | 8 | sheet/read/create/to-groups/dissect/style/formula/pivot |
| `chart` | 11 | ANOVA/Duncan、bar/line/scatter/pie/boxplot/histogram/heatmap/radar |
| `diagram` | 3 | flowchart/network/tree |
| `ocr` | 11 | PP-OCRv6 本地识别、云端 PaddleOCR-VL、模型安装、batch/video/screen/camera |
| `pdf` | 8 | check/dissect/render/images/merge/split/ocr/compress |
| `pptx` | 4 | read/slides/dissect/create |
| `lit` | 5 | CNKI-like DSL parse/validate/plan/search/export |
| `slice` | 4 | `nong-pandoc/package/v1` inspect/blocks/block/assets |
| `genre` | 2 | list/show |
| `icons` | 2 | list/search |
| `skill` | 4 | validate/scan/inventory/package |
| `progress` | 1 | HTML progress report |

精确参数以 `nong commands --json` 为准，不要从本文硬编码参数。

---

## OCR 当前合同

本地 OCR 当前是 PP-OCRv6 first：

```bash
nong ocr models --json
nong ocr install-model pp-ocrv6-medium --json
nong ocr local scan.png --json
```

支持的 v6 安装 ID：

- `pp-ocrv6`
- `pp-ocrv6-medium`
- `pp-ocrv6-small`
- `pp-ocrv6-tiny`

`pp-ocrv5-mobile` 保留为 legacy compatibility path。原生 OCR runtime 包维护在兄弟仓库 `Nong.OcrRuntime`，NuGet 前缀仍是 `Angri450.Nong.OcrRuntime.*`。`Cli/Common/OcrRuntimeVersion.cs` 不随 CLI 版本自动升级，除非兄弟 runtime 仓库发布新的已验证 runtime。

本地 OCR 不要求用户安装 Python、pip、`paddleocr` 或外部 OCR 可执行文件。

---

## JSON 输出

统一结构：

```json
{
  "status": "ok",
  "command": "word read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": { "docx": "out.docx" },
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 42, "version": "4.1.2" }
}
```

错误码：

| 代码 | 含义 |
|---|---|
| `E001` | file not found |
| `E002` | unsupported format |
| `E003` | missing argument |
| `E004` | internal error |
| `E005` | dependency missing |
| `E006` | validation failed |
| `E007` | read failed |
| `E008` | write failed |
| `E009` | not implemented |

---

## 发布口径

4.1.2 可发布工具包：

- `Angri450.Nong.Cli`
- `Angri450.Nong.Tool.Chart`
- `Angri450.Nong.Tool.Diagram`
- `Angri450.Nong.Tool.Pdf`
- `Angri450.Nong.Tool.Pptx`
- `Angri450.Nong.Tool.Ocr`
- `Angri450.Nong.Tool.Imaging`

发布前必须使用干净输出目录重新 pack 和 audit。根目录 `nupkg/` 可能包含历史产物，不能直接作为发布源。

Chart / Diagram / Imaging 当前 4.1.2 工具包采用 Windows native assets 策略。Linux/macOS 需要源码构建或后续 native runtime 拆包方案。
