# CLAUDE.md — Nong.NET

Pure .NET scientific document generation toolkit. Zero JavaScript. One merged foundation DLL.

## 仓库

- GitHub: `https://github.com/angri450/Nong.NET`
- 主分支: `master`（同时维护 `main`）
- 协议: MIT

## 包结构（9 个包，版本号全局统一）

| 包 | 项目路径 | 说明 |
|----|---------|------|
| `Angri450.Nong.ThirdParty` | `ThirdParty/` | **地基** — 合入 15 个第三方库源码编译为单一 DLL |
| `Angri450.Nong.Excel` | `Excel/` | 链式 Excel 生成 API |
| `Angri450.Nong.Chart` | `Chart/` | 18 种图表 + ANOVA/Duncan MRT |
| `Angri450.Nong.Diagram` | `Diagram/` | 流程图、网络图、系统发育树 |
| `Angri450.Nong.Docx` | `Docx/` | Word 生成 + 论文诊断 |
| `Angri450.Nong.Pptx` | `Pptx/` | PPT 生成、10 套主题 |
| `Angri450.Nong.MultiModal` | `MultiModal/` | PaddleOCR 云 + 本地 |
| `Angri450.Nong.Bioicons` | `Bioicons/` | 40 个 SVG 科学图标 |
| `Angri450.Nong.Skill.Manager` | `SkillManager/` | Skill CLI 工具 |

## 依赖链

```
ThirdParty (ClosedXML + OpenXml + ScottPlot + MSAGL + SkiaSharp + HarfBuzz + SixLabors + ...)
  ├── Excel   + System.IO.Packaging (NuGet)
  ├── Chart
  ├── Diagram + Bioicons
  └── Docx
       └── MultiModal

Pptx → ShapeCrawler (NuGet, 独立)
SkillManager → YamlDotNet (NuGet, 独立, CLI 工具)
```

## 开发约定

### 版本号
- 当前大版本: **3.x.x**
- 大版本号（3）全局统一，小版本号（x.x）各包独立
- 改一个包 → 只 bump 那个包的次版本号（如 3.0.1 → 3.0.2）→ 只推送那个包
- 大版本升级（3.x → 4.0.0）时才全部统一更新
- 不要为了小改动 bump 全部 9 个包

### NuGet 发布流程（单包更新）
改一个包的代码后，按顺序：
1. 改 `<Version>`（如 `3.0.1` → `3.0.2`）
2. `dotnet build <那个项目>.csproj -c Release`
3. `dotnet pack <那个项目>.csproj -c Release -o nupkg/ --no-build`
4. `dotnet nuget push nupkg/<包名>.<新版>.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
5. `gh release create v<新版> nupkg/<包名>.<新版>.nupkg --title "v<新版>" --notes "<改动说明>" --latest`
6. `git add -A && git commit -m "release: <包名> v<新版> — <改动摘要>" && git push`

### NuGet 发布流程（大版本升级，极少用）
全部 9 个包统一改 `<Version>` → 批量 build + pack + push → 一个 GitHub Release 包含全部 nupkg。

### 编译
- TargetFramework: `net10.0`（向前兼容 .NET 11+）
- 无 `global.json` SDK 锁定
- `ThirdParty.csproj` 通过 glob `**/*.cs` 包含第三方源码，排除 `bin/`、`obj/`、`common/` polyfill

### NuGet 包规范
- 每个包必须有: `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<RepositoryUrl>https://github.com/angri450/Nong.NET</RepositoryUrl>`
- 每个项目目录必须有 `README.md`
- `*.csproj` 中必须有 `<None Include="README.md" Pack="true" PackagePath="/" />`

### ThirdParty 源码合入注意事项
- SkiaSharp 源码版本 (m145) 高于可用本地库 (m119)，已手动适配：`VersionConstants` 修正为 119/0，`SKTypeface` 用 `match_family_style` 替代 `legacy_create_typeface`，`SKPathBuilder` 重写为旧路径 API，`FlowchartRenderer` 用 `DrawLine` 替代 `SKPath`
- 嵌入式资源名（字体、.resx）需 `LogicalName` + `CustomToolNamespace` 对齐原始程序集命名
- `OpenXmlSourceGenerator` 保留为独立分析器项目（`netstandard2.0`），不在 ThirdParty 中合入

### 分支策略
- 只推 `master`，`main` 由人工手动合并
- 不要自动切到 `main`，也不要把 `main` 合并回 `master`（只允许把 `master` 合并到 `main`）

### 双仓库同步 (GitHub + Gitee)
- GitHub: `https://github.com/angri450/Nong.NET`（主仓库）
- Gitee: `https://gitee.com/angri450/Nong.NET`（镜像）
- 每次 push 完 GitHub master，同步执行 `git push gitee master`
- Gitee remote 名称: `gitee`
- 初始化（仅首次）: `git remote add gitee https://gitee.com/angri450/Nong.NET.git`

## 禁止事项
- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能（仅 `MultiModal/scripts/ocr_local.py` 为辅助脚本）
- 不要为第三方库创建独立的 `.csproj` — 已全部删掉，统一走 ThirdParty
- 不要用 PowerShell 的 `-replace` 批量编辑 `.csproj` — 会损坏 XML，用 Edit 工具逐文件改

## 对项目维护者的说明

你不用自己写代码。告诉 Claude 要做什么，Claude 负责：
- 写代码、修 bug、加功能
- 改 csproj、build、pack、推送 NuGet
- 创建 GitHub Release
- 整理项目结构

你只需要：
- 决定方向（"加个饼图"、"发版"、"修那个 bug"）
- 提供 API Key（NuGet 推送时）
- 在 GitHub 网页上手动把 master 合并到 main（你想合的时候）

NuGet API Key 已保存在仓库的 CLAUDE.md 之外（环境变量 `$env:NUGET_API_KEY`），Claude 推送时会自动使用。

## 完整目录地图（新人/新会话速览）

### 你的代码（9 个项目，需要维护）

| 目录 | 关键文件 | 依赖 |
|------|---------|------|
| `ThirdParty/` | `ThirdParty.csproj` + `GlobalUsings.cs` + `DelegateProxies*.cs` + `HashCodeExt.cs` | 7 个本地库 NuGet + 2 个分析器 |
| `Excel/` | `ExcelBuilder.cs` + `AdvancedBuilder.cs` + `StylePresets.cs` + `FormulaValidator.cs` | ThirdParty + System.IO.Packaging |
| `Chart/` | `ChartBuilder.cs` + `ChartTypes.cs` + `StatsEngine.cs` + `DataLoader.cs` | ThirdParty |
| `Diagram/` | `DiagramBuilder.cs` + `Models/` + `Renderers/` + `Layout/` | ThirdParty + Bioicons |
| `Docx/` | `Builders.cs` + `DocumentWriter.cs` + `StyleBuilder.cs` + `TemplateEngine.cs` + `PaperDiagnostics.cs` | ThirdParty |
| `Pptx/` | `PresentationBuilder.cs` + `SlideBuilder.cs` + `ThemePreset.cs` + `LayoutSystem.cs` | ShapeCrawler (NuGet) |
| `MultiModal/` | `PaddleOcrVlClient.cs` + `LocalOcrClient.cs` + `LayoutToWordConverter.cs` | Docx |
| `Bioicons/` | `IconProvider.cs` + `*.svg` (40个) | 无 |
| `SkillManager/` | `Program.cs` + `Models/` + `Tools/` + `assets/` | YamlDotNet (NuGet) |

### 第三方源码（15 个目录，不要动）

`ClosedXML/` `ClosedXML.IO/` `ClosedXML.Parser/` `ExcelNumberFormat/` `RBush/`
`DocumentFormat.OpenXml/` `DocumentFormat.OpenXml.Framework/`
`ScottPlot/` `SkiaSharp/` `HarfBuzzSharp/` `SkiaSharp.HarfBuzz/`
`MSAGL/` `MSAGL.Drawing/` `SixLabors.Fonts/` `Binding.Shared/`

这些目录只读。编译时 ThirdParty.csproj 通过 glob 自动引入。里面有 `bin/` 和 `obj/` 残留是正常的（.gitignore 已排除）。

如果第三方源码有 bug 需要修：直接改 `.cs` 文件，但**不要**为它们创建 `.csproj`。

### 基础设施（不用管）

| 目录 | 用途 |
|------|------|
| `data/` | OpenXml 源生成器的数据文件（JSON/Schema） |
| `common/` | 旧 TFM 的 polyfill（net10.0 不需要） |
| `nupkg/` | 打包输出临时目录 |
| `tests-output/` | 测试项目 + 生成文件 |
| `DocumentFormat.OpenXml.Generator/` | Roslyn 源生成器（分析器项目） |
| `DocumentFormat.OpenXml.Generator.Models/` | 源生成器模型 |
| `UnicodeTrieGenerator/` | SixLabors 用到的 Unicode 状态机 |
| `changelog/` | 版本变更记录（NuGet 包 Agent ↔ Skill Agent 通讯） |
| `.gitignore` | 排除 bin/obj/nupkg |
| `CLAUDE.md` | 就是这个文件 |
| `README.md` + `README.zh-CN.md` | 中英文仓库首页 |
| `LICENSE` | MIT |

### changelog 目录约定

`changelog/` 是 NuGet 包开发 Agent 与 Skill 开发 Agent 之间的**通讯桥梁**。

**格式**：`changelog/YYYY-MM-DD-topic.md`

**规则**：
- 每次版本发布后，写入一个或多个 changelog 文件
- 每个文件聚焦一个主题（TFM 变更、新功能、Bug 修复）
- 文件名格式：`日期-主题.md`（日期为创建日期，主题用英文小写 + 连字符）
- 文件内容包含：时间、影响包、变更类型、详细说明、技能须知

**Skill Agent 职责**：
- 每次接管时，先读取 `changelog/` 目录中比上次更新时间晚的文件
- 根据"技能须知"更新对应 skill
- 更新后记录已处理的文件

**NuGet Agent 职责**：
- 每次发版后，创建对应的 changelog 文件
- 写清楚"影响哪个包"、"技能需要怎么改"

1. **SkiaSharp 版本不匹配**: 源码是 m145，NuGet 本地库最高只有 m119。`SKTypeface.cs`、`SKPathBuilder.cs`、`SkiaApi.cs`、`VersionConstants.cs`、`FlowchartRenderer.cs` 已手动降级适配。
2. **字体资源命名**: ClosedXML 的 `DefaultGraphicEngine.cs` 硬编码了资源名 `ClosedXML.Graphics.Fonts.xxx.ttf`，ThirdParty.csproj 必须用 `LogicalName` 覆盖默认的 `ThirdParty.xxx.ttf`。
3. **验证资源命名**: `ValidationResources.resx` 和 `ExceptionMessages.resx` 需要 `CustomToolNamespace` 对齐 `DocumentFormat.OpenXml` 命名空间。
4. **HarfBuzz + SkiaSharp 共享 partial class**: `DelegateProxies.shared.cs` 原本用 `#if HARFBUZZ` 条件编译，合并后拆成两份：`DelegateProxiesSkia.cs` + `DelegateProxiesHarfBuzz.cs`。
5. **ScottPlot 全局 usings**: `ScottPlot/Usings.cs` 包含 `global using System.IO;`，与 MSAGL 的 `Path` 类型冲突，已排除该文件，手动在 `GlobalUsings.cs` 中补回需要的 using。
6. **MSAGL 命名冲突**: `Path` 与 `System.IO.Path` 冲突，在 `RectilinearEdgeRouter.cs` 和 `StaticGraphUtility.cs` 中加了 using alias。`Timer` 与 `System.Threading.Timer` 冲突，在 `GlobalUsings.cs` 中加了全局 alias。
7. **OpenXml 源生成器**: 必须保留为独立项目（Roslyn 分析器），不能在 ThirdParty 中合入。`ThirdParty.csproj` 通过 `OutputItemType="Analyzer"` 引用。
8. **PowerShell 批量编辑 csproj 会损坏 XML**: 已写入禁止事项。用 Edit 工具逐个文件改。
