# Angri450.Nong.MultiModal

Multi-modal document processing library. Cloud OCR (PaddleOCR-VL-1.6), local CPU OCR (PaddleOCR), and pure .NET image structure analysis. Speech-to-text and text-to-speech planned.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.MultiModal)](https://www.nuget.org/packages/Angri450.Nong.MultiModal)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.MultiModal
```

### Optional: local OCR

```bash
pip install paddlepaddle paddleocr
```

Skip if you only use cloud API or image analysis — no Python required for those.

---

## Three Capabilities

| Capability | Class | Dependencies |
|-----------|-------|-------------|
| Cloud OCR | `PaddleOcrVlClient` | Network + `PADDLEOCR_TOKEN` |
| Image Analysis | `ImageAnalyzer` | None (pure .NET via SkiaSharp in ThirdParty) |
| Local OCR | `LocalOcrClient` | Python + PaddleOCR |

---

## ImageAnalyzer — Pure .NET Image Structure Analysis

Load any PNG/JPEG image and get a detailed structural report — no OCR, no Python, no cloud API. Understand image layout in code.

```csharp
using MultiModalCore;

var analyzer = new ImageAnalyzer();

// From file
var layout = analyzer.Analyze("diagram.png", targetWidth: 50);

// From bytes
var layout = analyzer.Analyze(imageBytes, targetWidth: 60);

// Print an ASCII map of the image layout
Console.WriteLine(layout.AsciiMap);

// Check whitespace ratio
Console.WriteLine($"Whitespace: {layout.WhitespaceRatio:P0}");

// Get detected content regions
foreach (var region in layout.Regions)
    Console.WriteLine($"[{region.Type}] at ({region.X},{region.Y}) {region.Width}x{region.Height}");

// Get content bounding box (useful for auto-cropping)
Console.WriteLine($"Content area: {layout.ContentWidth}x{layout.ContentHeight}");
```

### ImageLayout Properties

| Property | Type | Description |
|----------|------|-------------|
| `AsciiMap` | `string` | Pixel-to-character text map — print to "see" layout |
| `WhitespaceRatio` | `double` | Percentage of white/blank pixels (0–1) |
| `Regions` | `List<ContentRegion>` | Connected non-white content blocks |
| `ContentMinX/Y` | `int` | Top-left of content bounding box |
| `ContentWidth/Height` | `int` | Size of content bounding box |
| `BlackPixelCount` | `int` | Text-like dark pixels |
| `GraphicPixelCount` | `int` | Color/graphic pixels |
| `EdgePixelCount` | `int` | Edge/border pixels |

### ContentRegion Properties

| Property | Type | Description |
|----------|------|-------------|
| `X, Y, Width, Height` | `int` | Region bounding box in sample coordinates |
| `Type` | `RegionType` | `Text`, `Graphic`, `Edge`, or `Background` |
| `PixelCount` | `int` | Number of connected pixels in this region |

### How It Works

1. Load image via SkiaSharp → decode pixels
2. Downsample to target width (default 60 chars wide)
3. Classify each sample block: white (>240 brightness), black (<40), graphic (colorful), edge (dark gray)
4. Flood-fill to find connected non-white regions
5. Return ASCII map + region list + whitespace stats

### ASCII Map Characters

| Char | Meaning |
|------|---------|
| ` ` (space) | White/background |
| `#` | Black/dark text |
| `O` | Colored graphics |
| `+` | Edge/border |

### Use Cases

- **Debug diagram/chart output** — verify layout, whitespace ratio, content positioning
- **Pre-process before cloud OCR** — find text regions, skip blank pages
- **Validate generated images** — check that charts and diagrams render correctly
- **Image quality checks** — detect excessive whitespace, broken rendering

---

## Cloud OCR (PaddleOCR-VL-1.6)

```csharp
var client = new PaddleOcrVlClient();  // Token from PADDLEOCR_TOKEN env var

// File → Markdown
await client.ProcessAsync("scan.pdf", "output/");

// File → Word (layout-preserving)
await client.ProcessToWordAsync("scan.pdf", "output/result.docx");

// URL → Markdown
await client.ProcessAsync("https://example.com/document.png", "output/");

// Raw bytes → Markdown
await client.ProcessAsync(fileBytes, "scan.png", "output/");
```

### Step-by-Step Control

```csharp
var jobId = await client.SubmitFileAsync("scan.pdf");
var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
var mdFiles = await client.DownloadResultsAsync(resultUrl, "output/");

// Structured data for custom processing
var ocrResult = await client.DownloadResultsStructuredAsync(resultUrl, "output/");
foreach (var page in ocrResult.Pages)
    foreach (var block in page.Blocks)
        Console.WriteLine($"[{block.Label}] {block.Content}");
```

### Options

```csharp
var options = new OcrOptions
{
    UseDocOrientationClassify = true,   // Auto-detect orientation
    UseDocUnwarping = true,             // Document unwarping
    UseChartRecognition = true,         // Chart parsing
};
await client.ProcessAsync("scan.pdf", "output/", options);
```

---

## Local CPU OCR (PaddleOCR)

```csharp
var local = new LocalOcrClient(pythonExe: "python", lang: "ch");

var (ok, msg) = await local.CheckEnvironmentAsync();
if (!ok) Console.WriteLine("Install: pip install paddlepaddle paddleocr");

var blocks = await local.RecognizeAsync("crop.png");
foreach (var b in blocks)
    Console.WriteLine($"[{b.Confidence:P0}] {b.Text}");
```

---

## Word Output Pipeline

`ProcessToWordAsync` produces layout-preserving `.docx`:

1. Cloud API returns `parsing_res_list` — each block has `block_label`, `block_content`, `block_bbox`
2. `LayoutToWordConverter` maps blocks to Docx primitives:
   - `doc_title` → Title, `paragraph_title` → Heading, `text` → Body
   - `image` → embedded image (actual download), `table` → OpenXML table
   - `vision_footnote` → Footnote
3. Multi-column pages auto-detected from `block_bbox` coordinates
4. `ElementOrder.RectifyTree()` fixes OpenXML ordering before save

## Dependencies

- `Angri450.Nong.Docx` — Word generation for `ProcessToWordAsync` output
- `Angri450.Nong.ThirdParty` — SkiaSharp (merged, used by ImageAnalyzer)

## API Reference

### ImageAnalyzer

| Method | Description |
|--------|-------------|
| `Analyze(path, targetWidth)` | Analyze image from file path |
| `Analyze(bytes, targetWidth)` | Analyze image from byte array |
| `Analyze(bitmap, targetWidth)` | Analyze from SKBitmap |

### PaddleOcrVlClient (Cloud)

| Method | Description |
|--------|-------------|
| `ProcessAsync(input, outputDir)` | Submit → wait → download Markdown |
| `ProcessToWordAsync(input, docxPath)` | Submit → wait → download → Word |
| `SubmitFileAsync(path)` | Submit local file, returns jobId |
| `SubmitBytesAsync(bytes, name)` | Submit in-memory data |
| `SubmitUrlAsync(url)` | Submit remote URL |
| `WaitForJobAsync(jobId, interval)` | Poll until done, returns result URL |
| `DownloadResultsAsync(resultUrl, dir)` | Download Markdown + images |
| `DownloadResultsStructuredAsync(resultUrl, dir)` | Download and return `OcrResult` |

### LocalOcrClient (CPU)

| Method | Description |
|--------|-------------|
| `RecognizeAsync(path)` | OCR a single image |
| `RecognizeAsync(bytes)` | OCR from memory |
| `RecognizeBatchAsync(paths)` | OCR multiple images |
| `CheckEnvironmentAsync()` | Verify Python + PaddleOCR |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
