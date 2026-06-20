# validate → scan → inventory → package 标准管线

这四个命令组成了 Nong.Toolkit.Net 插件的发布前门禁。按顺序执行，每一步通过后才能进入下一步。

## 1. validate — 校验单个 skill

```powershell
nong skill validate .\word --json
```

检查：SKILL.md 是否存在、frontmatter 是否完整（name/description）、内容是否为空、reference 链接是否有效。

## 2. scan — 安全扫描整个插件

```powershell
nong skill scan . --json
```

检查：凭证泄露（API key、password）、不安全的 HTML 内容（innerHTML）、硬编码的绝对路径、过期包名或版本号。

输出按严重度分级：High（必须修复）、Medium（建议修复）、Low（可忽略）。打包前必须 0 High。

## 3. inventory — 列出所有 skill 的目录清单

```powershell
nong skill inventory . --json
```

输出每个 skill 的：名称、描述、references 数量、examples 数量、formats 数量、plugins 检测状态（hasPluginManifest、hasMarketplaceManifest、hasSkillsManifest）。

用于最后确认所有 skill 都在、目录结构完整。

## 4. package — 打包为 zip

```powershell
nong skill package . --json
```

自动执行 validate（全部 skill）+ scan + 生成 zip 包。zip 包含插件可安装面（skill 目录、plugin.json、marketplace.json、README、LICENSE 等）。

## 关键点

- 顺序不能乱：先 validate 每个改动的 skill，再 scan 整体，然后 inventory 确认清单，最后 package。
- package 会自动跑 validate 和 scan。如果前面单独跑过，package 仍会再跑一次（幂等安全）。
- 打包产物放到仓库外的 `../Nong.Toolkit_archive/package-artifacts/`，不要提交到仓库。
