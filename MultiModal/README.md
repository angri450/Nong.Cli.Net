# Angri450.Nong.MultiModal

多模态文档处理库。angri450 整合了云端 OCR（PaddleOCR-VL-1.6）、本地 CPU OCR（PP-OCRv5 .NET runtime）和纯 .NET 图像结构分析 —— 一条管线从扫描件直出 Word。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.MultiModal)](https://www.nuget.org/packages/Angri450.Nong.MultiModal)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Cloud OCR and image analysis are cross-platform. Local PP-OCRv5 uses the current-platform first-party `Angri450.Nong.OcrRuntime.*` native runtime bundle installed by `nong ocr install-model`. The runtime packages are maintained in the separate `Nong.OcrRuntime` repository for Windows x64, Linux x64, Linux arm64, macOS x64, and macOS arm64; each platform still needs target-machine smoke coverage before stable release claims.

## Install

```bash
dotnet add package Angri450.Nong.MultiModal
```

### 本地 OCR 部署

本地 OCR 使用 .NET/NuGet 部署，客户机不安装 Python、pip 或外部 OCR 可执行文件，也不在本机编译模型。managed ChineseV5 模型元数据随 CLI 引用，heavy native runtime 在独立 `Nong.OcrRuntime` 仓库按平台拆成 `Angri450.Nong.OcrRuntime.WinX64`、`LinuxX64`、`LinuxArm64`、`OsxX64`、`OsxArm64` 五个第一方包，并由 `nong ocr install-model` 从 NuGet 镜像/cache 部署。国内环境默认使用华为 NuGet v3 源：

`nong ocr install-model pp-ocrv5-mobile --json` 安装或检查成功后会自动清理 runtime cache 下的临时 `downloads` 目录，只长期保留推理所需 native runtime 文件。默认只安装 Nong 第一方 runtime 包；如确需临时回退上游 Sdcb/OpenCvSharp 包，显式添加 `--allow-upstream-fallback`。

```bash
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json
```

---

## 三大能力

angri450 整合的三条处理管线：

| 能力 | 类 | 依赖 |
|------|----|------|
| 云端 OCR | `PaddleOcrVlClient` | 网络 + `PADDLEOCR_ACCESS_TOKEN` |
| 图像分析 | `ImageAnalyzer` | 无（纯 .NET，通过 ThirdParty 中的 SkiaSharp） |
| 本地 OCR | `PpOcrV5Client` | Sdcb.PaddleOCR + ChineseV5 + current-platform `Angri450.Nong.OcrRuntime.*` runtime |

---

## ImageAnalyzer — 纯 .NET 图像结构分析

加载任意 PNG/JPEG 图片，获取详细的结构化报告 —— 无需 OCR、无需云 API。在代码中理解图像布局。

```csharp
using MultiModalCore;

var analyzer = new ImageAnalyzer();

// 从文件
var layout = analyzer.Analyze("diagram.png", targetWidth: 50);

// 从字节数组
var layout = analyzer.Analyze(imageBytes, targetWidth: 60);

// 打印 ASCII 图像布局图
Console.WriteLine(layout.AsciiMap);

// 检查空白率
Console.WriteLine($"Whitespace: {layout.WhitespaceRatio:P0}");

// 获取检测到的内容区域
foreach (var region in layout.Regions)
    Console.WriteLine($"[{region.Type}] at ({region.X},{region.Y}) {region.Width}x{region.Height}");

// 获取内容边界框（用于自动裁剪）
Console.WriteLine($"Content area: {layout.ContentWidth}x{layout.ContentHeight}");
```

### ImageLayout 属性

| Property | Type | Description |
|----------|------|-------------|
| `AsciiMap` | `string` | 像素到字符的文本映射 —— 打印即可"看到"布局 |
| `WhitespaceRatio` | `double` | 白色/空白像素占比（0–1） |
| `Regions` | `List<ContentRegion>` | 连通的非白色内容块 |
| `ContentMinX/Y` | `int` | 内容边界框左上角 |
| `ContentWidth/Height` | `int` | 内容边界框尺寸 |
| `BlackPixelCount` | `int` | 类文本暗像素数 |
| `GraphicPixelCount` | `int` | 彩色/图形像素数 |
| `EdgePixelCount` | `int` | 边缘/边框像素数 |

### ContentRegion 属性

| Property | Type | Description |
|----------|------|-------------|
| `X, Y, Width, Height` | `int` | 区域边界框（采样坐标） |
| `Type` | `RegionType` | `Text`、`Graphic`、`Edge` 或 `Background` |
| `PixelCount` | `int` | 该区域连通像素数 |

### 工作原理

angri450 的实现：通过 SkiaSharp 加载图片 → 降采样到目标宽度（默认 60 字符）→ 对每个采样块分类：白色（>240 亮度）、黑色（<40）、彩色、边缘（深灰）→ 洪水填充查找连通非白区域 → 返回 ASCII 图 + 区域列表 + 空白统计。

### ASCII 图字符

| 字符 | 含义 |
|------|------|
| ` ` (空格) | 白色/背景 |
| `#` | 黑色/深色文字 |
| `O` | 彩色图形 |
| `+` | 边缘/边框 |

### 典型用途

- 调试图表/图输出 —— 验证布局、空白率、内容位置
- 云端 OCR 预处理 —— 找到文字区域、跳过空白页
- 验证生成图片 —— 确认图表和图形正确渲染
- 图片质量检查 —— 检测过度留白、渲染异常

---

## 云端 OCR（PaddleOCR-VL-1.6）

```csharp
var client = new PaddleOcrVlClient();  // Token 从 PADDLEOCR_ACCESS_TOKEN 环境变量读取

// 文件 → Markdown
await client.ProcessAsync("scan.pdf", "output/");

// 文件 → Word（保留布局）
await client.ProcessToWordAsync("scan.pdf", "output/result.docx");

// URL → Markdown
await client.ProcessAsync("https://example.com/document.png", "output/");

// 原始字节 → Markdown
await client.ProcessAsync(fileBytes, "scan.png", "output/");
```

### 分步控制

```csharp
var jobId = await client.SubmitFileAsync("scan.pdf");
var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
var mdFiles = await client.DownloadResultsAsync(resultUrl, "output/");

// 结构化数据用于自定义处理
var ocrResult = await client.DownloadResultsStructuredAsync(resultUrl, "output/");
foreach (var page in ocrResult.Pages)
    foreach (var block in page.Blocks)
        Console.WriteLine($"[{block.Label}] {block.Content}");
```

### 选项

```csharp
var options = new OcrOptions
{
    UseDocOrientationClassify = true,   // 自动检测方向
    UseDocUnwarping = true,             // 文档展平
    UseChartRecognition = true,         // 图表识别
};
await client.ProcessAsync("scan.pdf", "output/", options);
```

---

## 本地 CPU OCR（PP-OCRv5 .NET runtime）

```csharp
using var local = new PpOcrV5Client();

var env = PpOcrV5Client.CheckEnvironment();
if (!env.Available) Console.WriteLine(env.Message);

var result = await local.RecognizeAsync("crop.png");
foreach (var page in result.Pages)
    foreach (var block in page.Blocks)
        Console.WriteLine($"[{block.Confidence:P0}] {block.Text}");
```

CLI `nong ocr local` performs a lightweight preflight before PP-OCRv5 inference. It first tries ZXing.Net barcode/QR decoding from the source merged into `Angri450.Nong.ThirdParty`, then falls back to image-structure heuristics for code-like or graphic-heavy non-text images. QR/barcode/code-like crops are skipped with `E006 validation_failed` and a `local_ocr_preflight_skipped` issue so the local OCR runtime does not spend tens of seconds hallucinating text from dense code/graphic patterns. Use `nong ocr local <image> --force --json` only when text OCR is explicitly required despite the warning.

---

## Word 输出管线

angri450 设计的 `ProcessToWordAsync` 生成保留布局的 `.docx`：

1. 云端 API 返回 `parsing_res_list` — 每个块包含 `block_label`、`block_content`、`block_bbox`
2. `LayoutToWordConverter` 将块映射为 Docx 原语：
   - `doc_title` → Title、`paragraph_title` → Heading、`text` → Body
   - `image` → 嵌入图片（实际下载）、`table` → OpenXML 表格
   - `vision_footnote` → Footnote
3. 多栏页面从 `block_bbox` 坐标自动检测
4. `ElementOrder.RectifyTree()` 在保存前修复 OpenXML 排序

## Dependencies

- `Angri450.Nong.Docx` — Word 生成（`ProcessToWordAsync` 输出用）
- `Angri450.Nong.ThirdParty` — SkiaSharp（ImageAnalyzer 使用）+ ZXing.Net decode-only source subset（OCR preflight 使用）

## API Reference

### ImageAnalyzer

| Method | Description |
|--------|-------------|
| `Analyze(path, targetWidth)` | 从文件路径分析图像 |
| `Analyze(bytes, targetWidth)` | 从字节数组分析图像 |
| `Analyze(bitmap, targetWidth)` | 从 SKBitmap 分析 |

### PaddleOcrVlClient (云端)

| Method | Description |
|--------|-------------|
| `ProcessAsync(input, outputDir)` | 提交 → 等待 → 下载 Markdown |
| `ProcessToWordAsync(input, docxPath)` | 提交 → 等待 → 下载 → 生成 Word |
| `SubmitFileAsync(path)` | 提交本地文件，返回 jobId |
| `SubmitBytesAsync(bytes, name)` | 提交内存数据 |
| `SubmitUrlAsync(url)` | 提交远程 URL |
| `WaitForJobAsync(jobId, interval)` | 轮询直到完成，返回结果 URL |
| `DownloadResultsAsync(resultUrl, dir)` | 下载 Markdown + 图片 |
| `DownloadResultsStructuredAsync(resultUrl, dir)` | 下载并返回 `OcrResult` |

### PpOcrV5Client (本地 CPU)

| Method | Description |
|--------|-------------|
| `RecognizeAsync(path)` | OCR 单张图片 |
| `CheckEnvironment()` | 验证 .NET PP-OCRv5 runtime 与内置 ChineseV5 模型 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0
