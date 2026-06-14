# 2026-06-06 Local OCR Numeric/Boundary Repair

## User Report

本地 OCR 可用，但暴露了两个硬问题：

- `nong ocr local --json` 在 Paddle/OpenCV 推理产出 `NaN`、`Infinity` 或 `-Infinity` 时会触发 `System.Text.Json` 序列化失败，最后变成 `E004 internal_error`。
- 非 JSON 文本模式虽然能输出识别文本，但 confidence 会显示 `NaN`，这说明底层推理数值异常已经污染了可信度。

同时确认了能力边界：

- 本地 PP-OCRv5 只适合作为单图文字识别。
- PDF、跨页图片拼接、表格/版面标签、Word 输出、pandoc/NongMark/Word 切片对齐，应该走云端 OCR/to-word 和 Word `nongmark/v1` 管线。

## Repair

- `PpOcrV5Client` 清洗本地 OCR 数值输出：
  - 非有限 confidence 转为 `null`。
  - 非有限 bbox/polygon 点被移除。
  - block 增加 `confidenceValid`、`geometryValid`、`numericIssue`。
- `ocr local --json` 保持标准 JSON，不启用 `JsonNumberHandling.AllowNamedFloatingPointLiterals`。
- 本地 OCR 快速 CPU 路径如果产出无效数值，会尝试用保守 CPU/BLAS 配置重跑；如果兜底也失败或更差，则保留已清洗结果并报告 warning。
- JSON 输出增加：
  - `data.runtime.inferenceMode`
  - `data.runtime.numericFallbackAttempted`
  - `data.runtime.numericFallbackApplied`
  - `data.capabilities`
  - `metrics.invalidConfidenceBlocks`
  - `metrics.invalidGeometryBlocks`
  - warning issues: `local_ocr_numeric_fallback`、`local_ocr_invalid_confidence`、`local_ocr_invalid_geometry`
- 非 JSON 文本模式中无效 confidence 显示为 `n/a`，不再打印 `NaN`。
- `ocr local <file.pdf> --json` 明确返回 `E002 unsupported_format`，提示使用 `ocr cloud` 或 `ocr to-word`。

## GroundPA Sync

同步更新 `Nong.Toolkit.Net`：

- `multimodal/SKILL.md`
- `multimodal/README.md`
- `multimodal/references/ocr-local.md`
- `multimodal/references/ocr-cloud.md`
- `README.md`
- `README.zh-CN.md`

新口径：本地 OCR 是 single-image text OCR only。不要用本地 OCR 证明 PDF、跨页图片、版面、表格、Word 排版或 `nongmark/v1` 对齐。

## Validation

- `dotnet build .\Nong.Cli.Net\Cli\NongCli.csproj -c Release --nologo`: PASS, 0 errors. Existing warnings remain in ThirdParty/OpenXML/Skia/PPTX areas.
- `dotnet test .\Nong.Cli.Net\Cli.Tests\Cli.Tests.csproj -c Release --nologo`: PASS, 74/74.
- Real OCR smoke:
  - Generated a Windows PNG containing `测试123`.
  - `nong ocr local <image.png> --json`: PASS, 1 text block, `confidenceValid=true`, no `NaN`/`Infinity`, no issues.
  - `nong ocr local <image.png>`: PASS, text mode printed `测试123	1`.
  - `nong ocr local <dummy.pdf> --json`: returned `E002 unsupported_format` with cloud/to-word guidance.

## Remaining Work

This does not make local OCR a layout engine.

The next OCR document work should target cloud + Word alignment:

- PDF page rendering/extraction with a page/image manifest.
- Cross-page image stitching with source page/bbox provenance.
- PaddleOCR cloud page/block output normalized into a `nong-ocr/v1` intermediate model.
- Bridge from `nong-ocr/v1` into `nongmark/v1` (`document.json`, `content.jsonl`, `structure.json`, `format.json`, `assets/manifest.json`).
- Word output comparison against `word dissect --output` so cloud pages and Word block IDs can be aligned instead of guessed from plain text.
