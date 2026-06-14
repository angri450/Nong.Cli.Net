# 仓库文档结构重组

## 时间
2026-06-09

## 影响范围
无代码变更，纯文档结构重组。

## 变更类型
项目基础设施 — Agent 渐进式披露改造。

## 做了什么

1. **CLAUDE.md 拆分**: 15KB 单体文件瘦身为约 100 行内核，详细内容拆到 `.claude/references/` 下 5 个文件
2. **log/ 目录重整**: 新建 `plans/`、`debug/`、`reports/` 三个子目录
3. **索引体系**: 为 `plans/`、`changelog/`、`debug/`、`guidance/` 四个目录创建 `index.md`
4. **CLAUDE.md 新增强制工作流**: Plan → Build → Changelog → Debug 四步循环
5. **CLAUDE.md 新增导航表**: 告诉 Agent 需要什么信息时读哪个文件

## 文件清单

新增：
- `.claude/references/project-structure.md`
- `.claude/references/nuget-publish.md`
- `.claude/references/third-party-notes.md`
- `.claude/references/branch-sync.md`
- `.claude/references/build-conventions.md`
- `log/plans/index.md`
- `log/plans/2026-06-09-repository-doc-restructure.md`
- `log/changelog/index.md`
- `log/debug/index.md`
- `log/guidance/index.md`
- `log/reports/README.md`
- `delete/`（空目录，软删除暂存区）

修改：
- `CLAUDE.md` — 从 257 行瘦身为约 100 行

## 技能须知

- Skill Agent 开工时先读 `CLAUDE.md`（内核），按导航表按需加载 `.claude/references/`
- Skill Agent 写 changelog 时同步更新 `log/changelog/index.md`
- Skill Agent 写施工方案时同步更新 `log/plans/index.md`
- Skill Agent 收到用户反馈时写入 `log/debug/` 并更新索引

## 剩余工作

- AGENTS.md 工作流更新（Codex 侧）
- HTML 进展报表 skill 开发
