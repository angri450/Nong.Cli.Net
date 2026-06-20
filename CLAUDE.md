# Nong.Cli.Net — 执行层

纯 .NET 科学文档与学术研究 CLI 工具包。零 JavaScript。

项目特定规则见本文件，共享设定见 `../.claude/CLAUDE.md`。

## 信息源

`../../.claude/PROJECT_STATE.md` 是全家桶唯一真相源。`../../.claude/references/agent-rules.md` 有本项目 agent 行为约束。

本项目的 `docs/`、`log/`、`tools/`、`PROJECT_STATE.md`、`AGENTS.md` 等开发文件已全部迁到 `../../.claude/`。


- GitHub: `https://github.com/angri450/Nong.Cli.Net`
- Gitee: `https://gitee.com/angri450/Nong.Cli.Net`
- GitCode: `git@gitcode.com:angri450/Nong.Cli.Net`
- 主分支: `main`
- 协议: Apache-2.0

## 源码地图

```
Nong.Cli.Net/
  Cli/                    ← nong CLI 路由 + 所有命令组
  Docx/                   ← OpenXML Word 引擎
  Literature/             ← 文献检索管线 (CNKI DSL + OpenAlex/Crossref/Unpaywall)
  Aminer/                 ← AMiner API (28 端点)
  Metaso/                 ← Metaso API (search/reader/chat)
  Excel/                  ← ClosedXML Excel 生成
  Genre/                  ← 文档格式模板库
  Inspect/                ← AI 审查
  Bioicons/               ← 40 个科学 SVG 图标
  Pandoc/                 ← 文档 AST/NongMark 投影
  OcrDictionary/          ← PP-OCRv6 字符字典
  ThirdParty/             ← 第三方源码（LiteDB + ClosedXML + SkiaSharp 等）
  Data/                   ← NongDb 统一数据库 + NongWorkplace（独立包 Angri450.Nong.Data）
  OpenXmlData/            ← OpenXml 源生成器数据（namespaces/schemas/parts，自动生成勿改）
  Common/                 ← .NET polyfill shims（给 ThirdParty 用）
  SkillManagerCore/       ← skill 管理器
  skills/                 ← 17 个 Toolkit.Net skill（单一真源，详见下方 Skill 系统）
  */tools/                ← 外部 dotnet tool 项目

## Skill 系统（Toolkit.Net 插件）

`skills/` 是本仓库和 Nong.Toolkit.Net 分发仓库的**共同源头**。改 CLI 命令时，必须同步更新对应 skill。

### 命令 → Skill 映射

| CLI 命令组 | Skill 目录 | 说明 |
|---|---|---|
| word (+ add) | `skills/word/` | 文档引擎主力（125 条命令） |
| excel | `skills/excel/` | 表格创建/图表/公式求值 |
| pptx | `skills/pptx/` | 幻灯片创建/编辑 |
| pdf | `skills/pdf/` | PDF 切片/生成/表单 |
| chart | `skills/chart/` | 农业统计图 |
| diagram | `skills/diagram/` | 流程图/网络图/树 |
| ocr | `skills/ocr/` | OCR 识别/环境检测 |
| lit | `skills/literature/` | 文献检索/缓存 |
| aminer | `skills/aminer/` | AMiner API |
| metaso | `skills/metaso/` | Metaso AI 搜索 |
| inspect (+ genre) | `skills/inspect/` | 论文诊断 + 模板 |
| icons | `skills/icons/` | Bioicons 科学图标 |
| slice | `skills/slice/` | NongPandoc 切片检查 |
| export | `skills/export/` | EPUB/LaTeX/HTML/ODF |
| markdown | `skills/markdown/` | GFM ↔ NongMark |
| nongcli (+ search) | `skills/nongcli/` | 工作区 + 语义搜索 |
| skill | `skills/skill-grader/` | Skill 生命周期 |

### 改命令时改什么

1. 改 Cli.Net 代码 → 如果命令语法/参数变了 → **同步改 `skills/<对应skill>/SKILL.md` 的命令签名字段**
2. 如果新增子命令 → 在 SKILL.md 的 Implemented Commands 表格里加一行
3. 如果删子命令 → 两处都删
4. 改完 Cli.Net 代码 + skills/ → **同一个 commit**

### 同步到 Toolkit.Net

`skills/` 目录通过 CI 或手动复制到 `../Nong.Toolkit.Net/`。Toolkit.Net 只做分发，不含开发文件（`sync-version.sh`、`.ps1` 不复制）。`skills/CLAUDE.md` 是插件自身的 agent 合约，两端都有。

## 外圈 tool 施工提醒（PdfDissect 三测试修复 780ef2c 的血训，详见 ../../.claude/guidance/2026-06-19-pdfdissect-postmortem.md）

1. **Pdf/Pptx/Chart/Diagram/Ocr/Imaging 命令不在 nong.exe 里** — 看 `Cli/NongCli.csproj`：这些命令文件被 `<Compile Remove>` 排除，运行时转发到独立 dotnet tool（`~/.dotnet/tools/nong-pdf` 等）。**改这些模块的源码后，build Cli 是不够的** — 必须 `dotnet build <Module>/tools/nong-<tool>.csproj -c Release`，然后把新二进制同步到 `~/.dotnet/tools/` 和 `.store/` 两个路径。

2. **FindTool 现在是 PATH-first，全局 tools 作 fallback** — 测试/CI 把本地 build 目录注入 PATH 就能用最新代码，不再被全局旧版截胡。

3. **PdfPopplerExtractor 的主循环（遍历 XML blocks）和预处理（table/column detection）各写各的 model.Blocks，没有互斥逻辑** — 加新的预处理必须在主循环里加对应的 skip/dedup。

4. **涉及 EnsureToolInstalled 的测试 class 必须串行化** — 加 `[Collection("PdfCommandTests")]`，否则并发 install 同一全局工具会竞态。

5. **改代码后先验证你改的代码真的被执行到** — 加一条临时 `File.AppendAllText` 到被怀疑的函数入口，比反复改代码猜根因成本低得多。

6. **当前测试基线：216/216 PASS, 0 fail, 0 skip**。别让后续改动退化。

## 开发工作流

| 做什么 | 怎么干 |
|---|---|
| 改 CLI 命令 | 1. 改 `Cli/Commands/XxxCommands.cs` → 2. 如果语法变了，同步改 `skills/<对应skill>/SKILL.md` → 3. 同 commit |
| 同步版本号 | `./sync-version.sh 12.x.0` — 一键更新所有 csproj + CliVersion + UserAgent + README |
| 架构全景 | `nong-packages.html` — 包依赖树、vendor 目录、运行时架构 |
| 改外圈 tool 代码 | 改 Pdf/Pptx/Chart 等源码 → `dotnet build <Tool>/tools/nong-<tool>.csproj -c Release` → 同步到 `~/.dotnet/tools/` |
| 全量回归 | `dotnet test Cli.Tests/Cli.Tests.csproj -c Release`（期望 216/216） |
| 清理编译垃圾 | `find . -name bin -o -name obj | xargs rm -rf` |
