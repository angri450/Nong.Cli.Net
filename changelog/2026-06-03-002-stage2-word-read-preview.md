# 阶段 2：word read + word preview 实现结果

日期：2026-06-03
状态：完成

---

## 改动清单

### 新增文件

| 文件 | 用途 |
|------|------|
| `Docx/WordTextReader.cs` | 新增 docx 文本读取 API（不截断，替代 WordPreview.Text） |
| `Cli/Commands/WordCommands.cs` | word 命令组：read/preview 真实实现 + 9 个空桩 |
| `Cli/Common/CliHelpers.cs` | 公共工具：文件验证、JSON 输出、计时、错误响应 |

### 重构文件

| 文件 | 改动 |
|------|------|
| `Cli/Program.cs` | 业务逻辑移到 WordCommands 和 CliHelpers，Program.cs 只做入口和注册 |
| `Cli/Common/Manifest.cs` | 补上 inspect refs resolve/generate、evidence/data-req/gap/semantics |

### 修复

| 问题 | 修复 |
|------|------|
| Manifest 与命令树漂移 | 补上 5 个缺失命令 |
| JSON PascalCase | 统一 camelCase（JsonNamingPolicy.CamelCase） |
| --version 含 hash | 改为 "nong v3.1.0" |

---

## 验收结果

| 命令 | 结果 |
|------|------|
| `nong word read docx-basic.docx` | 输出纯文本（29 段 + 2 表 + 脚注 + 尾注） |
| `nong word read docx-basic.docx --json` | JSON 含 text/paragraphs/tables/footnotes/endnotes + metrics |
| `nong word preview docx-basic.docx` | 诊断报告（10 warnings, 0 errors） |
| `nong word preview docx-basic.docx --json` | JSON 含 text/warnings/errors/info/statistics + metrics |
| `nong word read missing.docx --json` | 错误 JSON，code=E001，name=file_not_found |
| `dotnet build -c Release` | 0 错误 |

---

## 阶段 2 结论

- word read 可替代模型写 PowerShell 读取 docx
- JSON 输出稳定（camelCase，统一 schema）
- 错误输出机器可读（错误码 + 名称）
