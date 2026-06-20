# Security Scan 输出解读

`nong skill scan . --json` 返回的安全扫描结果，按严重度分级。

## 严重度分级

| 级别 | 含义 | 示例 | 打包要求 |
|------|------|------|----------|
| High | 高风险，必须修复 | 硬编码 API key、私钥、密码 | 必须修复才能打包 |
| Medium | 中风险，建议修复 | `innerHTML` 使用、`eval()` 调用、CDN 外部链接 | 强烈建议修复 |
| Low | 低风险，可接受 | 示例代码中的测试邮箱、绝对路径提示 | 可忽略，打包不阻塞 |

## 典型 High 问题和修复

### 硬编码凭证
```
Finding: credential_leak - file: scripts/deploy.ps1 - "apiKey = 'sk-abc123'"
Fix: 改为从环境变量读取：$apiKey = $env:MY_API_KEY
```

### 私钥示例
```
Finding: private_key_example - file: README.md - "---BEGIN SOME PRIVATE KEY---"
Fix: 改成占位符：<your-private-key> 或移除真实格式的示例
```

## 典型 Medium 问题和修复

### innerHTML
```
Finding: unsafe_inner_html - file: report.html - "element.innerHTML = data"
Fix: 用 textContent 替代，或在注释中说明这是本地只读报告
```

### CDN 引用
```
Finding: external_cdn - file: index.html - "<script src='https://example-cdn.example.com/lib.js'>"
Fix: 改为本地文件引用或内联，Toolkit 的标准是 CDN-free
```

## 关键点

- scan 结果是 build gate，不是建议。High 必须清零。
- 如果某个 Medium finding 确实无法修复（如第三方源码自带的），在 CLAUDE.md 或 changelog 中说明原因。
- scan 的输出中 `findings` 为空数组 `[]` 表示 0 问题，这是目标状态。
