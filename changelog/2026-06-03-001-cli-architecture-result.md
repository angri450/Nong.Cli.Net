# 阶段 1：CLI 架构搭建结果

日期：2026-06-03
状态：完成

---

## 工程信息

| 项 | 值 |
|----|-----|
| 位置 | `Cli/` |
| 项目文件 | `NongCli.csproj` |
| 程序集名 | `nong` |
| NuGet 包名 | `Angri450.Nong.Cli` |
| 工具命令名 | `nong` |
| 目标框架 | net8.0 |
| 依赖 | Docx + Inspect（ThirdParty 传递引用） |
| CLI 框架 | System.CommandLine 2.0.0-beta4 |

---

## 文件清单

```
Cli/
├── NongCli.csproj          # 项目文件
├── Program.cs              # 入口 + 命令注册 + 工作流别名
├── Common/
│   ├── JsonOutput.cs       # JSON 输出模型
│   ├── ErrorCodes.cs       # 错误码定义（E001-E008）
│   └── Manifest.cs         # 命令清单（40 条）
└── README.md               # 安装说明
```

---

## 命令统计

| 组 | 命令数 | 说明 |
|----|--------|------|
| word | 11 | read/preview/extract/dissect/rebuild/fill/stats/fonts/styles/validate/merge |
| inspect | 12 | classify/structure/diagnose/refs/varplan/evidence/data-req/gap/semantics + write paper/official/letter |
| chart | 6 | bar/line/scatter/pie/anova/duncan |
| diagram | 3 | flowchart/network/tree |
| excel | 3 | read/sheets/create |
| pptx | 2 | read/slides |
| ocr | 2 | local/cloud |
| icons | 2 | list/search |
| genre | 2 | list/show |
| **合计** | **43** | |

不含重复的 workflow aliases（paper/rets/official/stats 组）。

---

## 工作流别名

| 命令 | 等价于 | 原因 |
|------|--------|------|
| `nong paper classify` | `nong inspect classify` | 用户视角，省 token |
| `nong paper diagnose` | `nong inspect diagnose` | |
| `nong paper write` | `nong inspect write paper` | |
| `nong refs check` | `nong inspect refs` | |
| `nong official write` | `nong inspect write official` | |
| `nong official format` | 独立命令（唯一功能） | |
| `nong stats anova` | `nong chart anova` | |
| `nong stats duncan` | `nong chart duncan` | |

---

## JSON 输出 schema

```json
{
  "Status": "ok",
  "Command": "word read",
  "Summary": "Extracted 142 paragraphs",
  "Data": { ... },
  "Issues": [],
  "Artifacts": { "output": "out.docx" },
  "Metrics": { "paragraphs": 142 },
  "Errors": [],
  "Meta": { "DurationMs": 42, "Version": "3.1.0" }
}
```

---

## 错误码

| 代码 | 名称 | 含义 |
|------|------|------|
| E001 | file_not_found | 文件不存在 |
| E002 | unsupported_format | 文件格式不支持 |
| E003 | missing_argument | 参数缺失 |
| E004 | internal_error | 内部错误 |
| E005 | dependency_missing | 依赖缺失 |
| E006 | validation_failed | 验证失败 |
| E007 | read_failed | 读取失败 |
| E008 | write_failed | 写入失败 |

---

## 验证结果

| 测试项 | 结果 |
|--------|------|
| `nong --version` | 3.1.0 |
| `nong commands` | 40 条命令列表 |
| `nong commands --json` | 结构化 JSON 输出 |
| `nong word preview`（空实现） | `[nong word preview] Not yet implemented.` |
| Release build | 0 错误 |

---

## 下一步：阶段 2

实现 `nong word read` + `nong word preview` 两个核心命令。
