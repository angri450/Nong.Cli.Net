# Dependency Control Plan

This file records external runtime/build dependencies that Nong intends to control through forks, vendored source, first-party runtime bundles, lock files, and package mirrors.

## Forked Upstreams

| Package(s) | Current version | Upstream | Fork | Current locked upstream commit | License | Control plan |
| --- | --- | --- | --- | --- | --- | --- |
| Docnet.Core | 2.6.0 | https://github.com/GowenGit/docnet | https://github.com/angri450/docnet | 03191d3a3eb27efa8b542bd637489b388d1ba316 | MIT | Managed wrapper source and PDFium runtime assets are vendored into `Angri450.Nong.Pdf`; no `Docnet.Core` NuGet package is used by `PdfCore`. |
| PdfPig | fork HEAD 0.1.15 source | https://github.com/UglyToad/PdfPig | https://github.com/angri450/PdfPig | 9ada00796d133587bea258b978012ab06fb05096 | Apache-2.0 plus bundled PDFBox/FontBox/Adobe notices | Main parser/writer source is vendored under `PdfPig/` and compiled by `ThirdParty`; no `PdfPig` NuGet package is used by `PdfCore` or PDF tests. |
| Sdcb.PaddleOCR, Sdcb.PaddleOCR.Models.Local, Sdcb.PaddleOCR.Models.LocalV5, Sdcb.PaddleOCR.Models.Shared, Sdcb.PaddleInference | 3.3.1 / 3.0.0 / 2.7.0.1 | https://github.com/sdcb/PaddleSharp | https://github.com/angri450/PaddleSharp | 139bc184a6d86c0c60bb8b8a90fb641b21c0b0e6 / 6a6adefaf3a4230a93e02a08e3da315eb4cfb782 / 51d451aeafb9d786da1873931692c18ea2400f38 | Apache-2.0 | Keep forked. Do not rush a source merge; local OCR depends on native Paddle/OpenCV runtime, so prioritize first-party runtime bundles, package mirrors, and hash validation. |
| OpenCvSharp4 | 4.11.0.20250507 | https://github.com/shimat/opencvsharp | https://github.com/angri450/opencvsharp | not recorded in package metadata | Apache-2.0 | Keep forked. Treat as native-heavy dependency; prefer mirrored packages and Nong runtime bundles over source merging. |
| YamlDotNet | 18.0.0 / 16.3.0 | https://github.com/aaubry/YamlDotNet | https://github.com/angri450/YamlDotNet | 6fc4590001f201adc1cfed7a821adc104ee309ec / ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73 | MIT | Forked for package availability. Consider unifying to one version before any source merge. |
| System.CommandLine | 2.0.0-beta4.22272.1 | https://github.com/dotnet/command-line-api | https://github.com/angri450/command-line-api | 209b724a3c843253d3071e8348c353b297b0b8b5 | MIT | Forked because CLI depends on a beta package. Prefer moving to a stable upstream or first-party package before broad distribution. |

## Third-party Source Snapshot Policy

Third-party source directories in this repository are source snapshots, not nested Git repositories.

Rules:

- Fork each upstream under `https://github.com/angri450/`.
- Pin each imported snapshot to a specific fork commit.
- Use `_archive/` for local fork clones, upstream checkout work, and temporary source archives; `_archive/` is intentionally ignored and must not be committed.
- Import only the source files, resources, license files, and notices needed by this repository.
- Delete the upstream `.git/` directory before copying the snapshot into this repository.
- Do not commit nested `.git/`, `.gitmodules`, gitlinks/submodules, upstream CI caches, package outputs, `bin/`, `obj/`, demos, examples, or tests unless a build path explicitly needs them.
- Record local adaptations in `CLAUDE.md` and, when practical, in a `NONG_SOURCE.md` file inside the vendored directory.
- If a snapshot must be refreshed, update the fork first, copy a clean source snapshot, then update the locked commit below.

## Third-party Source Snapshot Inventory

`ThirdParty/ThirdParty.csproj` currently compiles these source snapshot directories directly. The `Fork` and `Locked commit` columns are the checklist for the fork pass; fill `TBD` values after each fork is created and pinned.

| Local path | Upstream project | Upstream URL | Fork | Locked commit | License | Build status |
| --- | --- | --- | --- | --- | --- | --- |
| `Binding.Shared/` | SkiaSharp binding shared source | https://github.com/mono/SkiaSharp | https://github.com/angri450/SkiaSharp | TBD | MIT | Compiled by `ThirdParty`; excludes conditional shared delegate/polyfill files. |
| `ClosedXML/` | ClosedXML | https://github.com/ClosedXML/ClosedXML | https://github.com/angri450/ClosedXML | TBD | MIT | Compiled by `ThirdParty`; fonts embedded with upstream logical names. |
| `ClosedXML.IO/` | ClosedXML.IO | https://github.com/ClosedXML/ClosedXML.IO | https://github.com/angri450/ClosedXML.IO | TBD | MIT | Compiled by `ThirdParty`. |
| `ClosedXML.Parser/` | ClosedXML.Parser | https://github.com/ClosedXML/ClosedXML.Parser | https://github.com/angri450/ClosedXML.Parser | TBD | MIT | Compiled by `ThirdParty`. |
| `DocumentFormat.OpenXml/` | Open XML SDK | https://github.com/dotnet/Open-XML-SDK | https://github.com/angri450/Open-XML-SDK | TBD | MIT | Compiled by `ThirdParty`; generator projects remain separate analyzer projects. |
| `DocumentFormat.OpenXml.Framework/` | Open XML SDK framework | https://github.com/dotnet/Open-XML-SDK | https://github.com/angri450/Open-XML-SDK | TBD | MIT | Compiled by `ThirdParty`; resources keep upstream namespaces. |
| `DocumentFormat.OpenXml.Generator/` | Open XML SDK generator | https://github.com/dotnet/Open-XML-SDK | https://github.com/angri450/Open-XML-SDK | TBD | MIT | Kept as analyzer project, not merged into `ThirdParty`. |
| `DocumentFormat.OpenXml.Generator.Models/` | Open XML SDK generator models | https://github.com/dotnet/Open-XML-SDK | https://github.com/angri450/Open-XML-SDK | TBD | MIT | Kept as analyzer support project. |
| `ExcelNumberFormat/` | ExcelNumberFormat | https://github.com/andersnm/ExcelNumberFormat | https://github.com/angri450/ExcelNumberFormat | TBD | MIT | Compiled by `ThirdParty`. |
| `HarfBuzzSharp/` | HarfBuzzSharp managed binding | https://github.com/mono/SkiaSharp | https://github.com/angri450/SkiaSharp | TBD | MIT | Compiled by `ThirdParty`; native assets remain NuGet runtime packages. |
| `MSAGL/` | Microsoft Automatic Graph Layout | https://github.com/microsoft/automatic-graph-layout | https://github.com/angri450/automatic-graph-layout | TBD | MIT | Compiled by `ThirdParty`; aliases resolve `Path`/`Timer` conflicts. |
| `MSAGL.Drawing/` | MSAGL drawing model | https://github.com/microsoft/automatic-graph-layout | https://github.com/angri450/automatic-graph-layout | TBD | MIT | Compiled by `ThirdParty`. |
| `RBush/` | RBush.NET | https://github.com/viceroypenguin/RBush | https://github.com/angri450/RBush | TBD | MIT | Compiled by `ThirdParty`. |
| `ScottPlot/` | ScottPlot | https://github.com/ScottPlot/ScottPlot | https://github.com/angri450/ScottPlot | TBD | MIT | Compiled by `ThirdParty`; tests and conflicting global usings excluded. |
| `SixLabors.Fonts/` | SixLabors.Fonts | https://github.com/SixLabors/Fonts | https://github.com/angri450/Fonts | TBD | Apache-2.0 | Compiled by `ThirdParty`; shared infrastructure subset included. |
| `SkiaSharp/` | SkiaSharp managed binding | https://github.com/mono/SkiaSharp | https://github.com/angri450/SkiaSharp | TBD | MIT | Compiled by `ThirdParty`; adapted to available native asset version. |
| `SkiaSharp.HarfBuzz/` | SkiaSharp.HarfBuzz | https://github.com/mono/SkiaSharp | https://github.com/angri450/SkiaSharp | TBD | MIT | Compiled by `ThirdParty`. |
| `ClippitPowerTools/` | Open-Xml-PowerTools / Clippit | https://github.com/OpenXmlDev/Open-Xml-PowerTools | https://github.com/angri450/Open-Xml-PowerTools | TBD | MIT | Word-related source subset compiled by `ThirdParty`; Excel/PowerPoint/HTML/internal subsets excluded. |
| `ShapeCrawler/` | ShapeCrawler | https://github.com/ShapeCrawler/ShapeCrawler | https://github.com/angri450/ShapeCrawler | TBD | MIT | Compiled by `ThirdParty`; package project remains only as source metadata. |
| `ZXing.Net/` | ZXing.Net | https://github.com/micjahn/ZXing.Net | https://github.com/angri450/ZXing.Net | TBD | Apache-2.0 | QR decode subset compiled by `ThirdParty`; no `ZXing.Net` NuGet package used. |
| `PdfPig/` | PdfPig | https://github.com/UglyToad/PdfPig | https://github.com/angri450/PdfPig | 9ada00796d133587bea258b978012ab06fb05096 | Apache-2.0 plus bundled PDFBox/FontBox/Adobe notices | Parser/writer source and resources compiled by `ThirdParty`; license and notices packed. |
| `UnicodeTrieGenerator/` | SixLabors Unicode trie generator | https://github.com/SixLabors/Fonts | https://github.com/angri450/Fonts | TBD | Apache-2.0 | Three state-machine files compiled by `ThirdParty`. |
| `common/` | Compatibility shims from upstream sources | mixed upstreams | mixed forks | TBD | mixed | Only specific shims are compiled; do not treat as a standalone dependency. |
| `data/` | Open XML SDK generated data | https://github.com/dotnet/Open-XML-SDK | https://github.com/angri450/Open-XML-SDK | TBD | MIT | Used by OpenXml source generator through `data/OpenXmlData.targets`. |

## Runtime/Package Dependencies Not Source-Merged

| Dependency | Status |
| --- | --- |
| Docnet.Core | Source is vendored under `Pdf/Docnet/`; PDFium native assets are vendored under `Pdf/runtimes/` and packed by `Pdf/PdfCore.csproj`. |
| SkiaSharp/HarfBuzzSharp native assets | Native assets are locked via NuGet packages and mirrors. If full first-party control is needed, create Nong graphics runtime bundles instead of committing native binaries into `ThirdParty`. |
| OCR native runtime | Maintained in sibling repository `Nong.OcrRuntime`; Nong.NET consumes `Angri450.Nong.OcrRuntime.*` packages through `nong ocr install-model`. |

## Not Forked For Now

| Packages | Reason |
| --- | --- |
| System.* runtime packages | Microsoft platform packages; use lock files and package mirrors instead of forks. |
| xunit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio | Test-only dependencies; they do not ship in the user runtime path. |
| SkiaSharp/HarfBuzzSharp native assets | Native assets are locked and mirrored first. If full first-party control is needed, create Nong graphics runtime bundles instead of source-merging native binaries into `ThirdParty`. |

## Current Guardrails

- `Directory.Build.props` enables `RestorePackagesWithLockFile`.
- CI restore should run with locked mode through `ContinuousIntegrationBuild=true`.
- `packages.lock.json` files are committed for normal projects.
- Upstream package fallback for OCR native runtime is explicit only; default install uses Nong first-party runtime bundles.
