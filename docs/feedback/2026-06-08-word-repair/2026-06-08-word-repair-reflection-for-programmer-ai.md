# Word 修复实战反思：给程序员 AI 的改进说明

## 背景

这次处理的文件是：

`沸石/校企共建沸石基矿物材料教授工作站方案书-美化版.docx`

用户的真实目标不是“让程序能读”，而是“打开 Word 后能明显变好，能继续当方案书底稿使用”。

我一开始误判了目标，把“机器结构修复”和“可见排版修复”混在一起，导致第一次交付的 `project-fixed.docx` 对用户来说几乎没有价值。用户打开后看不到明显变化，所以反馈“修了个寂寞”是准确的。

## 这次暴露的问题

### 1. 项目把“结构正确”误当成“文档修好了”

第一次修复主要解决了：

- `word outline` 原本只识别 9 个一级标题。
- 二级、三级标题因为 `styleId=21/31`、`styleName=heading 2/heading 3` 被当成普通段落。
- `word fix-order` 清掉了 OOXML 表格 warning。

这些对程序是有意义的，但对用户打开 Word 看，变化很小。

问题在于：项目没有明确区分三种修复：

- 结构修复：让 Nong 能正确读。
- OOXML 修复：让 Word 文件内部合法。
- 视觉排版修复：让人打开 Word 后觉得像正式文档。

这三件事不能混用一个“fixed”命名。

### 2. 成功标准错了

第一次我用这些结果判断“修好了”：

- `word validate`: ok
- `word preview`: OOXML warning 变少
- `word outline`: 标题数量正确
- `word dissect`: 切片无警告

但这些都不是用户最关心的验收标准。

真正该检查的是：

- 标题是否真的套上 Word 标准样式 `Heading1/Heading2/Heading3`。
- 标题字号、黑体、居中/左对齐、段前段后距是否实际进入 OOXML。
- 正文是否宋体、Times New Roman、两端对齐、首行缩进。
- 表格是否真正变成三线表或可读表格。
- 打开 Word 后是否肉眼可见改善。

项目需要把“格式证据”作为一等验收，不是只看 schema valid。

### 3. 标题识别逻辑重复且不一致

同一个“标题识别”逻辑散落在多个地方：

- `OutlineReader`
- `WordSlice`
- `WordAcademicFormatter`
- 其它 comment/image/revision 辅助逻辑里也有类似判断

最初只修了 `OutlineReader` 和 `WordSlice`，所以大纲和切片好了；但 `academic-format` 仍然不能稳定识别这些标题，导致可见排版没有真正改善。

后续应该统一到一个共享能力，比如：

`WordHeadingStyles.GetHeadingLevel(styleId, styleName, outlineLvl, text)`

所有 Word 命令都调用它，不要各自写一套。

### 4. `academic-format` 只改了部分外观，没有规范样式

修复前的 `academic-format` 有一个关键问题：

它会改部分段落属性和 run 属性，但没有强制把段落样式改成标准 Word 样式。

正确行为应该是：

- 封面标题 -> `Title`
- 一级标题 -> `Heading1`
- 二级标题 -> `Heading2`
- 三级标题 -> `Heading3`
- 表题 -> `Caption`
- 正文 -> `Normal`

这次返工后已经补上了这点。否则用户在 Word 样式面板、大纲导航、目录生成里仍然会看到混乱样式。

### 5. 文件命名误导用户

`project-fixed.docx` 这个名字很糟糕。

它实际含义是：

“项目内部结构和 OOXML 清理版”

但用户会理解为：

“我可以打开看的修复版”

建议以后输出命名明确区分：

- `*.structure-fixed.docx`
- `*.ooxml-fixed.docx`
- `*.academic-fixed.docx`
- `*.rebuilt-clean.docx`
- `*.visual-report.html`

不能用笼统的 `fixed`。

## 这次实际修了什么

### 已修复项目代码

- 新增 `Docx/WordHeadingStyles.cs`
- `OutlineReader` 改为同时识别 `styleId` 和 `styleName`
- `WordSlice` 改为使用共享标题识别
- `WordAcademicFormatter` 改为：
  - 使用 style name 识别标题
  - 把段落样式标准化到 `Title/Heading1/Heading2/Heading3/Caption/Normal`
  - 对 `科学问题`、`研究内容`、`SCI论文创新点` 等短学术小标题按三级标题处理

### 真实文件结果

修复前：

- 大纲只有 9 个标题，都是一级。
- 二级、三级标题在切片里是普通段落。
- `project-fixed.docx` 打开后肉眼变化很小。

修复后：

- `academic-fixed.docx` 大纲为 44 个标题：
  - 一级 9 个
  - 二级 23 个
  - 三级 12 个
- 标题已是标准 Word 样式：
  - `Heading1`
  - `Heading2`
  - `Heading3`
- 可见格式证据：
  - 标题：黑体
  - 一级标题：居中，字号 32 half-points
  - 二级标题：左对齐，字号 28 half-points
  - 三级标题：左对齐，字号 26 half-points
  - 正文：`Normal`，宋体 / Times New Roman，首行缩进，两端对齐

## 项目需要改进的方向

### 1. 增加一个面向用户的 Word 修复总命令

现在用户必须组合：

```text
word fix-order
word academic-format
word validate
word dissect
word preview
```

建议新增：

```text
nong word repair input.docx -o output.docx --profile academic --json
```

内部流程：

```text
check
-> fix-order
-> academic-format
-> validate
-> dissect
-> format evidence audit
-> write html report
```

它应该直接返回：

- 输出 docx 路径
- 结构是否修好
- OOXML 是否干净
- 标题/正文/表格格式是否真正落地
- 还有哪些肉眼风险

### 2. 增加 `word format-audit`

需要一个专门检查“可见格式”的命令：

```text
nong word format-audit file.docx --json
```

至少输出：

- 标题数量和层级分布
- 每级标题的 styleId/styleName/字体/字号/对齐/段前段后
- 正文的字体/字号/行距/首行缩进/对齐
- 表格边框、表头、单元格缩进、是否三线表
- 是否存在大量混乱样式
- 是否存在“看似标题但不是标题”的段落

不能再只靠 `word preview`。

### 3. 增加 `word compare-format`

这次用户明确说手写版更好。项目应该支持：

```text
nong word compare-format bad.docx good.docx --json
```

输出：

- 两个文件的样式数量差异
- 标题格式差异
- 表格数量和表题差异
- 正文段落格式差异
- 哪个文件更适合当底稿

这会让 AI 不再靠主观猜。

### 4. 所有“修复命令”都必须输出 HTML 证据报告

JSON 给机器用，但用户需要看得懂。

建议每次 repair 自动生成：

```text
output.repair-report.html
```

内容包括：

- 修了什么
- 哪些地方只是内部修复
- 哪些地方肉眼会变
- 修复前后标题层级对比
- 修复前后 OOXML warning 对比
- 下一步还要人工看的风险

### 5. 测试不能只测 validate

以后 Word 修复类测试必须检查 OOXML 证据：

- 段落样式是否等于 `Heading1/2/3`
- 字号是否正确
- 字体是否正确
- 对齐是否正确
- 缩进是否正确
- 表格边框是否正确

仅 `word validate` 通过不能代表修复成功。

## 给后续程序员 AI 的具体任务

1. 把 Word 标题识别逻辑彻底统一，不允许每个 Reader/Formatter 自己判断。
2. 新增 `word repair` 聚合命令，不让用户手动串命令。
3. 新增 `word format-audit`，输出可见格式证据。
4. 新增 `word compare-format`，支持拿手写版当参照。
5. 修复命令输出命名必须区分：
   - structure fixed
   - ooxml fixed
   - academic fixed
   - rebuilt clean
6. 所有 Word 修复结果都要生成 HTML 报告。
7. 回归测试必须使用真实脏 Word 文件，不只用最小 docx。

## 最重要的教训

用户说“修复 Word”时，默认指的是“打开 Word 后能看出变好”。

程序说“修复 Word”时，往往只修了：

- schema
- XML 顺序
- JSON 结构
- outline

这两套语言不一样。项目必须把“人能看”的格式证据纳入一等公民，否则 AI 很容易交付一个技术上正确、用户体验上失败的文件。
