# MultiModal 新能力：ImageAnalyzer 纯 .NET 图像结构分析

**时间**: 2026-06-01
**影响包**: Angri450.Nong.MultiModal (3.0.2 → 3.0.6)
**类型**: New Feature

## 新增类

`MultiModalCore.ImageAnalyzer` — 纯 .NET 图像结构分析器。

## API

```csharp
var analyzer = new ImageAnalyzer();

// 从文件/字节/SKBitmap 分析
var layout = analyzer.Analyze("image.png", targetWidth: 50);

// 输出
Console.WriteLine(layout.AsciiMap);          // 像素→字符文本地图
Console.WriteLine(layout.WhitespaceRatio);   // 空白比例 0-1
foreach (var r in layout.Regions)            // 连通内容区块
    Console.WriteLine($"[{r.Type}] {r.X},{r.Y} {r.Width}x{r.Height}");
```

## ImageLayout 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| AsciiMap | string | 可直接打印的字符布局 |
| WhitespaceRatio | double | 空白比例 |
| Regions | List<ContentRegion> | 连通内容区块 |
| ContentWidth/Height | int | 内容区尺寸 |
| ContentMinX/Y | int | 内容区起点 |
| BlackPixelCount | int | 暗色文字像素数 |
| GraphicPixelCount | int | 彩色图形像素数 |
| EdgePixelCount | int | 边缘/边框像素数 |

## 实现原理

1. SkiaSharp 解码图像
2. 降采样到 targetWidth
3. 像素分类：白(>240)、黑(<40)、彩色(RGB 差>30)、边缘(<100)
4. 泛洪填充检测连通区域
5. 返回结构化结果

## 依赖

零额外依赖。SkiaSharp 已在 ThirdParty 中合并。

## 三能力架构

| 能力 | 类 | 用途 |
|------|-----|------|
| Cloud OCR | PaddleOcrVlClient | 文字识别、表格解析 |
| Image Analysis | ImageAnalyzer | 布局分析、质量验证 |
| Local OCR | LocalOcrClient | 离线文字识别 |

## 技能须知

- MultiModal skill 应新增 ImageAnalyzer 完整文档
- 适用于：作图质量验证、OCR 预处理空白页过滤、布局调试
- 不适用于：精确文字提取（请用 Cloud OCR）
