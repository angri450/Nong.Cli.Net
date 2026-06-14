# Skill 模板全线清除 <skill-path> 绝对路径

**时间**: 2026-06-01
**影响范围**: Nong.Toolkit.Net — Word/Excel/Pptx/Chart skills
**类型**: Bug Fix (Skill template hardening)

## 问题

多个 skill 的参考文档和模板中包含 `<skill-path>` 和 `<skill-root>` 占位符。当 Agent 解析这些占位符时，会将它们替换为包含插件版本号（如 `1.1.1`）的绝对路径。插件升级到 `1.1.2` 后，所有之前写入的 `Program.cs` 中的路径全部失效。

## 涉及的 Skill

| Skill | 文件 | 问题 |
|-------|------|------|
| Word | `workspace-setup.md` | `<format-json-path>` → 绝对路径 |
| Excel | `write-excel.md` | `BuildFromJson("<skill-path>/...")` |
| Pptx | `write-pptx.md` | `BuildFromJson("<skill-path>/...")` |
| Pptx | `formats/INDEX.md` | `BuildFromJson("<skill-path>/...")` |
| Chart | `workspace-setup.md` | 残留注释 |

## 修复方案

统一模式：**复制 JSON 到项目目录 → 相对路径引用**

```
cp <skill-root>/<skill>/formats/*.json <project-dir>/formats/
```

Program.cs 中使用相对路径：`"formats/xxx.json"`

每个 skill 的参考文档末尾添加警告：**禁止直接引用 `<skill-path>` 或 `<skill-root>` 路径。**

## 教训

- `<skill-root>` 只能在临时 shell 指令中使用（如 `cp`），绝不能在持久化代码模板中使用
- 所有需要本地文件的 skill 必须遵循"先复制，再引用"模式
- Skill 升级后用户需手动重新复制 format JSON（或者 Agent 检测到项目已存在时跳过复制步骤）
