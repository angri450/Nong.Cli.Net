# debug 索引

> Agent 收到用户测试反馈后写入此目录并同步更新此文件。格式：`- YYYY-MM-DD | 文件名 | 一句话摘要 | 状态（待处理/已修复/已验证）`

- 2026-06-09 | 2026-06-09-progress-report-wrong-approach.md | progress-report skill 创建偏离架构：独立 .NET 项目而非 nong CLI 命令；nong 工具链状态误判；SKILL.md 调用链路不合理 | 待处理
- 2026-06-09 | 2026-06-09-groundpa-vs-nong-comparison.md | Nong.Toolkit.Net vs Angri450.Nong 完整对比：10 个不对齐问题（版本、skill 归属、日志结构、skill-manager 位置等） | 待处理
- 2026-06-09 | 2026-06-09-merge-assessment.md | 合并方案评估：Nong.Toolkit.Net → Nong.NanoBot.Net，结论是 skill 合并进 NanoBot、Nong 保持独立 | 参考
- 2026-06-13 | 2026-06-13-core-issues-round1.md | 用户反馈6问题：preflight误判/word_fill缺占位符/NongMark无字体/OCR噪音/PDF解码/版本号误报 | 5/6已修复 (4.1.5)
- 2026-06-09 | 2026-06-09-repo-naming-design.md | 三仓库命名统一设计：Nong 不改名（NuGet 包已固定），NanoBot 可微调 | 参考
