# nong word CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong word` 是 Word 引擎层（Angri450.Nong.Docx）的 CLI 入口。覆盖读、查、改、写四类操作，共 28 个命令。数学公式已融入各类别。

底层实现：调用 DocxCore 命名空间中的 DocumentWriter、StyleBuilder、StyleRebuilder、ImageEmbedder、TemplateEngine、AdvancedFeatures、WordPreview、ElementOrder、TocBuilder、MathRenderer 等类。

所有命令支持 `--json` 输出结构化结果。非 `--json` 时输出人类可读文本。

---

## 命令清单（28 个）

### 一、读（8 个）

#### `nong word read <file>`
提取文档纯文本内容。

输入：.docx 文件路径
输出：文本内容（段落+表格+脚注+尾注），一行一段

实现：TemplateEngine 读取 docx → 遍历所有段落/表格/脚注 → 拼接文本

#### `nong word outline <file>`
提取文档结构（标题层级）。

输入：.docx 文件路径
输出：
```
1  引言 ................... Heading1
1.1  研究背景 ............ Heading2
1.2  研究目的 ............ Heading2
2  材料与方法 ............. Heading1
```

实现：读取 styles.xml 识别 Heading1/Heading2/Heading3 → 按出现顺序输出

#### `nong word stats <file>`
文档统计信息。

输入：.docx 文件路径
输出：
```
段落: 142
表格: 8
图片: 12
脚注: 3
尾注: 0
页数: 15（估算）
字数: 约 8500
```

实现：WordPreview.Preview() 的 Statistics 部分

#### `nong word fonts <file>`
列出文档中所有使用的字体。

输入：.docx 文件路径
输出：
```
中文字体: 宋体, 黑体, 仿宋
拉丁字体: Times New Roman, Arial
未嵌入字体: 仿宋
```

实现：WordPreview 步骤 6 字体信息提取

#### `nong word styles <file>`
列出所有样式定义。

输入：.docx 文件路径
输出：
```
Normal      — 宋体 10.5pt 两端对齐 首行缩进420
Heading1    — 黑体 16pt 左对齐 加粗
Title       — 黑体 14pt 居中
...
```

实现：读取 styles.xml → 解析 Style 元素 → 提取 font/size/alignment/indent

#### `nong word images <file>`
列出嵌入图片及尺寸。

输入：.docx 文件路径
输出：
```
image1.png  — 1200×800  段落 3
image2.jpeg — 800×600   段落 7
```

实现：ImageHeaderReader 读取图片尺寸 + 遍历文档找图片引用位置

#### `nong word comments <file>`
列出所有批注。

输入：.docx 文件路径
输出：
```
[作者: 张三]  第 3 段："此处数据需核实"
[作者: 李四]  第 7 段："建议补充参考文献"
```

实现：AdvancedFeatures 批注读取

#### `nong word revisions <file>`
列出所有修订记录。

输入：.docx 文件路径
输出：
```
第 2 段  插入: "经实验验证"
第 5 段  删除: "初步结果表明"
```

实现：AdvancedFeatures 修订读取

---

### 二、查（4 个）

#### `nong word preview <file>`
7 步文件结构诊断。

输入：.docx 文件路径
输出：诊断报告（错误/警告/信息/统计）

实现：WordPreview.Preview() 完整流程

#### `nong word validate <file>`
OOXML Schema 合规性验证。

输入：.docx 文件路径
输出：Schema 错误列表

实现：OpenXmlValidator 验证

#### `nong word dissect <file>`
提取文档格式指纹 → JSON。

输入：.docx 文件路径
输出：format.json（样式定义+页面布局+字体+表格边框+引用格式）

实现：TemplateEngine.Analyze() 完整分析

#### `nong word infer-format <text>`
中文排版描述 → OpenXML 参数。

输入：中文排版描述文本，如 "黑体，四号，居中，固定行距 28 磅"
输出：
```json
{
  "fontCJK": "黑体",
  "fontSize": "28",
  "alignment": "center",
  "lineSpacing": "560",
  "lineRule": "exact"
}
```

实现：TemplateEngine.InferFormatFromText() 方法（如不存在则新建）

---

### 三、改（6 个）

#### `nong word rebuild <file>`
清理 OOXML 样式污染（WPS/Office 遗留属性）。

输入：.docx 文件路径
输出：清理后的 docx（原地修改或 `-o` 指定输出）

实现：StyleRebuilder.RebuildAllParagraphs()

#### `nong word fix-order <file>`
修正 ECMA-376 元素顺序（某些编辑器产出乱序 XML）。

输入：.docx 文件路径
输出：修正后的 docx

实现：ElementOrder.RectifyTree() + FixOrphanBorders()

#### `nong word fill <tmpl> <data> [-o <file>]`
模板填充。

输入：模板 .docx + 数据 .json
输出：填充后的 docx

实现：DocxTemplate.Fill()

#### `nong word merge <a> <b> [-o <file>]`
合并两个 docx 文件。

输入：两个 .docx 文件路径
输出：合并后的 docx

实现：AdvancedFeatures.AppendDocument()

#### `nong word protect <file> [--mode <mode>]`
文档保护。

输入：.docx 文件路径 + 保护模式
模式：readonly / comments / tracked / forms
输出：受保护的 docx

实现：AdvancedFeatures.ProtectDocument()

#### `nong word embed-font <file> <font>`
嵌入 TrueType 字体到文档。

输入：.docx 文件路径 + 字体名称
输出：嵌入了字体的 docx

实现：AdvancedFeatures.EmbedFont()

---

### 四、写（10 个，第二版）

以下命令从 JSON 规格生成 Word 元素，直接写入已有 docx。

#### `nong word add paragraph <file> --spec <json>`
添加段落。JSON 规格：
```json
{"text": "正文", "style": "Normal", "bold": false, "italic": false}
```

#### `nong word add table <file> --spec <json>`
添加表格。JSON 规格：
```json
{"caption": "表1", "headers": ["列1","列2"], "rows": [["a","b"]]}
```

#### `nong word add footnote <file> --text <text>`
添加脚注。

#### `nong word add endnote <file> --text <text>`
添加尾注。

#### `nong word add image <file> --src <path> [--caption <text>]`
嵌入图片。

#### `nong word add toc <file>`
插入目录。

#### `nong word add xref <file> --to <bookmark> --text <display>`
交叉引用。

#### `nong word add link <file> --url <url> --text <display>`
超链接。

#### `nong word add bookmark <file> --name <name>`
书签。

#### `nong word add comment <file> --text <text> [--author <name>]`
批注。

#### `nong word add math <file> --latex <latex> [--display]`
插入数学公式。内联模式（默认）插入 OfficeMath 到当前段落末尾，`--display` 独立行居中。

```
nong word add math doc.docx --latex "E=mc^2"
nong word add math doc.docx --latex "\frac{a}{b}" --display
```

实现：MathRenderer.RenderInline() / RenderDisplay()

---

## 数学公式（跨类功能）

数学公式不是独立类别，已融入四个分类：

| 类别 | 命令 | 数学相关行为 |
|------|------|-------------|
| 读 | `read` / `outline` | 遇到 OMML 公式元素时输出 LaTeX 源码（如 `$E=mc^2$`） |
| 查 | `validate` | 检查 OMML 公式结构是否合法 |
| 改 | `rebuild` | 跟段落一样透传，无特殊处理 |
| 写 | `add math` | 插入 LaTeX 公式 → OMML |

---

## 命令总数：28 个

| 类别 | 数量 |
|------|------|
| 读 | 8 |
| 查 | 4 |
| 改 | 6 |
| 写 | 10 |
| **合计** | **28** |

数学公式已融入四个分类（read 输出 LaTeX、validate 检查 OMML、rebuild 透传、add math 写入）。

---

## 第一版实施计划

| 类别 | 命令 | 优先级 | 实现文件 |
|------|------|--------|---------|
| 读 | read | P0 | WordCommands.cs |
| 读 | stats | P0 | WordCommands.cs |
| 查 | preview | P0 | WordCommands.cs |
| 查 | validate | P0 | WordCommands.cs |
| 查 | dissect | P0 | WordCommands.cs |
| 读 | fonts | P1 | WordCommands.cs |
| 读 | styles | P1 | WordCommands.cs |
| 改 | rebuild | P1 | WordCommands.cs |
| 改 | fill | P1 | WordCommands.cs |
| 其余 | 19 个 | P2 | 后续迭代 |

---

## 全局行为

- `--json`：输出 JSON 格式，供 AI 解析
- `--verbose`：输出详细过程信息
- `--output/-o <path>`：指定输出文件路径（写入类命令）
- 命令失败时返回非零退出码 + stderr 错误信息
