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
  OcrModels/              ← PP-OCRv6 字典
  ThirdParty/             ← 第三方源码（LiteDB + ClosedXML + SkiaSharp 等）
  Data/                   ← NongDb 统一数据库 + NongWorkplace（独立包 Angri450.Nong.Data）
  OpenXmlData/            ← OpenXml 源生成器数据（namespaces/schemas/parts，自动生成勿改）
  Common/                 ← .NET polyfill shims（给 ThirdParty 用）
  SkillManagerCore/       ← skill 管理器
  */tools/                ← 外部 dotnet tool 项目
```
