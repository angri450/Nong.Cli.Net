# Angri450.Nong.ThirdParty

17 个开源库合一的 .NET 基础 DLL。angri450 手工合并、适配、裁剪，编译为单一程序集，尽量减少外部托管依赖，一个 DLL 替代一整张 NuGet 依赖图。

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.ThirdParty)](https://www.nuget.org/packages/Angri450.Nong.ThirdParty)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.ThirdParty
```

## Merged Libraries

angri450 将以下 17 个开源库的源码合入单一项目，解决了版本冲突、类型重复和依赖膨胀问题：

| Library | License | Purpose |
|---------|---------|---------|
| ClosedXML | MIT | Read/write Excel (.xlsx) |
| DocumentFormat.OpenXml | MIT | Low-level OpenXML operations |
| ScottPlot | MIT | Statistical chart rendering |
| MSAGL | MIT | Graph layout (Sugiyama, force-directed) |
| SkiaSharp | MIT | 2D graphics engine |
| HarfBuzzSharp | MIT | Text shaping |
| SixLabors.Fonts | Apache-2.0 | Font metrics and rendering |
| ExcelNumberFormat | MIT | Excel number format parsing |
| RBush | MIT | R-tree spatial indexing |
| SkiaSharp.HarfBuzz | MIT | SkiaSharp + HarfBuzz integration |
| ZXing.Net | Apache-2.0 | QR/barcode decoding |
| PdfPig | Apache-2.0 | PDF parsing, text/image extraction, and PDF writer utilities |

## Usage

所有合入库的 API 直接可用：

```csharp
using ClosedXML.Excel;
using ScottPlot;
using SkiaSharp;
using Microsoft.Msagl.Drawing;
using ZXing;
using UglyToad.PdfPig;
```

引用 ThirdParty 一个包，即可获得全部 17 个库的能力。

## Runtime Dependencies

SkiaSharp 和 HarfBuzzSharp 的本地二进制文件（约 65 MB）由 NuGet 在运行时自动按平台下载。ZXing.Net 和 PdfPig 以源码形式合入，不需要单独安装 `ZXing.Net` 或 `PdfPig` NuGet 包。

## When to Use

直接安装 `Angri450.Nong.ThirdParty` 以获得底层库的完全访问权。如果你已经引用了其他 Nong 包（Excel、Chart、Docx 等），它们会自动传递依赖 ThirdParty，无需重复安装。

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0（合入的库保留原始许可证 —— 参见源码中各 LICENSE/COPYING 文件）
