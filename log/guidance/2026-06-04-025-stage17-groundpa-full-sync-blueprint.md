# 阶段 17 指导：GroundPA 全面同步 Nong 真实命令面

日期：2026-06-04
目标仓库：`C:\Users\Administrator\Documents\Github\GroundPA-Toolkit`
源头仓库：`C:\Users\Administrator\Documents\Github\Angri450.Nong`

本阶段不是最小修复。目标是把 GroundPA skill 层一次性同步到 Nong 当前真实 CLI 能力，清掉 2.0.0 时代的旧口径，避免 agent 再因为 stale skill 文档绕开 Nong、误报未实现、调用旧 token、或暴露半成品 OCR。

## 0. 当前事实锁定

本指导基于 2026-06-04 本地实测，不以历史 changelog 的营销口径为准。

已验证事实：

```text
Angri450.Nong git short hash: fe0b7ec
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
summary: 71 commands available
meta.version: 3.1.0
status: ok
GroundPA skill inventory: 17 skills found
GroundPA skill scan: 15 findings, 0 High+
GroundPA git status: clean before Stage 17 implementation
```

注意：

```text
1. 现有 Stage 16 release 日志有历史污染，出现过 68 commands / 24 tests / Stage15 未完成等旧事实。
2. Stage15b 已修复后，当前口径是 71 commands / 58 contract tests PASS。
3. 当前本地 nong.dll 报 meta.version = 3.1.0。不要在 GroundPA 文档硬写 3.2.0，除非发布包安装后也实测为 3.2.0。
4. 如果后续 NuGet/GitHub/Gitee 发布版本号升级到 3.2.0，最终 changelog 必须同时记录 package version、`nong commands --json` 的 `meta.version`、git hash。
```

阶段 17 的唯一信源：

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet build .\Cli\NongCli.csproj -c Release
```

不得把 `changelog/2026-06-04-022-overnight-stage16-release-result.md` 当成当前事实。

## 1. 阶段 17 定义

阶段 17 = GroundPA Toolkit 2.1.0 级别的 Nong command-surface full sync。

完成后的 GroundPA 必须满足：

```text
1. GroundPA 只做 skill 路由，不复刻 Nong 业务逻辑。
2. 所有 Nong-facing skill 都以 `nong commands --json` 的 implemented 命令为准。
3. 已 implemented 的命令不再被 skill 文档说成 not implemented。
4. E005/E009 能力可以被诚实描述，但不得包装成稳定可用工作流。
5. Word/NongMark 一刀三流是 Word skill 的主路径。
6. OCR token 只允许 `PADDLEOCR_ACCESS_TOKEN`，禁止 `--token` 和旧 `PADDLEOCR_TOKEN`。
7. 不允许 skill 层回退到 Python PaddleOCR、PowerShell 正则拆 docx、临时 .NET 项目绕 CLI。
8. plugin manifest、skills.sh、README、DEVELOP、SKILL.md、references 口径一致。
9. 最终验收不能是 49/50、部分通过、跳过若干项；所有必跑门禁必须通过或明确外部不可用。
```

## 2. 当前 GroundPA 审计发现

以下问题是阶段 17 必修，不是建议项。

### 2.1 Word skill 严重落后

当前 `GroundPA-Toolkit\word\SKILL.md` 只暴露：

```text
word read
word preview
word fill
word rebuild
```

并且仍说 image extraction、style listing、font listing、validation、merge、format fingerprinting 未实现。

这已经错误。当前 Nong Word 命令面包含 30 个 implemented leaf commands：

```text
word read
word preview
word fill
word rebuild
word extract
word dissect
word stats
word fonts
word styles
word validate
word merge
word outline
word images
word comments
word revisions
word infer-format
word fix-order
word protect
word embed-font
word add paragraph
word add table
word add footnote
word add endnote
word add image
word add toc
word add xref
word add link
word add bookmark
word add comment
word add math
```

必须把 `word dissect -o/--output` 写成 agent 处理 docx 的主路径：

```powershell
nong word dissect paper.docx --output paper.slice --json
```

输出目录要明确包含 NongMark 一刀三流的核心文件：

```text
document.json
content.jsonl
structure.json
format.json
assets/manifest.json
content.md
summary.json
```

`word add-*` 只能作为兼容 alias 提及，不得作为 canonical 示例。文档和 examples 必须使用 nested command：

```powershell
nong word add paragraph doc.docx --spec paragraph.json -o out.docx --json
nong word add table doc.docx --spec table.json -o out.docx --json
nong word add math doc.docx --latex "E=mc^2" -o out.docx --json
```

### 2.2 Inspect skill 严重自我限缩

当前 `inspect/SKILL.md` 只暴露：

```text
inspect diagnose
inspect refs
inspect write-paper
```

并且仍说 classify、structure、varplan、evidence、data-req、gap、semantics 未实现。

这已经错误。必须同步为 10 个 implemented 命令：

```text
inspect diagnose
inspect refs
inspect write-paper
inspect classify
inspect structure
inspect varplan
inspect evidence
inspect data-req
inspect gap
inspect semantics
```

`inspect` 仍然只处理论文语义、结构、证据链、参考文献、变量计划等 paper-level 工作；docx 读取先走 `word dissect` 或 `word read`。

### 2.3 Chart skill 未暴露已实现图表

当前 `chart/SKILL.md` 暴露 analyze/anova/duncan/bar，但说 line/scatter/pie 未实现。

这已经错误。必须同步为：

```text
chart analyze
chart bar
chart anova
chart duncan
chart line
chart scatter
chart pie
```

仍不得承诺 box、histogram、heatmap、radar、combined panels，除非 `nong commands --json` 中出现 implemented。

生成 PNG 后可以接入视觉验收：

```powershell
nong ocr analyze-image fig.png -o fig.analysis --json
```

说明必须准确：这是图像结构/空白/留白/内容框 QA，不是语义理解，也不是 OCR。

### 2.4 Excel skill 未暴露 create

当前 `excel/SKILL.md` 暴露 sheets/read/to-groups，并说 create/style/formula/dashboard/pivot 不可用。

当前 `excel create` 已 implemented，必须暴露：

```text
excel sheets
excel read
excel to-groups
excel create
```

边界仍要保留：

```text
不要承诺复杂公式、数据透视表、仪表盘、任意样式编辑，除非 spec 和 CLI 契约真实支持。
```

### 2.5 Diagram skill 未暴露 tree

当前 `diagram/SKILL.md` 暴露 flowchart/network，并说 tree/Newick 未实现。

这已经错误。必须同步为：

```text
diagram flowchart
diagram network
diagram tree
```

生成 PNG 后同样可以用 `ocr analyze-image` 做结构验收。

### 2.6 PPTX 已可恢复为有限 skill

当前 plugin 不暴露 `pptx`，`pptx/README.md` 仍说 PPTX 是 stub。

当前 Nong 已有：

```text
pptx read
pptx slides
```

必须创建或恢复：

```text
GroundPA-Toolkit\pptx\SKILL.md
```

只暴露读取能力：

```powershell
nong pptx read deck.pptx --json
nong pptx slides deck.pptx --json
```

禁止承诺 PPTX 生成、主题设计、图表嵌入、动画、版式编辑，除非后续 CLI 真实 implemented。

### 2.7 Multimodal/OCR 已不再是纯 stub，但必须诚实暴露

当前 `multimodal/README.md` 仍写 OCR commands are stubs。

当前 Nong OCR 命令：

```text
ocr cloud
ocr local
ocr check-env
ocr analyze-image
ocr models
ocr install-model
ocr to-word
```

必须创建或恢复：

```text
GroundPA-Toolkit\multimodal\SKILL.md
```

推荐暴露方式：

```powershell
nong ocr check-env --json
nong ocr analyze-image scan.png -o scan.analysis --json
nong ocr cloud scan.png -o ocr-out --json
nong ocr to-word scan.png -o out.docx --json
nong ocr models --json
nong ocr install-model pp-ocrv5-mobile --json
```

边界必须写清：

```text
1. `ocr cloud` 和 `ocr to-word` 需要 `PADDLEOCR_ACCESS_TOKEN` 环境变量。
2. skill 文档不得出现 `--token`。
3. skill 文档不得出现旧名 `PADDLEOCR_TOKEN`，除非是在迁移说明中明确说“旧名禁用/废弃”，并且不作为示例。
4. `ocr local` 是 implemented CLI 入口，但当前可能返回 E005 或 E009。除非 `ocr check-env` 和一次实际图片 smoke test EXIT:0，否则不得把本地 OCR 推荐为稳定识别路径。
5. `ocr install-model pp-ocrv5-mobile` 当前可能返回 E009，必须作为诚实限制，不得写成 PP-OCRv5 fully available。
6. 禁止让 GroundPA skill 调 Python PaddleOCR、pip install paddleocr、pythonExe、ad hoc cloud wrapper。
```

必须更新或删除以下旧参考口径：

```text
multimodal\references\ocr-cloud.md 里的 PADDLEOCR_TOKEN 示例
multimodal\references\ocr-local.md 里的 pip install paddlepaddle paddleocr / pythonExe / paddleocr.PaddleOCR 路线
```

### 2.8 README / DEVELOP / manifests 全部旧

当前 `README.md`、`README.zh-CN.md`、`DEVELOP.md` 仍写：

```text
PPTX and OCR are not exposed because current nong CLI marks those commands as stubs.
```

这已经错误。必须更新为当前命令面。

当前 `.claude-plugin/plugin.json` 和 `skills.sh.json` 版本是 `2.0.0`，且 skills 列表缺少：

```text
./pptx
./multimodal
```

阶段 17 建议版本：

```text
GroundPA Toolkit 2.1.0
```

如果维护者决定发 3.0.0，必须在 changelog 中解释破坏性变化。单纯同步 Nong implemented 命令面，优先用 2.1.0。

### 2.9 skill-manager 口径需要收口到 nong skill

当前 README 仍说 `Angri450.Nong.Skill.Manager` 是 lifecycle tool。

当前 Nong CLI 已实现：

```text
skill validate
skill scan
skill inventory
skill package
```

GroundPA 自身发布/验收路径必须优先写：

```powershell
nong skill inventory C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill scan C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill package C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
```

注意：`nong skill validate <plugin-root>` 当前会按单个 skill 目录处理，并因为根目录没有 `SKILL.md` 而失败。这不是 GroundPA 插件失败，而是 validate 子命令的使用边界。

正确做法是遍历 inventory 里的每个 skill path，再逐个 validate。

`skill-manager/SKILL.md` 可以保留为历史/meta skill，但必须避免让 GroundPA 发布流程优先依赖旧全局 `skill-manager`。如果继续保留外部 skill-manager 工作流，要明确它是 legacy/meta，不是 Nong-facing skill 验收主路径。

### 2.10 验收命令本身需要修正

旧的“直接跑 `nong skill validate . --json`”对 plugin root 是错的。

阶段 17 必须使用下面的验证策略：

```powershell
$nong = 'C:\Users\Administrator\Documents\Github\Angri450.Nong\Cli\bin\Release\net8.0\nong.dll'
$repo = 'C:\Users\Administrator\Documents\Github\GroundPA-Toolkit'

$inventory = dotnet $nong skill inventory $repo --json | ConvertFrom-Json
foreach ($skill in $inventory.data.skills) {
  dotnet $nong skill validate $skill.path --json
}

dotnet $nong skill scan $repo --json
dotnet $nong skill package $repo --json
```

`skill package` 会在插件根目录生成 `GroundPA-Toolkit.zip`。如果不是发布本次 zip，记录结果后删除或移动到明确的 release artifacts 目录，不得把临时 zip 混入提交。

## 3. 修复范围

允许并要求修改：

```text
GroundPA-Toolkit\word\SKILL.md
GroundPA-Toolkit\word\references\*.md
GroundPA-Toolkit\inspect\SKILL.md
GroundPA-Toolkit\excel\SKILL.md
GroundPA-Toolkit\chart\SKILL.md
GroundPA-Toolkit\diagram\SKILL.md
GroundPA-Toolkit\pptx\SKILL.md
GroundPA-Toolkit\pptx\README.md
GroundPA-Toolkit\pptx\references\*.md
GroundPA-Toolkit\multimodal\SKILL.md
GroundPA-Toolkit\multimodal\README.md
GroundPA-Toolkit\multimodal\references\*.md
GroundPA-Toolkit\skill-manager\SKILL.md
GroundPA-Toolkit\README.md
GroundPA-Toolkit\README.zh-CN.md
GroundPA-Toolkit\DEVELOP.md
GroundPA-Toolkit\skills.sh.json
GroundPA-Toolkit\.claude-plugin\plugin.json
GroundPA-Toolkit\.claude-plugin\marketplace.json
GroundPA-Toolkit\changelog\*.md
```

只在必要时修改脚本：

```text
chart\scripts\*.ps1
diagram\scripts\*.ps1
excel\scripts\*.ps1
word\scripts\*.ps1
pptx\scripts\*.ps1
```

禁止：

```text
1. 不要在 GroundPA 内实现 docx/xlsx/pptx/OCR 业务逻辑。
2. 不要新建临时 .NET 工程作为 skill 主路径。
3. 不要新增 Python OCR fallback。
4. 不要写入真实 token、API key、cookie、PAT。
5. 不要把 `word add-*` 写成 canonical。
6. 不要把 E005/E009 包装成成功。
7. 不要为了过 scan 删除历史 changelog，除非确认是本阶段新增问题。
8. 不要提交 `.zip`、`.nupkg`、`bin/`、`obj/`。
```

## 4. 多 agent 并行方案

必须采用 coordinator + parallel agents，避免重复踩同一批坑。

### Coordinator：事实锁和集成

职责：

```text
1. 在 Angri450.Nong 跑 build/test/commands，生成命令真相表。
2. 锁定 GroundPA 目标版本：默认 2.1.0。
3. 分配文件所有权，防止并行 agent 同时改 README/manifests。
4. 收集每个 agent 的修改摘要和验证输出。
5. 最终统一更新 README、DEVELOP、manifests、changelog。
6. 最后跑完整验收，不允许“部分通过”收尾。
```

Coordinator 先执行：

```powershell
cd C:\Users\Administrator\Documents\Github\Angri450.Nong
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json

cd C:\Users\Administrator\Documents\Github\GroundPA-Toolkit
git status --short
```

### Agent A：Word / NongMark

文件：

```text
word\SKILL.md
word\references\api-reference.md
word\references\read-word.md
word\references\write-word.md
word\references\paper-analysis.md
word\references\workspace-setup.md
```

必须完成：

```text
1. Word skill 暴露 30 个当前 implemented Word commands。
2. `word dissect --output` 作为复杂 docx 的首选读取路径。
3. 说明 document.json/content.jsonl/structure.json/format.json/assets/manifest.json 分工。
4. add 系列全部改为 `word add paragraph/table/...`。
5. 删除或改写 PowerShell raw XML regex fallback。
6. 写明 `status: error` 和 E003/E006 等错误处理。
```

Word smoke commands：

```powershell
nong word dissect tests-output\05-docx\docx-basic.docx --output tests-output\stage17-word-slice --json
nong word add paragraph tests-output\05-docx\docx-basic.docx --spec tests-output\paragraph.json -o tests-output\stage17-add-paragraph.docx --json
nong word add math tests-output\05-docx\docx-basic.docx --latex "E=mc^2" -o tests-output\stage17-add-math.docx --json
```

### Agent B：Inspect / Genre / PPTX

文件：

```text
inspect\SKILL.md
genre\SKILL.md
pptx\SKILL.md
pptx\README.md
pptx\references\*.md
```

必须完成：

```text
1. Inspect 暴露 10 个 implemented commands。
2. Genre 保持 template discovery，不抢 inspect/write-paper 职责。
3. 新增或恢复 PPTX skill，只暴露 `pptx read` 和 `pptx slides`。
4. 移除 “PPTX is stub / do not install” 的旧口径。
5. 不承诺 PPTX 生成和版式编辑。
```

PPTX smoke commands：

```powershell
nong pptx read <sample.pptx> --json
nong pptx slides <sample.pptx> --json
```

如果仓库没有 sample.pptx，记录“fixture unavailable”，但 SKILL.md 仍必须按 `nong commands --json` 更新。

### Agent C：Excel / Chart / Diagram / Visual QA

文件：

```text
excel\SKILL.md
excel\references\*.md
chart\SKILL.md
chart\references\*.md
diagram\SKILL.md
diagram\references\*.md
```

必须完成：

```text
1. Excel 暴露 `excel create`。
2. Chart 暴露 line/scatter/pie。
3. Diagram 暴露 tree/Newick。
4. Chart/Diagram 生成 PNG 后加入可选 `ocr analyze-image` QA。
5. 不承诺未出现在 commands --json 中的图表类型和编辑能力。
```

示例：

```powershell
nong excel create spec.json -o out.xlsx --json
nong chart line line.json -o line.png --json
nong chart scatter scatter.json -o scatter.png --json
nong chart pie pie.json -o pie.png --json
nong diagram tree tree.nwk -o tree.png --json
nong ocr analyze-image line.png -o line.analysis --json
```

### Agent D：Multimodal / OCR

文件：

```text
multimodal\SKILL.md
multimodal\README.md
multimodal\references\image-analyzer.md
multimodal\references\ocr-cloud.md
multimodal\references\ocr-local.md
```

必须完成：

```text
1. 新增或恢复 multimodal skill。
2. 暴露 check-env/analyze-image/cloud/to-word/models/install-model。
3. `ocr local` 只作为 gated local path，默认不承诺成功。
4. 全部 token 示例改成 `PADDLEOCR_ACCESS_TOKEN`。
5. 删除 `--token` 示例。
6. 删除 Python PaddleOCR fallback 作为 GroundPA 路径。
7. 明确 analyze-image 不是 OCR。
```

OCR smoke commands：

```powershell
nong ocr check-env --json
nong ocr analyze-image <sample.png> -o ocr-analysis --json
nong ocr models --json
```

Cloud 只在环境变量存在时跑：

```powershell
nong ocr cloud <sample.png> -o ocr-cloud-out --json
nong ocr to-word <sample.png> -o ocr.docx --json
```

如果没有 `PADDLEOCR_ACCESS_TOKEN`，记录：

```text
cloud smoke skipped: PADDLEOCR_ACCESS_TOKEN unavailable
```

不得伪造成功。

### Agent E：Registry / README / skill-manager

文件：

```text
README.md
README.zh-CN.md
DEVELOP.md
skills.sh.json
.claude-plugin\plugin.json
.claude-plugin\marketplace.json
skill-manager\SKILL.md
```

必须完成：

```text
1. 版本更新到 2.1.0，除非 coordinator 明确选择其他版本。
2. plugin skills 加入 `./pptx` 和 `./multimodal`。
3. skills.sh 增加 pptx/multimodal。
4. README command tables 更新到当前 implemented surface。
5. 删除 “PPTX/OCR still stubs” 旧口径。
6. 生命周期工具优先写 `nong skill ...`。
7. 如果保留 `Angri450.Nong.Skill.Manager`，必须作为 legacy/meta 补充，不得作为 GroundPA 发布主路径。
```

### Agent F：Validation / Security / Changelog

文件：

```text
changelog\2026-06-04-stage17-groundpa-sync-nong-command-surface.md
```

必须完成：

```text
1. 跑完整 stale string scan。
2. 跑 skill inventory/validate/scan/package。
3. 跑 claude plugin validate，如果本机有 claude CLI。
4. 清理 package 生成的临时 zip，除非本次明确要发布 zip。
5. 写结果日志，包含所有命令、结果、剩余限制。
```

## 5. 命令映射总表

GroundPA README 和每个 SKILL.md 必须与下表一致。

### Word

```text
read: read, preview, dissect, stats, fonts, styles, outline, images, comments, revisions
validate/repair: validate, infer-format, fix-order, rebuild
generate/edit: fill, merge, protect, embed-font, extract
add: add paragraph/table/footnote/endnote/image/toc/xref/link/bookmark/comment/math
```

Canonical examples：

```powershell
nong word read paper.docx --json
nong word preview paper.docx --json
nong word dissect paper.docx --output paper.slice --json
nong word outline paper.docx --json
nong word images paper.docx --json
nong word validate paper.docx --json
nong word add paragraph paper.docx --spec paragraph.json -o out.docx --json
nong word add table paper.docx --spec table.json -o out.docx --json
nong word add image paper.docx --src fig.png --caption "Figure 1" -o out.docx --json
nong word add math paper.docx --latex "E=mc^2" --display -o out.docx --json
```

### Inspect

```powershell
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
nong inspect write-paper spec.json -o paper.docx --json
nong inspect classify paper.txt --json
nong inspect structure paper.txt --json
nong inspect varplan paper.txt --json
nong inspect evidence paper.txt --json
nong inspect data-req paper.txt --json
nong inspect gap paper.txt --json
nong inspect semantics paper.txt --json
```

### Excel

```powershell
nong excel sheets data.xlsx --json
nong excel read data.xlsx --json
nong excel read data.xlsx --sheet Sheet1 --range A1:D20 --json
nong excel to-groups data.xlsx --group Treatment --value Yield --raw
nong excel create spec.json -o out.xlsx --json
```

### Chart

```powershell
nong chart analyze groups.json --json
nong chart anova groups.json --json
nong chart duncan groups.json --alpha 0.05 --json
nong chart bar groups.json -o fig.png --json
nong chart line line.json -o line.png --json
nong chart scatter scatter.json -o scatter.png --json
nong chart pie pie.json -o pie.png --json
```

### Diagram

```powershell
nong diagram flowchart flow.json -o flow.png --json
nong diagram network network.json -o network.png --json
nong diagram tree tree.nwk -o tree.png --json
```

### PPTX

```powershell
nong pptx read deck.pptx --json
nong pptx slides deck.pptx --json
```

### OCR / Multimodal

```powershell
nong ocr check-env --json
nong ocr analyze-image scan.png -o scan.analysis --json
nong ocr cloud scan.png -o ocr-out --json
nong ocr to-word scan.png -o out.docx --json
nong ocr models --json
nong ocr install-model pp-ocrv5-mobile --json
nong ocr local scan.png --json
```

Boundary text that must appear in multimodal docs:

```text
`ocr cloud` and `ocr to-word` require PADDLEOCR_ACCESS_TOKEN.
`ocr analyze-image` performs structural image QA and does not recognize text.
`ocr local` may return E005/E009 unless the local PP-OCRv5 path is installed and verified.
```

### Skill lifecycle

```powershell
nong skill inventory C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill scan C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill package C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
nong skill validate C:\Users\Administrator\Documents\Github\GroundPA-Toolkit\word --json
```

## 6. Stale string gates

完成修改后，在 GroundPA 根目录跑：

```powershell
cd C:\Users\Administrator\Documents\Github\GroundPA-Toolkit

rg -n "not implemented in the current `nong` CLI|not implemented in the current nong CLI|not exposed in 2\.0\.0|commands as stubs|still.*stub|仍是 stub|暂不暴露|PPTX and OCR are not exposed|PPTX 和 OCR|PADDLEOCR_TOKEN|--token|pip install paddlepaddle|pip install paddleocr|paddleocr\.PaddleOCR|pythonExe|Python script calls|PowerShell fallback|Regex parsing of raw XML|word add-[a-z]" .
```

允许保留的命中必须写进 changelog allowlist，并说明为什么不是当前 Nong-facing 文档错误。

再跑 secret scan：

```powershell
rg -n "sk-[A-Za-z0-9_-]{20,}|github_pat_|ghp_|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\." .
```

要求：

```text
0 secret hits
0 当前 Nong-facing stale hits
```

历史 changelog 或 skill-manager 研究材料中的命中如果保留，必须是非触发路径且有 allowlist。

## 7. 验收门禁

### 7.1 Nong 源头门禁

在 Angri450.Nong：

```powershell
cd C:\Users\Administrator\Documents\Github\Angri450.Nong
dotnet build .\Cli\NongCli.csproj -c Release
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

期望：

```text
build: 0 errors
tests: 58/58 PASS, 0 SKIP
commands: 71 implemented
```

如果实际命令数变化，不要硬改成 71；必须解释新增/删除原因，并以当前 `commands --json` 为准更新 GroundPA。

### 7.2 GroundPA skill 门禁

在 GroundPA：

```powershell
cd C:\Users\Administrator\Documents\Github\GroundPA-Toolkit
git status --short

$nong = 'C:\Users\Administrator\Documents\Github\Angri450.Nong\Cli\bin\Release\net8.0\nong.dll'
$repo = 'C:\Users\Administrator\Documents\Github\GroundPA-Toolkit'
$inventory = dotnet $nong skill inventory $repo --json | ConvertFrom-Json
$inventory.data.skillCount
foreach ($skill in $inventory.data.skills) {
  dotnet $nong skill validate $skill.path --json
}
dotnet $nong skill scan $repo --json
dotnet $nong skill package $repo --json
```

期望：

```text
inventory: 19 skills after adding pptx + multimodal
validate: every skill valid
scan: 0 High+
package: packageType plugin, skillCount 19
```

如果 scan 仍有 Medium `HOME_PATH_REFERENCE` 等历史命中，可以不阻断，但必须：

```text
1. 确认 0 High/Critical。
2. 确认不是本阶段新增。
3. 在 changelog 写 allowlist。
```

### 7.3 Claude plugin 门禁

如果本机有 `claude` CLI：

```powershell
claude plugin validate .
```

如果没有：

```text
claude plugin validate: unavailable on this machine
```

不得写成 passed。

### 7.4 Smoke gates

至少跑这些不依赖外部 token 的 smoke：

```powershell
nong commands --json
nong word dissect <sample.docx> --output <slice-dir> --json
nong word add paragraph <sample.docx> --spec <paragraph.json> -o <out.docx> --json
nong inspect classify <sample.txt> --json
nong excel create <spec.json> -o <out.xlsx> --json
nong chart line <line.json> -o <line.png> --json
nong chart scatter <scatter.json> -o <scatter.png> --json
nong chart pie <pie.json> -o <pie.png> --json
nong diagram tree <tree.nwk> -o <tree.png> --json
nong ocr check-env --json
nong ocr analyze-image <line.png> -o <line.analysis> --json
nong ocr models --json
```

有 token 才跑：

```powershell
nong ocr cloud <sample.png> -o <ocr-out> --json
nong ocr to-word <sample.png> -o <ocr.docx> --json
```

有 pptx fixture 才跑：

```powershell
nong pptx read <sample.pptx> --json
nong pptx slides <sample.pptx> --json
```

没有 fixture 不能伪造，写 fixture unavailable。

## 8. 最终 changelog 要求

在 GroundPA 写：

```text
GroundPA-Toolkit\changelog\2026-06-04-stage17-groundpa-sync-nong-command-surface.md
```

必须包含：

```text
1. Nong source:
   - git hash
   - package version if global tool tested
   - `nong commands --json` meta.version
   - command count
   - build/test result

2. GroundPA version:
   - old version
   - new version
   - plugin manifest version
   - skills.sh version

3. Skills changed:
   - word
   - inspect
   - excel
   - chart
   - diagram
   - pptx
   - multimodal
   - skill-manager
   - README/DEVELOP/manifests

4. Commands exposed:
   - exact command list by group

5. Commands intentionally not exposed or gated:
   - OCR local if E005/E009
   - cloud/to-word if token unavailable
   - PPTX generation
   - unsupported chart/Excel/Word operations

6. Validation:
   - inventory result
   - per-skill validate result
   - scan result
   - package result
   - claude plugin validate result or unavailable
   - stale string scan result
   - secret scan result

7. Artifacts:
   - package zip path if retained
   - note if zip removed after validation

8. Known limitations:
   - only true external limitations, not fixable stale docs
```

## 9. Definition of Done

阶段 17 完成标准：

```text
1. GroundPA no longer claims current Nong implemented commands are unavailable.
2. `pptx/SKILL.md` exists and is registered if `pptx read/slides` remain implemented.
3. `multimodal/SKILL.md` exists and is registered if OCR commands remain implemented.
4. README.md / README.zh-CN.md / DEVELOP.md / plugin.json / skills.sh.json agree on skill list and version.
5. Word skill uses `word dissect --output` and nested `word add ...` canonical examples.
6. OCR docs use `PADDLEOCR_ACCESS_TOKEN`, no `--token`, no Python PaddleOCR fallback path.
7. `nong skill inventory` shows expected skill count.
8. Every skill path validates individually.
9. `nong skill scan` has 0 High/Critical.
10. `nong skill package` succeeds.
11. Stale string scan has no unreviewed current-doc hits.
12. Secret scan has 0 hits.
13. GroundPA changelog exists and records exact command outputs.
14. No generated `.zip` remains in git status unless intentionally release-tracked.
```

不能接受的收尾话术：

```text
基本完成，只差一个测试。
49/50 pass。
README 后续再同步。
OCR 暂时先写可用。
PPTX 后续再看。
validate root failed but package passed so算了。
```

本阶段要么全量同步并过门禁，要么明确阻塞在外部条件上；不要用“已完成”掩盖 stale 文档和错配 manifest。
