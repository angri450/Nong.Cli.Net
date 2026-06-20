---
name: ocr
description: OCR and image-structure QA via nong. Trigger on OCR, PaddleOCR, scanned image, image-to-Word, chart visual QA, image structure analysis, or OCR text ingestion (--ingest).
---

# OCR

Use `nong ocr` as the only entrypoint for OCR and image-structure analysis. Do not route through Python OCR libraries or custom OCR wrappers.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `ocr` command group.

**Modular:** `nong ocr` routes to the standalone `Angri450.Nong.Tool.Ocr` dotnet tool. First use auto-installs. Command surface unchanged.

## v4.5.0: ONNX Runtime Migration

PP-OCRv6 now runs on ONNX Runtime. **PaddleInference is retired.** No native runtime packages (Nong.OcrRuntime) are needed.

- Inference engine: `Microsoft.ML.OnnxRuntime` (shared with `nong search`)
- Models: downloaded from ModelScope via git clone
- Install: `nong ocr install-model pp-ocrv6-medium --json` — no `--source` flag needed

## Prerequisites

Run once before OCR work:

```powershell
nong ocr check-env --json
nong ocr install-model pp-ocrv6-medium --json   # ONNX models (132MB), one-time
```

Cloud OCR and image-to-Word require this environment variable:

```powershell
$env:PADDLEOCR_ACCESS_TOKEN = "<access-token>"
```

Get the cloud token from `https://aistudio.baidu.com/account/accessToken`. If the user has no token, use local OCR only after its smoke test passes.

Do not write real credentials into repository files, logs, or examples.

## Route Table

| User wants | Command | Reference |
|------------|---------|-----------|
| Environment check | `nong ocr check-env --json` | |
| Model inventory | `nong ocr models --json` | |
| Install ONNX model (medium) | `nong ocr install-model pp-ocrv6-medium --json` | |
| Install ONNX model (small) | `nong ocr install-model pp-ocrv6-small --json` | |
| Install ONNX model (tiny) | `nong ocr install-model pp-ocrv6-tiny --json` | |
| Single-image text OCR | `nong ocr local <img> --json` | [ocr-local.md](references/ocr-local.md) |
| OCR + ingest into NongDb | `nong ocr local <img> --ingest --json` | ← nong search can find |
| Batch directory OCR | `nong ocr batch <dir> --pattern "*.png" --json` | [ocr-local.md](references/ocr-local.md) |
| Video frame OCR + subtitles | `nong ocr video <file> -o <dir> --json` | |
| Screen region OCR (Win) | `nong ocr screen --region x,y,w,h --json` | |
| Camera capture OCR | `nong ocr camera --device 0 --json` | |
| Cloud OCR with layout | `nong ocr cloud <input> -o <dir> --json` | [ocr-cloud.md](references/ocr-cloud.md) |
| Cloud OCR + ingest | `nong ocr cloud <input> -o <dir> --ingest --json` | |
| Image to Word | `nong ocr to-word <input> -o <out.docx> --json` | [ocr-cloud.md](references/ocr-cloud.md) |
| Image structure QA | `nong ocr analyze-image <img> -o <dir> --json` | [image-analyzer.md](references/image-analyzer.md) |

## Implemented Commands

```powershell
# Environment
nong ocr check-env --json
nong ocr models --json

# Model install (ONNX Runtime, from ModelScope)
nong ocr install-model pp-ocrv6-medium --json        # 132MB, recommended
nong ocr install-model pp-ocrv6-medium --dry-run --json
nong ocr install-model pp-ocrv6-small --json          # 31MB
nong ocr install-model pp-ocrv6-tiny --json           # 6MB

# Local OCR (PP-OCRv6 ONNX, no Python)
nong ocr local <image.png> --json
nong ocr local <image.png> --ingest --json            # + ingest into NongDb
nong ocr local <image.png> --force --json

# Cloud OCR (PaddleOCR-VL-1.6)
nong ocr cloud <image-or.pdf> -o <dir> --json
nong ocr cloud <image-or.pdf> -o <dir> --ingest --json
nong ocr to-word <image-or.pdf> -o <out.docx> --json

# Batch / Video / Screen / Camera
nong ocr batch <dir> --pattern "*.png" --json
nong ocr video <video.mp4> -o <dir> --fps 1 --json
nong ocr screen --region 100,100,800,600 --json
nong ocr camera --device 0 --interval 2000 --count 5 --json

# Image analysis (no text recognition)
nong ocr analyze-image <img> -o <dir> --json
```

## Dispatch

1. For environment status, run `nong ocr check-env --json`. Shows ONNX Runtime status.
2. For first use, run `nong ocr install-model pp-ocrv6-medium --json` to download ONNX models.
3. For single-image text recognition, run `nong ocr local <image.png> --json`.
4. For OCR results that need to be searchable later, add `--ingest`.
5. For scanning a directory, run `nong ocr batch <dir> --pattern "*.png" --json`.
6. For video frames, run `nong ocr video <video.mp4> -o <dir> --json`.
7. For screen/camera, use `nong ocr screen` or `nong ocr camera`.
8. For PDF, multi-page, table/layout, require `PADDLEOCR_ACCESS_TOKEN`, then `nong ocr cloud`.
9. For Word output from scan/PDF, require token, then `nong ocr to-word`.
10. For model inventory, run `nong ocr models --json`. Default: `pp-ocrv6-medium`.

## Boundaries

- `ocr local`, `ocr batch`: single-image text recognition. No PDF, no layout analysis, no table structure.
- `ocr video`: requires `opencv_videoio_ffmpeg*.dll`. Samples frames with dHash deduplication.
- `ocr screen`: Windows only.
- `ocr camera`: requires camera + opencv_videoio.
- `ocr cloud` and `ocr to-word`: require `PADDLEOCR_ACCESS_TOKEN`.
- `ocr analyze-image`: structural QA. Does NOT recognize text.
- **No more PaddleInference.** ONNX Runtime handles all inference. No native runtime packages needed.
- **No more v5.** `pp-ocrv5-mobile` is removed. Only PP-OCRv6 ONNX models.
- If local OCR returns E005, do not suggest Python or pip. Suggest `nong ocr install-model pp-ocrv6-medium`.
