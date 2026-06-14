# 2026-06-05 后续指导：本地 OCR 禁止回退 Python

## 当前唯一口径

- Nong CLI：`3.2.3`。
- 本地 OCR：纯 .NET PP-OCRv5，engine=`pp-ocrv5-dotnet-sdcb`。
- 模型/runtime：managed PP-OCRv5 模型元数据随 CLI 引用；heavy native runtime 通过按平台拆分的 `Angri450.Nong.OcrRuntime.*` NuGet 包部署到用户缓存；默认不回退上游 Sdcb/OpenCvSharp runtime 包。
- 客户机要求：不安装 Python、pip、`paddleocr` Python 包或外部 OCR 可执行文件。
- `ocr check-env` 字段：`localDotNetPpOcrV5`。
- `ocr local`：不再接受 `--python`。
- `ocr install-model pp-ocrv5-mobile --dry-run`：输出华为 NuGet 部署方案，不输出 pip 计划。
- `ocr install-model pp-ocrv5-mobile --json`：安装/检查 runtime 后自动清理 `runtimeCache\downloads` 临时下载缓存；JSON 会返回 `downloadCleanup`。
- `ocr install-model pp-ocrv5-mobile --allow-upstream-fallback`：显式救急路径；只有用户/维护者明确接受上游大包下载时使用。

## 禁止事项

1. 不要恢复 `LocalOcrClient` Python bridge。
2. 不要把 `scripts/ocr_local.py` 重新打包进 MultiModal。
3. 不要在 GroundPA skill 中提示 `pip install paddlepaddle paddleocr`。
4. 不要把 `localPythonPaddleOcr` 写回 JSON 合同。
5. 不要把 Linux/macOS 本地 OCR 描述成已稳定验收；runtime 包已生成，但仍需要对应平台真实图片 smoke test。
6. 不要把腾讯/清华地址写成默认 NuGet v3 源；默认只写已验证的华为源。
7. 不要在 first-party runtime 包未同步时静默 fallback；应报告发布/镜像同步问题，除非显式使用 `--allow-upstream-fallback`。

## 发布前必须验证

```powershell
dotnet build .\Cli\NongCli.csproj -c Release --nologo
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File .\OcrRuntime\pack-runtimes.ps1
.\Cli\bin\Release\net8.0\nong.exe ocr check-env --json
.\Cli\bin\Release\net8.0\nong.exe ocr install-model pp-ocrv5-mobile --dry-run --json
.\Cli\bin\Release\net8.0\nong.exe ocr install-model pp-ocrv5-mobile --source .\nupkg --json
.\Cli\bin\Release\net8.0\nong.exe ocr local <real-image.png> --json
```

如果验证下载缓存清理，可临时创建 `runtimeCache\downloads\dummy.nupkg`，重跑 `install-model --json`，期待 `downloadCleanup.cleaned=true` 且 `downloads` 目录被删除。

GroundPA 同步验证：

```powershell
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill validate .\Nong.Toolkit.Net\multimodal --json
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill scan .\Nong.Toolkit.Net --json
claude plugin validate .\Nong.Toolkit.Net
```

## 国内镜像说明

客户机安装 Nong CLI 时使用 NuGet 国内镜像源，例如：

```powershell
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
```

如果已经安装旧版：

```powershell
dotnet tool update --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
```

首次使用本地 OCR 前部署 native runtime。华为云同步 Nong runtime 包后会下载当前平台 `Angri450.Nong.OcrRuntime.*`。未同步时默认报错，提示发布/镜像同步问题；只有显式添加 `--allow-upstream-fallback` 才会下载上游 Sdcb/OpenCvSharp runtime 包：

```powershell
nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json
```

发布顺序必须是：先推 5 个 `Angri450.Nong.OcrRuntime.*`，再推 `Angri450.Nong.Cli`，等华为镜像同步后从华为源验证 `install-model`。
