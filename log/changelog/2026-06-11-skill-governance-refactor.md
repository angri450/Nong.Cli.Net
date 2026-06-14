# 2026-06-11 skill 治理拆分：农业五角色体系

## What changed

Toolkit 的 skill 治理体系从 2 个（`skill` + `skill-manager`）拆分为 5 个农业拟人角色。

| 旧 | 新 | 角色 | 职责 |
|----|-----|------|------|
| `skill` | `skill-breeder` | 育种员 | 编写：模板、命名、结构 |
| `skill-manager` | `skill-tester` | 验种员 | 检验：触发精度、失败反馈回收 |
| — | `skill-grader` | 入库员 | 过闸：validate → scan → inventory → package |
| — | `skill-patrol` | 巡田员 | 监护：CLI 升级后检测过期（待实现） |
| — | `skill-pruner` | 修剪员 | 收窄到：合并 / 拆分 / 废弃 |

## 命名原则

全部用农业拟人词，风格统一：
- breeder（育种）、tester（验种）、grader（入库）、patrol（巡田）、pruner（修剪）

五个角色管一条完整生命周期：breed → test → grade → patrol → prune。

## CLI 侧

- `nong skill validate/scan/inventory/package` 四个命令不变 — 它们是底层引擎，不绑定 skill 名
- `SkillManagerCore` 不变 — 它只处理目录结构，不知道 skill 叫什么
- 旧 `skill-manager` global tool 的弃用说明保留为历史记录

## Toolkit 侧

- `skill/` 目录删除，拆分为 `skill-breeder/`、`skill-tester/`、`skill-grader/`、`skill-pruner/`
- `skill-manager/` 收窄为 `skill-pruner/`，只保留 lifecycle.md（合并/拆分/废弃）
- authoring.md 移入 `skill-breeder/`，trigger-audit.md + feedback-loop.md 移入 `skill-tester/`
- plugin.json、skills.sh.json、skill.zh、CLAUDE.md、README 全部同步
- 15 → 17 skills（新增 2 个，实际拆出 4 个，旧 2 个合并为 1 个）

## 验证

```text
nong skill validate → all 17 PASS
nong skill scan → 0 findings
nong skill inventory → 17 skills
```
