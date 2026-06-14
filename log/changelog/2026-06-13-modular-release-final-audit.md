# 模块化发布路线图最终审计

日期: 2026-06-13
状态: GO

## 审计总览

| 维度 | 状态 |
|------|------|
| 架构 | PASS |
| 包身份 | PASS |
| 体积 | PASS |
| 命令合同 | PASS |
| native | PASS |
| 测试 | PASS |
| 文档 | PASS |
| 发布风险 | GO |

## 1. 架构 — PASS

主 CLI (Angri450.Nong.Cli, 12.04 MB) 仅做路由和轻功能:
- 内嵌: word / excel / genre / bioicons / literature / pandoc / inspect / skill
- 外部路由: chart / diagram / ocr / pdf / pptx → 独立 dotnet tool
- 子进程透传 stdout/stderr, `ProcessStartInfo.ArgumentList` 避免空格注入

## 2. 包身份 — PASS

| 工具命令 | PackageId | 旧 PackageId | 状态 |
|----------|----------|-------------|------|
| nong | Angri450.Nong.Cli | (不变) | ok |
| nong-chart | Angri450.Nong.Tool.Chart | Angri450.Nong.Chart | 已分离 |
| nong-diagram | Angri450.Nong.Tool.Diagram | Angri450.Nong.Diagram | 已分离 |
| nong-pdf | Angri450.Nong.Tool.Pdf | Angri450.Nong.Pdf | 已分离 |
| nong-pptx | Angri450.Nong.Tool.Pptx | Angri450.Nong.Pptx | 已分离 |
| nong-ocr | Angri450.Nong.Tool.Ocr | Angri450.Nong.MultiModal | 已分离 |
| nong-imaging | Angri450.Nong.Tool.Imaging | Angri450.Nong.Imaging | 已分离 |

核心库 ID (Angri450.Nong.Chart 等) 保留给未来库包发布，不再作为 tool 包发布。

## 3. 体积 — PASS

| 包 | 瘦身后 | 瘦身前 | 阈值 | 状态 |
|---|---|------:|------:|------:|------|
| Angri450.Nong.Cli | 12.04 MB | 12.04 MB | ≤15 | ok |
| Angri450.Nong.Tool.Chart | 25.87 MB | 83.38 MB | ≤50 | ok |
| Angri450.Nong.Tool.Diagram | 25.86 MB | 83.37 MB | ≤50 | ok |
| Angri450.Nong.Tool.Imaging | 26.20 MB | 83.70 MB | ≤50 | ok |
| Angri450.Nong.Tool.Pdf | 28.68 MB | 28.68 MB | ≤50 | ok |
| Angri450.Nong.Tool.Ocr | 12.14 MB | 12.14 MB | ≤20 | ok |
| Angri450.Nong.Tool.Pptx | 10.74 MB | 10.74 MB | ≤20 | ok |

瘦身策略: 仅打包 Windows (Win32) native assets，移除 macOS/Linux SkiaSharp/HarfBuzz。
- Chart: 83→26 MB (-69%)
- Diagram: 83→26 MB (-69%)
- Imaging: 84→26 MB (-69%)
- 总节省: ~172 MB

每包仍含 ThirdParty.dll (21.66 MB 未压缩)。ThirdParty 边界审计 (log/guidance/2026-06-13-thirdparty-boundary-audit.md) 结论: 暂不拆分。

## 4. 命令合同 — PASS

- 所有外部工具命令仍输出结构化 JSON
- PP-OCRv6 口径统一: `ocr local`/`ocr install-model` 命令描述和错误消息均使用 PP-OCRv6
- `ocr models` 同时列出 v5 和 v6 模型 (v5 保留向后兼容)
- 外部工具缺失时自动安装提示使用新 `Angri450.Nong.Tool.*` 包名

## 5. native — PASS

- PDFium: 多平台 native DLL 保留在 Pdf 包内 (28.68 MB 在阈值内)
- SkiaSharp/HarfBuzz: Windows-only 策略，非 Windows 平台文档提示
- OCR runtime: 外部 NuGet 包 (Angri450.Nong.OcrRuntime.*)，不在本仓库编译

## 6. 测试 — PASS

- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release`: 154 passed, 0 failed
- 本地安装冒烟:
  - `nong --version`: PASS
  - `nong ocr models --json`: PASS (子进程路由到 nong-ocr)
  - `nong chart --help`: PASS
  - `nong-chart --help`: PASS
  - `nong-pdf --help`: PASS
  - 全部 7 个工具通过 `dotnet tool install --tool-path` 本地安装

## 7. 文档 — PASS

产出文件:
- `log/changelog/2026-06-13-ppocrv6-modular-split.md` (阶段 0-1)
- `log/plans/2026-06-13-modular-release-to-audit-roadmap.md` (路线图)
- `log/guidance/2026-06-13-thirdparty-boundary-audit.md` (ThirdParty 审计)
- `log/changelog/2026-06-13-modular-release-final-audit.md` (本文件)
- `tools/pack-audit.ps1` (体积闸门脚本)

## 8. 发布风险 — GO

### 可发布

| 包 | 版本 | 验证 |
|---|------|------|
| Angri450.Nong.Cli | 4.1.0 | build + test + local install + smoke |
| Angri450.Nong.Tool.Chart | 4.1.0 | build + local install + smoke |
| Angri450.Nong.Tool.Diagram | 4.1.0 | build + local install + smoke |
| Angri450.Nong.Tool.Pdf | 4.1.0 | build + local install + smoke |
| Angri450.Nong.Tool.Pptx | 4.1.0 | build + local install + smoke |
| Angri450.Nong.Tool.Ocr | 4.1.0 | build + local install + smoke |
| Angri450.Nong.Tool.Imaging | 4.1.0 | build + local install + smoke |

### 暂缓发布

无。全部 7 包可发布。

### 已知限制

1. Chart/Diagram/Imaging 仅含 Windows native assets。Linux/macOS 用户需从源码构建。
2. ThirdParty.dll 21.66 MB 未拆分，每包携带完整 ThirdParty 代码。
3. 所有 package 版本号硬编码为 4.1.0，发版前需确认版本策略。

### 不处理

- 不直接推送 NuGet
- 不 push GitHub/Gitee/GitCode
- 不拆分 ThirdParty
- 不改变用户命令入口

## 审计结论

**GO** — 7 个包可发布为 4.1.0。体积闸门通过、测试通过、本地安装通过、命令合同稳定。

提交记录:
- `73eb44a` — 阶段 1: PackageId 重命名
- `287ef84` — 阶段 2-3: 体积闸门 + RID 瘦身 + OCR 文案修复
