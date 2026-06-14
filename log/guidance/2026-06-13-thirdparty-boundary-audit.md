# ThirdParty 边界审计

日期: 2026-06-13
状态: done

## 目的

评估 ThirdParty.dll (21.66 MB) 在每个工具包中的实际使用情况，判断是否需要从单一地基拆分为功能地基。

## ThirdParty 包含的 22 个源码目录

| 目录 | 上游 | 用途 |
|------|------|------|
| SkiaSharp | SkiaSharp | 2D 图形渲染 (Chart/Diagram/Imaging) |
| ScottPlot | ScottPlot | 科学图表 (Chart) |
| MSAGL | MSAGL | 图布局算法 (Diagram) |
| MSAGL.Drawing | MSAGL Drawing | 图绘制 (Diagram) |
| HarfBuzzSharp | HarfBuzzSharp | 文字 shaping (Chart/Diagram/Imaging) |
| SixLabors.Fonts | SixLabors.Fonts | 字体处理 |
| SkiaSharp.HarfBuzz | SkiaSharp.HarfBuzz | Skia-HarfBuzz bridge (Chart/Diagram/Imaging) |
| ClosedXML | ClosedXML | Excel 读写 (Excel) |
| ClosedXML.IO | ClosedXML.IO | Excel IO (Excel) |
| ClosedXML.Parser | ClosedXML.Parser | Excel 公式解析 (Excel) |
| ExcelNumberFormat | ExcelNumberFormat | Excel 数字格式 (Excel) |
| DocumentFormat.OpenXml | Open XML SDK | Office 文件格式 (Word/Excel/PPTX/Pdf) |
| DocumentFormat.OpenXml.Framework | Open XML SDK Framework | OOXML 框架 |
| PdfPig | PdfPig | PDF 解析/写入 (Pdf) |
| ShapeCrawler | ShapeCrawler | PPTX 处理 (Pptx) |
| ClippitPowerTools | Open-Xml-PowerTools | Word 处理增强 (Word) |
| ZXing.Net | ZXing.Net | QR 码解码 (Ocr/Imaging) |
| RBush | RBush.NET | R-tree 空间索引 |
| Binding.Shared | SkiaSharp binding | Skia 绑定共享 |
| UnicodeTrieGenerator | SixLabors | Unicode 字典生成 |
| common | 多上游 | 兼容 shim |
| data | Open XML SDK | 生成器数据 |

## 工具包到 ThirdParty 子组件的使用矩阵

| ThirdParty 子组件 | Chart | Diagram | Imaging | Pdf | Pptx | Ocr | Cli |
|---|---|---|---|---|---|---|---|
| SkiaSharp | YES | YES | YES | - | - | - | - |
| ScottPlot | YES | - | - | - | - | - | - |
| MSAGL | - | YES | - | - | - | - | - |
| MSAGL.Drawing | - | YES | - | - | - | - | - |
| HarfBuzzSharp | YES | YES | YES | - | - | - | - |
| SixLabors.Fonts | YES | YES | YES | - | - | - | - |
| SkiaSharp.HarfBuzz | YES | YES | YES | - | - | - | - |
| ClosedXML | - | - | - | - | - | - | YES |
| ClosedXML.IO | - | - | - | - | - | - | YES |
| ClosedXML.Parser | - | - | - | - | - | - | YES |
| ExcelNumberFormat | - | - | - | - | - | - | YES |
| OOXML | - | - | YES | YES | YES | - | YES |
| OOXML.Framework | - | - | YES | YES | YES | - | YES |
| PdfPig | - | - | - | YES | - | - | - |
| ShapeCrawler | - | - | - | - | YES | - | - |
| ClippitPowerTools | - | - | - | - | - | - | YES |
| ZXing.Net | - | - | YES | - | - | YES | - |
| 其他 (RBush/Binding/Unicode/common/data) | YES | YES | YES | YES | YES | YES | YES |

## 每个工具包中 ThirdParty.dll 的必要性判断

| 工具包 | 是否必须 ThirdParty.dll | 真正需要的子组件数量 | 当前浪费 |
|--------|----------------------|-------------------|---------|
| Cli | YES | ~12/22 (Excel/OOXML/Word) | 中等 |
| Chart | YES | ~8/22 (Skia/ScottPlot/Fonts) | 高: 含 Excel/OOXML/PdfPig 无用代码 |
| Diagram | YES | ~9/22 (Skia/MSAGL/Fonts) | 高: 含 Excel/PdfPig 无用代码 |
| Imaging | YES | ~10/22 (Skia/OOXML/Fonts/ZXing) | 高: 含 Excel/PdfPig 无用代码 |
| Pdf | YES | ~6/22 (PdfPig/OOXML) | 低: 主要是 PdfPig+OOXML |
| Pptx | YES | ~6/22 (OOXML/ShapeCrawler) | 低 |
| Ocr | YES | ~5/22 (ZXing + basic shims) | 低 |

## 拆分候选

### 候选 1: ThirdParty.OpenXml (Office 格式层)
- 包含: DocumentFormat.OpenXml, DocumentFormat.OpenXml.Framework, ClosedXML*, ExcelNumberFormat, ClippitPowerTools, ShapeCrawler
- 服务: Cli, Pptx, Imaging, Pdf (OOXML part)
- 估算体积: ~8 MB

### 候选 2: ThirdParty.Skia (图形渲染层)
- 包含: SkiaSharp, HarfBuzzSharp, SkiaSharp.HarfBuzz, SixLabors.Fonts, ScottPlot, MSAGL*
- 服务: Chart, Diagram, Imaging
- 估算体积: ~12 MB

### 候选 3: ThirdParty.Pdf (PDF 层)
- 包含: PdfPig
- 服务: Pdf
- 估算体积: ~3 MB

### 候选 4: ThirdParty.Core (基础设施层)
- 包含: RBush, Binding.Shared, UnicodeTrieGenerator, common, data, ZXing.Net
- 服务: 全部
- 估算体积: ~3 MB

## 当前决策: 暂不拆分

**理由**:

1. 瘦身后的三个最大包 (Chart/Diagram/Imaging) 已从 83MB 降到 26MB，均通过 pack audit 闸门 (<50MB)。
2. ThirdParty.dll 的 21.66 MB 是未压缩大小，在 nupkg 中压缩后约 10-12 MB。
3. 拆分 ThirdParty 会引入:
   - 4 个新 NuGet 包的维护负担
   - ProjectReference 矩阵复杂度翻倍
   - 版本同步成本
   - 可能引入循环依赖风险
4. 当前各工具包的体积在可接受范围内：
   - 轻工具 (Cli/Ocr/Pptx): 10-12 MB
   - 中工具 (Pdf): 29 MB
   - 重工具 (Chart/Diagram/Imaging): 26 MB

**触发拆分的条件**: 如果任一大包未来再次超过 50 MB 闸门。

## 建议

保持 ThirdParty.dll 作为合并地基，但后续可以：
1. 在 ThirdParty.csproj 中按条件编译 (#if SKIA / IF OOXML 等)，让各工具通过 MSBuild property 只编译自己需要的源码
2. 使用 InternalsVisibleTo + 多输出 DLL 方式替代单一巨型 DLL
3. 如果 SkiaSharp 在 .NET 9 中不再需要内部源码快照，可以切换为纯 NuGet 引用

## 不做事项

- 不新增独立 ThirdParty.* .csproj
- 不在本阶段拆 ThirdParty
- 不改变第三方源码快照策略

## 状态

done — 审计完成，结论: 暂不拆分。
