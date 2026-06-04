# nong CLI 当前能力表 v3.2.1

日期：2026-06-04
源：`nong commands --json` + 实测
命令数：71 implemented（含诚实 E005/E009 命令）
测试：58 contract tests PASS

---

## 快速安装

```bash
dotnet tool install --global Angri450.Nong.Cli
nong commands --json       # 命令发现
nong word read file.docx   # 第一核心命令
```

---

## 已实现命令（71 个）

### word —— Word 文档引擎（30 个）

口径：29 个阶段 15 canonical Word leaf commands + 保留旧能力 `word extract`，所以 Word 实际为 30 个 implemented leaf commands。`word add-*` hyphen 入口保留兼容，文档和 `commands --json` 以 `word add ...` 为 canonical。

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong word read <file>` | 提取纯文本 | .docx | `nong word read paper.docx` |
| `nong word preview <file>` | 7 步诊断 | .docx | `nong word preview paper.docx` |
| `nong word fill <tmpl> <data> -o <f>` | 模板填充 | .docx + .json | `nong word fill t.docx d.json -o out.docx` |
| `nong word rebuild <file> -o <f>` | 样式清理 | .docx | `nong word rebuild dirty.docx -o clean.docx` |
| `nong word stats <file>` | 段落/表格/图片/脚注统计 | .docx | `nong word stats paper.docx --json` |
| `nong word fonts <file>` | 列出所有字体 | .docx | `nong word fonts paper.docx --json` |
| `nong word styles <file>` | 列出所有样式定义 | .docx | `nong word styles paper.docx --json` |
| `nong word validate <file>` | OOXML 校验 | .docx | `nong word validate paper.docx --json` |
| `nong word extract <file> -o <dir>` | 提取嵌入图片 | .docx | `nong word extract paper.docx -o imgs/ --json` |
| `nong word dissect <file> [-o <dir>]` | 格式指纹聚合；带 `-o` 时输出 nongmark/v1 一刀三流 | .docx | `nong word dissect paper.docx -o paper.slice --json` |
| `nong word merge <f1> <f2> ... -o <f>` | 合并多个 docx | .docx | `nong word merge a.docx b.docx -o merged.docx` |
| `nong word outline <file>` | 提取文档大纲 | .docx | `nong word outline paper.docx --json` |
| `nong word images <file> [-o <dir>]` | 列出/提取图片 | .docx | `nong word images paper.docx -o imgs/ --json` |
| `nong word comments <file>` | 读取批注 | .docx | `nong word comments paper.docx --json` |
| `nong word revisions <file>` | 列出修订记录 | .docx | `nong word revisions paper.docx --json` |
| `nong word infer-format <text>` | 从中文推断格式 | 文本 | `nong word infer-format "黑体 四号 居中" --json` |
| `nong word fix-order <file> -o <f>` | 修复 OOXML 元素顺序 | .docx | `nong word fix-order broken.docx -o fixed.docx` |
| `nong word protect <file> -o <f> [--mode] [-p]` | 文档保护 | .docx | `nong word protect paper.docx -o protected.docx --mode readonly` |
| `nong word embed-font <file> <font> -o <f> [--name]` | 嵌入字体 | .docx + .ttf | `nong word embed-font paper.docx font.ttf -o out.docx` |
| `nong word add paragraph <file> --spec <spec.json> -o <f> [--after]` | 追加段落 | .docx + JSON | `nong word add paragraph doc.docx --spec paragraph.json -o out.docx` |
| `nong word add table <file> --spec <spec.json> -o <f> [--after]` | 追加表格 | .docx + JSON | `nong word add table doc.docx --spec table.json -o out.docx` |
| `nong word add footnote <file> --text <t> -o <f> [--after]` | 追加脚注 | .docx | `nong word add footnote doc.docx --text "note" -o out.docx` |
| `nong word add endnote <file> --text <t> -o <f> [--after]` | 追加尾注 | .docx | `nong word add endnote doc.docx --text "note" -o out.docx` |
| `nong word add image <file> --src <img> [--caption] -o <f> [--after]` | 追加图片 | .docx + image | `nong word add image doc.docx --src img.png -o out.docx` |
| `nong word add toc <file> -o <f> [--title] [--after]` | 追加目录 | .docx | `nong word add toc doc.docx -o out.docx` |
| `nong word add xref <file> --to <bookmark> --text <display> -o <f> [--after]` | 追加交叉引用 | .docx | `nong word add xref doc.docx --to bm1 --text "see table" -o out.docx` |
| `nong word add link <file> --url <url> --text <display> -o <f> [--after]` | 追加超链接 | .docx | `nong word add link doc.docx --url https://example.com --text "link" -o out.docx` |
| `nong word add bookmark <file> --name <name> -o <f> [--after]` | 追加书签 | .docx | `nong word add bookmark doc.docx --name bm1 -o out.docx` |
| `nong word add comment <file> --text <t> [--author] -o <f> [--after]` | 追加批注 | .docx | `nong word add comment doc.docx --text "review" -o out.docx` |
| `nong word add math <file> --latex <f> -o <f> [--display] [--after]` | 追加公式 | .docx | `nong word add math doc.docx --latex "E=mc^2" -o out.docx` |

### inspect —— 论文诊断与写作（10 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong inspect diagnose <file>` | 完整论文诊断 | .txt | `nong inspect diagnose paper.txt --json` |
| `nong inspect refs <file>` | 参考文献检查 | .txt | `nong inspect refs paper.txt` |
| `nong inspect write-paper <spec> -o <f>` | 论文生成 | .json | `nong inspect write-paper spec.json -o paper.docx` |
| `nong inspect classify <file>` | 论文类型分类（16 型） | .txt | `nong inspect classify paper.txt --json` |
| `nong inspect structure <file>` | 提取论文结构 | .txt | `nong inspect structure paper.txt --json` |
| `nong inspect evidence <file>` | 证据链诊断 | .txt | `nong inspect evidence paper.txt --json` |
| `nong inspect data-req <file>` | 数据需求诊断 | .txt | `nong inspect data-req paper.txt --json` |
| `nong inspect gap <file>` | 缺口等级评估 | .txt | `nong inspect gap paper.txt --json` |
| `nong inspect varplan <file>` | 变量操作化方案 | .txt | `nong inspect varplan paper.txt --json` |
| `nong inspect semantics <file>` | 语义/逻辑风险诊断 | .txt | `nong inspect semantics paper.txt --json` |

### chart —— 统计与图表（7 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong chart analyze <data>` | ANOVA+Duncan+描述统计 | .json | `nong chart analyze groups.json` |
| `nong chart anova <data>` | 单因素方差分析 | .json | `nong chart anova groups.json --json` |
| `nong chart duncan <data> [--alpha]` | Duncan 多重比较 | .json | `nong chart duncan groups.json` |
| `nong chart bar <data> -o <png>` | 柱状图（误差棒+显著性字母） | .json | `nong chart bar groups.json -o fig.png --json` |
| `nong chart line <spec> -o <png>` | 多系列折线图 | .json | `nong chart line spec.json -o line.png --json` |
| `nong chart scatter <spec> -o <png>` | 散点图（可选 trendline） | .json | `nong chart scatter spec.json -o scatter.png --json` |
| `nong chart pie <spec> -o <png>` | 饼图 | .json | `nong chart pie spec.json -o pie.png --json` |

### excel —— Excel 数据入口（4 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong excel sheets <file>` | 列出 sheet | .xlsx | `nong excel sheets data.xlsx --json` |
| `nong excel read <file> [--sheet] [--range]` | 读取内容 | .xlsx | `nong excel read data.xlsx --json` |
| `nong excel to-groups <file> --group <col> --value <col> [--raw]` | Excel→分组数据 | .xlsx | `nong excel to-groups data.xlsx --group A --value B --raw` |
| `nong excel create <spec> -o <file>` | 从 JSON 创建 xlsx | .json | `nong excel create spec.json -o data.xlsx --json` |

### diagram —— 科学图表（3 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong diagram flowchart <spec> -o <png>` | 流程图 | .json | `nong diagram flowchart spec.json -o flow.png` |
| `nong diagram network <spec> -o <png>` | 网络图 | .json | `nong diagram network spec.json -o net.png` |
| `nong diagram tree <spec> -o <png>` | 系统发育树（Newick/JSON） | .nwk/.json | `nong diagram tree tree.nwk -o tree.png --json` |

### pptx —— 幻灯片读取（2 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong pptx read <file>` | 抽取全部 slide 文本 | .pptx | `nong pptx read slides.pptx --json` |
| `nong pptx slides <file>` | 按 slide 统计形状/元素 | .pptx | `nong pptx slides slides.pptx --json` |

### ocr —— 文字识别（7 个）

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong ocr cloud <file> -o <dir>` | PaddleOCR-VL 云端 OCR | image/pdf | `nong ocr cloud scan.png -o out/ --json` |
| `nong ocr local <file>` | 本地 PP-OCRv5 识别（E005/E009） | image | `nong ocr local test.png --json` |
| `nong ocr check-env` | 检查 OCR 环境状态 | 无 | `nong ocr check-env --json` |
| `nong ocr analyze-image <file> -o <dir>` | 图像结构分析（无需 token） | image | `nong ocr analyze-image scan.png -o out/ --json` |
| `nong ocr models` | 列出可用 OCR 模型 | 无 | `nong ocr models --json` |
| `nong ocr install-model <id>` | 安装 OCR 模型（pp-ocrv5-mobile 返回 E009） | model-id | `nong ocr install-model pp-ocrv5-mobile --json` |
| `nong ocr to-word <file> -o <docx> [--pages]` | 云端 OCR 转 Word 文档 | image/pdf | `nong ocr to-word scan.png -o out.docx --json` |

`ocr local` 返回 E005（模型未下载）或 E009（模型已下载但推理未实现）。`ocr install-model pp-ocrv5-mobile` 返回 E009（ONNX 模型尚未发布），均为诚实行为。

### genre / icons —— 模板与素材（4 个）

| 命令 | 功能 | 示例 |
|------|------|------|
| `nong genre list` | 列出格式模板 | `nong genre list --json` |
| `nong genre show <name>` | 查看模板内容 | `nong genre show degree-thesis` |
| `nong icons list` | 列出图标 | `nong icons list --json` |
| `nong icons search <q>` | 搜索图标 | `nong icons search "dna"` |

### skill —— Skill 生命周期（4 个）

| 命令 | 功能 | 示例 |
|------|------|------|
| `nong skill validate <dir>` | 验证 SKILL.md 结构和引用 | `nong skill validate ./word --json` |
| `nong skill scan <dir>` | 安全扫描 skill/插件目录 | `nong skill scan ./plugin --json` |
| `nong skill inventory <dir>` | 列出目录内容（单 skill + 插件根） | `nong skill inventory ./plugin --json` |
| `nong skill package <dir>` | validate+scan+打包 .zip | `nong skill package ./word --json` |

注意：请使用 `nong skill`，旧版 `skill-manager` global tool 已废弃。

---

## 核心工作流

### 1. Excel → 统计 → 图表
```bash
nong excel to-groups data.xlsx --group A --value B --raw > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
```
### 2. Word 生成 → 再读取
```bash
nong inspect write-paper spec.json -o paper.docx --json
nong word preview paper.docx --json
nong word read paper.docx --json
```
### 3. 论文诊断
```bash
nong word read paper.docx > paper.txt
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
```
### 4. 论文分步诊断（节省 token）
```bash
nong inspect classify paper.txt --json
nong inspect structure paper.txt --json
nong inspect evidence paper.txt --json
nong inspect gap paper.txt --json
```
### 5. 文档审计
```bash
nong word stats paper.docx --json
nong word fonts paper.docx --json
nong word dissect paper.docx --json
nong word dissect paper.docx -o paper.slice --json
```
### 6. 从零建 Excel
```bash
nong excel create spec.json -o data.xlsx --json
nong excel sheets data.xlsx --json
```
### 7. 图表生成（3 种新类型）
```bash
nong chart line line-spec.json -o line.png --json
nong chart scatter scatter-spec.json -o scatter.png --json
nong chart pie pie-spec.json -o pie.png --json
```
### 8. PPTX 文本抽取
```bash
nong pptx read slides.pptx --json
nong pptx slides slides.pptx --json
```
### 9. 文档扩写（add 系列）
```bash
nong word add paragraph doc.docx --spec paragraph.json -o out.docx
nong word add table doc.docx --spec table.json -o out.docx
nong word add image doc.docx --src chart.png --caption "Figure 1" -o out.docx
nong word add math doc.docx --latex "E=mc^2" --display -o out.docx
```
### 10. 云端 OCR
```bash
nong ocr check-env --json
nong ocr cloud scan.png -o ocr-out/ --json
nong ocr to-word scan.png -o out.docx --json
```
### 11. 图像分析
```bash
nong ocr analyze-image scan.png -o analysis/ --json
```
### 12. Skill 打包
```bash
nong skill validate ./word --json
nong skill scan ./plugin --json
nong skill package ./plugin --json
```

---

## JSON 输出 schema

每个命令 `--json` 返回统一结构：

```json
{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": { "png": "fig.png" },
  "metrics": { "paragraphs": 29 },
  "errors": [],
  "meta": { "durationMs": 42, "version": "3.2.0" }
}
```

## 错误码

| 代码 | 名称 | 含义 |
|------|------|------|
| E001 | file_not_found | file not found |
| E002 | unsupported_format | wrong extension |
| E003 | missing_argument | arg required |
| E004 | internal_error | unexpected crash |
| E005 | dependency_missing | tool/token not installed |
| E006 | validation_failed | bad input |
| E007 | read_failed | can't read |
| E008 | write_failed | can't write |
| E009 | not_implemented | command not done yet |

---

## 输入格式速查

| 格式 | 喂给哪些命令 |
|------|------------|
| .docx | word 全部 30 个命令 |
| .txt | inspect 全部 10 个命令 |
| .json (paper spec) | inspect write-paper |
| .json (groups: `{"A":[1,2],"B":[3,4]}`) | chart analyze / anova / duncan / bar |
| .json (chart spec) | chart line / scatter / pie |
| .json (diagram spec) | diagram flowchart / network / tree |
| .json (excel spec) | excel create |
| .json (paragraph spec) | word add paragraph |
| .json (table spec) | word add table |
| .xlsx | excel sheets / read / to-groups |
| .pptx | pptx read / slides |
| image/pdf | ocr cloud / to-word / analyze-image |
| model-id | ocr install-model |
| LaTeX | word add math |
| 目录 | skill validate / scan / inventory / package |
| 中文描述 | word infer-format |

---

## 关键约束

- Duncan — 使用简化 Q 值近似，正式论文需复核
- 生成命令 — 输出目录不存在会自动创建
- word rebuild — 输入输出不能同一路径
- word merge — 输入输出不能同一路径
- word fix-order / protect / embed-font / add-* — 输入输出不能同一路径
- excel to-groups — `--raw` 输出裸 JSON 用于管道给 chart 命令；`--json` 输出含完整 schema，`data` 直接为分组字典
- excel to-groups — sheet 名不存在返回 E006 而非崩溃；支持 AA/AB 等多字母列
- ocr cloud — 需 `PADDLEOCR_ACCESS_TOKEN` 环境变量（旧名 `PADDLEOCR_TOKEN` 已弃用）
- ocr local — 返回 E005（模型未下载）或 E009（推理未实现），均为诚实行为
- ocr install-model pp-ocrv5-mobile — 返回 E009，PP-OCRv5 ONNX 模型尚未发布
- ocr to-word — 需 `PADDLEOCR_ACCESS_TOKEN`，调用云端 OCR 后转换为 .docx
- skill package — 支持单 skill 目录和插件根目录两种模式
- skill scan — Critical/High 发现 → status:error + EXIT:1
- 旧 skill-manager global tool — 已废弃，使用 `nong skill` 代替
- 目标框架 net8.0，不要求 .NET 11 preview
- 所有错误消息不泄露 API token（如 "sk-"、"bearer" 模式）

## 测试

```bash
dotnet test Cli.Tests/Cli.Tests.csproj -c Release    # 58 contract tests
pwsh -File tests-output/stage13/verify.ps1            # 15 skill behavior tests
```
