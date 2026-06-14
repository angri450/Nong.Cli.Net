# 2026-06-05 Stage17c 纯 .NET PP-OCRv5 本地 OCR

## 变更原因

用户明确要求客户机不安装 Python、pip、`paddleocr` Python 包或其他外部 OCR 执行文件。本地 OCR 必须符合 Nong 的纯 .NET 项目定位，同时模型部署要足够快、足够方便，并优先支持国内镜像源。

## 实现

- CLI 版本升至 `3.2.3`。
- 删除本地 OCR 的 Python bridge 出货路径：
  - 删除 `MultiModal/LocalOcrClient.cs`。
  - 删除 `MultiModal/scripts/ocr_local.py`。
  - `MultiModalCore.csproj` 不再打包 Python 脚本。
- 新增纯 .NET 本地 OCR runtime：
  - `Sdcb.PaddleOCR 3.3.1`
  - `Sdcb.PaddleOCR.Models.Local 3.3.1`
  - native runtime 由 `ocr install-model` 从当前平台 Nong first-party runtime bundle / NuGet 镜像 / 本机 NuGet cache 安装：
    - `Angri450.Nong.OcrRuntime.WinX64 3.2.3`，约 `115.32 MB`。
    - `Angri450.Nong.OcrRuntime.LinuxX64 3.2.3`，约 `124.56 MB`。
    - `Angri450.Nong.OcrRuntime.LinuxArm64 3.2.3`，约 `82.26 MB`。
    - `Angri450.Nong.OcrRuntime.OsxX64 3.2.3`，约 `82.20 MB`。
    - `Angri450.Nong.OcrRuntime.OsxArm64 3.2.3`，约 `65.28 MB`。
  - 上游 fallback 包仅在显式 `--allow-upstream-fallback` 时使用：
    - `Sdcb.PaddleInference.runtime.win64.mkl 3.3.1.70`
    - `OpenCvSharp4.runtime.win 4.11.0.20250507`
- `PpOcrV5Client` 从骨架变成真实可用客户端：
  - 使用 `LocalFullModels.ChineseV5`。
  - Windows 使用 MKL/oneDNN CPU runtime；Linux/macOS 使用对应平台 Paddle/OpenCV native runtime。
  - 在 `Cv2.ImRead()` 之前预加载 native runtime，避免 check-env 假阳性。
  - JSON 模式下压制 Paddle/oneDNN native 日志，stdout 保持纯 JSON。
  - 返回文本、confidence、bbox、polygon。
  - `CheckEnvironment()` 报告 `noPython=true`。
- `nong ocr local` 改为纯 .NET PP-OCRv5 入口，不再有 `--python` 参数。
- `nong ocr check-env` 改为报告 `localDotNetPpOcrV5`，不再报告 `localPythonPaddleOcr`。
- `nong ocr models` 报告 managed model + native runtime cache、`noPython=true`。
- `nong ocr install-model pp-ocrv5-mobile --dry-run` 报告华为 NuGet 部署方案；非 dry-run 默认只从当前平台 `Angri450.Nong.OcrRuntime.*` bundle 提取 native DLL/SO/DYLIB 到 Nong runtime cache。
- `nong ocr install-model pp-ocrv5-mobile --allow-upstream-fallback` 是显式救急路径；未同步 first-party runtime 包时不会再静默下载回上游大包。
- `nong ocr install-model pp-ocrv5-mobile --json` 在安装成功或 runtime 已可用时，会自动删除 runtime cache 下的 `downloads` 临时下载缓存，并在 JSON 中返回 `downloadCleanup`。

## 国内镜像策略

本地 OCR 不依赖 Python。managed 模型元数据随 Nong CLI 引用部署；heavy native runtime 由按平台拆分的 `Angri450.Nong.OcrRuntime.*` 包承载，`nong ocr install-model pp-ocrv5-mobile` 从 NuGet v3 源或本机 NuGet cache 部署。客户机安装/更新时默认使用华为 NuGet v3 源：

- `https://mirrors.huaweicloud.com/repository/nuget/v3/index.json`

腾讯/清华 NuGet 地址不再作为默认 v3 源承诺；只有经过实际 `PackageBaseAddress` 验证后才能写入技能文档。

## 验证

- `dotnet build .\Nong.Cli.Net\Cli\NongCli.csproj -c Release --nologo`：PASS。
- `dotnet test .\Nong.Cli.Net\Cli.Tests\Cli.Tests.csproj -c Release --nologo`：72/72 PASS。
- `nong commands --json`：`status=ok; version=3.2.3; commandCount=73`。
- `nong ocr check-env --json`：`local=ok; noPython=True`。
- `nong ocr install-model pp-ocrv5-mobile --dry-run --json`：`engine=pp-ocrv5-dotnet-sdcb; noPython=True; upstreamFallbackDefault=disabled; mirrors=1`。
- `powershell -File .\Nong.Cli.Net\OcrRuntime\pack-runtimes.ps1`：生成并校验 WinX64 / LinuxX64 / LinuxArm64 / OsxX64 / OsxArm64 五个 runtime bundle 包。
- `NONG_OCR_RUNTIME_DIR=<temp> nong ocr install-model pp-ocrv5-mobile --source .\Nong.Cli.Net\nupkg --json`：`installed[0].origin=nong-bundle`，从 `Angri450.Nong.OcrRuntime.WinX64` 成功解包。
- 创建临时 `runtimeCache\downloads\dummy.nupkg` 后运行 `nong ocr install-model pp-ocrv5-mobile --json`：`downloadCleanup.cleaned=True`，命令结束后 `downloads` 目录不存在。
- `dotnet pack .\Nong.Cli.Net\Cli\NongCli.csproj -c Release -o .\Nong.Cli.Net\nupkg --nologo`：`Angri450.Nong.Cli.3.2.3.nupkg`，约 `114.55 MB`；包内不包含 Paddle/OpenCV native 大 DLL。
- 生成中文测试图 `测试123` 后运行 `nong ocr local <image> --json`：识别文本为 `测试123`，confidence 约 `0.99994`，stdout 为纯 JSON，stderr 为空。

## 注意

Windows x64 本地 OCR 已做真实图片 smoke test。Linux/macOS runtime bundle 已可打包，但受 OpenCvSharp 跨平台 runtime 版本差异影响，发布稳定口径前必须在对应 Linux/macOS 机器上跑 `ocr check-env` 与真实图片 `ocr local` smoke test。
