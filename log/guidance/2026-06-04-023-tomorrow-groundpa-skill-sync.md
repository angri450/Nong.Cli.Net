# 2026-06-04 明早指导：GroundPA Skill 全面同步 Nong 3.2.0

本文件给明早的 skill 管理 agent 使用。今晚 ClaudeCode 可能会完成 Stage 15 + Stage 16a 并发布 `Angri450.Nong.Cli 3.2.0`。明早先审计 Nong，再更新 GroundPA。

## 0. 先验条件

必须先确认：

```text
Angri450.Nong 已有最终结果日志：
changelog/2026-06-04-022-overnight-stage16-release-result.md

GitHub/Gitee/NuGet 发布状态明确。
nong CLI 真实可运行。
```

如果没有最终结果日志，或 build/test/发布失败，不要更新 GroundPA。

## 1. GroundPA 的定位

GroundPA 是 skill 层：

```text
只路由用户意图。
只调用 nong implemented 命令。
不复刻 Nong 业务逻辑。
不暴露 stub。
不把 E005 依赖缺失能力包装成“已可用”。
```

## 2. 明早审计顺序

先在 `Angri450.Nong` 跑：

```powershell
dotnet tool uninstall --global Angri450.Nong.Cli
dotnet tool install --global Angri450.Nong.Cli --version 3.2.0
nong commands --json
nong commands --all --json
nong word dissect <sample.docx> -o <out-dir> --json
nong ocr check-env --json
nong ocr analyze-image <sample.png> -o <out-dir> --json
```

记录：

```text
implemented 命令清单
E005 命令清单
E009 命令清单
Word 新命令清单
OCR 新命令清单
```

## 3. GroundPA 更新范围

允许更新：

```text
Nong.Toolkit.Net\word\SKILL.md
Nong.Toolkit.Net\word\references\*.md
Nong.Toolkit.Net\multimodal\README.md
Nong.Toolkit.Net\multimodal\SKILL.md   # 只有 OCR 命令真实 implemented 且适合暴露时
Nong.Toolkit.Net\chart\SKILL.md        # 若接入 ImageAnalyzer 作为生成后验收
Nong.Toolkit.Net\diagram\SKILL.md      # 若接入 ImageAnalyzer 作为生成后验收
Nong.Toolkit.Net\README.md
Nong.Toolkit.Net\README.zh-CN.md
Nong.Toolkit.Net\DEVELOP.md
Nong.Toolkit.Net\skills.sh.json
Nong.Toolkit.Net\.claude-plugin\plugin.json
```

禁止：

```text
不要写 PowerShell 正则解析 docx 的 fallback。
不要绕过 nong 调用 Python OCR。
不要暴露 PP-OCRv5 local 为可用，除非 `nong ocr local` 已真实识别文本并 EXIT:0。
不要在 skill 中写 token。
不要让 skill 使用 --token。
```

## 4. Word Skill 更新重点

如果 Stage 15 完成，Word skill 应改为优先使用：

```powershell
nong word dissect <file.docx> -o <slice-dir> --json
```

说明：

```text
content.md 只是 preview。
document.json 是 nongmark/v1 canonical。
structure.json / format.json / assets/manifest.json 是 agent 低 token 决策依据。
```

Word skill 必须加入：

```text
读：read / outline / images / comments / revisions
查：preview / validate / dissect / infer-format
改：rebuild / fix-order / merge / protect / embed-font
写：add paragraph/table/footnote/endnote/image/toc/xref/link/bookmark/comment/math
```

但只写 `nong commands --json` 中真实 implemented 的命令。

## 5. OCR / Multimodal Skill 更新重点

如果 Stage 16a 完成，multimodal skill 可以恢复为“有限暴露”：

```powershell
nong ocr analyze-image <image> -o <dir> --json
nong ocr check-env --json
nong ocr cloud <file> -o <dir> --json    # 需要 PADDLEOCR_ACCESS_TOKEN
```

边界：

```text
ImageAnalyzer 是通用视觉验收工具，不是 OCR。
ocr local 如果仍返回 E005，不要作为可用 OCR 命令推荐。
PP-OCRv5 是 planned，不能写成已可用。
cloud 只支持 PaddleOCR-VL-1.6，不提供模型选择。
token 使用 PADDLEOCR_ACCESS_TOKEN，兼容 PADDLEOCR_TOKEN 只是迁移期。
```

## 6. Chart/Diagram 视觉验收

如果 `nong ocr analyze-image` 已实现，chart/diagram skill 可以在生成 PNG 后建议或自动调用：

```powershell
nong ocr analyze-image fig.png -o fig.analysis --json
```

用途：

```text
检测空白图、留白过大、内容偏移、渲染失败。
```

不要把它说成图像语义理解或 OCR。

## 7. 插件清单同步

更新：

```text
skills.sh.json
.claude-plugin/plugin.json
README.md
README.zh-CN.md
DEVELOP.md
```

原则：

```text
新增 skill 目录必须真实存在 SKILL.md。
plugin manifest 声明必须和目录一致。
不要把未恢复的 multimodal skill 写进 manifest。
```

## 8. 验收

GroundPA 更新后跑：

```powershell
cd C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net
claude plugin validate .
nong skill validate . --json
nong skill inventory . --json
nong skill package . --json
```

再抽查：

```powershell
rg -n "stub|not implemented|PP-OCRv5.*可用|PADDLEOCR_TOKEN|--token|PowerShell.*docx|python.*paddleocr" .
```

## 9. 结果日志

写：

```text
Nong.Toolkit.Net\changelog\2026-06-05-groundpa-sync-nong-3.2.0.md
```

必须包含：

```text
Nong version:
Nong command count:
Skills updated:
Commands exposed:
Commands intentionally not exposed:
Validation commands run:
Plugin package result:
Known limitations:
Next prompt-agent update notes:
```
