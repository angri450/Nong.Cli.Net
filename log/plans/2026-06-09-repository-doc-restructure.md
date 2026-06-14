# 施工方案：项目文档结构重组 + 渐进式披露

## 目标

按用户定义的 Agent 开发工作流，重组 Nong.NET 项目的文档和日志结构，让 AI Agent（Claude Code / Codex）能渐进式加载上下文，减少 token 消耗，提高开发效率。

## 改动范围

### 1. 创建 `.claude/references/`（5 个文件）

从 CLAUDE.md 后半段拆出详细参考文档，CLAUDE.md 只保留内核 + 导航表。

| 文件 | 内容来源 |
|------|---------|
| `project-structure.md` | 完整目录地图 + 依赖链 + 包结构 |
| `nuget-publish.md` | NuGet 单包发布流程 + 大版本升级流程 + OCR runtime 发布流程 |
| `third-party-notes.md` | 第三方源码注意事项（SkiaSharp 版本适配等 9 个已知坑） |
| `branch-sync.md` | 分支策略 + 三仓库同步命令 |
| `build-conventions.md` | 编译约定 + NuGet 包规范 + 版本号规则 |

### 2. 重整 CLAUDE.md

瘦身为内核：

- 项目身份（Nong.NET 是什么）
- 当前版本和进度
- 强制工作流（Plan → Build → Changelog → Debug）
- 核心禁令
- 导航表（需要 X 信息时去读 Y 文件）

### 3. 重整 `log/` 目录结构

现有：`log/changelog/`（60+ 文件）、`log/guidance/`（28+ 文件）

目标结构：

```
log/
├── plans/          ← 施工方案（新建，Agent 开工前写入）
│   └── index.md
├── changelog/      ← 变更记录（已有，新增索引）
│   └── index.md    ← 新建
├── debug/          ← 用户测试反馈（新建）
│   └── index.md    ← 新建
├── guidance/       ← 开发指导（已有）
│   └── index.md    ← 新建
└── reports/        ← HTML 进展报告（新建，skill 自动生成）
```

为每个子目录创建 index.md，格式统一：

```
# 目录名 索引

- YYYY-MM-DD | 文件名 | 一句话摘要 | 状态
```

### 4. 不动的文件

- `AGENTS.md` — Codex 专用，后续单独修改
- 所有现有 `log/changelog/*.md` — 内容不变，只加索引
- 所有现有 `log/guidance/*.md` — 内容不变，只加索引
- `docs/` — 仓库级维护文档，不动
- 根目录 README.md / README.zh-CN.md — 不动

### 5. 暂不做

- 开发进展 HTML skill（本次只搭报表目录骨架，skill 后续单独开发）
- AGENTS.md 工作流更新（Codex 侧，后续单独处理）

## 执行顺序

1. 创建 `.claude/references/` 目录
2. 写入 5 个参考文件
3. 创建 `log/plans/`、`log/debug/`、`log/reports/` 目录
4. 为 `log/plans/`、`log/changelog/`、`log/debug/`、`log/guidance/` 创建 index.md
5. 重写 CLAUDE.md（内核化）
6. 验证：git status 检查改动清单

## 风险

- 低风险：都是文档操作，不碰代码和 csproj
- CLAUDE.md 拆分后需确保导航路径正确

## 执行结果（2026-06-09）

- PASS: `.claude/references/` 5 个文件已创建
- PASS: `log/plans/`、`log/debug/`、`log/reports/` 目录已创建
- PASS: `log/plans/index.md`、`log/changelog/index.md`、`log/debug/index.md`、`log/guidance/index.md` 4 个索引文件已创建
- PASS: CLAUDE.md 已瘦身为内核（约 100 行，含导航表 + 工作流约束）
- PASS: `delete/` 目录已创建（本方案无删除内容，暂空）
- 未做: AGENTS.md 更新（Codex 侧，后续单独处理）
- 未做: HTML 报表 skill（后续单独开发）
