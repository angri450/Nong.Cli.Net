# Nong.Toolkit.Net vs Angri450.Nong 完整对比分析

## 时间
2026-06-09

## 概述

两个仓库是**上层 / 下层**关系，共同构成 Nong.NET 生态。但二者在版本对齐、职责边界、目录结构和开发流程上存在多处置未对齐。

---

## 一、身份与定位

| 维度 | Nong.Toolkit.Net | Angri450.Nong |
|------|-----------------|---------------|
| 本质 | Claude Code 多 skill 插件 | 纯 .NET CLI 工具集 |
| 代码量 | 0 行 C#（纯文档 + PowerShell 脚本） | 15 个第三方源码目录 + 10+ 个项目 |
| 二进制 | 无 | 一个 `nong.dll`（93 个命令） |
| 文件数 (depth 2) | 127 | 3209 |
| 版本 | 2.3.1（plugin.json） | 4.0.0（NuGet / csproj） |
| 安装方式 | `claude plugin install nong-toolkit@angri450` | `dotnet tool install --global Angri450.Nong.Cli` |
| Git tags | 9 个 (v1.0.0 ~ v2.2.0)，**v2.3.1 无 tag** | 大量 |
| 主分支 | main | master |

## 二、架构关系

```
Nong.Toolkit.Net (上层 — 教学层)
  │ 告诉 Claude Code "怎么调用 nong"
  │ 11 个 SKILL.md → 每个包装一组 nong 命令
  │ 包含: formats/ recipes, references/ 详细指导, scripts/ PowerShell 辅助
  │
  └──→ 调用 Angri450.Nong.Cli (底层 — 执行层)
         │ 93 个命令, 一个 nong.dll
         │ 实际做: Word 读写、PDF 切片、图表渲染、OCR 推理
         │ SkillManagerCore 提供: nong skill validate/scan/inventory/package
```

**关键设计**: Nong.Toolkit.Net 不包含任何可执行代码（0 个 .csproj）。它是一个纯文档分发包。这就是为什么 README 说"零 JavaScript、纯 .NET CLI"——确定性工作全在 nong 二进制里，skill 只做路由和教学。

## 三、版本不对齐

| 文件 | Nong.Toolkit.Net | Angri450.Nong | 问题 |
|------|-----------------|---------------|------|
| plugin.json version | 2.3.1 | - | 无 git tag |
| skills.sh.json target | Nong 3.2.5+ | **4.0.0** | 声明的目标版本落后两个大版本 |
| 命令数 | 82 commands | **93 commands** | 对不上了 |
| OCR runtime | 3.2.5 | 4.0.0 | 版本引用过期 |
| README install | `--version 3.2.5` | 4.0.0 | 安装命令指向旧版 |

**Nong.Toolkit.Net 需要更新到 2.4.0 以对齐 Nong 4.0.0**。上次 sync changelog（2026-06-07）只做了 2.3.0 → 2.3.1 对应 Nong 3.2.5，此后 Nong 发布 4.0.0 后 Nong.Toolkit.Net 没有联动。

## 四、skill 目录结构对比

### Nong.Toolkit.Net 的 11 个 skill

```
word/       ← 最完整的 skill: SKILL.md (195 行) + 7 references + 4 formats + 3 scripts
pdf/        ← 简化: 只有 SKILL.md
literature/ ← 简化: 只有 SKILL.md
inspect/    ← 简化: 只有 SKILL.md
excel/      ← 中等: SKILL.md + 3 references + 6 formats + 2 scripts
chart/      ← 中等: SKILL.md + 2 references + 2 scripts
diagram/    ← 中等: SKILL.md + 2 references + 3 examples + 1 script
pptx/       ← 中等: SKILL.md + 3 references + 6 formats + 2 scripts
multimodal/ ← 中等: SKILL.md + 3 references + README
genre/      ← 中等: SKILL.md + 3 references + 1 script
icons/      ← 最简: 只有 SKILL.md
```

每个 skill 的 SKILL.md 都包含相同的 "Nong CLI Preflight" 代码块（安装/更新 nong），内容高度重复。

### Angri450.Nong 的 .claude/skills/（不应存在）

```
.claude/skills/progress-report/  ← 位置错误！应该在 Nong.Toolkit.Net
```

## 五、skill-manager 的尴尬位置

### 三个 skill-manager 相关实体

| 实体 | 位置 | 版本 | 功能 |
|------|------|------|------|
| skill-manager (meta-skill) | `~/.claude/plugins/cache/angri450/nong-toolkit/2.2.1/skill-manager/` | 2.2.1 | 创建/维护 skill 的指导文档 |
| SkillManagerCore (库) | `Angri450.Nong/SkillManagerCore/` | 4.0.0 | `nong skill validate/scan/inventory/package` |
| skill-manager SKILL.md | plugin cache 中 | 2.2.1 | 教 Claude Code 如何创建 skill |

**问题**:
- skill-manager 是 2.2.1 版本，不在 Nong.Toolkit.Net 的 11 个 skill 列表中
- skill-manager 不知道 Nong.Toolkit.Net 的结构（它被装在 plugin cache，和 Nong.Toolkit.Net 是分离的）
- skill-manager 说"Deterministic work goes into .NET tools"，但没说是走 nong CLI 还是独立项目
- 这次 progress-report 的创建就栽在这个模糊点上

## 六、log 目录结构对比

| 维度 | Nong.Toolkit.Net/log/ | Nong.Cli.Net/log/ |
|------|----------------------|---------------------|
| 文件数 | 15 个 .md（扁平） | plans/ changelog/ debug/ guidance/ reports/（结构化） |
| 索引 | 无 | 所有子目录有 index.md |
| 分类 | 发现问题、解决问题、审查通过（code-review 格式） | 按功能分类 |
| 记录粒度 | 每轮开发一个文件 | 每次变更一个文件（60+ changelog） |

**Nong.Toolkit.Net 缺少**: plans/、debug/、guidance/ 目录，无 index.md，日志格式不统一。

## 七、.claude/ 目录对比

| 维度 | Nong.Toolkit.Net | Angri450.Nong |
|------|-----------------|---------------|
| .claude/ 目录 | 无（被 .gitignore 排除） | 有，包含 references/（5 个文件） + skills/ |
| CLAUDE.md | 无（被 .gitignore 排除） | 有（71 行内核） |
| AGENTS.md | 无 | 有（Codex 专用） |

**Nong.Toolkit.Net 不需要 .claude/**（它是插件，不含开发配置）。但开发 Nong.Toolkit.Net 本身的 Agent 需要一个 CLAUDE.md 或 AGENTS.md 来指导开发。

## 八、skill 与 nong 命令的映射

| GroundPA 11 skills | 映射的 nong 命令 |
|-------------------|-----------------|
| word | word check/convert/create/read/preview/fill/rebuild/stats/fonts/styles/validate/extract/merge/outline/images/comments/revisions/infer-format/fix-order/academic-format/format-audit/repair-plan/table-reflow/protect/embed-font/add paragraph/table/footnote/image/toc/xref/link/bookmark/comment/math |
| pdf | pdf dissect/extract |
| literature | lit parse/validate/plan/search/export |
| inspect | inspect diagnose/refs/classify/structure/evidence/data-req/gap/varplan/semantics/write-paper |
| excel | excel sheets/read/to-groups/create/dissect |
| chart | chart analyze/anova/duncan/bar/line/scatter/pie |
| diagram | diagram flowchart/network/tree |
| pptx | pptx read/slides/dissect |
| multimodal | ocr cloud/local/check-env/analyze-image/models/install-model/to-word |
| genre | genre list/show |
| icons | icons list/search |

另外还有 `slice inspect/blocks/block/assets` 和 `skill validate/scan/inventory/package` 没有对应的 GroundPA skill。

## 九、开发流程不对齐

### 正确流程（应该的）

```
用户提出需求
  → 如果需求是"教 Claude 怎么用 nong"（skill） → 改 Nong.Toolkit.Net
  → 如果需求是"nong 能做新的事情"（命令） → 改 Angri450.Nong
  → 改完 nong 后 → 联动更新 Nong.Toolkit.Net（版本号 + README）
```

### 实际发生（progress-report 案例）

```
用户要求创建 progress-report skill
  → skill-manager 指导创建了独立的 .NET 工具（tools/ProgressReport/）
  → 这个工具放在 Angri450.Nong 但不在 nong CLI 里
  → SKILL.md 放在 .claude/skills/progress-report/（应该在 Nong.Toolkit.Net）
  → 结果：孤立的功能，既不是 nong 命令也不是 GroundPA skill
```

## 十、具体问题清单

### P0 — 版本不对齐

Nong.Toolkit.Net 声明的 Nong CLI 版本是 3.2.5，实际是 4.0.0。

**修复**: 更新所有 SKILL.md 的 Preflight 块、plugin.json、skills.sh.json、README.md、README.zh-CN.md 中的版本引用。同步到 4.0.0。

### P1 — progress-report 放错位置

- `Angri450.Nong/tools/ProgressReport/` 应该撤回
- `Angri450.Nong/.claude/skills/progress-report/` 应该移到 Nong.Toolkit.Net
- 如果 progress-report 需要 nong 命令支持，应该在 `Angri450.Nong/Cli/Commands/` 里加 `ProgressCommands.cs`

### P2 — Nong.Toolkit.Net 缺少日志结构

应该加上与 Angri450.Nong 对齐的 `log/` 目录结构：
- `log/plans/` + index.md
- `log/changelog/` + index.md
- `log/debug/` + index.md

### P3 — skill SKILL.md 重复代码

11 个 SKILL.md 文件都有相同的 "Nong CLI Preflight" 块，如果需要统一更新安装命令或版本号，要改 11 个文件。建议用一个共享 reference。

### P4 — skill-manager 版本孤立

skill-manager 2.2.1 在 plugin cache，Nong.Toolkit.Net 是 2.3.1。skill-manager 不知道 Nong.Toolkit.Net 的结构和约束。

### P5 — Nong.Toolkit.Net 缺少开发指导

没有 CLAUDE.md 或 AGENTS.md，Agent 开发 Nong.Toolkit.Net 技能时只能靠 skill-manager 的通用规则，不了解项目特定的架构约束。

### P6 — 两个 skill 的归属模糊

- `slice inspect/blocks/block/assets`：nong 有这些命令，但没有对应的 GroundPA skill
- `skill validate/scan/inventory/package`：nong 有，但 GroundPA 没有 skill。skill-manager 本身 SKILL.md 里提到了这些命令，但它不在 11 skill 列表里

## 十一、建议修复顺序

1. **Nong.Toolkit.Net 版本对齐**（P0）：更新所有版本引用到 Nong 4.0.0
2. **撤回 progress-report 错误产物**（P1）：删除 tools/ProgressReport/，SKILL.md 移到 Nong.Toolkit.Net
3. **在 Cli/Commands/ 实现 progress 命令**（P1）：ProgressCommands.cs，挂到 nong progress report
4. **Nong.Toolkit.Net 日志结构化**（P2）：加 plans/changelog/debug 目录和索引
5. **Nong.Toolkit.Net 加 CLAUDE.md**（P5）：写开发指导，讲清楚"两层"关系
6. **减少重复代码**（P3）：创建共享 preflight.md reference
7. **skill-manager 同步 Nong.Toolkit.Net 结构**（P4）：更新 skill-manager 让它知道 GroundPA 的约束
