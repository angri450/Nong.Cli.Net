# install-model v6 路径补全 native runtime 部署

Date: 2026-06-13
相关 debug: `log/debug/2026-06-13-ocr-install-model-native-runtime-missing.md`

## 问题

`nong ocr install-model pp-ocrv6-medium` 只从 PaddleOCR CDN 下载了 det/rec 模型和字典，没有部署 native inference DLL。导致安装后 `nong ocr local` 仍然报错：

```
Native OCR runtime not installed.
Run 'nong ocr install-model pp-ocrv6-medium --json'.
Cache: %LocalAppData%\Angri450.Nong\runtimes\pp-ocrv6-win-x64
```

用户反馈建议把 native runtime 包写进 csproj 的 PackageReference，但这会让 nupkg 从 26MB 膨胀到 350MB，破坏模块化拆分设计。

## 修复

正确的方案：让 v6 路径复用已有的 `GetNativeRuntimePlan()` + `InstallNativeRuntime()` 基础设施。

`InstallV6Model` 重写：

- 参数从 `(modelId, dryRun, json)` 扩展为 `(modelId, dryRun, source, allowUpstreamFallback, json)`
- dryRun 同时展示模型 CDN 计划 + native runtime NuGet 计划
- 实际安装时：
  1. 模型缓存已存在 → 检查 native runtime 是否就绪
  2. 模型缺失 → 先下载 CDN 模型，再部署 native runtime
  3. native runtime 缺失 → 单独部署（调用新方法 `InstallV6NativeRuntime`）
- 三个场景的 JSON 输出都包含完整信息

`InstallV6NativeRuntime` 新方法：封装 native runtime 从 NuGet 提取到 `runtimes/pp-ocrv6-{rid}/` 的流程，含成功/失败两种输出。

## 验证

- `--dry-run` → 同时展示 model + runtime 部署计划
- `--source /path/to/nupkg` → 从本地 nupkg 提取 11 个 native DLL 到 runtime cache
- `ocr check-env --json` → `localDotNetPpOcrV5: ok`, `localDotNetPpOcrV6: ok`
- 工具包尺寸不变（不往 csproj 加 native PackageReference）

## 影响

- `Angri450.Nong.Tool.Ocr` → 新版 4.1.4
- 不影响其他包
