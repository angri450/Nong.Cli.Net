# Nong.NET v3.x 架构重构方案

日期：2026-06-02
状态：开发中，待发版

---

## 一、背景

当前 `Angri450.Nong.Docx`（v3.0.2）混合了两层职责：底层 Word 引擎操作（读写、格式、图片嵌入、样式）和高层论文语义（类型分类、结构提取、质量诊断、变量生成）。AI 调用时需经过"写 Program.cs → dotnet run"路径，编译开销大，导致 word skill 频繁降级为 PowerShell 直接解析 XML。

## 二、决策

### 2.1 包拆分

原有 9 个包不变。**新增 2 个包**，从 Docx 拆出：

| 包 | 版本 | 定位 | 职责 |
|----|------|------|------|
| `Angri450.Nong.Docx` | 3.0.2 → 3.1.0 | Word 引擎 | 读写 Word、排版格式、图片嵌入、样式引擎、模板填充、OOXML 修复、数学公式渲染 |
| `Angri450.Nong.Inspect` | 3.0.0（新建） | 内容审查+写作 | 论文诊断（6 个分析器）、论文写作（PaperWriter）、公文写作（OfficialDocWriter）、信函写作（LetterWriter）、参考文献管理（ReferenceManager） |
| `Angri450.Nong.Genre` | 3.0.0（新建） | 格式模板库 | 纯 JSON 模板：期刊论文、毕业论文、竞赛论文、答辩 PPT、通知公文、商务信函 |

依赖链：`ThirdParty → Docx → Inspect`，`Genre` 独立（纯资源包）。

### 2.2 CLI 化方案（待实施）

确定性的"一个文件进去、一个结果出来"操作走 CLI，创作型任务保留库调用。

CLI 覆盖范围：`nong word read/extract/dissect/preview`、`nong inspect classify/structure/diagnose`、`nong chart bar`、`nong ocr`。

不分发 NuGet 包给终端用户（隐藏旧包），仅通过 CLI 二进制分发。源码在 GitHub/Gitee 公开，协议后续统一切换为 Apache 2.0。

### 2.3 协议

暂保持 MIT。等 CLI 就绪后统一切换为 Apache 2.0，与 nong-toolkit skill 保持一致。原因：允许商用合作、专利保护条款、与依赖的开源项目协议兼容。

## 三、新功能

### 3.1 Pandoc 内联格式标记

PaperWriter.Body() 支持以下标记语法（Pandoc Markdown 子集）：

| 标记 | 效果 | 示例 |
|------|------|------|
| `*text*` | 斜体 | `*Bacillus subtilis*` |
| `**text**` | 加粗 | `**P < 0.01**` |
| `***text***` | 加粗+斜体 | `***关键结论***` |
| `==text==` | 荧光黄高亮 | `==需复核==` |
| `~~text~~` | 删除线 | `~~旧数据~~` |
| `^text^` | 上标 | `^注1^` |
| `~text~` | 下标 | `H~2~O` |
| `[N]` | 引文上标 | 自动检测 |
| `（Latin）` | 拉丁名斜体 | 自动检测 |

### 3.2 数学公式渲染

DocxCore.MathRenderer：LaTeX 语法 → OMML（Office Math Markup Language），利用 ThirdParty 合并的 DocumentFormat.OpenXml.Math 类型（128+ 类）。

支持：分数 `\frac`、根号 `\sqrt`、上下标、希腊字母（28 个）、积分/求和 `\sum\int`、函数名 `\sin\cos\log\lim`、重音 `\hat\bar\dot\vec`、矩阵 `\begin{matrix}`、括号 `\left(...\right)`。

### 3.3 参考文献管理

用 `[@smith2024]` 标记替代手写 `[1]`，ReferenceManager 在生成时自动分配编号并格式化 GB/T 7714 条目。

流程：`Body("...[@smith2024]...")` → ReferenceManager.Resolve → `"...[1]..."` → Word 自动上标 + 编号。`AutoReferences()` 从数据库中读取条目并自动生成参考文献列表。

支持 8 种文献类型：article / book / thesis / conference / patent / report / standard / online。

## 四、Bug 修复

| # | 严重度 | 描述 | 修复 |
|---|--------|------|------|
| 2 | MEDIUM | StyleBuilder JSON 缺少 lineRule 字段，公文固定行距无法设置 | 增加 lineRule 字段解析（auto/exact/atLeast） |
| 3 | LOW | Body() 逐字 w:t 切分，每个中文字符独立 Run | 改用 Regex.Split 按标记分组，连续文本合并 |
| 7 | MEDIUM | 参考文献序号重复/缺失，手写 [1] 与 Word 自动编号冲突 | References() 自动剥离手写 [N] 前缀 + AutoReferences() 统一管理 |

另有 3 个问题归属 skill 层（nong-toolkit），非包问题：Write 工具中文引号转换（#1）、dissect 脚本路由检测（#4）、word skill dispatch 优先级（#6）。

## 五、待办

- [ ] CLI 工具 `nong`（Angri450.Nong.Cli）开发
- [ ] 协议 MIT → Apache 2.0 批量切换
- [ ] NuGet 旧包 unlist，CLI 发布后执行
- [ ] nong-toolkit word skill 适配新包结构
- [ ] nong-toolkit genre skill 新建
- [ ] Docx 旧论文文件清理（6 个文件仍保留在 Docx/ 中，待测试通过后删除）
- [ ] 打包发版：Docx 3.1.0 + Inspect 3.0.0 + Genre 3.0.0

## 六、相关文件

- 仓库：`C:\Users\Administrator\Documents\Github\Nong.Cli.Net`
- changelog：`changelog/2026-06-02-docx-split-and-bugs.md`（报错诊断）
- 新增源文件：`Docx/MathRenderer.cs`、`Inspect/PaperWriter.cs`、`Inspect/ReferenceManager.cs`、`Inspect/ReferenceModels.cs`、`Inspect/OfficialDocWriter.cs`、`Inspect/LetterWriter.cs`、`Genre/GenreTemplate.cs`
