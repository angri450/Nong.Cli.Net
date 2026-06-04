<h1 align="center">Nong.NET</h1>

<p align="center">
  <strong>纯 .NET CLI 农学文档与科研图表工具集</strong><br>
  零 JavaScript。一个二进制文件。71 个命令。跨平台运行。
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Angri450.Nong.Cli/"><img src="https://img.shields.io/nuget/v/Angri450.Nong.Cli.svg?label=NuGet" alt="NuGet"></a>
  <a href="https://github.com/angri450/Nong.NET/blob/master/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
  <a href="https://dotnet.microsoft.com/en-us/download"><img src="https://img.shields.io/badge/.NET-8.0-8A2BE2" alt=".NET 8.0"></a>
  <img src="https://img.shields.io/badge/commands-71-green" alt="71 commands">
  <a href="README.md"><img src="https://img.shields.io/badge/English-README.md-blue" alt="English"></a>
</p>

<hr>

<h2>快速安装</h2>

<pre><code>dotnet tool install --global Angri450.Nong.Cli
nong commands --json</code></pre>

<p>仅此而已。不需要 Node.js、不需要 Python、不需要 Docker。终端里只有一个 <code>nong</code> 命令。</p>

<hr>

<h2>Nong.NET 是什么？</h2>

<p><strong>Nong</strong>（农）是一个纯 .NET CLI 工具集，面向农学论文和科研文档工作流。它把分散的脚本链路——Word COM 自动化、Python 图表脚本、JavaScript 图表工具——替换为一个确定性的、跨平台的二进制文件。</p>

<table>
  <tr>
    <td width="50%">
      <h3>大模型管语义</h3>
      <p>Claude / GPT 选择工作流、准备 JSON spec、解读诊断结果。</p>
    </td>
    <td width="50%">
      <h3>CLI 管确定性工作</h3>
      <p>读取、写入、渲染、布局、统计——全部用编译好的 C# 代码执行。不靠 prompt 猜测。</p>
    </td>
  </tr>
</table>

<hr>

<h2>能力概览 — 全部 71 个命令</h2>

<h3>word — Word 文档引擎（30 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong word read</code></td><td>提取纯文本</td></tr>
  <tr><td><code>nong word preview</code></td><td>7 步诊断报告</td></tr>
  <tr><td><code>nong word fill</code></td><td>模板填充（.docx + .json）</td></tr>
  <tr><td><code>nong word rebuild</code></td><td>样式清理与规范化</td></tr>
  <tr><td><code>nong word stats</code></td><td>段落/表格/图片/脚注统计</td></tr>
  <tr><td><code>nong word fonts</code></td><td>列出所有字体</td></tr>
  <tr><td><code>nong word styles</code></td><td>列出所有样式定义</td></tr>
  <tr><td><code>nong word validate</code></td><td>OOXML 校验</td></tr>
  <tr><td><code>nong word extract</code></td><td>提取嵌入图片</td></tr>
  <tr><td><code>nong word dissect</code></td><td>格式指纹聚合（nongmark/v1 一刀三流）</td></tr>
  <tr><td><code>nong word merge</code></td><td>合并多个 .docx</td></tr>
  <tr><td><code>nong word outline</code></td><td>提取文档大纲</td></tr>
  <tr><td><code>nong word images</code></td><td>列出或提取所有图片</td></tr>
  <tr><td><code>nong word comments</code></td><td>读取批注</td></tr>
  <tr><td><code>nong word revisions</code></td><td>列出修订记录</td></tr>
  <tr><td><code>nong word infer-format</code></td><td>从中文描述推断格式</td></tr>
  <tr><td><code>nong word fix-order</code></td><td>修复 OOXML 元素顺序</td></tr>
  <tr><td><code>nong word protect</code></td><td>文档保护（readonly/comments/track-changes）</td></tr>
  <tr><td><code>nong word embed-font</code></td><td>嵌入 TrueType 字体</td></tr>
  <tr><td><code>nong word add paragraph</code></td><td>追加段落（JSON spec）</td></tr>
  <tr><td><code>nong word add table</code></td><td>追加表格（JSON spec）</td></tr>
  <tr><td><code>nong word add footnote</code></td><td>追加脚注</td></tr>
  <tr><td><code>nong word add endnote</code></td><td>追加尾注</td></tr>
  <tr><td><code>nong word add image</code></td><td>追加图片（可选 caption）</td></tr>
  <tr><td><code>nong word add toc</code></td><td>插入目录</td></tr>
  <tr><td><code>nong word add xref</code></td><td>插入交叉引用</td></tr>
  <tr><td><code>nong word add link</code></td><td>插入超链接</td></tr>
  <tr><td><code>nong word add bookmark</code></td><td>插入书签</td></tr>
  <tr><td><code>nong word add comment</code></td><td>插入批注</td></tr>
  <tr><td><code>nong word add math</code></td><td>插入 LaTeX 公式</td></tr>
</table>

<h3>inspect — 论文诊断与写作（10 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong inspect diagnose</code></td><td>完整论文诊断</td></tr>
  <tr><td><code>nong inspect refs</code></td><td>参考文献检查</td></tr>
  <tr><td><code>nong inspect write-paper</code></td><td>从 JSON spec 生成论文 .docx</td></tr>
  <tr><td><code>nong inspect classify</code></td><td>论文类型分类（16 型）</td></tr>
  <tr><td><code>nong inspect structure</code></td><td>提取论文结构</td></tr>
  <tr><td><code>nong inspect evidence</code></td><td>证据链诊断</td></tr>
  <tr><td><code>nong inspect data-req</code></td><td>数据需求诊断</td></tr>
  <tr><td><code>nong inspect gap</code></td><td>缺口等级评估</td></tr>
  <tr><td><code>nong inspect varplan</code></td><td>变量操作化方案</td></tr>
  <tr><td><code>nong inspect semantics</code></td><td>语义/逻辑风险诊断</td></tr>
</table>

<h3>chart — 统计与图表（7 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong chart analyze</code></td><td>ANOVA + Duncan MRT + 描述统计</td></tr>
  <tr><td><code>nong chart anova</code></td><td>单因素方差分析</td></tr>
  <tr><td><code>nong chart duncan</code></td><td>Duncan 多重比较</td></tr>
  <tr><td><code>nong chart bar</code></td><td>柱状图（误差棒 + 显著性字母）</td></tr>
  <tr><td><code>nong chart line</code></td><td>多系列折线图</td></tr>
  <tr><td><code>nong chart scatter</code></td><td>散点图（可选趋势线）</td></tr>
  <tr><td><code>nong chart pie</code></td><td>饼图</td></tr>
</table>

<p>图表基于 <strong>ScottPlot</strong> 渲染，统计分析使用简化 Q 值近似——正式发表论文请用专业工具复核。</p>

<h3>excel — Excel 数据入口（4 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong excel sheets</code></td><td>列出 worksheet</td></tr>
  <tr><td><code>nong excel read</code></td><td>读取单元格内容</td></tr>
  <tr><td><code>nong excel to-groups</code></td><td>处理/值列转为分组 JSON</td></tr>
  <tr><td><code>nong excel create</code></td><td>从 JSON spec 创建 .xlsx</td></tr>
</table>

<h3>diagram — 科学图表（3 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong diagram flowchart</code></td><td>流程图（Sugiyama 布局）</td></tr>
  <tr><td><code>nong diagram network</code></td><td>网络/关系图（力导向布局）</td></tr>
  <tr><td><code>nong diagram tree</code></td><td>系统发育树（Newick/JSON 输入）</td></tr>
</table>

<p>基于 <strong>MSAGL</strong>（自动布局）+ <strong>SkiaSharp</strong>（光栅化）渲染。不需要 Graphviz、不需要 Mermaid、不需要 JavaScript。</p>

<h3>pptx — 幻灯片读取（2 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong pptx read</code></td><td>抽取全部 slide 文本</td></tr>
  <tr><td><code>nong pptx slides</code></td><td>按 slide 统计形状/元素</td></tr>
</table>

<h3>ocr — 文字识别（7 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong ocr cloud</code></td><td>PaddleOCR-VL 云端 OCR</td></tr>
  <tr><td><code>nong ocr local</code></td><td>本地 PP-OCRv5 识别（E005/E009，诚实返回）</td></tr>
  <tr><td><code>nong ocr check-env</code></td><td>检查 OCR 环境状态</td></tr>
  <tr><td><code>nong ocr analyze-image</code></td><td>图像结构分析（无需 token）</td></tr>
  <tr><td><code>nong ocr models</code></td><td>列出可用 OCR 模型</td></tr>
  <tr><td><code>nong ocr install-model</code></td><td>安装 OCR 模型</td></tr>
  <tr><td><code>nong ocr to-word</code></td><td>云端 OCR 转 .docx</td></tr>
</table>

<h3>genre / icons — 模板与素材（4 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong genre list</code></td><td>列出写作模板</td></tr>
  <tr><td><code>nong genre show</code></td><td>查看模板内容</td></tr>
  <tr><td><code>nong icons list</code></td><td>列出 40 个科学 SVG 图标</td></tr>
  <tr><td><code>nong icons search</code></td><td>按关键词搜索图标</td></tr>
</table>

<h3>skill — Skill 生命周期管理（4 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong skill validate</code></td><td>验证 SKILL.md 结构和引用</td></tr>
  <tr><td><code>nong skill scan</code></td><td>安全扫描 skill/插件目录</td></tr>
  <tr><td><code>nong skill inventory</code></td><td>列出目录内容（单 skill + 插件根）</td></tr>
  <tr><td><code>nong skill package</code></td><td>验证 + 扫描 + 打包 .zip</td></tr>
</table>

<hr>

<h2>设计理念</h2>

<table>
  <tr>
    <td><strong>1. 大模型管语义</strong></td>
    <td>AI 模型选择工作流、准备 JSON spec、解读诊断结果。CLI 永远不猜测。</td>
  </tr>
  <tr>
    <td><strong>2. 确定性工作交给 .NET</strong></td>
    <td>所有读取、写入、渲染、布局、统计都是编译后的 C# 代码执行。每次结果可复现。</td>
  </tr>
  <tr>
    <td><strong>3. JSON 优先输出</strong></td>
    <td>每个命令都支持 <code>--json</code> 以输出机器可读结果。专为 AI agent 消费和 shell 管道设计。</td>
  </tr>
  <tr>
    <td><strong>4. 统一错误码体系</strong></td>
    <td>E001 到 E009 覆盖所有故障模式。脚本和 agent 获得可预测、可解析的错误信息。</td>
  </tr>
  <tr>
    <td><strong>5. 永不引入 JavaScript</strong></td>
    <td>没有 npm、没有 webpack、没有 Node.js。从解析到渲染，整条链路都是 C#。</td>
  </tr>
</table>

<hr>

<h2>核心工作流</h2>

<h3>1. Excel → 统计 → 图表</h3>
<pre><code>nong excel to-groups data.xlsx --group A --value B --raw &gt; groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json</code></pre>

<h3>2. 论文生成 → 检查</h3>
<pre><code>nong inspect write-paper spec.json -o paper.docx --json
nong word preview paper.docx --json
nong word read paper.docx --json</code></pre>

<h3>3. 论文诊断</h3>
<pre><code>nong word read paper.docx &gt; paper.txt
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json</code></pre>

<h3>4. 文档审计</h3>
<pre><code>nong word stats paper.docx --json
nong word fonts paper.docx --json
nong word dissect paper.docx -o paper.slice --json</code></pre>

<h3>5. 云端 OCR 管线</h3>
<pre><code>nong ocr check-env --json
nong ocr cloud scan.png -o ocr-out/ --json
nong ocr to-word scan.png -o out.docx --json</code></pre>

<hr>

<h2>JSON 输出格式</h2>

<p>所有带 <code>--json</code> 的命令返回统一结构：</p>

<pre><code>{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": { "png": "fig.png" },
  "metrics": { "paragraphs": 29 },
  "errors": [],
  "meta": { "durationMs": 42, "version": "3.2.0" }
}</code></pre>

<hr>

<h2>错误码</h2>

<table>
  <tr><th>代码</th><th>名称</th><th>含义</th></tr>
  <tr><td><code>E001</code></td><td>file_not_found</td><td>文件未找到</td></tr>
  <tr><td><code>E002</code></td><td>unsupported_format</td><td>错误的扩展名或不支持的格式</td></tr>
  <tr><td><code>E003</code></td><td>missing_argument</td><td>缺少必需参数</td></tr>
  <tr><td><code>E004</code></td><td>internal_error</td><td>意外崩溃</td></tr>
  <tr><td><code>E005</code></td><td>dependency_missing</td><td>工具或 token 未安装</td></tr>
  <tr><td><code>E006</code></td><td>validation_failed</td><td>输入错误或格式不符</td></tr>
  <tr><td><code>E007</code></td><td>read_failed</td><td>无法读取文件</td></tr>
  <tr><td><code>E008</code></td><td>write_failed</td><td>无法写入文件</td></tr>
  <tr><td><code>E009</code></td><td>not_implemented</td><td>命令尚未实现</td></tr>
</table>

<hr>

<h2>项目结构 — 9 个 NuGet 包</h2>

<p>所有包统一版本号 <strong>3.2.0</strong>。每个包职责单一明确。</p>

<table>
  <tr><th>包名</th><th>用途</th></tr>
  <tr><td><code>Angri450.Nong.ThirdParty</code></td><td><strong>地基</strong> — 合入 15 个第三方开源库源码，编译为单一 DLL</td></tr>
  <tr><td><code>Angri450.Nong.Docx</code></td><td>Word 生成、模板填充、论文诊断</td></tr>
  <tr><td><code>Angri450.Nong.Excel</code></td><td>链式 Excel 生成 API，支持公式验证</td></tr>
  <tr><td><code>Angri450.Nong.Chart</code></td><td>18 种图表 + ANOVA/Duncan MRT 统计分析</td></tr>
  <tr><td><code>Angri450.Nong.Diagram</code></td><td>流程图、网络图、系统发育树渲染</td></tr>
  <tr><td><code>Angri450.Nong.Pptx</code></td><td>PowerPoint 生成，10 套主题预设</td></tr>
  <tr><td><code>Angri450.Nong.MultiModal</code></td><td>PaddleOCR 云 + 本地 OCR 集成</td></tr>
  <tr><td><code>Angri450.Nong.Bioicons</code></td><td>40 个 SVG 科学图标</td></tr>
  <tr><td><code>Angri450.Nong.Skill.Manager</code></td><td>Skill 生命周期管理 CLI</td></tr>
</table>

<hr>

<h2>合并的第三方库</h2>

<table>
  <tr><th>库</th><th>协议</th><th>用途</th></tr>
  <tr><td>ClosedXML</td><td>MIT</td><td>Excel 读写</td></tr>
  <tr><td>DocumentFormat.OpenXml</td><td>MIT</td><td>Word/PPTX OOXML 处理</td></tr>
  <tr><td>ScottPlot</td><td>MIT</td><td>图表渲染</td></tr>
  <tr><td>MSAGL</td><td>MIT</td><td>自动图布局</td></tr>
  <tr><td>SkiaSharp</td><td>MIT</td><td>2D 图形光栅化</td></tr>
  <tr><td>HarfBuzzSharp</td><td>MIT</td><td>文字塑形</td></tr>
  <tr><td>SixLabors.Fonts</td><td>Apache-2.0</td><td>字体加载与测量</td></tr>
</table>

<hr>

<h2>运行要求</h2>

<ul>
  <li><strong>.NET SDK 8.0</strong> 或更高（向前兼容 9.0、10.0、11.0）</li>
  <li>Windows、macOS 或 Linux</li>
  <li>SkiaSharp/HarfBuzzSharp 原生库（NuGet 自动安装）</li>
</ul>

<hr>

<h2>许可协议</h2>

<p>Apache-2.0。详见 <a href="LICENSE">LICENSE</a> 文件。</p>
