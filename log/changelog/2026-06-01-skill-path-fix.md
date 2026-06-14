# Word 技能模板：format JSON 路径硬编码插件版本号

**时间**: 2026-06-01
**影响范围**: Nong.Toolkit.Net Word skill (`workspace-setup.md`)
**类型**: Bug Fix (Skill)

## 问题

Word 技能的 `workspace-setup.md` 第 154 行指示 agent 用 `<skill-root>/formats/life-sciences-contest.json` 替换 `<format-json-path>`。

Agent 将 `<skill-root>` 解析为包含插件版本号的绝对路径：`C:\Users\...\nong-toolkit\1.1.1\word\formats\...json`

插件从 1.1.1 升级到 1.1.2 后，所有曾经运行过的 Word 项目的 `Program.cs` 中硬编码的路径全部指向不存在的 `1.1.1` 目录，报 `DirectoryNotFoundException`。

## 修复

1. 新增步骤 4：将 format JSON 从 `<skill-root>/word/formats/` 复制到项目目录 `formats/`
2. Program.cs 模板中的路径改为相对路径：`"formats/life-sciences-contest.json"`
3. 删除旧的"替换为绝对路径"指令
4. Chart workspace-setup 删除了同样的残留注释

## 影响

- Word skill 的用户需要在项目目录下执行手动修复
- 新项目不受影响（模板已修复）
- 旧的 Program.cs 需要手动把绝对路径改为 `"formats/xxx.json"`

## 技能须知

- 所有 skill 的 workspace-setup 模板都**禁止使用** `<skill-root>` 来构造文件路径
- 需要本地文件时，必须先复制到项目目录，再用相对路径
- Plugin marketplace 的 Nong.Toolkit.Net 需更新到包含此修复的版本
