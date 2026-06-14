# 2026-06-11 施工质量审计 + 全面修复方案

## 审计来源

2026-06-10 全部施工结束后，CLI 侧和 Toolkit 侧各做了一次对齐审查。本方案汇总了所有发现，按优先级排列。

## P0：文档和命令直接不对（会导致 AI 调用失败）

### 1. `inspect/SKILL.md:63` — paper spec 示例 `"body"` → 应为 `"content"`

**根因**：昨天阶段三写 inspect SKILL.md 时示例用了 `"body"`，但 CLI 解析的是 `"content"`。write-paper-spec.md 是对的。

**修复**：
1. `inspect/SKILL.md` — `"body"` → `"content"`
2. CLI `InspectCommands.cs` — 防御性同时接受 `"content"` 和 `"body"`（防止后续再出同类问题）

估时：15min。

### 2. `inspect/references/write-paper-spec.md` — keywords 类型 `string[]` → 应为 string

**根因**：CLI 当初设计时 keywords 是分号分隔字符串（`"kw1; kw2"`），但 reference 文档写成了数组。SKILL.md 反而写对了。

**修复**：
1. `write-paper-spec.md` — keywords 字段说明改为 `string`，注明分隔符
2. CLI `InspectCommands.cs` — 放宽校验，同时接受 `string` 和 `string[]`，数组自动 join

估时：20min。

### 3. `excel/examples/read-to-statistics.md` — `--treatment-col`/`--value-col` → 应为 `--group`/`--value`

**根因**：写 example 时凭记忆写了参数名，没对照 SKILL.md 核实。

**修复**：example 文件改参数名。

估时：5min。

### 4. `slice/examples/` 3 个文件 — `--block-id` flag → 应为 positional arg

**根因**：写 example 时用了 `--block-id` flag 形式，但 CLI 接受的是 positional arg。

**修复**：`read-word-slice.md`、`read-pdf-slice.md`、`block-level-evidence.md` — 改参数格式。

估时：10min。

## P1：新命令没补文档/测试

### 5. `pdf ocr` — 有命令面无 skill 文档无行为测试

**修复**：
1. `pdf/SKILL.md` — 补充 `pdf ocr` 命令说明和 dispatch
2. `Cli.Tests/CliContractTests.cs` — 增加 pdf ocr 行为测试

估时：30min。

### 6. `inspect official-check` — 有命令面无 skill 文档

**修复**：`inspect/SKILL.md` — 补充 `inspect official-check` 命令说明和 dispatch。

估时：15min。

### 7. `pptx create` — 缺少 happy-path 测试

**修复**：`Cli.Tests/CliContractTests.cs` — 增加 pptx create 正常生成测试。

估时：15min。

### 8. `chart heatmap/radar` — 测试断言偏弱（缺 JSON status 检查和文件大小检查）

**修复**：补强断言。

估时：10min。

## P2：参数名核实（需逐一确认 CLI 实际行为）

### 9. `literature/examples/` — lit parse/validate/plan/search 用 positional arg 还是 `--query`

**修复**：先核实 CLI 实际接受的参数格式，再决定修 example 还是修 SKILL.md。

估时：15min。

### 10. `skill validate` — 插件根目录报错信息不友好

**修复**：`SkillManagerCore/Tools/SkillValidator.cs` — 检测 plugin root，增加提示信息。

估时：15min。

## P3：eval 体系起步

### 11. 核心 4 个 skill 各有 1 个最小 eval

eval 是放在 `skill/evals/` 下的 CLI 调用快照，`nong skill package` 用它做 blind eval gate。

**修复**：给 word、inspect、chart、literature 各补 1 个 eval 文件。

估时：1h。

## 修复顺序（按阻塞程度）

| 序号 | 优先级 | 问题 | 状态 |
|------|--------|------|------|
| 1 | P0 | inspect SKILL.md + CLI body/content 双重防御 | **DONE** |
| 2 | P0 | write-paper-spec.md keywords 类型 + CLI 放宽 | **DONE** |
| 3 | P0 | excel example 参数名 | **DONE** |
| 4 | P0 | slice examples block-id 格式 | **DONE** |
| 5 | P2 | lit 参数格式核实修复 (4 文件 14 处) | **DONE** |
| 6 | P1 | pdf ocr 文档 | **DONE** |
| 7 | P1 | inspect official-check 文档 | **DONE** |
| 8 | P1 | pptx create happy-path 测试 | **DONE** |
| 9 | P1 | heatmap/radar 测试断言补强 | **DONE** |
| 10 | P2 | skill validate 报错改进 | **DONE** |
| 11 | P3 | 4 核心 skill eval | 待做（后续） |

## 验证状态

- CLI 编译: 0 errors ✅
- Toolkit scan: 0 findings ✅
- Toolkit validate: 15/15 PASS ✅
- CLI 全量测试: 运行中...

## 改动摘要

**CLI 侧**：
- `InspectCommands.cs` — keywords 接受 string[]，body 和 content 双别名
- `SkillValidator.cs` — 插件根目录友好提示

**Toolkit 侧**：
- `inspect/SKILL.md` — body/content 统一 + official-check dispatch
- `inspect/references/write-paper-spec.md` — keywords 类型修正 + body 字段
- `pdf/SKILL.md` — 补充 pdf ocr
- `excel/examples/read-to-statistics.md` — 参数名修正
- `slice/examples/` 3 个文件 — block-id 格式修正
- `literature/examples/` 3 个文件 + `inspect/examples/` 1 个 — lit --query 参数修正

总计：约 4h。

## 不改什么

- 不改其他 13 个 skill 的主体结构
- 不改 word 那 18 个老命令的文档（word skill 是教程式，不是命令清单式）
- 不改 chart/excel/pdf 的 skill 文档（已确认更新到位）

## 状态

executing — 2026-06-11 开工中。
