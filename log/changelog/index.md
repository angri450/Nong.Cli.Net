# changelog 索引

> Agent 添加变更记录后同步更新此文件。格式：`- YYYY-MM-DD | 文件名 | 一句话摘要`

## 2026-06-14
- 2026-06-14 | 2026-06-14-4.2.1-cellreplace-refactor.md | Word 模板填充引擎重构: cellReplace 键值匹配 + tableRows 数据行追加，告别 index/match 定位漂移

## 2026-06-13
- 2026-06-13 | 2026-06-13-4.1.5-core-fixes.md | v4.1.5: OCR preflight 去阻塞化 + word fill 表格位置填充 + NongMark 字体属性 + OCR 日志抑制 + 版本号修正：一键安装模型+运行时，不再分开
- 2026-06-13 | 2026-06-13-nong-ocr-skiasharp-native-fix.md | nong-ocr 缺 SkiaSharp.NativeAssets.Win32 导致 libSkiaSharp 加载失败；ocr check-env imageAnalyzer 硬编码修复
- 2026-06-13 | 2026-06-13-4.1.2-new-publish.md | 4.1.2 新版本发布：版本同步、清理 nupkg、7 包本地安装验证、NuGet Created 并索引可见
- 2026-06-13 | 2026-06-13-4.1.0-nupkg-refresh-nuget-push.md | 清理 root nupkg 并刷新 7 个 4.1.0 工具包；NuGet push 全部 duplicate skip，确认 4.1.0 已存在
- 2026-06-13 | 2026-06-13-4.1.0-local-install-closure.md | 4.1.0 本地安装闭环：7 包 clean pack、本地 tool-path 安装、installed smoke、nupkg 策略收口
- 2026-06-13 | 2026-06-13-4.1.0-release-doc-sync.md | 4.1.0 发布真相源同步：版本常量/csproj、README/Agent/能力表/checklist、126 命令合同、clean pack audit
- 2026-06-13 | 2026-06-13-builder-planner-prompts.md | 新增根目录提示词文件：施工窗口.md / 计划窗口.md，对应双窗口开发流程
- 2026-06-13 | 2026-06-13-ai-context-map.md | AI 上下文入口重构：PROJECT_STATE 当前真相源、docs/wiki 项目百科、log 历史归档、双窗口计划/施工流程
- 2026-06-13 | 2026-06-13-cli-gap-fill.md | CLI 功能缺口补全：PPTX create 验证 DONE / Word compare 验证 DONE / PDF ocr-pdf --with-ocr 真正 OCR 文本层 / 9/10 缺口 DONE
- 2026-06-13 | 2026-06-13-modular-release-final-audit.md | 模块化发布最终审计：7 包全 GO、RID 瘦身 83→26MB、pack audit 闸门、OCR v6 文案统一、本地安装闭环

## 2026-06-12
- 2026-06-12 | 2026-06-12-ocr-expansion.md | OCR 4 新命令：batch(批量)/video(视频帧+SRT)/screen(屏幕区域)/camera(摄像头)
- 2026-06-12 | 2026-06-12-ppocrv6-adaptation.md | PP-OCRv6 全线适配：4 model-ID + CDN 下载 + 嵌入式字典 + v6 优先自动检测
- 2026-06-12 | 2026-06-12-word-fit-images.md | nong word fit-images：inline 多图段落合并+缩放并排
- 2026-06-12 | 2026-06-12-compaction-pipeline.md | 文档紧缩管线：compact-tables + regroup-images + estimate（25页→8页）
- 2026-06-12 | 2026-06-12-keepnext-pagination.md | keepNext/cantSplit 分页控制升级：compact-tables 表头粘连 + 行保护
- 2026-06-12 | 2026-06-12-p2-full-ooxml-coverage.md | P2 收尾：image-wrap + cell-format + run-format + 4 bug fixes（OOXML 51 属性全覆盖完工）

## 2026-06-11
- 2026-06-11 | skill-governance-refactor.md | skill 治理重构：农业五角色体系（breeder/tester/grader/pruner/patrol），15→17 skills
- 2026-06-11 | 2026-06-11-word-crop-imaging.md | nong word crop：内容感知图片裁剪 + Angri450.Nong.Imaging 包

## 2026-06-10
- 2026-06-10 | chart-heatmap-radar.md | 阶段四+：Chart heatmap + radar（106 命令，143 全过）
- 2026-06-10 | pptx-create.md | 阶段四+：PPTX create（104 命令，SlideBuilder API）
- 2026-06-10 | inspect-official-check.md | 阶段四+：inspect official-check（103 命令）
- 2026-06-10 | excel-style-formula.md | 阶段四+：excel style + formula（102 命令）
- 2026-06-10 | pdf-merge-split.md | 阶段四+：PDF merge + split（100 命令）
- 2026-06-10 | chart-boxplot-histogram.md | 阶段四+：Chart boxplot + histogram（98 命令）
- 2026-06-10 | p1-alias.md | 阶段一完工：5 个 P1 alias（word diagnose/clean-styles、inspect references/variables/data-requirements） + 6 个回归测试
- 2026-06-10 | final-blueprint.md | 最终蓝图：用中文定稿三层结构、命名词典、命令组职责与 AI 口径
- 2026-06-10 | command-naming-audit.md | Toolkit/CLI 命令命名审计：明确区分命令重复、命令缺少、命令不对
- 2026-06-10 | toolkit-cli-command-parity.md | Toolkit/CLI 命令面对齐：新增 `word format-gongwen` 与 `inspect write-official`
- 2026-06-10 | lockfile-and-local-tool-sync.md | P9 lock 文件重算并修复全局 nong 4.0.0 本地工具安装

## 2026-06-09
- 2026-06-09 | ocr-runtime-repo-name-sync.md | OCR runtime 仓库名称同步
- 2026-06-09 | progress-report-skill.md | progress-report skill 创建：纯 .NET CLI 工具 + SKILL.md，读取 log/ 生成 90 页 HTML 报表
- 2026-06-09 | repository-doc-layout-cleanup.md | 仓库文档布局清理
- 2026-06-09 | repository-doc-restructure.md | 仓库文档结构重组：CLAUDE.md 拆分 + log/ 目录重整 + 索引体系 + 导航表
- 2026-06-09 | third-party-source-snapshot-policy.md | 第三方源码按 fork 固定版本源码快照管理，临时 clone 放 ignored `_archive/`

## 2026-06-08 — 4.0.0 发布线
- 2026-06-08 | agent-development-rules.md | Agent 开发规则建立
- 2026-06-08 | nong-4.0.0-release.md | Nong.NET 4.0.0 正式发布
- 2026-06-08 | nong-pandoc-contract-alignment.md | Pandoc 合同对齐
- 2026-06-08 | nong-pandoc-internal-writer-alignment.md | Pandoc 内部写入器对齐
- 2026-06-08 | nong-pandoc-manifest-and-office-slices.md | Pandoc manifest 与 Office slice 定义
- 2026-06-08 | nong-pandoc-package-kickoff.md | Pandoc 包启动
- 2026-06-08 | nong-pandoc-slice-reader-contract.md | Pandoc slice reader 合同
- 2026-06-08 | nong-pandoc-strict-query-visual-evidence.md | Pandoc 严格查询与视觉证据
- 2026-06-08 | nong-pandoc-unified-slice-evidence.md | Pandoc 统一 slice 证据
- 2026-06-08 | ocr-runtime-version-decoupling.md | OCR runtime 版本解耦
- 2026-06-08 | word-format-audit-visible-evidence.md | Word 排版审计可视证据
- 2026-06-08 | word-format-gates-and-slice-strict-regression.md | Word 排版闸门与严格回归
- 2026-06-08 | word-regression-visual-evidence-and-reflow-rules.md | Word 回归可视证据与重排规则

## 2026-06-07 — 3.2.5 发布线
- 2026-06-07 | release-3-2-5-worker-runtime-result.md | 3.2.5 worker runtime 发布结果
- 2026-06-07 | stage19-lit-unified-literature-dsl-result.md | Stage 19 文献统一 DSL 结果

## 2026-06-06 — 本地 OCR / PDF
- 2026-06-06 | local-ocr-numeric-boundary-repair.md | 本地 OCR 数字边界修复
- 2026-06-06 | stage18-local-pdf-one-cut-three-stream-result.md | Stage 18 本地 PDF 一刀三流结果

## 2026-06-05 — Word 修复 / OCR
- 2026-06-05 | stage17b-nong-groundpa-blocker-repair.md | Stage 17b GroundPA 阻塞修复
- 2026-06-05 | stage17c-pure-dotnet-ppocrv5.md | Stage 17c 纯 .NET PP-OCRv5 实现
- 2026-06-05 | word-format-slice-read-enhancement.md | Word 排版 slice 读取增强
- 2026-06-05 | word-realcase-contract-matrix-repair.md | Word 真实案例合同矩阵修复

## 2026-06-04 — OCR 基础设施 / Stage 15-16
- 2026-06-04 | 021-stage16-ocr-feasibility-report.md | Stage 16 OCR 可行性报告
- 2026-06-04 | 022-overnight-stage16-release-result.md | Stage 16 隔夜发布结果
- 2026-06-04 | 024-stage15b-one-shot-repair-result.md | Stage 15b 一键修复结果

## 2026-06-03 — CLI 架构 / Stage 11-18
- 2026-06-03 | 001-cli-architecture-result.md | CLI 架构结果
- 2026-06-03 | 002-stage2-word-read-preview.md | Stage 2 Word 读取预览
- 2026-06-03 | 003-stage3-research-core.md | Stage 3 研究核心
- 2026-06-03 | 004-stage4-generation-core.md | Stage 4 生成核心
- 2026-06-03 | 005-010-stages5-10-complete.md | Stage 5-10 完成
- 2026-06-03 | 011-stage11-audit.md | Stage 11 审计
- 2026-06-03 | 011-stage11-audit-full.md | Stage 11 审计（完整版）
- 2026-06-03 | 011-stage11-audit-round3.md | Stage 11 审计第三轮
- 2026-06-03 | 011-stage11-audit-round4.md | Stage 11 审计第四轮
- 2026-06-03 | 016-stage13-nong-core-hardening-result.md | Stage 13 Nong 核心加固结果
- 2026-06-03 | 017-stage14-stub-completion-result.md | Stage 14 存根补全结果
- 2026-06-03 | 018-stage15-gate-report.md | Stage 15 闸门报告
- 2026-06-03 | merge-execution.md | 合并执行
- 2026-06-03 | merge-roadmap.md | 合并路线图

## 2026-06-02 — 架构决策 / CLI 规范
- 2026-06-02 | architecture-decision.md | 架构决策
- 2026-06-02 | cli-chart-spec.md | CLI Chart 规范
- 2026-06-02 | cli-diagram-spec.md | CLI Diagram 规范
- 2026-06-02 | cli-excel-spec.md | CLI Excel 规范
- 2026-06-02 | cli-icons-genre-spec.md | CLI Icons/Genre 规范
- 2026-06-02 | cli-inspect-spec.md | CLI Inspect 规范
- 2026-06-02 | cli-master-summary.md | CLI 主汇总
- 2026-06-02 | cli-ocr-spec.md | CLI OCR 规范
- 2026-06-02 | cli-pptx-spec.md | CLI PPTX 规范
- 2026-06-02 | cli-word-spec.md | CLI Word 规范
- 2026-06-02 | competitive-analysis-conclusions.md | 竞品分析结论
- 2026-06-02 | docx-split-and-bugs.md | Docx 拆分与 Bug
- 2026-06-02 | full-session.md | 全会话记录
- 2026-06-02 | skill-adaptation.md | Skill 适配

## 2026-06-01 — 基础设施
- 2026-06-01 | cjk-font.md | CJK 字体问题
- 2026-06-01 | diagram-layout.md | Diagram 布局
- 2026-06-01 | imganalyzer.md | 图片分析器
- 2026-06-01 | skill-path-audit.md | Skill 路径审计
- 2026-06-01 | skill-path-fix.md | Skill 路径修复
- 2026-06-01 | tfm-unlock.md | TFM 解锁
- 2026-06-01 | transitive-deps.md | 传递依赖
