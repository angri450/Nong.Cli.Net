# 文档对比发现修改

## 用户想要什么
一篇论文修改了两轮，想快速看清第二版相对第一版改了什么。

## 做了什么
```powershell
nong word compare paper_v1.docx paper_v2.docx --json
```

## 结果
```json
{
  "status": "ok",
  "command": "word compare",
  "metrics": {"changes":3,"added":1,"removed":0,"modified":2}
}
```
输出列出了 3 处变更：1 段新增、2 段修改。每段显示新旧文本和样式。

## 关键点
- `word compare` 做段落级对比，不是字符级 diff
- 完全相同的段落不会被报告
- 修改检测基于标准化文本（多余空格折叠），不基于 OOXML 元素级别
