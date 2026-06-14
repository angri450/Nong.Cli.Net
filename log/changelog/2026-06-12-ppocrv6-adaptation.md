# PP-OCRv6 Full-Stack Adaptation

## What Changed

### New Files
- `MultiModal/PpOcrV6/PpOcrV6ModelResolver.cs` — v6 model ID parsing, CDN URL templates, cache paths, dict resource extraction, platform RID resolution
- `MultiModal/PpOcrV6/PpOcrV6Client.cs` — v6 directory-model inference client with dual-engine (Fast/Safe) fallback
- `MultiModal/PpOcrV6/ppocrv6_dict.txt` (18,708 lines) — embedded resource, used by medium/small
- `MultiModal/PpOcrV6/ppocrv6_tiny_dict.txt` (6,904 lines) — embedded resource, used by tiny

### Modified Files

| File | Change |
|------|--------|
| `MultiModal/MultiModalCore.csproj` | Embed v6 dict resources |
| `Cli/Commands/OcrCommands.cs` | `install-model` accepts 4 v6 IDs; `models` lists v6 tiers with `isDefault`; `check-env` reports v6 status; `ocr local` auto-detects v6 first |
| `../Nong.Toolkit.Net/ocr/SKILL.md` | Route table + command list + dispatch logic now include v6 |
| `../Nong.Toolkit.Net/ocr/references/ocr-local.md` | v6 model matrix + install commands |
| `../Nong.Toolkit.Net/ocr/references/runtime-chain.md` | v5/v6 shared runtime chain |

### Model ID Design

```
pp-ocrv6          → alias for pp-ocrv6-medium (default)
pp-ocrv6-medium   → 50-language unified model, best accuracy
pp-ocrv6-small    → balanced size/accuracy
pp-ocrv6-tiny     → embedded/mobile, 6904-char dict
pp-ocrv5-mobile   → legacy v5, unchanged
```

`pp-ocrv6-medium` is the default. `ocr local` auto-preferences v6 over v5 when both are installed.

### What was NOT changed

- `Nong.OcrRuntime` — native runtime DLLs (PaddleInference 3.3.1.70) unchanged. Verified PIR format v6 models load natively.
- `PpOcrV5Client` — retained as-is for v5 fallback.
- `PaddleOcrVlClient` — cloud OCR unchanged.
- nuget package references — no new NuGet dependencies.

## Why

PP-OCRv6 achieves +4.6% detection and +5.1% recognition over v5_server with 34.5M parameters. The 5.2x CPU speedup makes medium-tier feasible on consumer hardware. v6 also unifies 50 languages in one model.

v6 models use Paddle 3.0 PIR format (inference.json + .pdiparams, no .pdmodel). Sdcb.PaddleOCR 3.3.1 has no `ModelVersion.V6` enum, but `FileDetectionModel`/`FileRecognizationModel` from directory with `ModelVersion.V5` works correctly because all new operators (PPLCNetV4, RepLKPAN, MultiHead, lightsvtr) execute in the native PaddleInference layer.

## Verification

```
nong ocr models --json           → 4 models, medium.isDefault=true
nong ocr check-env --json        → v6 status=ok, modelSize=medium
nong ocr install-model pp-ocrv6-medium --dry-run --json  → PASS
nong ocr install-model pp-ocrv6-tiny --dry-run --json    → PASS
nong ocr local test.png --json   → engine=pp-ocrv6-dotnet-sdcb, confidence 0.9998
```

Native runtime slim-test: 5-DLL set (paddle_inference_c + mklml + mkldnn + phi + OpenCvSharpExtern) passed full PIR model load + end-to-end OCR. Redundant files (onnxruntime, paddle2onnx, opencv_videoio_ffmpeg) identified but slim-down deferred.

## Remaining Risk

- Sdcb.PaddleOCR upstream may add `ModelVersion.V6` enum — our V5 fallback should still work with directory-based loading
- PaddleInference API deprecation warning (`PD_ConfigSetOnednnCacheCapacity`) is cosmetic now but may hard-fail in future releases — tracked for OcrRuntime bump
- CDN download path uses `System.Formats.Tar` (available in .NET 7+) — download-only, no deployment
