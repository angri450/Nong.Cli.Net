# Word 修复实战反思：给程序员 AI 的项目改进说明

## 你要接手的问题

这个项目叫 Nong.NET，本次实战处理的是：

`沸石/校企共建沸石基矿物材料教授工作站方案书-美化版.docx`

用户的真实目标不是“程序能读出来”，也不是“OOXML 验证通过”，而是：

打开 Word 后，这个方案书必须肉眼可见地变好，能继续作为正式材料的底稿使用。

这次一开始失败，根本原因是程序和用户对“修复 Word”的定义不一样。程序侧把结构、schema、warning、outline 当成修复结果；用户侧只认打开 Word 后是否像一份能交付的文档。

## 对话复盘

1. 用户先要求在实战目录里轻装处理，不要把文件弄出工作文件夹。
2. 后续创建了 `沸石/runtime/`，所有运行产物都放在里面。
3. 项目内的 Release 版 `nong.exe` 不是当前源码构建产物，于是重新构建并替换。
4. 初始读取发现，美化版 Word 能被程序读取，但标题层级识别严重错误：
   - 只识别出 9 个一级标题。
   - 大量二级、三级标题因为 `styleId=21/31`、`styleName=heading 2/heading 3` 被当成普通段落。
5. 第一次输出 `project-fixed.docx` 后，程序侧看到：
   - `word validate` 通过。
   - OOXML warning 从 10 个降到 0 个。
   - 大纲和切片结构改善。
6. 但用户打开后几乎看不出变化，所以反馈“修了个寂寞”。这个反馈是准确的。
7. 后来才转向真正的人眼可见修复：
   - 让 `academic-format` 识别 style name。
   - 把段落强制规范成 Word 标准样式：`Title`、`Heading1`、`Heading2`、`Heading3`、`Caption`、`Normal`。
   - 明确检查字号、字体、对齐、首行缩进等 OOXML 格式证据。
8. 第二次输出 `academic-fixed.docx` 后，用户说“起码我能看了”。这说明方向才算对了。

## 核心失败点

### 1. 项目把内部修复误当成用户修复

`project-fixed.docx` 这个名字误导很大。

它实际只是：

- 结构修复
- OOXML 顺序修复
- 程序解析修复

但用户会理解成：

- 打开 Word 后排版修好了
- 文档能直接看了
- 材料更像正式方案书了

以后不能再用笼统的 `fixed` 命名。必须明确区分：

- `*.structure-fixed.docx`
- `*.ooxml-fixed.docx`
- `*.academic-fixed.docx`
- `*.rebuilt-clean.docx`
- `*.repair-report.html`

### 2. 验收标准错了

下面这些只能证明“机器读得过去”，不能证明“文档修好了”：

- `word validate` 通过
- OOXML warning 变少
- `word outline` 标题数正确
- `word dissect` 没报错

真正该作为 Word 修复验收的是：

- 标题是否真的套上 `Heading1/Heading2/Heading3`
- 标题字体、字号、对齐、段前段后是否落进 OOXML
- 正文是否 `Normal`，是否宋体 / Times New Roman，是否两端对齐、首行缩进
- 表格边框、表题、表头、单元格缩进是否变得可读
- 打开 Word 后是否比原始版、美化版明显更像正式文档

### 3. 标题识别逻辑分散

同一件事散在多个模块里判断：

- `OutlineReader`
- `WordSlice`
- `WordAcademicFormatter`
- 其它 Word 诊断或提取命令

这会导致“outline 修好了，但 formatter 没修好”的半截结果。

必须统一到一个共享能力，例如：

`WordHeadingStyles.GetHeadingLevel(styleId, styleName, outlineLevel, text)`

所有 Word 命令都使用同一套标题识别逻辑，不能各自猜。

### 4. 缺少面向人的修复总命令

现在用户需要自己或靠 AI 串联：

```text
word fix-order
word academic-format
word validate
word dissect
word preview
```

这不适合真实使用。

项目应该提供一个聚合命令：

```text
nong word repair input.docx -o output.docx --profile academic --json
```

它内部至少执行：

```text
read
-> structure audit
-> fix-order
-> academic-format
-> validate
-> format-audit
-> write html report
```

最终一次性给用户：

- 修复后的 docx
- JSON 机器报告
- HTML 人话报告
- 仍需人工确认的风险

### 5. 缺少可见格式审计

必须新增：

```text
nong word format-audit file.docx --json
```

它不要只说 valid / invalid，而要输出：

- 标题层级分布
- 每级标题的 styleId、styleName、字体、字号、对齐、段距
- 正文字体、字号、行距、缩进、对齐
- 表格数量、边框、表题、表头、单元格缩进
- 疑似标题但未套标题样式的段落
- 样式混乱程度

Word 修复类功能必须把这个结果作为验收依据。

### 6. 缺少参照文档比较

用户明确说“手写的那个就好很多”。这说明项目需要支持拿好文档当参照。

建议新增：

```text
nong word compare-format bad.docx good.docx --json
```

输出：

- 两个文件的标题层级差异
- 标题样式差异
- 正文样式差异
- 表格样式差异
- 哪个文件更适合作为继续修改的底稿
- 从好文档迁移到坏文档的可执行建议

不要让 AI 靠主观猜哪个“更好”。

## 必须落地的项目改进

1. 新增 `word repair` 聚合命令，面向真实用户，而不是只面向内部调试。
2. 新增 `word format-audit`，把人眼可见格式变成 JSON 证据。
3. 新增 `word compare-format`，支持用用户手写版作为参照样本。
4. 所有 repair 命令自动生成 HTML 报告，用人话说明修了什么、没修什么、打开 Word 会看到什么变化。
5. 统一 Word 标题识别逻辑，禁止 Reader、Slice、Formatter 各写各的判断。
6. 所有 Word 修复测试都要检查 OOXML 格式证据，不允许只测 validate。
7. 加入真实脏文档回归资产，至少覆盖：
   - 数字 styleId + 英文 styleName 的标题
   - WPS/Word 混合样式
   - 表格 warning
   - 看似标题但不是标准 Heading 的段落
8. 修复输出文件命名必须表达真实含义，不要再用泛泛的 `fixed`。

## 验收门槛

以后如果程序员 AI 说“Word 修好了”，至少要同时给出这些证据：

```text
1. 输出 docx 路径
2. validate 结果
3. OOXML warning 数量变化
4. outline 标题数量和层级变化
5. 标题/正文/表格的格式审计结果
6. HTML 报告路径
7. 仍需人工打开 Word 确认的风险
```

如果没有第 5 项和第 6 项，不能称为面向用户的 Word 修复，只能称为内部结构修复。

## 本次已经修过的关键点

代码层面已经补了一部分：

- 新增共享标题识别能力。
- `word outline` 能识别 style name。
- `word dissect` 能把 `heading 2` / `heading 3` 识别成标题块。
- `word academic-format` 能把段落规范成 Word 标准样式。
- 加了针对数字 styleId 的回归测试。

真实文件修复后的证据：

- 大纲从 9 个标题变成 44 个标题。
- 分布为 9 个一级、23 个二级、12 个三级。
- `academic-fixed.docx` 中：
  - 标题使用 `Title` / `Heading1` / `Heading2` / `Heading3`
  - 一级标题黑体、居中
  - 二级、三级标题黑体、左对齐
  - 正文为 `Normal`，宋体 / Times New Roman，两端对齐，首行缩进

但这还不够。项目还需要把这些能力产品化成命令，而不是靠 AI 临时拼流程。

## 一句话教训

用户说“修 Word”，意思是“打开 Word 后能明显变好”。

程序说“修 Word”，经常只是“XML 合法、结构能读、warning 少了”。

这个项目必须把“人能看”的格式证据纳入第一等公民，否则会不断交付技术上正确、用户体验上失败的文件。
