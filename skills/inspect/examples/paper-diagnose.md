# 论文全诊断流程

## 用户想要什么

一篇待投稿的论文，需要全方位质量诊断。

## 做了什么

```powershell
# 1. 提取论文纯文本（如果是 .docx）
nong word read paper.docx --json | Out-File paper.txt

# 2. 全诊断
nong inspect diagnose paper.txt --json
```

## 结果

诊断输出摘要：
- **类型**：研究论文 (Research Article)
- **结构**：IMRaD 完整
- **证据链**：3 个结论中 1 个缺少统计检验支撑
- **引用**：5 处引用格式不一致，1 处年份矛盾
- **变量**：自变量和因变量定义清晰
- **数据需求**：样本量 n=15，可能不足以支撑 3 因素交互分析
- **缺口等级**：中等 — 需要补充统计分析，修复引用格式

## 后续处理

1. 引用格式问题 → 手动修复引用列表
2. 统计分析 → 用户补充 ANOVA 交互效应分析
3. 修完后再跑 `inspect diagnose` 验证缺口是否消除

## 关键点

- `inspect diagnose` 是诊断工具，不是修复工具。它告诉你问题在哪，不自动修复。
- 输入必须是纯文本 `.txt`。如果用户给的是 `.docx`，先 `word read` 提取文本。
- 缺口等级（gap grade）是综合评分。中等及以上建议修复后再投稿。
- 每个子维度都可以单独诊断：`inspect refs`、`inspect variables`、`inspect data-requirements` 等。
