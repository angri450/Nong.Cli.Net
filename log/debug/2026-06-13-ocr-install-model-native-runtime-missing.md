# install-model v6 安装后本地 OCR 仍报 native runtime 缺失

Date: 2026-06-13
来源: 用户测试反馈

## 反馈内容

```
nong ocr local "image.jpg" 2>&1
→ [E005] dependency_missing: Local PP-OCRv6 .NET runtime is unavailable:
  Unable to load DLL 'libSkiaSharp' or one of its dependencies: 找不到指定的模块。
  Run 'nong ocr install-model pp-ocrv6-medium --json'.

# libSkiaSharp 修复后（SkiaSharp.NativeAssets.Win32 加入 csproj）
→ [E005] Native OCR runtime not installed.
  Run 'nong ocr install-model pp-ocrv6-medium --json'.
  Cache: %LocalAppData%\Angri450.Nong\runtimes\pp-ocrv6-win-x64
```

## 问题诊断

`nong ocr install-model pp-ocrv6-medium` 执行成功（det/rec/dict 全部就位），但 `nong ocr local` 仍报 runtime 缺失。根因分两层：

### 第一层：libSkiaSharp 丢失

`nong-ocr` 工具 `ThirdParty/ThirdParty.csproj` 编译了 SkiaSharp 托管源码，P/Invoke 调用 `libSkiaSharp.dll`，但 csproj 缺 `SkiaSharp.NativeAssets.Win32` NuGet 引用。其他三个 tool（nong-imaging、nong-chart、nong-diagram）都有，唯独 nong-ocr 漏了。

修复：`MultiModal/tools/nong-ocr.csproj` + `SkiaSharp.NativeAssets.Win32 3.119.0` → 4.1.3 发布。

附带问题：`ocr check-env` 的 `imageAnalyzer` 硬编码为 `true`，改为 `IsImageAnalyzerAvailable()` 真实探测。

### 第二层：install-model v6 只有模型没有运行时

`InstallV6Model` 从头到尾只处理模型下载（CDN tar → det/rec/dict），完全没有调用 native runtime 部署逻辑。而 v5 路径有完整的 `GetNativeRuntimePlan()` + `InstallNativeRuntime()` 流程。

用户反馈建议把 `Sdcb.PaddleInference.runtime.win64.mkl` + `OpenCvSharp4.runtime.win` 写进 csproj 的 PackageReference。**不采纳** —— 这会让 nupkg 从 25MB 膨胀到 350MB，打破模块化拆分设计。正确做法是让 v6 路径复用现有基础设施。

## 修复方案

`InstallV6Model` 在模型下载之后调用 `InstallNativeRuntime()`：

```
install-model pp-ocrv6-medium
  ├── Step 1: CDN 下载模型 → models/pp-ocrv6-medium/
  └── Step 2: NuGet 提取运行时 → runtimes/pp-ocrv6-win-x64/
```

三个场景：
- 模型就绪 + 运行时缺失 → 只部署运行时
- 模型缺失 → 先下载模型再部署运行时
- 都已就绪 → 输出 ok，跳过

## 修复结果

- nong-ocr 4.1.3: libSkiaSharp 问题已解决（+ NativeAssets.Win32）
- nong-ocr 4.1.4: install-model 一键安装模型+运行时
- 工具包尺寸 25MB，未膨胀
- check-env: v5=ok, v6=ok

## 影响范围

- `Angri450.Nong.Tool.Ocr` 从 4.1.2 升级到 4.1.4
- 不影响其他 6 个工具包
- NuGet 废止包清理：Chart/Diagram/MultiModal/Imaging/Skill.Manager 全部 unlist

## 状态

已修复 (nong-ocr 4.1.4) / 已推送 NuGet / 已推送 GitHub
