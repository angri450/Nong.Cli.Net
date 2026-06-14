# 阶段 12 审计：nong skill CLI 命令

日期：2026-06-03
状态：2 项修复，10 项测试通过

---

## 发现并修复

### #1 空字符串路径导致未处理异常

- **问题**：`nong skill validate "" --json` 在所有四个命令中触发 `ArgumentException`（`Path.GetFullPath("")`），stack trace 裸奔，无 JSON 输出
- **修复**：四个命令（validate/scan/inventory/package）均在 `Path.GetFullPath` 前加入 `string.IsNullOrWhiteSpace(dir)` 守卫，空路径返回 E003 missing_argument + `"Directory path is required."`，EXIT:1
- **影响命令**：skill validate、skill scan、skill inventory、skill package
- **文件**：`Cli/Commands/SkillCommands.cs`

### #2 skill package 缺少 CheckArtifact

- **问题**：其他生成命令（chart bar、word fill、word rebuild）在生成产物后调用 `CheckArtifact` 验证文件存在且非空，`skill package` 缺失此检查
- **修复**：PackageAsync 后加入 `CheckArtifact(outputPath, "ZIP")`，文件缺失或 0 bytes 返回 E008 write_failed
- **文件**：`Cli/Commands/SkillCommands.cs`

---

## 审计通过的测试（10 项）

| # | 测试场景 | 预期 | 结果 |
|---|---------|------|------|
| 1 | 空路径 | E003 + EXIT:1 | PASS |
| 2 | 不存在的目录 | E001 + 含路径 + EXIT:1 | PASS |
| 3 | `skill validate` word skill | status:ok, valid:true, lines:50 | PASS |
| 4 | `skill scan` Nong.Toolkit.Net | 0 High+, 15 findings (14M+1L) | PASS |
| 5 | `skill inventory` Nong.Toolkit.Net | 17 skills found | PASS |
| 6 | `skill package` word skill | zip 生成, CheckArtifact 通过 | PASS |
| 7 | scan 含 HIGH 发现 (EMAIL_EXPOSED) | status:error, errors[] populated, EXIT:1 | PASS |
| 8 | validate 无效 SKILL.md (无 frontmatter) | status:error, valid:false, EXIT:1 | PASS |
| 9 | package 被 validation 阻断 (无 SKILL.md) | 不生成 zip, EXIT:1 | PASS |
| 10 | inventory 空目录 | 0 skills, EXIT:0 | PASS |

### 测试参数缺失（System.CommandLine 内置行为，无需修复）

- `nong skill validate --json`（缺 <dir>）→ 自动显示 help + EXIT:1 — PASS
- `nong skill package --json`（缺 <dir>）→ 自动显示 help + EXIT:1 — PASS

---

## JSON 契约一致性

与现有 20 个 CLI 命令对齐：

- `status`: ok/error 语义正确
- `command`: "skill validate" / "skill scan" / "skill inventory" / "skill package"
- `errors[]`: 使用 `{code, name, message}` ErrorEntry 格式
- `issues[]`: 使用 `{id, severity, message}` Issue 格式（Medium/Low 扫描发现）
- `artifacts`: package 命令返回 `{"zip": "<absolute-path>"}`
- `metrics`: 各命令填充相关指标
- `meta`: durationMs + version 一致
- 退出码：error 路径 EXIT:1，成功 EXIT:0

---

## 已知未修复（不在本阶段范围）

1. `data.hasPluginManifest` 和 `data.hasMarketplaceManifest` 都检查 `marketplace.json`，应区分 `plugin.json` 和 `marketplace.json`。当前 Nong.Toolkit.Net 无这两个文件，不影响使用
2. SkillValidator 中 broken references 在非 JSON 文本输出中同时出现在 WARNING 列表和 "Broken references" 区域，属上游 SkillValidator 展示重复。JSON 输出无此问题

---

## 编译

Release build: 0 错误，0 警告（ThirdParty 的 CS3003 CLS 警告不计）
