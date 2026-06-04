# nong CLI 命令总表

日期：2026-06-02
状态：全部规划完成，待开发

---

## 全部 10 个包 → 109 个 CLI 命令

| # | 包名 | CLI 前缀 | 命令数 | 源文件覆盖率 | 规范文件 |
|---|------|---------|--------|-------------|---------|
| 1 | Docx | `nong word` | 28 | 15/15 | `cli-word-spec.md` |
| 2 | Inspect | `nong inspect` | 16 | 12/12 | `cli-inspect-spec.md` |
| 3 | Chart | `nong chart` | 24 | 7/7 | `cli-chart-spec.md` |
| 4 | Diagram | `nong diagram` | 5 | 11/11 | `cli-diagram-spec.md` |
| 5 | Excel | `nong excel` | 17 | 5/5 | `cli-excel-spec.md` |
| 6 | Pptx | `nong pptx` | 13 | 9/9 | `cli-pptx-spec.md` |
| 7 | MultiModal | `nong ocr` | 6 | 5/5 | `cli-ocr-spec.md` |
| 8 | Bioicons | `nong icons` | 4 | 1/1 | `cli-icons-genre-spec.md` |
| 9 | Genre | `nong genre` | 2 | 1/1 | 同上 |
| 10 | ThirdParty | — | 不暴露，内部使用 | 15 库合一 DLL | — |
| **合计** | | | **109** | **66/66** | |

---

## 按优先级分

### P0：立刻做（18 个，对 skill 有直接价值）

| 命令 | 包 | 说明 |
|------|-----|------|
| `word read` | Docx | 提取文本，终结 PowerShell |
| `word stats` | Docx | 文档统计 |
| `word preview` | Docx | 7 步诊断 |
| `word validate` | Docx | OOXML 验证 |
| `word dissect` | Docx | 格式指纹 |
| `inspect classify` | Inspect | 论文类型分类 |
| `inspect structure` | Inspect | 结构提取 |
| `inspect diagnose` | Inspect | 完整诊断 |
| `inspect refs` | Inspect | 参考文献检查 |
| `chart bar` | Chart | 柱状图 |
| `chart line` | Chart | 折线图 |
| `chart scatter` | Chart | 散点图 |
| `diagram flowchart` | Diagram | 流程图 |
| `excel read` | Excel | 读取 Excel |
| `excel sheets` | Excel | 列出 sheet |
| `excel create` | Excel | 创建文件 |
| `pptx read` | Pptx | 提取文本 |
| `ppt slides` | Pptx | 列出结构 |

### P1：第二版（22 个）

| 命令 | 包 |
|------|-----|
| `word fonts/styles/rebuild/fill` | Docx |
| `inspect varplan` / `write paper` / `evidence/data-req/gap/semantics` / `top-type` | Inspect |
| `chart pie/anova/duncan/analyze/combine` | Chart |
| `chart stock/bubble/heatmap` | Chart |
| `diagram tree` | Diagram |
| `excel preview/validate` | Excel |
| `pptx create/add slide/validate` | Pptx |
| `ocr local` / `check-env` | MultiModal |
| `icons list/search` | Bioicons |
| `genre list/show` | Genre |

### P2：第三版（69 个）

其余全部命令。

---

## 命令树

```
nong (109 commands)
├── word (28)
│   ├── 读: read, outline, stats, fonts, styles, images, comments, revisions
│   ├── 查: preview, validate, dissect, infer-format
│   ├── 改: rebuild, fix-order, fill, merge, protect, embed-font
│   └── 写: add paragraph/table/footnote/endnote/image/toc/xref/link/bookmark/comment/math
├── inspect (16)
│   ├── 分析: classify, top-type, structure
│   ├── 诊断: diagnose, evidence, data-req, gap, semantics
│   ├── 引文: refs, cite-check, refs resolve, refs generate
│   ├── 变量: varplan
│   └── 写作: write paper/official/letter
├── chart (24)
│   ├── 图表: bar, pie, donut, line, area, scatter, multi-scatter, box,
│   │        histogram, radar, stock, bubble, heatmap, gauge, coxcomb,
│   │        lollipop, population, function, error-bar
│   ├── 统计: anova, duncan, analyze
│   ├── 拼接: combine
│   └── 数据: 内部加载
├── diagram (5)
│   ├── flowchart, network, tree, dsl, icons
├── excel (17)
│   ├── 读: read, sheets
│   ├── 查: preview, validate, audit, eval
│   ├── 写: create, sort, protect
│   └── 高级: add pivot/picture/sparkline/filter/conditional/databar/colorscale/iconset
├── pptx (13)
│   ├── 读: read, slides, shape-map
│   ├── 查: validate
│   ├── 写: create, add title-slide/slide/table-slide/chart-slide
│   └── 改: raw list-parts/get-part/set-part
├── ocr (6)
│   ├── 本地: local, check-env
│   ├── 云端: cloud, to-word
│   └── 分析: analyze-image
├── icons (4)
│   └── list, search, show, export
└── genre (2)
    └── list, show
```
