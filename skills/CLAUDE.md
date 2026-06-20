# Nong.Toolkit.Net — Skill 层

Claude Code skill 编排层。维护 Nong 家族的 Skills + Agent 编排文档。

项目特有规则见本文件，共享设定见 `../../.claude/CLAUDE.md`。



## 当前开发状态

- 版本线: 与 Nong.Cli.Net 主版本同步（当前适配 12.1.0）
- Skill 同步状态: 已同步到 12.1.0 实际命令面 (332 commands)
- 当前技能数: 17（word / excel / pptx / pdf / chart / diagram / ocr / literature / aminer / metaso / inspect（含 genre） / icons / slice / export / markdown / nongcli（含 search） + 1 skill-grader（含 breeder/pruner/tester））
- **插件版本: 2.0**（仅大版本号，不随 CLI 小版本号更新——2.0 → 3.0 → 4.0，不用 2.1/2.2）
- **CLI 版本同步**: 当 Nong.Cli.Net 主版本号变化时，运行 `./sync-version.sh <新版本>` 更新文档中的 CLI 版本引用（README/CLAUDE.md/reference docs）。插件版本不受影响。
- 本次更新: v4.5.0→v12.1.0 全线贯通，新增 export/markdown/nongcli 3 个 skill，合并 genre→inspect + 4 skill-*→1 skill-grader，word/excel/pptx/pdf SKILL.md 同步到 v12.1.0 命令面
- 仓库地址: `https://github.com/angri450/Nong.Toolkit.Net`

## 项目特有约束

- pkg 目录: 安装包分发，不走 npm
- 每个 Skill 有独立的 `SKILL.md` + `references/` 目录
- Skill 文档是路由层，详细规则放 references
- 安装方式: `claude plugin install <name>@nong-toolkit`
- 不发 NuGet

## 目录结构

```
Nong.Toolkit.Net/
  .claude-plugin/           ← 插件清单 (marketplace.json + plugin.json)
  word/                     ← Word skill
  pdf/                      ← PDF skill
  literature/               ← 文献搜索 / 本地缓存 skill
  aminer/                   ← AMiner API skill
  metaso/                   ← Metaso 搜索 / reader / chat skill
  inspect/                  ← 论文审查 skill
  excel/                    ← Excel skill
  chart/                    ← 统计图表 skill
  diagram/                  ← 科学绘图 skill
  pptx/                     ← PPTX skill
  ocr/                      ← OCR skill
  genre/                    ← 文档模板 skill
  icons/                    ← 科学图标 skill
  slice/                    ← Pandoc 切片 skill
  export/                   ← EPUB / LaTeX / HTML / ODF 导出 skill
  markdown/                 ← Markdown ↔ NongMark 双向转换 skill
  nongcli/                  ← 工作区管理 skill
  search/                   ← 语义搜索 skill
  skill-grader/             ← Skill 生命周期门控
  skill-breeder/            ← Skill 创作指南
  skill-tester/             ← Skill 质量测试
  skill-pruner/             ← Skill 生命周期治理
```
