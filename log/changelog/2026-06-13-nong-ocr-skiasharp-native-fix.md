# nong-ocr SkiaSharp native DLL missing 修复

Date: 2026-06-13

## 问题

`nong ocr local` 报错：`Unable to load DLL 'libSkiaSharp' or one of its dependencies`。`nong-ocr` 工具编译了 SkiaSharp 托管源码（P/Invoke `libSkiaSharp`），但没有引用 `SkiaSharp.NativeAssets.Win32` 来提供原生 DLL。其他工具（`nong-imaging`、`nong-chart`、`nong-diagram`）都有这个引用，唯 `nong-ocr` 缺失。

附带问题：`ocr check-env` 中 `imageAnalyzer` 状态硬编码为 `true`，不做真实探测。

## 修复

1. `MultiModal/tools/nong-ocr.csproj`：添加 `<PackageReference Include="SkiaSharp.NativeAssets.Win32" Version="3.119.0" />`，与 VersionConstants (Milestone=119) 对齐
2. `Cli/Commands/OcrCommands.cs`：`ocr check-env` 的 `imageAnalyzer` 状态改为调用 `IsImageAnalyzerAvailable()`，用 1x1 PNG 内存解码真实探测 SkiaSharp 加载

## 验证

- `dotnet build nong-ocr.csproj -c Release` → 0 errors
- `nong-ocr.exe ocr check-env --json` → `imageAnalyzer: ok`（不再硬编码）
- `imageAnalyzer` 状态现在在 SkiaSharp native DLL 不可用时返回 `error`

## 影响范围

- `Angri450.Nong.Tool.Ocr` 4.1.2（NuGet 包需要重新打包推送）
- 不影响其他 6 个工具包
- 不影响 CLI 核心 `Angri450.Nong.Cli`
