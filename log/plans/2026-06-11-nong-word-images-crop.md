# 2026-06-11 nong word images 图片裁剪施工方案

## 背景

实战文档（艾草产业汇报书）有 11 张图片，大量空白由"嵌入型"图片独占整行造成。用户希望：
1. 内容感知裁剪——去图片四边空白
2. 统一近似尺寸——缩放图片到一致宽度
3. 不依赖 COM——纯 .NET OOXML + SkiaSharp

## 新增包：Angri450.Nong.Imaging

| 项目 | 值 |
|------|-----|
| 目录 | `Imaging/` |
| Assembly | `Angri450.Nong.Imaging` |
| NuGet PackageId | `Angri450.Nong.Imaging` |
| 版本 | `4.0.0` |
| 依赖 | ThirdParty.csproj（已有 SkiaSharp） |
| 协议 | Apache-2.0 |

### 核心 API

```
Angri450.Nong.Imaging
├── ImageAnalyzer
│   ├── Analyze(byte[] imageBytes) → ImageContentBounds
│   │   └── 四边扫描，像素级检测内容区域边界
│   └── AnalyzeAll(IEnumerable<string> paths) → List<ImageContentBounds>
├── ImageCropper
│   ├── Crop(byte[] imageBytes, ImageContentBounds bounds) → byte[]
│   ├── AutoCrop(byte[] imageBytes, float safetyMargin = 0.05f) → byte[]
│   │   └── Analyze + Crop 一键，5% 安全内边距
│   └── ResizeUniform(IEnumerable<(byte[] data, string format)> images, int targetWidthPx) → List<byte[]>
└── ImageContentBounds (record)
    ├── OriginalWidth, OriginalHeight (px)
    ├── CropLeft, CropTop, CropRight, CropBottom (px)
    └── ContentWidth, ContentHeight (px)
```

### 边缘检测算法

1. 逐行扫描，计算像素方差
2. 方差阈值 < 2.0（接近纯色）视为空白
3. 从四边向内收缩，找到第一个"有内容"的行/列
4. safetyMargin 在裁完后从内容区域内缩 5%

## CLI 命令扩展

### 现状

`nong word images` — 列出/提取图片（只读）

### 新增选项

```
nong word images <file.docx> --analyze --json
  → 分析所有图片，返回每张图片的可裁剪边界
  → 不修改文件

nong word images <file.docx> --crop --out <file.docx> --json
  → AutoCrop 所有图片 + 替换回 DOCX
  → 源文件不修改

nong word images <file.docx> --crop --width 80mm --out <file.docx> --json
  → 裁剪后统一缩放到 80mm 宽
```

### CLI 路由

```
WordCommands.CreateImages() → 解析 --analyze / --crop / --width
  ├── --analyze: DocxCore.ExtractImageBytes() → ImagingCore.Analyze() → JSON
  └── --crop:    DocxCore.ExtractImageBytes() → ImagingCore.AutoCrop()
                 → DocxCore.ReplaceImages() → 输出 DOCX + JSON
```

CLI 层不碰 SkiaSharp，不写像素循环。

## 关联改动

| 位置 | 改动 |
|------|------|
| `Imaging/` | 新建包（4 文件） |
| `Imaging/README.md` | 包说明 |
| `Cli/Commands/WordCommands.cs` | CreateImages 扩展 `--analyze` `--crop` `--width` |
| `Cli/NongCli.csproj` | 添加 ProjectReference → Imaging |
| `Cli/Common/Manifest.cs` | 注册新参数 |
| `DocxCore/` | 可能需要 `ExtractImageBytes()` 和 `ReplaceImages()` 方法 |
| `Cli.Tests/CliContractTests.cs` | 新增测试 |
| `Nong.sln` | 添加 Imaging 项目 |

## 工作估算

| 步骤 | 预计时间 |
|------|---------|
| Imaging 包 + ImageAnalyzer + ImageCropper | 30 分钟 |
| DocxCore 图像提取/替换方法 | 20 分钟 |
| CLI 参数扩展 | 20 分钟 |
| 测试 | 20 分钟 |
| 编译 + 真实文档验证 | 15 分钟 |
| Changelog | 10 分钟 |
