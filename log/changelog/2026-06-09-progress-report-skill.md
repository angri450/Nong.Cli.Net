# progress-report skill 创建

## 时间
2026-06-09

## 影响范围
新增项目 skill + .NET 工具，无现有代码变更。

## 变更类型
新功能 — 项目开发进展报表。

## 做了什么

1. 创建项目级 skill `.claude/skills/progress-report/SKILL.md`
2. 创建 .NET CLI 工具 `tools/ProgressReport/`，读取 `log/` 下四个子目录的 markdown 文件，生成 HTML 报告
3. 首次运行生成 90 页 HTML 报告到 `log/reports/`

## 文件清单

新增：
- `.claude/skills/progress-report/SKILL.md` — skill 内核
- `tools/ProgressReport/ProgressReport.csproj` — .NET 项目
- `tools/ProgressReport/Program.cs` — 报告生成器（单文件 C#）

生成（由工具自动产出）：
- `log/reports/index.html` — 总览仪表盘（90 条记录，4 个板块卡片）
- `log/reports/plans.html` — 施工方案汇总
- `log/reports/changelog.html` — 变更记录汇总
- `log/reports/debug.html` — 用户反馈汇总
- `log/reports/guidance.html` — 开发指导汇总
- `log/reports/style.css` — 共享样式
- `log/reports/pages/*.html` — 90 个条目独立页面

## 使用方法

```powershell
dotnet run --project tools/ProgressReport -- --project-root .
# 然后用浏览器打开 log/reports/index.html
```

## 设计特点

- 纯 .NET 8 CLI 工具，零外部 NuGet 依赖，零 JavaScript
- 单文件 C#（约 450 行），内联 CSS 样式
- 内存中 markdown 解析（支持标题、列表、代码块、表格、粗体、链接）
- 文件名解析容错：自动尝试 date-prefix 和 exact-match 两种模式
- 颜色编码：蓝=方案、绿=变更、红=反馈、紫=指导
- 响应式布局，手机/桌面均可读

## 技能须知

- 用户在 Claude Code 中输入"看进展"、"生成报告"等即可触发
- Skill Agent 每次写 log 后应运行工具更新 HTML
- 工具全量重新生成，确保与源文件一致
