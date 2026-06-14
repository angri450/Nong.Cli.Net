<h1 align="center">Nong.Cli.Net</h1>

<p align="center">
  <strong>纯 .NET CLI 农学文档与科研图表工具集</strong><br>
  零 JavaScript。模块化架构：轻路由 + 6 个独立子工具。126 个命令。跨平台运行。
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Angri450.Nong.Cli/"><img src="https://img.shields.io/nuget/v/Angri450.Nong.Cli.svg?label=NuGet" alt="NuGet"></a>
  <a href="https://github.com/angri450/Nong.Cli.Net/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
  <a href="https://dotnet.microsoft.com/en-us/download"><img src="https://img.shields.io/badge/.NET-8.0-8A2BE2" alt=".NET 8.0"></a>
  <img src="https://img.shields.io/badge/commands-126-green" alt="126 commands">
  <img src="https://img.shields.io/badge/tests-155-blue" alt="155 tests">
  <a href="README.md"><img src="https://img.shields.io/badge/English-README.md-blue" alt="English"></a>
</p>

<hr>

<h2>快速安装</h2>

<p>主 CLI 仅 12MB，不含重 native 依赖。六类重模块（chart/diagram/pdf/pptx/ocr/imaging）按需自动安装：</p>

<pre><code>dotnet tool install --global Angri450.Nong.Cli
nong commands --json</code></pre>

<p>首次使用 chart/diagram/pdf/pptx/ocr 等外部命令时，CLI 会自动检测并安装对应的独立工具包（<code>Angri450.Nong.Tool.*</code>）。不需要手动逐个安装。</p>

<p>本地 OCR 首次使用前运行 <code>nong ocr install-model pp-ocrv6-medium --json</code>。PP-OCRv6 模型从 PaddleOCR CDN 下载，纯 .NET 运行时，不需要 Python。</p>

<p>CLI 目标框架是 <code>net8.0</code>，打包工具已启用主版本 roll-forward，因此 .NET 9/10 运行时也能运行。旧安装包如果在新运行时失败，请更新工具，或设置 <code>DOTNET_ROLL_FORWARD=LatestMajor</code>。</p>

<hr>

<h2>架构：CLI 路由 + 独立子工具</h2>

<p>4.1.x 版本采用模块化架构。主 <code>nong</code> 是轻量路由器，不直接引用 SkiaSharp/PDFium/PaddleOCR 等重 native 依赖。重型模块各自打成独立 <code>dotnet tool</code>，按需下载安装：</p>

<pre><code>nong (12MB，轻路由)
  ├── 内嵌轻模块（纯 .NET）：
  │     word / excel / inspect / genre / bioicons
  │     lit / pandoc / slice / skill / progress
  │
  └── 外部路由（独立 dotnet tool，按需自动安装）：
        nong-chart     (26MB)  统计图表
        nong-diagram   (26MB)  科学绘图
        nong-pdf       (29MB)  PDF 处理
        nong-pptx      (11MB)  PPT 读写
        nong-ocr       (12MB)  文字识别
        nong-imaging   (26MB)  图像分析</code></pre>

<table>
  <tr><th>工具命令</th><th>PackageId（用户不感知）</th><th>大小</th><th>触发命令</th></tr>
  <tr><td><code>nong</code></td><td><code>Angri450.Nong.Cli</code></td><td>12 MB</td><td>主入口</td></tr>
  <tr><td><code>nong-chart</code></td><td><code>Angri450.Nong.Tool.Chart</code></td><td>26 MB</td><td><code>nong chart ...</code></td></tr>
  <tr><td><code>nong-diagram</code></td><td><code>Angri450.Nong.Tool.Diagram</code></td><td>26 MB</td><td><code>nong diagram ...</code></td></tr>
  <tr><td><code>nong-pdf</code></td><td><code>Angri450.Nong.Tool.Pdf</code></td><td>29 MB</td><td><code>nong pdf ...</code></td></tr>
  <tr><td><code>nong-pptx</code></td><td><code>Angri450.Nong.Tool.Pptx</code></td><td>11 MB</td><td><code>nong pptx ...</code></td></tr>
  <tr><td><code>nong-ocr</code></td><td><code>Angri450.Nong.Tool.Ocr</code></td><td>12 MB</td><td><code>nong ocr ...</code></td></tr>
  <tr><td><code>nong-imaging</code></td><td><code>Angri450.Nong.Tool.Imaging</code></td><td>26 MB</td><td><code>nong word images ...</code></td></tr>
</table>

<p>所有外部子工具也支持直接调用：<code>nong-chart bar ...</code>、<code>nong-pdf merge ...</code> 等。</p>

<hr>

<h2>能力概览 — 全部 126 个命令</h2>

<h3>word — Word 文档引擎（39 个命令）</h3>

<table>
  <tr><th>命令</th><th>功能</th></tr>
  <tr><td><code>nong word check</code></td><td>预检 .doc/.docx</td></tr>
  <tr><td><code>nong word convert</code></td><td>.doc → .docx 转换</td></tr>
  <tr><td><code>nong word create</code></td><td>从 NongMark 直接生成 DOCX</td></tr>
  <tr><td><code>nong word read</code></td><td>提取纯文本</td></tr>
  <tr><td><code>nong word preview</code></td><td>7 步诊断报告</td></tr>
  <tr><td><code>nong word fill</code></td><td>模板填充</td></tr>
  <tr><td><code>nong word rebuild</code></td><td>样式清理与规范化</td></tr>
  <tr><td><code>nong word extract</code></td><td>提取嵌入图片</td></tr>
  <tr><td><code>nong word dissect</code></td><td>格式指纹聚合</td></tr>
  <tr><td><code>nong word stats</code></td><td>文档统计</td></tr>
  <tr><td><code>nong word fonts</code></td><td>列出所有字体</td></tr>
  <tr><td><code>nong word styles</code></td><td>列出所有样式</td></tr>
  <tr><td><code>nong word validate</code></td><td>OOXML 校验</td></tr>
  <tr><td><code>nong word merge</code></td><td>合并多个 .docx</td></tr>
  <tr><td><code>nong word outline</code></td><td>提取文档大纲</td></tr>
  <tr><td><code>nong word compare</code></td><td>两份 DOCX 段落级 diff 对比</td></tr>
  <tr><td><code>nong word images</code></td><td>列出/提取/分析图片</td></tr>
  <tr><td><code>nong word crop</code></td><td>内容感知图片裁剪</td></tr>
  <tr><td><code>nong word fit-images</code></td><td>多图段落并排缩放</td></tr>
  <tr><td><code>nong word compact-tables</code></td><td>表格紧缩</td></tr>
  <tr><td><code>nong word regroup-images</code></td><td>图片重组成对布局</td></tr>
  <tr><td><code>nong word estimate</code></td><td>页面空白估算</td></tr>
  <tr><td><code>nong word page-setup</code></td><td>页面尺寸/边距/分栏</td></tr>
  <tr><td><code>nong word indent</code></td><td>段落缩进控制</td></tr>
  <tr><td><code>nong word paragraph-control</code></td><td>分页控制 (keepNext/cantSplit)</td></tr>
  <tr><td><code>nong word image-wrap</code></td><td>图片浮动环绕</td></tr>
  <tr><td><code>nong word cell-format</code></td><td>表格单元格格式</td></tr>
  <tr><td><code>nong word run-format</code></td><td>字符级格式</td></tr>
  <tr><td><code>nong word comments</code></td><td>读取批注</td></tr>
  <tr><td><code>nong word revisions</code></td><td>列出修订</td></tr>
  <tr><td><code>nong word infer-format</code></td><td>从中文描述推断格式</td></tr>
  <tr><td><code>nong word academic-format</code></td><td>学术格式修复</td></tr>
  <tr><td><code>nong word format-gongwen</code></td><td>公文格式应用</td></tr>
  <tr><td><code>nong word format-audit</code></td><td>排版证据审计</td></tr>
  <tr><td><code>nong word repair-plan</code></td><td>修复命令路由说明</td></tr>
  <tr><td><code>nong word table-reflow</code></td><td>长表格拆续表</td></tr>
  <tr><td><code>nong word protect</code></td><td>文档保护</td></tr>
  <tr><td><code>nong word embed-font</code></td><td>嵌入字体</td></tr>
  <tr><td><code>nong word fix-order</code></td><td>修复 OOXML 元素顺序</td></tr>
</table>

<p>word add 子命令：<code>paragraph</code> / <code>table</code> / <code>footnote</code> / <code>endnote</code> / <code>image</code> / <code>toc</code> / <code>xref</code> / <code>link</code> / <code>bookmark</code> / <code>comment</code> / <code>math</code> — 按 JSON spec 追加元素。</p>

<h3>inspect — 论文诊断与写作（12 个命令）</h3>

<table>
  <tr><td><code>nong inspect diagnose</code></td><td>完整论文诊断</td></tr>
  <tr><td><code>nong inspect refs</code></td><td>参考文献检查</td></tr>
  <tr><td><code>nong inspect write-paper</code></td><td>从 JSON spec 生成论文 .docx</td></tr>
  <tr><td><code>nong inspect write-official</code></td><td>从 JSON spec 生成公文 .docx</td></tr>
  <tr><td><code>nong inspect official-check</code></td><td>公文格式合规审计</td></tr>
  <tr><td><code>nong inspect classify</code></td><td>论文类型分类 (16 型)</td></tr>
  <tr><td><code>nong inspect structure</code></td><td>提取论文结构 (IMRaD)</td></tr>
  <tr><td><code>nong inspect evidence</code></td><td>证据链诊断</td></tr>
  <tr><td><code>nong inspect data-req</code></td><td>数据需求诊断</td></tr>
  <tr><td><code>nong inspect gap</code></td><td>缺口等级评估</td></tr>
  <tr><td><code>nong inspect varplan</code></td><td>变量操作化方案</td></tr>
  <tr><td><code>nong inspect semantics</code></td><td>语义诊断</td></tr>
</table>

<h3>chart — 统计与图表（11 个命令，外部工具 nong-chart）</h3>

<table>
  <tr><td><code>nong chart bar</code></td><td>柱状图（误差棒 + 显著性字母）</td></tr>
  <tr><td><code>nong chart line</code></td><td>折线图</td></tr>
  <tr><td><code>nong chart scatter</code></td><td>散点图</td></tr>
  <tr><td><code>nong chart pie</code></td><td>饼图</td></tr>
  <tr><td><code>nong chart boxplot</code></td><td>箱线图</td></tr>
  <tr><td><code>nong chart histogram</code></td><td>直方图</td></tr>
  <tr><td><code>nong chart heatmap</code></td><td>热力图</td></tr>
  <tr><td><code>nong chart radar</code></td><td>雷达图</td></tr>
  <tr><td><code>nong chart analyze</code></td><td>ANOVA + Duncan MRT + 描述统计</td></tr>
  <tr><td><code>nong chart anova</code></td><td>单因素方差分析</td></tr>
  <tr><td><code>nong chart duncan</code></td><td>Duncan 多重比较</td></tr>
</table>

<h3>excel — Excel（8 个命令，纯 .NET 内嵌）</h3>

<table>
  <tr><td><code>nong excel sheets</code></td><td>列出 worksheet</td></tr>
  <tr><td><code>nong excel read</code></td><td>读取内容</td></tr>
  <tr><td><code>nong excel create</code></td><td>从 JSON spec 创建 .xlsx</td></tr>
  <tr><td><code>nong excel to-groups</code></td><td>列转为分组 JSON</td></tr>
  <tr><td><code>nong excel style</code></td><td>单元格样式</td></tr>
  <tr><td><code>nong excel formula</code></td><td>公式写入</td></tr>
  <tr><td><code>nong excel pivot</code></td><td>透视表创建</td></tr>
  <tr><td><code>nong excel dissect</code></td><td>切片为 NongPandoc 包</td></tr>
</table>

<h3>diagram — 科学图表（3 个命令，外部工具 nong-diagram）</h3>

<table>
  <tr><td><code>nong diagram flowchart</code></td><td>流程图</td></tr>
  <tr><td><code>nong diagram network</code></td><td>网络/关系图</td></tr>
  <tr><td><code>nong diagram tree</code></td><td>系统发育树 (Newick)</td></tr>
</table>

<h3>ocr — 文字识别（11 个命令，外部工具 nong-ocr）</h3>

<table>
  <tr><td><code>nong ocr local</code></td><td>本地 PP-OCRv6 识别（纯 .NET，不要 Python）</td></tr>
  <tr><td><code>nong ocr cloud</code></td><td>云端 PaddleOCR-VL-1.6</td></tr>
  <tr><td><code>nong ocr to-word</code></td><td>云端 OCR 转 .docx</td></tr>
  <tr><td><code>nong ocr models</code></td><td>列出可用模型</td></tr>
  <tr><td><code>nong ocr install-model</code></td><td>安装 PP-OCRv6 模型</td></tr>
  <tr><td><code>nong ocr check-env</code></td><td>检查 OCR 环境</td></tr>
  <tr><td><code>nong ocr analyze-image</code></td><td>图像结构分析</td></tr>
  <tr><td><code>nong ocr batch</code></td><td>批量 OCR（目录扫描）</td></tr>
  <tr><td><code>nong ocr video</code></td><td>视频帧 OCR + SRT 字幕</td></tr>
  <tr><td><code>nong ocr screen</code></td><td>屏幕区域截图 OCR</td></tr>
  <tr><td><code>nong ocr camera</code></td><td>摄像头实时 OCR</td></tr>
</table>

<h3>pdf — PDF 处理（8 个命令，外部工具 nong-pdf）</h3>

<table>
  <tr><td><code>nong pdf check</code></td><td>预检，text/hybrid/scan 分类</td></tr>
  <tr><td><code>nong pdf dissect</code></td><td>切片为 NongPandoc 包</td></tr>
  <tr><td><code>nong pdf render</code></td><td>页面渲染为 PNG</td></tr>
  <tr><td><code>nong pdf images</code></td><td>提取嵌入图片</td></tr>
  <tr><td><code>nong pdf merge</code></td><td>合并 PDF</td></tr>
  <tr><td><code>nong pdf split</code></td><td>拆分 PDF</td></tr>
  <tr><td><code>nong pdf ocr</code></td><td>扫描件加 OCR 层</td></tr>
  <tr><td><code>nong pdf compress</code></td><td>压缩 PDF</td></tr>
</table>

<h3>pptx — 幻灯片读写（4 个命令，外部工具 nong-pptx）</h3>

<table>
  <tr><td><code>nong pptx read</code></td><td>提取 slide 文本</td></tr>
  <tr><td><code>nong pptx slides</code></td><td>列出 slide 结构</td></tr>
  <tr><td><code>nong pptx dissect</code></td><td>切片为 NongPandoc 包</td></tr>
  <tr><td><code>nong pptx create</code></td><td>从 JSON spec 生成 .pptx</td></tr>
</table>

<h3>lit — 文献检索（5 个命令）</h3>

<table>
  <tr><td><code>nong lit parse</code></td><td>解析类 CNKI 检索式</td></tr>
  <tr><td><code>nong lit validate</code></td><td>校验检索式语法</td></tr>
  <tr><td><code>nong lit plan</code></td><td>规划文献查询</td></tr>
  <tr><td><code>nong lit search</code></td><td>检索 OpenAlex/Crossref/Unpaywall</td></tr>
  <tr><td><code>nong lit export</code></td><td>导出 JSON/Markdown/BibTeX</td></tr>
</table>

<h3>slice — 统一包检查（4 个命令）</h3>

<table>
  <tr><td><code>nong slice inspect</code></td><td>检查 NongPandoc 包合同</td></tr>
  <tr><td><code>nong slice blocks</code></td><td>列出内容块</td></tr>
  <tr><td><code>nong slice block</code></td><td>读取单个块</td></tr>
  <tr><td><code>nong slice assets</code></td><td>列出素材</td></tr>
</table>

<h3>genre / icons / skill / progress（12 个命令）</h3>

<table>
  <tr><td><code>nong genre list</code></td><td>列出写作模板</td></tr>
  <tr><td><code>nong genre show</code></td><td>查看模板内容</td></tr>
  <tr><td><code>nong icons list</code></td><td>列出 40 个科学 SVG 图标</td></tr>
  <tr><td><code>nong icons search</code></td><td>搜索图标</td></tr>
  <tr><td><code>nong skill validate</code></td><td>验证 SKILL.md</td></tr>
  <tr><td><code>nong skill scan</code></td><td>安全扫描 skill 目录</td></tr>
  <tr><td><code>nong skill inventory</code></td><td>列出 skill 内容</td></tr>
  <tr><td><code>nong skill package</code></td><td>打包 skill 为 .zip</td></tr>
  <tr><td><code>nong progress report</code></td><td>生成 HTML 进度报告</td></tr>
  <tr><td><code>nong commands</code></td><td>列出所有命令（支持 --json / --format openai-tools）</td></tr>
</table>

<hr>

<h2>设计理念</h2>

<table>
  <tr><td><strong>1. 大模型管语义</strong></td><td>AI 选择工作流、准备 JSON spec、解读诊断结果。CLI 不猜测。</td></tr>
  <tr><td><strong>2. 确定性工作交给 .NET</strong></td><td>读取、写入、渲染、布局、统计全部是编译后的 C# 代码。</td></tr>
  <tr><td><strong>3. JSON 优先输出</strong></td><td>每个命令支持 <code>--json</code>，专为 AI agent 和 shell 管道设计。</td></tr>
  <tr><td><strong>4. 统一错误码</strong></td><td>E001-E009 覆盖所有故障模式。</td></tr>
  <tr><td><strong>5. 模块化按需加载</strong></td><td>主 CLI 12MB，重模块独立安装。用户不用就不下载。</td></tr>
  <tr><td><strong>6. 永不引入 JavaScript</strong></td><td>从解析到渲染全链路 C#。</td></tr>
</table>

<hr>

<h2>核心工作流</h2>

<h3>1. Excel → 统计 → 图表</h3>
<pre><code>nong excel to-groups data.xlsx --group A --value B --raw &gt; groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json</code></pre>

<h3>2. 论文生成 → 检查</h3>
<pre><code>nong inspect write-paper spec.json -o paper.docx --json
nong word preview paper.docx --json</code></pre>

<h3>3. 论文诊断</h3>
<pre><code>nong word read paper.docx &gt; paper.txt
nong inspect diagnose paper.docx --json</code></pre>

<h3>4. 文档紧缩管线</h3>
<pre><code>nong word crop paper.docx -o out.docx --json
nong word compact-tables out.docx -o out2.docx --json
nong word fit-images out2.docx -o final.docx --json</code></pre>

<h3>5. PDF 一刀三流</h3>
<pre><code>nong pdf check guide.pdf --json
nong pdf render guide.pdf -o pages/ --dpi 200 --json
nong pdf merge a.pdf,b.pdf -o merged.pdf --json</code></pre>

<h3>6. 文献检索 DSL</h3>
<pre><code>nong lit parse --query "SU=('水稻'+'小麦')*('产量'+'品质')" --json
nong lit search "drought tolerance maize" -o refs.json --json</code></pre>

<hr>

<h2>项目结构 — 4.1.2 发布包</h2>

<table>
  <tr><th>PackageId</th><th>类型</th><th>大小</th><th>用途</th></tr>
  <tr><td><code>Angri450.Nong.Cli</code></td><td>dotnet tool</td><td>12 MB</td><td>主路由 + 纯 .NET 轻模块</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Chart</code></td><td>dotnet tool</td><td>26 MB</td><td>统计图表（SkiaSharp+ScottPlot）</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Diagram</code></td><td>dotnet tool</td><td>26 MB</td><td>科学绘图（MSAGL+SkiaSharp）</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Pdf</code></td><td>dotnet tool</td><td>29 MB</td><td>PDF 处理（PDFium+PdfPig）</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Pptx</code></td><td>dotnet tool</td><td>11 MB</td><td>PPT 读写（Open XML SDK）</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Ocr</code></td><td>dotnet tool</td><td>12 MB</td><td>文字识别（PaddleOCR v6）</td></tr>
  <tr><td><code>Angri450.Nong.Tool.Imaging</code></td><td>dotnet tool</td><td>26 MB</td><td>图像分析（SkiaSharp）</td></tr>
</table>

<p>核心库包（<code>Angri450.Nong.Chart</code> / <code>.Diagram</code> / <code>.Pdf</code> 等）保留 ID 作为未来库包发布，不与工具包混用。</p>

<hr>

<h2>运行要求</h2>

<ul>
  <li><strong>.NET SDK 8.0</strong> 或更高（支持 9/10/11 roll-forward）</li>
  <li>Windows（本版本优先支持）、macOS 或 Linux</li>
  <li>SkiaSharp/HarfBuzzSharp 原生库（NuGet 自动安装）</li>
  <li>chart/diagram/imaging 三包当前只打包 Windows native assets；Linux/macOS 用户需从源码构建</li>
</ul>

<hr>

<h2>许可协议</h2>

<p>Apache-2.0。详见 <a href="LICENSE">LICENSE</a> 文件。</p>
