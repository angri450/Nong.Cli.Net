# OCR Runtime Chain

When OCR fails to install or run, the problem can be in any layer of this chain:

```
ocr skill (Toolkit)
  → nong ocr CLI (Nong.Cli.Net)
    → Anti450.Nong.MultiModal (NuGet core package, Sdcb.PaddleOCR + ONNX Runtime)
      → Angri450.Nong.OcrRuntime.* (5 platform runtime packages, shared by v5 and v6)
      → PP-OCR model files (v5: NuGet embedded / v6: CDN download to local cache)
```

## Platform Packages

Five platform-specific runtime packages:

| Package | RID |
|---------|-----|
| `Angri450.Nong.OcrRuntime.WinX64` | win-x64 |
| `Angri450.Nong.OcrRuntime.LinuxX64` | linux-x64 |
| `Angri450.Nong.OcrRuntime.macOSX64` | osx-x64 |
| `Angri450.Nong.OcrRuntime.WinArm64` | win-arm64 |
| `Angri450.Nong.OcrRuntime.LinuxArm64` | linux-arm64 |

## Install Path

```powershell
nong ocr check-env --json
```

If `localDotNetPpOcrV6.status` is not `ok` (or `localDotNetPpOcrV5.status` for legacy):

```powershell
# v6 (recommended) — downloads model from PaddleOCR CDN, no NuGet source needed
nong ocr install-model pp-ocrv6-medium --json

# v5 legacy — deploys native runtime from NuGet
nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json
```

Expect `noPython: true` and `upstreamFallbackDefault: "disabled"` in the output.

If the Huawei mirror hasn't synced yet:
```powershell
nong ocr install-model pp-ocrv5-mobile --source https://api.nuget.org/v3/index.json --json
```

Use `--allow-upstream-fallback` only when the user explicitly accepts downloading upstream Sdcb/OpenCvSharp native packages.

## Troubleshooting

- **"First-party runtime bundle unavailable"**: The NuGet mirror hasn't synced. Retry with NuGet.org or wait.
- **"localDotNetPpOcrV5.status=missing"**: The platform runtime package is not installed. Run install-model.
- **"E005" (not installed)**: Do not suggest Python or pip. Run install-model with the Huawei NuGet source.
- **Repository mismatch**: The runtime lives in the separate `Nong.OcrRuntime` repo. If the runtime itself is buggy, look there, not in Nong.Cli.Net.

## Environment Variable

Cloud OCR requires:
```
PADDLEOCR_ACCESS_TOKEN
```

Get it from `https://aistudio.baidu.com/account/accessToken`. Do not write tokens in logs, JSON output, or committed files.
