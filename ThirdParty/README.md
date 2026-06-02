# Angri450.Nong.ThirdParty

Foundation DLL for the Nong.NET toolkit. Merges source code from 15 open-source libraries into a single assembly. Zero external managed dependencies — one DLL, no NuGet graph.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.ThirdParty)](https://www.nuget.org/packages/Angri450.Nong.ThirdParty)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.ThirdParty
```

## Merged Libraries

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

## Usage

All merged library APIs are directly accessible:

```csharp
using ClosedXML.Excel;
using ScottPlot;
using SkiaSharp;
using Microsoft.Msagl.Drawing;
```

Build your app against ThirdParty and you get all 15 libraries with a single package reference.

## Runtime Dependencies

Native platform binaries for SkiaSharp and HarfBuzzSharp (~65 MB) are auto-downloaded at runtime via NuGet. These are platform-specific native DLLs, not managed code:

- `SkiaSharp.NativeAssets.Win32` / `macOS` / `Linux`
- `HarfBuzzSharp.NativeAssets.Win32` / `macOS` / `Linux`

No additional install steps needed — NuGet handles platform-specific asset selection automatically.

## When to Use This Package

Install `Angri450.Nong.ThirdParty` when you want direct access to the merged libraries. You do not need to install it separately if you already reference other Nong packages — they depend on it transitively.

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT (merged libraries retain their original licenses — see individual LICENSE files in source)
