# 2026-06-11 nong word crop — content-aware image border trimming

## What changed

新命令 `nong word crop` 和新包 `Angri450.Nong.Imaging`。

### 新包：Angri450.Nong.Imaging

| 项目 | 值 |
|------|-----|
| 目录 | `Imaging/` |
| Assembly | `Angri450.Nong.Imaging` |
| 依赖 | ThirdParty.csproj（已有 SkiaSharp） |
| 文件 | ImageProcessor.cs, ImageContentBounds.cs, ImagingCore.csproj, README.md |

核心能力：
- `ImageProcessor.Analyze(byte[])` — 四边像素方差扫描，检测内容边界
- `ImageProcessor.AutoCrop(byte[])` — 分析 + 裁剪 + 5% 安全内边距
- `ImageProcessor.Crop(byte[], bounds)` — 精确裁剪到指定边界
- 不依赖 AI / 云端 — 纯像素级 SkiaSharp 运算

### 新命令：nong word crop

```
nong word crop <file.docx> -o <output.docx> --json
```

直接将所有图片的空白边缘裁掉，写入新 DOCX。源文件不动。

### 扩展：nong word images --analyze

```
nong word images <file.docx> --analyze --json
```

只分析不裁剪 — 返回每张图片可裁剪的像素数据（四边 margin、节省百分比）。

### CLI 只做路由

WordCommands 不碰 SkiaSharp。图像处理全在 ImagingCore，DOCX 拆包/装包全在 DocxCore（新增 DocxImageEditor.ReplaceImages + ExtractImageBytes）。

## 实战验证

对"北方荒山荒地艾草产业开发战略汇报书.docx"（129 段, 7 表, 11 图, 352 audit issues）：

```
nong word crop → 10/10 images cropped
nong word academic-format → 272 paragraphs + 7 tables formatted  
nong word format-audit → 66 issues (was 352)
```

0 COM, 0 Python, 0 external dependency.

## Files touched

- `Imaging/` — 新建包（4 文件）
- `Cli/Commands/WordCommands.cs` — 新增 CreateCrop, CreateImages 扩展 --analyze + --crop
- `Cli/Common/Manifest.cs` — 注册 word crop 命令
- `Cli/NongCli.csproj` — 添加 ProjectReference → Imaging
- `Docx/DocxImageEditor.cs` — 新建：DOCX 图片替换/提取工具
