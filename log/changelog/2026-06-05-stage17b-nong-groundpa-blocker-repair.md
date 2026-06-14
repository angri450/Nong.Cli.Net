# 2026-06-05 Stage17b Nong/GroundPA 阻断问题修复

> Superseded on 2026-06-05 by Stage17c pure .NET OCR. The Python/PaddleOCR bridge notes in this file are historical and are no longer the current implementation. Current local OCR uses `pp-ocrv5-dotnet-sdcb`, `localDotNetPpOcrV5`, and `nong ocr install-model pp-ocrv5-mobile` for current-platform first-party `Angri450.Nong.OcrRuntime.*` native runtime deployment.

## 用户反馈

这次反馈集中在 10 个真实使用阻断点：Plugin Marketplace 只安装 skills 不安装 `nong` CLI；.NET 10 运行时无法运行 net8 工具；VML 公式图片在 Word dissect 中静默丢失；`content.jsonl` 的 `blockId` 为空导致 `word add --after` 不可用；`word images` 遇到 VML 相对 URI 抛 E004；`ocr install-model` 是 E009 空桩；云端 OCR token 来源不清楚；缺少 `.doc -> .docx` 边界转换；缺少文档级预检命令；SKILL.md 错误恢复指引不足。

## Nong CLI 修复

- CLI 升到 `3.2.2`，`JsonOutput` 和错误分支统一使用 `CliVersion.Current`，清掉 `3.1.0` 硬编码。
- `NongCli.csproj` 写入 `RollForward=LatestMajor`，解决只有 .NET 9/10 运行时的机器无法运行 net8 CLI 的问题。
- 新增 `nong word check <file.doc|file.docx> --json`：提前报告 `.doc` 转换需求、docx 段落/表格/图片统计、VML 图片风险、blockId 可用性和下一步建议。
- 新增 `nong word convert <file.doc|file.docx> -o <out.docx> --json`：`.docx` 直接复制到目标；`.doc` 优先使用 LibreOffice，Windows 上可回退到隐藏 Word COM 作为边界转换器。
- `word dissect` 对 VML `<w:pict>/<v:imagedata>` 不再静默空行：输出 image block、asset、Markdown 图片标记和 warning。
- `word images` 同时扫描 DrawingML 和 VML 图片，并用 raw URI 拆分避免 “relative URI” 内部异常；无 relationship id 的 VML 引用也会作为 `extractable=false` 的 image reference 返回。
- `content.jsonl` 每个块追加 `blockId` 和 `index`，使 `word add paragraph --after <blockId>` 的规范路径可用。
- `ocr local` 改为真实调用本地 Python/PaddleOCR bridge，依赖缺失返回 E005。
- `ocr install-model pp-ocrv5-mobile` 不再返回 E009 空桩；支持 `--dry-run`，非 dry-run 调用 `python -m pip install -U paddlepaddle paddleocr`。

## 文档同步

- `README.md` / `README.zh-CN.md` / `CAPABILITY.md` / `release-checklist.md` 从 71/30/58 更新到 73/32/67。
- OCR 文案从 “E005/E009 诚实空桩” 改为 “本地 PaddleOCR bridge + E005 依赖缺失 + install-model dry-run/安装计划”。
- `Cli/README.md` 不再提示旧的 `nong paper diagnose`，改为 `nong inspect diagnose`，并补充 `word check/convert` 和 roll-forward 说明。
- `Cli/AGENT.md` 补充 AI Studio AccessToken 页面、`localPythonPaddleOcr` 环境字段、`3.2.2` JSON 示例和 E009 恢复路径。

## 验证

- `dotnet test .\Nong.Cli.Net\Cli.Tests\Cli.Tests.csproj -c Release --nologo`：68/68 PASS。
- `nong commands --json`：`status=ok; version=3.2.2; commandCount=73`。
- `nong ocr install-model pp-ocrv5-mobile --dry-run --json`：`status=ok`，输出本地 PaddleOCR 安装计划。
- `nong --version`：`3.2.2+fe3cfc330e623296286649dd145371feed728c97`。
- 真实合同 `.doc`：`word check` 提示 legacy `.doc` 需转换；`word convert` 通过 `word-com` 输出 `.docx`；转换后 `word check` 报 38 段、3 表、3 个 VML 引用；`word images` 返回 3 个 `source=vml`、`extractable=false` 的 image references。
- Nong.Toolkit.Net：9 个 Nong-facing skill validate 均 0 error/0 warning；`nong skill scan` 为 0 findings；`claude plugin validate` 通过。

## 未执行

- 未执行非 dry-run 的 `ocr install-model`，避免在验收中强制联网安装 PaddleOCR。
- 未推送 NuGet/GitHub/Gitee；当前是代码与文档修复完成，发布仍需按 `release-checklist.md` 执行。
