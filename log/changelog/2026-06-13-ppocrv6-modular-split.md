# PP-OCRv6 与 CLI 模块化拆分审计修复

## 概览

本轮把“主 `nong` 轻路由 + 重功能独立 dotnet tool”的拆分链路修到可构建、可路由、可测试状态。

实际本地 pack 结果：

| 包 | nupkg 大小 |
| --- | ---: |
| `Angri450.Nong.Cli` | 12.04 MB |
| `Angri450.Nong.MultiModal` / `nong-ocr` | 12.14 MB |
| `Angri450.Nong.Pptx` / `nong-pptx` | 10.73 MB |
| `Angri450.Nong.Pdf` / `nong-pdf` | 28.68 MB |
| `Angri450.Nong.Chart` / `nong-chart` | 83.37 MB |
| `Angri450.Nong.Diagram` / `nong-diagram` | 83.37 MB |
| `Angri450.Nong.Imaging` / `nong-imaging` | 83.70 MB |

结论：主 CLI 已明显变轻；但 chart / diagram / imaging 单个工具包仍然很大，模块化只解决“用户不用就不下载”，没有解决“重工具包自己上传/分发过大”的根因。

## 已修复

- 主 `nong` 对 `chart` / `diagram` / `ocr` / `pdf` / `pptx` 做外部工具路由。
- `CliHelpers.RunTool` 改用 `ProcessStartInfo.ArgumentList`，避免路径和参数带空格时被字符串拼接破坏。
- 独立工具入口支持短形式：
  - `nong-chart bar ...`
  - `nong-diagram flowchart ...`
  - `nong-ocr models ...`
  - `nong-pdf check ...`
  - `nong-pptx read ...`
- `nong-chart` / `nong-diagram` 恢复内部 `__render-worker`，修复拆分后 PNG 渲染输出非 JSON 的问题。
- `nong-pdf` 增加 `PdfNativeRuntime` resolver，从 `runtimes/<rid>/native/pdfium.*` 加载 PDFium，修复 `pdf merge` / `pdf split` 找不到 `pdfium` 的问题。
- `nong-imaging` 增加 DOCX 图片分析/裁剪入口，`word images --analyze`、`word images --crop`、`word crop` 改为转发到 `nong-imaging`。
- `PdfOcrRecognizerAdapter` 改为通过 `nong-ocr local <image> --force --json` 外部调用，避免 `nong-pdf` 重新引用 OCR 模块。
- `Cli.Tests` 改为测试拆分后的本地工具产物，自动把各 `*/tools/bin/Release/net8.0` 加到 PATH。

## 文件改动

- `Cli/Program.cs`
- `Cli/Common/CliHelpers.cs`
- `Cli/NongCli.csproj`
- `Cli/Commands/WordCommands.cs`
- `Cli/Commands/PdfCommands.cs`
- `Cli/Adapters/PdfOcrRecognizerStub.cs`
- `Pdf/PdfNativeRuntime.cs`
- `Chart/tools/Program.cs`
- `Chart/tools/ChartRenderWorkerCommands.cs`
- `Diagram/tools/Program.cs`
- `Diagram/tools/DiagramRenderWorkerCommands.cs`
- `Pdf/tools/Program.cs`
- `Pptx/tools/Program.cs`
- `MultiModal/tools/Program.cs`
- `Imaging/tools/Program.cs`
- `MultiModal/tools/nong-ocr.csproj`
- `Imaging/tools/nong-imaging.csproj`
- `Cli.Tests/*`

## 验证

- `dotnet build Cli\NongCli.csproj -c Release --no-restore`
- `dotnet build Chart\tools\nong-chart.csproj -c Release --no-restore`
- `dotnet build Diagram\tools\nong-diagram.csproj -c Release --no-restore`
- `dotnet build Pdf\tools\nong-pdf.csproj -c Release --no-restore`
- `dotnet build Pptx\tools\nong-pptx.csproj -c Release --no-restore`
- `dotnet build MultiModal\tools\nong-ocr.csproj -c Release --no-restore`
- `dotnet build Imaging\tools\nong-imaging.csproj -c Release --no-restore`
- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-build`：154 passed
- 本地冒烟：
  - `nong chart histogram ... --json` 实际生成 PNG
  - `nong diagram flowchart ... --json` 实际生成 PNG
  - `nong pdf merge/split ... --json` 实际生成 PDF
  - `nong ocr models --json` 返回 PP-OCRv6 模型状态
  - `nong-imaging images missing.docx --analyze --json` 返回结构化 E001

## 剩余风险

- 工具包 `PackageId` 仍复用库包 ID，例如 `Angri450.Nong.Chart` 现在既像库包又像 dotnet tool 包。后续应改成独立工具 ID，例如 `Angri450.Nong.Tool.Chart`，否则 NuGet 身份语义会混乱。
- chart / diagram / imaging 仍把全平台 SkiaSharp / HarfBuzz native assets 放进单包，所以单个工具包仍约 83 MB。要解决上传/分发压力，需要 RID 拆包或只发布当前平台工具包。
- `ThirdParty.dll` 仍约 21.66 MB，是每个工具包的共同大头。后续若继续瘦身，需要拆 ThirdParty 的功能边界，而不是只拆 CLI。
- OCR 命令文案仍有 PP-OCRv5/PP-OCRv6 混用痕迹，功能可跑，但发布前需要统一用户可见描述。
