# 2026-06-10 Nong.Cli.Net 包依赖全景图

## 目的

这份文件是 Nong.Cli.Net 4.0.0 的包依赖关系权威参考。每次新增命令或改动包依赖时更新。

## 包层级总图

```
                    NongCli（CLI 统一入口，dotnet tool）
                   /    |    |    |    |    |    |    |    |    |    |    \
                  /     |    |    |    |    |    |    |    |    |    |     \
          DocxCore  Inspect Excel Chart Diagram Pptx PDF MultiModal Literature Genre Bioicons Pandoc SkillMgr
            |    \     |     |     |    \     |   |     |                   |
            |     \    |     |     |     \    |   |     |                   |
       ThirdParty  PandocCore  |     |   ThirdParty | ThirdParty            |
            |              \   |     |         \   |                        |
            |               \  |     |          \  |                        |
       [22 sources]      ThirdParty |       ThirdParty                 YamlDotNet
                              \     |          /
                               \    |         /
                              PandocCore  Bioicons
```

## 14 个 NuGet 包（全部 4.0.0，Apache-2.0）

| PackageId | 源 .csproj | 类型 | 外部 NuGet 依赖 |
|-----------|-----------|------|----------------|
| `Angri450.Nong.ThirdParty` | `ThirdParty/ThirdParty.csproj` | 地基库 | System.IO.Packaging 10.0.2, SkiaSharp.* 3.119.0, HarfBuzzSharp.* 8.3.1.1, System.IO.Hashing 9.0.0, System.Resources.Extensions 9.0.0 |
| `Angri450.Nong.Excel` | `Excel/ExcelCore.csproj` | 核心库 | System.IO.Packaging 10.0.2 |
| `Angri450.Nong.Chart` | `Chart/ChartCore.csproj` | 核心库 | 无（全来自 ThirdParty） |
| `Angri450.Nong.Diagram` | `Diagram/DiagramCore.csproj` | 核心库 | 无 |
| `Angri450.Nong.Docx` | `Docx/DocxCore.csproj` | 核心库 | 无 |
| `Angri450.Nong.Pptx` | `Pptx/PptxCore.csproj` | 核心库 | 无 |
| `Angri450.Nong.Pdf` | `Pdf/PdfCore.csproj` | 核心库 | 无（PDFium native 内嵌） |
| `Angri450.Nong.MultiModal` | `MultiModal/MultiModalCore.csproj` | 核心库 | Sdcb.PaddleOCR 3.3.1, Sdcb.PaddleOCR.Models.Local 3.3.1 |
| `Angri450.Nong.Bioicons` | `Bioicons/Bioicons.csproj` | 核心库 | 无 |
| `Angri450.Nong.Literature` | `Literature/LiteratureCore.csproj` | 核心库 | 无 |
| `Angri450.Nong.Pandoc` | `Pandoc/PandocCore.csproj` | 核心库 | 无 |
| `Angri450.Nong.Genre` | `Genre/Genre.csproj` | 核心库 | 无 |
| `Angri450.Nong.Inspect` | `Inspect/Inspect.csproj` | 核心库 | 无 |
| `Angri450.Nong.Cli` | `Cli/NongCli.csproj` | CLI 工具 | System.CommandLine 2.0.0-beta4.22272.1 |

非 NuGet 项目：
- `SkillManagerCore/SkillManagerCore.csproj` — 被 Cli 引用但不单独打包（YamlDotNet 18.0.0）
- `Tests/Tests.csproj` — 测试
- `Cli.Tests/Cli.Tests.csproj` — CLI 测试
- `ShapeCrawler/ShapeCrawler.csproj` — 元数据/源码被 ThirdParty 编译
- `DocumentFormat.OpenXml.Generator/*.csproj` — Roslyn 分析器

## ProjectReference 依赖矩阵

```
ThirdParty        → 无项目依赖（纯第三方源码编译）
PandocCore        → 无项目依赖（叶子节点）
LiteratureCore    → 无项目依赖（叶子节点）
Genre             → 无项目依赖（叶子节点）
Bioicons          → 无项目依赖（叶子节点）
SkillManagerCore  → 无项目依赖（叶子节点）

ExcelCore         → ThirdParty, PandocCore
ChartCore         → ThirdParty
DiagramCore       → ThirdParty, Bioicons
DocxCore          → ThirdParty, PandocCore
PptxCore          → ThirdParty, PandocCore
PdfCore           → ThirdParty
Inspect           → ThirdParty, DocxCore
MultiModalCore    → DocxCore (间接到 ThirdParty)

NongCli           → 以上全部 13 个 ProjectReference + System.CommandLine
```

## 依赖约束

1. **MultiModalCore → DocxCore 是单向依赖**。DocxCore 不能引用 MultiModalCore（会循环依赖）。OCR 转 Word 的桥接在 CLI 层做 adapter。
2. **ThirdParty 是唯一包含外部 NuGet 原生包的地基层**。其他核心库通过 ProjectReference 间接到 SkiaSharp/HarfBuzzSharp/System.IO.Packaging。
3. **MultiModalCore 是唯一引用 Sdcb.PaddleOCR NuGet 的库**。OCR runtime 模型包（`Angri450.Nong.OcrRuntime.*`）是独立仓库，不在这里编译。
4. **PandocCore 是纯合约层**。它定义 NongMark/v1 AST、切片包读/写契约，被 Excel/Docx/Pptx/Pdf/Slice 五个模块共同消费。
5. **LiteratureCore 无外部 NuGet 依赖**。纯 HttpClient + System.Text.Json + System.Xml.Linq。

## ThirdParty 包含的 22 个第三方源码目录

所有通过 `ThirdParty.csproj` 的 glob 直接编译为一个 DLL：

| 目录 | 上游 | 许可证 | 用途 |
|------|------|--------|------|
| `ClosedXML/` | ClosedXML | MIT | Excel 读写 |
| `ClosedXML.IO/` | ClosedXML.IO | MIT | Excel IO |
| `ClosedXML.Parser/` | ClosedXML.Parser | MIT | Excel 公式解析 |
| `DocumentFormat.OpenXml/` | Open XML SDK | MIT | Office 文件格式 |
| `DocumentFormat.OpenXml.Framework/` | Open XML SDK Framework | MIT | OOXML 框架 |
| `ExcelNumberFormat/` | ExcelNumberFormat | MIT | Excel 数字格式 |
| `HarfBuzzSharp/` | HarfBuzzSharp | MIT | 文字 shaping |
| `MSAGL/` | MSAGL | MIT | 图布局算法 |
| `MSAGL.Drawing/` | MSAGL Drawing | MIT | 图绘制 |
| `RBush/` | RBush.NET | MIT | R-tree 空间索引 |
| `ScottPlot/` | ScottPlot | MIT | 科学图表 |
| `SixLabors.Fonts/` | SixLabors.Fonts | Apache-2.0 | 字体处理 |
| `SkiaSharp/` | SkiaSharp | MIT | 2D 图形渲染 |
| `SkiaSharp.HarfBuzz/` | SkiaSharp.HarfBuzz | MIT | 文字 shaping 桥接 |
| `ClippitPowerTools/` | Open-Xml-PowerTools | MIT | Word 处理增强 |
| `ShapeCrawler/` | ShapeCrawler | MIT | PPTX 处理 |
| `ZXing.Net/` | ZXing.Net | Apache-2.0 | QR 码解码 |
| `PdfPig/` | PdfPig | Apache-2.0 | PDF 解析/写入 |
| `Binding.Shared/` | SkiaSharp binding | MIT | Skia 绑定共享 |
| `UnicodeTrieGenerator/` | SixLabors | Apache-2.0 | Unicode 字典生成 |
| `common/` | 多上游 | 混合 | 兼容 shim |
| `data/` | Open XML SDK | MIT | 生成器数据 |

## 命令模块到核心库的映射

| CLI 模块 | 子命令 | 核心库 | Toolkit skill |
|----------|--------|--------|---------------|
| `word` | 39 | DocxCore + ThirdParty + PandocCore | word |
| `inspect` | 11 | Inspect + DocxCore + ThirdParty | inspect |
| `chart` | 7 | ChartCore + ThirdParty | chart |
| `ocr` | 7 | MultiModalCore + DocxCore | multimodal→ocr |
| `excel` | 5 | ExcelCore + ThirdParty + PandocCore | excel |
| `lit` | 5 | LiteratureCore | literature |
| `pdf` | 4 | PdfCore + ThirdParty | pdf |
| `slice` | 4 | PandocCore | slice |
| `skill` | 4 | SkillManagerCore | skill |
| `diagram` | 3 | DiagramCore + ThirdParty + Bioicons | diagram |
| `pptx` | 3 | PptxCore + ThirdParty + PandocCore | pptx |
| `genre` | 2 | Genre | genre |
| `icons` | 2 | Bioicons | icons |
| `progress` | 1 | CLI 内部 | progress-report |

## OCR Runtime 特殊链路

`nong ocr` → `Angri450.Nong.MultiModal` (NuGet) → `Sdcb.PaddleOCR` + `Sdcb.PaddleOCR.Models.Local` (NuGet 上游) → `Angri450.Nong.OcrRuntime.*` (独立仓库 `Nong.OcrRuntime`，5 个平台包：WinX64/LinuxX64/macOSX64/WinArm64/LinuxArm64)

OCR runtime 模型包不在本仓库编译。`Nong.OcrRuntime` 是独立仓库，本仓库只消费已发布的 runtime 版本。

## 版本更新规则

1. 所有 14 个 NuGet 包版本号同步（当前均为 4.0.0）。
2. `packages.lock.json` 用 `dotnet restore --use-lock-file --force-evaluate` 重算，不要手改。
3. 发布顺序：先推所有核心库 NuGet 包，再推 CLI tool 包。
4. OCR runtime 包版本独立管理，不跟随 CLI 版本号。

## 状态

reference — 每次包结构变化时更新。
