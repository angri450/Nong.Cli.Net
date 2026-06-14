# B 线：外部源码合并路线

日期：2026-06-03
状态：规划中

---

## 一、已有（ThirdParty 已合并，15 个库）

| 库 | 协议 | 用途 |
|----|------|------|
| ClosedXML | MIT | Excel 读写 |
| ClosedXML.IO | MIT | Excel 流式 IO |
| ClosedXML.Parser | MIT | Excel 公式解析 |
| DocumentFormat.OpenXml | MIT | OOXML 底层操作 |
| DocumentFormat.OpenXml.Framework | MIT | OOXML 框架 |
| ScottPlot | MIT | 60+ 科学图表 |
| MSAGL | MIT | 图布局（Sugiyama/力导向） |
| MSAGL.Drawing | MIT | 图绘制 |
| SkiaSharp | MIT | 2D 渲染引擎 |
| HarfBuzzSharp | MIT | 文字塑形 |
| SkiaSharp.HarfBuzz | MIT | Skia+HB 集成 |
| SixLabors.Fonts | Apache-2.0 | 字体处理 |
| ExcelNumberFormat | MIT | Excel 数字格式化 |
| RBush | MIT | R-tree 空间索引 |
| Binding.Shared | MIT | Skia/HB 共享绑定 |

---

## 二、待合并（按优先级排序）

### 第一批：公文排版（直接集成进 OfficialDocWriter）

#### 1. gongwen-paiban

| 项 | 详情 |
|----|------|
| 源码位置 | `测试文件夹\office-skill-research\gongwen-paiban` |
| 核心文件 | `GongWenFormatter.cs`（约 1000 行） |
| 依赖 | 仅 DocumentFormat.OpenXml（ThirdParty 已有） |
| 协议 | MIT |
| 合并后位置 | 可直接合入 `Inspect/OfficialDocWriter.cs`，不单独进 ThirdParty |
| 功能 | 五级段落角色识别 → 按 GB/T 9704 清洗重排 → 页码分节 |
| 风险 | 低。单文件，无外部依赖，纯算法 |

**为什么直接进 Inspect 不进 ThirdParty**：公文排版是业务逻辑（"什么是公文标准"），不是通用引擎能力。ThirdParty 只放纯工具库。

---

### 第二批：文档工具集

#### 2. Clippit（含 Open-Xml-PowerTools 遗产）

| 项 | 详情 |
|----|------|
| 源码位置 | `测试文件夹\office-skill-research\Clippit` |
| 核心模块 | DocumentAssembler（模板填充）、DocumentBuilder（合并拆分）、RevisionAccepter（Track Changes）、WmlToHtmlConverter、MarkupSimplifier |
| 依赖 | DocumentFormat.OpenXml（ThirdParty 已有） |
| 协议 | MIT |
| 合并后位置 | ThirdParty（纯工具能力，Word 引擎层需要） |
| 功能 | docx 合并/拆分/比较/HTML互转/修订批处理 |
| 风险 | 中。模块较多，需评估哪些确实需要。按需合并，不全量入 |

**按需合并策略**：
- DocumentAssembler → 增强 DocxTemplate
- DocumentBuilder → `nong word merge`
- WmlComparer → 文档差异比较（Inspect 审查层可用）
- WmlToHtmlConverter → 预览渲染（CLI 可选功能）
- 其余模块不合并

---

### 第三批：PPT 形状操作

#### 3. ShapeCrawler

| 项 | 详情 |
|----|------|
| 源码位置 | `测试文件夹\office-skill-research\ShapeCrawler` |
| 核心能力 | 形状读写、文本替换、图表操作、图片替换、SmartArt |
| 依赖 | DocumentFormat.OpenXml + Magick.NET + SkiaSharp |
| 协议 | MIT |
| 合并后位置 | ThirdParty（PPT 底层引擎） |
| 功能 | `nong pptx read/slides` 的底层实现 |
| 风险 | 高。依赖 Magick.NET（ImageMagick 绑定），体量大。需评估是否用 SkiaSharp 替代 Magick.NET 的图像处理部分 |

**降风险策略**：
- 先只合并核心的形状读写模块（不含 Magick.NET 依赖的部分）
- 图片处理走 ThirdParty 已有的 SkiaSharp
- Magick.NET 如果确实需要，后续单独评估

---

### 第四批：轻量模板引擎（可选，已有替代）

#### 4. docxMiniWord

| 项 | 详情 |
|----|------|
| 源码位置 | `测试文件夹\office-skill-research\docxMiniWord` |
| 核心文件 | `MiniWord.cs`（约 1180 行） |
| 依赖 | DocumentFormat.OpenXml |
| 协议 | Apache 2.0 |
| 合并后位置 | 可选。DocxTemplate 已实现类似功能，仅在模板语法需要对齐时参考 |

**建议**：不合并。DocxTemplate 已经覆盖了模板填充场景。仅在需要兼容 MiniWord 的 `{{tag}}` 语法时参考其实现。

---

## 三、合并路线图

```
C 线完成（当前）
  │
  ▼
B1：gongwen-paiban → Inspect/OfficialDocWriter（1 天）
  │  产出：nong official format 命令可用
  │
  ▼
B2：Clippit 核心模块 → ThirdParty（3 天）
  │  产出：docx 合并/拆分/比较能力，nong word merge + diff 命令可用
  │
  ▼
B3：ShapeCrawler 核心模块 → ThirdParty（5 天）
  │  产出：PPT 形状读写能力，nong pptx read/slides 命令可用
  │
  ▼
B4：补充测试 + 致谢文档（1 天）
  │
  ▼
A 线：CLI 代码开发
```

## 四、致谢清单

合并完成后在根 README 添加致谢：

```markdown
## Acknowledgments

This project incorporates source code from:

- [gongwen-paiban](https://github.com/...) — Chinese official document formatting (MIT)
- [Clippit](https://github.com/...) — OpenXML PowerTools successor (MIT)
- [ShapeCrawler](https://github.com/...) — PowerPoint shape manipulation (MIT)
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) — Excel library (MIT)
- [ScottPlot](https://github.com/ScottPlot/ScottPlot) — Scientific charts (MIT)
- [MSAGL](https://github.com/microsoft/automatic-graph-layout) — Graph layout (MIT)
- [SkiaSharp](https://github.com/mono/SkiaSharp) — 2D rendering (MIT)
- And others listed in ThirdParty/README.md
```

## 五、不合并的项目及原因

| 项目 | 不合并原因 |
|------|-----------|
| OfficeCLI | Apache 2.0，功能完整但架构不同。参考其 L1/L2/L3 设计，不复用代码 |
| Open-Xml-PowerTools | 已归档，Clippit 是其继承者 |
| OfficeIMO | 过于庞大（50+ 子项目），与我们的 CLI 定位冲突 |
| OpenXML-Office | AGPL-3.0，协议不兼容 |
| docx (dolanmiu/docx) | TypeScript，非 .NET |
| pandoc | GPLv2+，协议不兼容，仅作为外部进程调用 |
| ppt-master | Python，非 .NET |
| PPTAgent | Python，非 .NET |
| AIWriteX | Python/CrewAI，非 .NET |
