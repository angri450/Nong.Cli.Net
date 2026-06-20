# Scan 发现问题后的处理流程

## 用户想要什么

安全扫描报了一个 Medium 和一个 High，需要处理。

## 出了什么问题

```powershell
nong skill scan . --json
```

```json
{
  "status": "ok",
  "findings": [
    {"severity": "high", "rule": "credential_leak",
     "file": "scripts/install.ps1", "detail": "$env:API_KEY = 'sk-abc123'"},
    {"severity": "medium", "rule": "unsafe_inner_html",
     "file": "log/reports/summary.html", "detail": "element.innerHTML = data"}
  ]
}
```

## 怎么处理的

**High — 凭证泄露（必须修）**：
```powershell
# 原来：$env:API_KEY = 'sk-abc123'
# 改为：$env:API_KEY = $env:MY_API_KEY  # 从环境变量读取
```
修复后在 `install.ps1` 中只读环境变量，不写入任何真实或看起来像真实的 key。

**Medium — innerHTML（报告生成器，可接受）**：
这是 `nong progress report` 自动生成的本地只读报告，HTML 内容是项目日志数据，不含用户输入。在 CLAUDE.md 中加说明："auto-generated local reports use innerHTML for static log data, not user input"。

修复 High 后重新 scan：
```powershell
nong skill scan . --json
# findings: []  ← 清零
```

## 关键点

- High 必须修复，不能跳过或标记为"已知"。
- Medium 可以接受但要说明原因——在 changelog 或 CLAUDE.md 中记录。
- 修复后重新 scan 确认清零。
- 不要在 scan 之前手动跳过问题——scan 是自动化门禁，不应该有例外。
