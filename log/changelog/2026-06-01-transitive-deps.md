# NuGet 传递依赖修复

**时间**: 2026-06-01
**影响包**: Angri450.Nong.Chart (3.0.3), Angri450.Nong.Excel (3.0.3)
**类型**: Bug Fix

## 问题

Chart 和 Excel 的 csproj 中 `PrivateAssets="all"` 阻止 ThirdParty 作为 NuGet 传递依赖流向消费者。但这两个包的公开 API 暴露了 ThirdParty 中的类型（如 ScottPlot.Color）。消费者只安装 Chart/Excel 时编译报 CS0012 "类型在未引用的程序集中定义"。

## 修复

移除 Chart 和 Excel 的 `PrivateAssets="all"`，与 Diagram/Docx 保持一致。ThirdParty 作为正常传递依赖被拉取。

## 技能须知

- 消费者**不要额外安装** `ClosedXML` 或 `ScottPlot` NuGet 包——这些类型已合并到 `Angri450.Nong.ThirdParty` 中，重复安装会导致 CS0433 类型冲突。
- Excel skill 的 Quick Start 中应加入此警告。
