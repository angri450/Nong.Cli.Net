# B 线：外部源码合并记录

日期：2026-06-03
状态：B1 + B2 + B3 完成

---

## B1：gongwen-paiban → Inspect（已完成）

| 项目 | 详情 |
|------|------|
| 来源 | gongwen-paiban（MIT） |
| 目标 | `Inspect/`（Nong.Inspect 命名空间） |
| 文件数 | 5 个，+1715 行 |
| 核心文件 | GongWenFormatter.cs、GongWenStyleSpec.cs、GongWenFormatOptions.cs、GongWenNumberingResolver.cs、GongWenMarkerPatternInferrer.cs |
| 功能 | GB/T 9704 公文排版：五级段落角色识别 → 按标准清洗重排 → 页码分节 |
| 处理 | 命名空间 MiniMaxAIDocx.Core → Nong.Inspect，其余代码不变 |

---

## B2：Clippit Word 模块 → ThirdParty（已完成）

| 项目 | 详情 |
|------|------|
| 来源 | Clippit（MIT，Open-Xml-PowerTools 继承者） |
| 目标 | `ClippitPowerTools/`（Clippit 命名空间），通过 ThirdParty.csproj glob 编译 |
| 全量 | 100 个文件，~58K 行 |
| 合并策略 | 保留 Word 模块 + 公共文件 + Core，约 70 个文件 |

### 排除的模块及原因

| 模块 | 原因 |
|------|------|
| Excel/ | 依赖 ImageSharp，我们用 ClosedXML 替代 |
| PowerPoint/ | 依赖 ImageSharp，PPT 走 ShapeCrawler |
| Html/ | WmlToHtmlConverter + HtmlToWmlConverterCore 依赖 ImageSharp |
| Internal/ | TextReplacer 依赖 PmlDocument（已删除） |
| MetricsGetter.cs | 依赖 SmlDocument/PmlDocument |
| DocumentAssembler.cs | 依赖 ImageSharp（图片模板填充），已有 DocxTemplate 替代 |
| WmlToHtmlConverter.cs | 依赖 ImageSharp |
| OxPtHelpers.cs | 依赖 ImageSharp |
| Assembler/HtmlConverter.cs | 依赖 ImageSharp |

### 代码修改

| 文件 | 修改 |
|------|------|
| PtOpenXmlDocument.cs | 移除 SmlDocument/PmlDocument 工厂方法，仅保留 WmlDocument |
| WmlDocument.cs | 移除 TextReplacer.SearchAndReplace 方法 |
| WmlComparer.Private.Methods.Util.cs | 移除 using Clippit.Internal |
| PtOpenXmlUtil.cs | MetricsGetter.GetTextWidth → 估算值替代 |

### 保留的能力

- DocumentBuilder：docx 合并/拆分
- WmlComparer：文档语义 Diff（LCS+Jaccard）
- RevisionAccepter：Track Changes 批处理
- MarkupSimplifier：OOXML 标记简化
- WmlDocument：Word 文档加载/保存基类

---

## B3：ShapeCrawler 核心 → ThirdParty（已完成）

| 项目 | 详情 |
|------|------|
| 来源 | ShapeCrawler v0.79.2（MIT） |
| 目标 | `ShapeCrawler/`（ShapeCrawler 命名空间），通过 ThirdParty.csproj glob 编译 |
| 全量 | 249 个文件 |
| 合并策略 | 保留核心读写层，排除 Magick.NET 依赖和渲染模块，约 247 个文件 |

### 排除的文件

| 文件 | 原因 |
|------|------|
| Slides/Image.cs | Magick.NET 硬依赖（图片格式检测/SVG光栅化） |
| Slides/PictureShapeCollection.cs | Magick.NET 硬依赖（添加图片到幻灯片） |
| Slides/XmlPicture.cs | 依赖 DocumentFormat.OpenXml.Linq NuGet 包 |

### 代码修改（C# 14 → C# 12 降级）

| 文件 | C# 14 语法 | 修改 |
|------|-----------|------|
| Extensions/ShapePropertiesExtensions.cs | `extension()` 块 | 改写为标准 static 扩展方法 |
| Slides/UserSlide.cs | `field` 关键字 | 添加 `_fill` 显式 backing field |
| Texts/TextBox.cs | `?. =` null 条件赋值 | 改写为 `if != null` |
| Charts/IChartTitle.cs | `?. =` null 条件赋值 | 改写为 `if != null` |

### 其他修复

| 文件 | 问题 | 修复 |
|------|------|------|
| Charts/PieChart.cs 等 6 个文件 | `Index` 歧义（OpenXML vs System.Index） | → `DocumentFormat.OpenXml.Drawing.Charts.Index` |

### 保留的能力

- IPresentation：PPT 文件加载/保存
- IUserSlide / ISlideCollection：幻灯片访问
- IShape / IShapeCollection：形状读写和文本提取
- ITextBox / IParagraph：文本框和段落读取
- ITable：表格读取（合并单元格/行列）
- IMasterSlide / ILayoutSlide：母版和布局
- Colors / Fonts / Units：颜色/字体/单位工具

### 暂缺的能力（待后续补）

- 图片添加（PictureShapeCollection 需要 Magick.NET → SkiaSharp 重写）
- 幻灯片渲染为 PNG（依赖 SkiaSharp 渲染管线，ThirdParty 已有 SkiaSharp，但需要适配）
- 图表读写（ChartShape 等，依赖 SkiaSharp 渲染）

---

## ThirdParty 依赖变更

新增 NuGet 包：
- `System.IO.Hashing` 9.0.0（ImageSharp 被排除，但此包保留以防未来使用）
- `System.Resources.Extensions` 9.0.0（ShapeCrawler 需要）

新增 csproj glob：
- `..\ClippitPowerTools\**\*.cs`（排除 Excel/PowerPoint/Html）
- `..\ShapeCrawler\**\*.cs`（排除 XmlPicture.cs）

新增 csproj Remove：
- ClippitPowerTools/Properties/**
- ShapeCrawler/Properties/**

---

## 编译状态

| 项目 | 状态 |
|------|------|
| ThirdParty | 0 错误 |
| Docx | 0 错误 |
| Inspect | 0 错误 |

---

## 待办

- [ ] PictureShapeCollection 重写（Magick.NET → SkiaSharp/ImageSharp）
- [ ] 幻灯片渲染管线适配（SkiaSharp 已有，需接上）
- [ ] 图表模块适配
- [ ] ThirdParty README 更新致谢列表
- [ ] 根 README 添加致谢

---

## 排除模块对 CLI 的影响分析

### 结论：不影响 109 命令规划中的任何 P0/P1 命令

### 丢了的能力 vs CLI 需求

| 排除的模块 | 提供的能力 | 是否影响 CLI | 替代方案 |
|-----------|-----------|-------------|---------|
| Clippit Excel/ | Excel 读取和 HTML 转换 | 否 | ClosedXML 已在 ThirdParty 中，用于 `nong excel read/sheets` |
| Clippit PowerPoint/ | PPT 合并/拆分 | 否 | `nong pptx` 使用 ShapeCrawler 核心 |
| Clippit Html/ | docx → HTML 互转 | 否 | 109 命令规划中无 HTML 转换命令 |
| ImageSharp | 图片格式检测/转换 | 否 | 图片尺寸读取有 ImageHeaderReader，图片写有 SkiaSharp |
| ShapeCrawler Image.cs | 给幻灯片添加图片 | 否 | `nong pptx read` 只读不写 |
| ShapeCrawler PictureShapeCollection.cs | 给幻灯片添加图片 | 否 | 同上 |
| ShapeCrawler XmlPicture.cs | 创建 Picture XML | 否 | 同上 |
| ShapeCrawler Magick.NET | 图片格式检测/SVG光栅化 | 否 | 仅在"添加图片到幻灯片"时需要 |

### 保住的能力 vs CLI 命令

| 合并的模块 | 对应的 CLI 命令 |
|-----------|---------------|
| Clippit DocumentBuilder | `nong word merge` |
| Clippit WmlComparer | 文档差异比较（Inspect 审查层可用） |
| Clippit RevisionAccepter | Track Changes 批处理 |
| ShapeCrawler 核心读写 | `nong pptx read` / `nong pptx slides` |
| gongwen-paiban | `nong official format` |

### 为什么排除的这些模块不可惜

1. **重复造轮子**：Clippit Excel 模块跟 ClosedXML 功能重叠，选 ClosedXML（更成熟、star 更多）
2. **暂时不需要**：幻灯片加图片、docx↔HTML 互转都不在我们 CLI P0/P1 规划里
3. **成本不划算**：修 ImageSharp 的 1324 文件只为保住 Clippit 的 3 个 HTML 文件——得不偿失
4. **依赖链传染**：砍 Magick.NET（native C 库，不能吃源码）导致 PictureShapeCollection 不能用，但那是添图片功能，不是读PPT。读PPT 完全不受影响
