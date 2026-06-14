# CLAUDE.md — Nong.Cli.Net

Pure .NET scientific document generation toolkit. Zero JavaScript.

## 先读这里

`PROJECT_STATE.md` 是当前真相源。每次开发前先读它，再读本文件。

不要通过批量阅读 `log/` 来判断当前项目状态。`log/` 是历史档案；只有 `PROJECT_STATE.md`、当前 active plan/handoff 或用户明确引用的 log 文件才是本轮输入。

稳定项目知识放在 `docs/wiki/`：

- `docs/wiki/architecture.md`
- `docs/wiki/development-history.md`
- `docs/wiki/planning-workflow.md`

开发计划仍写在 `log/plans/`。但施工窗口只读 `PROJECT_STATE.md` 指向的 active plan/handoff；不要自行遍历所有旧 plan。

## 仓库

- GitHub: `https://github.com/angri450/Nong.Cli.Net`
- Gitee: `https://gitee.com/angri450/Nong.Cli.Net`
- GitCode: `git@gitcode.com:angri450/Nong.Cli.Net.git`
- 主分支: `main`
- 协议: Apache-2.0

## 当前进度（2026-06-13）

- 当前施工线: `4.1.2` 已发布
- 当前分支: `main`
- 当前命令面: `nong commands --json` 返回 126 条命令
- 当前 active handoff: `log/plans/2026-06-13-4.1.2-new-publish.md`
- OCR runtime 已拆到独立 `Nong.OcrRuntime` 仓库维护，主仓库只消费已发布 runtime 版本
- OCR runtime 版本由 `Cli/Common/OcrRuntimeVersion.cs` 独立锁定，不随 CLI 小版本自动变化
- 重要架构变化:
  - 主 `nong` 是轻路由器 + 纯 .NET 轻模块
  - `chart` / `diagram` / `pdf` / `pptx` / `ocr` / `imaging` 是独立 dotnet tool
  - 工具包使用 `Angri450.Nong.Tool.*`，不要再用核心库 PackageId 作为 tool PackageId
  - Chart/Diagram/Imaging 当前 4.1.2 包采用 Windows native assets 策略
  - 默认测试套件不跑 Chart/Diagram/OCR/PDF 的 native 图像渲染路径

---

## Agent 开发工作流（强制）

每次 Agent 开发任务必须按以下四步循环执行：

### 1. Context — 先确认当前真相

- 先读 `PROJECT_STATE.md`
- 再读本文件
- 再读 `PROJECT_STATE.md` 中链接的 active plan/handoff
- 只在需要历史证据时，按链接读取 `log/` 中的具体文件
- 不要批量读完整 `log/` 目录后自行推断当前任务

### 2. Plan — 写清楚本轮施工

- 如果是实质代码/发布/结构变更，写 `log/plans/YYYY-MM-DD-topic.md`
- 同步更新 `log/plans/index.md`
- 如果这个 plan 要交给另一个窗口施工，同步更新 `PROJECT_STATE.md` 的 active plan/handoff 指针
- 对小型文档修复，可以把计划写在当前 active handoff 或简短 plan 中
- 计划必须明确 current-state 文件、施工范围、验证命令、非目标

### 3. Build — 按方案施工

- 遵守 `PROJECT_STATE.md` 中的当前架构与风险提示
- 跟随既有项目模式
- 不为核心功能引入 JavaScript 或 Python
- 不改变用户命令入口，除非用户明确要求
- 不动不相关 dirty worktree 变更

### 4. Verify — 验证

- 先跑最窄有用验证
- CLI 行为优先用命令级测试和 JSON 合同检查
- 文档导航/上下文改动至少读回相关文件确认
- 报告无法运行的测试

### 5. Changelog — 完工记录

- 代码完工后，写变更记录到 `log/changelog/YYYY-MM-DD-topic.md`
- 同步更新 `log/changelog/index.md`
- 如果是发布相关变更，写清楚影响哪个包、Skill Agent 需要怎么跟进

### 6. Debug — 收集反馈

- 用户测试反馈存入 `log/debug/YYYY-MM-DD-topic.md`
- 同步更新 `log/debug/index.md`
- Debug 反馈是下一轮 Plan 的输入

---

## 导航表

需要以下信息时，读对应的参考文件（按需加载，不要全读）：

| 你需要 | 去这里 |
|--------|--------|
| 当前项目真相 | `PROJECT_STATE.md` |
| 当前施工方案 | `PROJECT_STATE.md` 链接的 active plan/handoff |
| 稳定架构说明 | `docs/wiki/architecture.md` |
| 开发历程摘要 | `docs/wiki/development-history.md` |
| 双窗口计划/施工流程 | `docs/wiki/planning-workflow.md` |
| 历史开发指导 / Stage 蓝图 | `log/guidance/index.md`，仅按需 |
| 历史变更记录 | `log/changelog/index.md`，仅按需 |
| 用户测试反馈 | `log/debug/index.md` |
| 仓库级维护文档 | `docs/` |

详细的构建规范、NuGet 发布流程、第三方源码注意事项等开发参考信息存放在 `.claude/references/`（本地文件，不推送到远程仓库）。Agent 开发时可直接读取。

---

## 禁止事项

- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能；本地 OCR 当前走纯 .NET PP-OCRv6 first，PP-OCRv5 仅为 legacy compatibility
- 不要为第三方库创建独立的 `.csproj` — 统一走 ThirdParty
- 不要提交第三方上游 `.git/` 目录；第三方源码只能作为 fork-pinned source snapshot 提交，清单维护在 `docs/DEPENDENCY_CONTROL.md`
- 不要用 PowerShell 的 `-replace` 批量编辑 `.csproj` — 会损坏 XML，用 Edit 工具逐文件改
- 不要把 Chart/Diagram/OCR/PDF render 的真实 PNG 生成测试加回默认 `dotnet test`
- 不要把 `log/` 中的旧计划当成当前任务；旧 log 只有历史证据价值

---

## 第三方源码快照规则

- 第三方源码目录要保留，因为 `ThirdParty/ThirdParty.csproj` 直接编译这些源码。
- 每个上游先 fork 到 `https://github.com/angri450/`，在 `docs/DEPENDENCY_CONTROL.md` 记录 fork URL 和 locked commit。
- 拉取/刷新上游源码时，先在 `_archive/` clone/fetch，确认 commit，再复制需要的源码、资源、LICENSE、NOTICE 到本仓库；`_archive/` 是本地临时区，不能提交。
- 复制进本仓库前必须删除上游 `.git/`、CI 缓存、包输出、demo、example、test、`bin/`、`obj/` 等非必要内容。
- 如果需要保留裁剪说明，在对应第三方目录放 `NONG_SOURCE.md`；已有本地适配和冲突处理也要写入 CLAUDE 或 changelog。
- 不要把第三方源码改成 Git submodule；当前策略是“源码快照 + 固定 fork commit + 本仓库直接编译”。

---

## 对项目维护者的说明

你不用自己写代码。告诉 Claude 要做什么，Claude 负责：
- 写代码、修 bug、加功能
- 改 csproj、build、pack、推送 NuGet
- 创建 GitHub Release
- 整理项目结构

你只需要：
- 决定方向（"加个饼图"、"发版"、"修那个 bug"）
- 提供 API Key（NuGet 推送时，环境变量 `$env:NUGET_API_KEY` 已就绪）
- 决定何时推送/发布；Agent 不会在未明确要求时发布 NuGet 或 push 远端
