<h1 align="center">Nong.Cli.Net</h1>

<p align="center">
  <strong>纯 .NET CLI 农学文档与科研图表工具集</strong><br>
  零 JavaScript。模块化架构：轻路由(17MB) + 6 个独立子工具。179 命令。语义向量检索。
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Angri450.Nong.Cli/"><img src="https://img.shields.io/nuget/v/Angri450.Nong.Cli.svg?label=NuGet" alt="NuGet"></a>
  <a href="https://github.com/angri450/Nong.Cli.Net/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
  <a href="https://dotnet.microsoft.com/en-us/download"><img src="https://img.shields.io/badge/.NET-8.0-8A2BE2" alt=".NET 8.0"></a>
  <img src="https://img.shields.io/badge/commands-179-green" alt="179 commands">
  <a href="README.md"><img src="https://img.shields.io/badge/English-README.md-blue" alt="English"></a>
</p>

<hr>

<h2>快速安装</h2>

<p>主 CLI 约 17MB（含 ONNX Runtime 原生库），不含重 native 依赖。六类外部模块按需自动安装：</p>

<pre><code>dotnet tool install --global Angri450.Nong.Cli
nong commands --json</code></pre>

<p>首次使用 chart/diagram/pdf/pptx/ocr 等外部命令时，CLI 会自动检测并安装对应的独立工具包（<code>Angri450.Nong.Tool.*</code>）。不需要手动逐个安装。</p>

<p>本地 OCR 首次使用前安装 PP-OCRv6 ONNX 模型（从魔搭社区 git clone，不需要原生运行时包）：</p>

<pre><code>nong ocr install-model pp-ocrv6-medium --json</code></pre>

<p>语义搜索需要 Embedding 模型（263MB，jina-embeddings-v5-omni-nano Q4F16）：</p>

<pre><code>git clone --depth 1 https://modelscope.cn/onnx-community/jina-embeddings-v5-omni-nano-ONNX.git .nong/models/jina-v5-nano
# 复制 text tower 文件
copy .nong/models/jina-v5-nano/onnx/text_model_q4f16.onnx .nong/models/jina-v5-nano/model.onnx
copy .nong/models/jina-v5-nano/onnx/text_model_q4f16.onnx_data .nong/models/jina-v5-nano/
nong search "你的查询"</code></pre>

<p>CLI 目标框架是 <code>net8.0</code>，打包工具已启用主版本 roll-forward，.NET 9/10 运行时也能运行。</p>

<hr>

<h2>架构：CLI 路由 + 独立子工具</h2>

<p>4.x 版本采用模块化架构。主 <code>nong</code> 是轻量路由器（17MB，含 ONNX Runtime），重型模块各自打成独立 <code>dotnet tool</code>，按需下载安装：</p>

<pre><code>nong (17MB, 含 ONNX Runtime)
  ├── 内嵌模块（纯 .NET）：
  │     word / excel / inspect / lit / aminer
  │     metaso / genre / icons / slice / skill / search
  │
  └── 外部路由（独立 dotnet tool，按需自动安装）：
        nong-chart     (26MB)  统计图表
        nong-diagram   (26MB)  科学绘图
        nong-pdf       (29MB)  PDF 处理
        nong-pptx      (11MB)  PPT 读写
        nong-ocr       (~15MB) 文字识别 (ONNX Runtime)
        nong-imaging   (26MB)  图像分析</code></pre>

<table>
  <tr><th>工具命令</th><th>PackageId</th><th>体积</th><th>触发命令</th></tr>
  <tr><td><code>nong</code></td><td><code>Angri450.Nong.Cli</code></td><td>17 MB</td><td>主入口 (含 ONNX Runtime)</td></tr>
  <tr><td><code>nong-chart</code></td><td><code>Angri450.Nong.Tool.Chart</code></td><td>26 MB</td><td><code>nong chart ...</code></td></tr>
  <tr><td><code>nong-diagram</code></td><td><code>Angri450.Nong.Tool.Diagram</code></td><td>26 MB</td><td><code>nong diagram ...</code></td></tr>
  <tr><td><code>nong-pdf</code></td><td><code>Angri450.Nong.Tool.Pdf</code></td><td>29 MB</td><td><code>nong pdf ...</code></td></tr>
  <tr><td><code>nong-pptx</code></td><td><code>Angri450.Nong.Tool.Pptx</code></td><td>11 MB</td><td><code>nong pptx ...</code></td></tr>
  <tr><td><code>nong-ocr</code></td><td><code>Angri450.Nong.Tool.Ocr</code></td><td>~15 MB</td><td><code>nong ocr ...</code></td></tr>
  <tr><td><code>nong-imaging</code></td><td><code>Angri450.Nong.Tool.Imaging</code></td><td>26 MB</td><td><code>nong word images ...</code></td></tr>
</table>

<hr>

<h2>v4.5.0 主要更新</h2>

<ul>
  <li><strong>nong search</strong> — 语义向量检索。对所有已入库文档块做语义搜索。模型：jina-embeddings-v5-omni-nano Q4F16 (263MB)，冷启动 1511ms。</li>
  <li><strong>--ingest 统一入库 (31 命令)</strong> — 所有产文本的命令都支持 <code>--ingest</code>，入库后 <code>nong search</code> 可检索。</li>
  <li><strong>OCR ONNX 统一</strong> — PP-OCRv6 迁移到 ONNX Runtime。PaddleInference 退役（不再需要 Nong.OcrRuntime 原生运行时包）。模型从魔搭社区 git clone 下载。</li>
  <li><strong>单推理引擎</strong> — Embedding 搜索和 OCR 共用 <code>Microsoft.ML.OnnxRuntime</code>。nupkg 按平台裁剪（win-x64 17MB），不含多余原生库。</li>
  <li><strong>179 命令</strong> — v4.3.0 的 167 个增加到 179 个。</li>
</ul>

<hr>

<h2>能力概览 (v4.5.0, 179 命令)</h2>

<p>当前 4.5.0 本地构建回读为 <code>179 commands available</code>。精确命令面以 CLI 实际输出为准：</p>

<pre><code>nong commands --json
nong commands --format openai-tools</code></pre>

<h3>search — 语义检索 (v4.5.0 新增)</h3>
<table>
  <tr><td><code>nong search</code></td><td>跨所有已入库文档的语义向量搜索</td></tr>
</table>

<h3>--ingest 统一入库 (31 命令)</h3>
<p>所有搜索结果、诊断结果、OCR 文本都可以通过 <code>--ingest</code> 写入 NongDb，然后被 <code>nong search</code> 检索：</p>
<pre><code>nong word dissect paper.docx -o slice --ingest      # 文档切片入库
nong lit search "水稻" --ingest                        # 文献检索入库
nong inspect diagnose paper.txt --ingest              # 诊断结果入库
nong ocr local scan.png --ingest                      # OCR 文本入库
nong aminer scholar --name "张三" --ingest            # 学者检索入库
nong search "水稻产量影响因素" --limit 5                # 跨库搜索</code></pre>

<h3>word — Word 文档引擎 (56 命令)</h3>
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
  <tr><td><code>nong word dissect --ingest</code></td><td>格式指纹 + 切片入库</td></tr>
  <tr><td><code>nong word stats</code></td><td>文档统计</td></tr>
  <tr><td><code>nong word fonts</code></td><td>列出所有字体</td></tr>
  <tr><td><code>nong word styles</code></td><td>列出所有样式</td></tr>
  <tr><td><code>nong word validate</code></td><td>OOXML 校验</td></tr>
  <tr><td><code>nong word merge</code></td><td>合并多个 .docx</td></tr>
  <tr><td><code>nong word outline</code></td><td>提取文档大纲</td></tr>
  <tr><td><code>nong word compare</code></td><td>两份 DOCX 段落级 diff 对比</td></tr>
  <tr><td><code>nong word academic-format</code></td><td>学术格式修复</td></tr>
  <tr><td><code>nong word format-audit</code></td><td>排版证据审计</td></tr>
  <tr><td><code>nong word format-gongwen</code></td><td>公文格式应用</td></tr>
  <tr><td><code>nong word table-reflow</code></td><td>长表格拆续表</td></tr>
  <tr><td><code>nong word compact-tables</code></td><td>表格紧缩</td></tr>
  <tr><td><code>nong word embed-font</code></td><td>嵌入字体</td></tr>
  <tr><td><code>nong word to-pdf</code></td><td>转换为 PDF</td></tr>
</table>
<p>word add 子命令（11 个）：paragraph / table / footnote / endnote / image / toc / xref / link / bookmark / comment / math。</p>

<h3>inspect — 论文诊断与写作 (12 命令)</h3>
<table>
  <tr><td><code>nong inspect diagnose --ingest</code></td><td>完整论文诊断 (入库)</td></tr>
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

<h3>chart — 统计与图表 (11 命令, 外部工具)</h3>
<table>
  <tr><td><code>nong chart bar</code></td><td>柱状图（误差棒 + 显著性字母）</td></tr>
  <tr><td><code>nong chart line</code></td><td>折线图</td></tr>
  <tr><td><code>nong chart scatter</code></td><td>散点图</td></tr>
  <tr><td><code>nong chart pie</code></td><td>饼图</td></tr>
  <tr><td><code>nong chart boxplot</code></td><td>箱线图</td></tr>
  <tr><td><code>nong chart histogram</code></td><td>直方图</td></tr>
  <tr><td><code>nong chart heatmap</code></td><td>热力图</td></tr>
  <tr><td><code>nong chart radar</code></td><td>雷达图</td></tr>
  <tr><td><code>nong chart analyze --ingest</code></td><td>ANOVA + Duncan MRT + 入库</td></tr>
  <tr><td><code>nong chart anova</code></td><td>单因素方差分析</td></tr>
  <tr><td><code>nong chart duncan</code></td><td>Duncan 多重比较</td></tr>
</table>

<h3>excel — Excel (9 命令, 纯 .NET)</h3>
<table>
  <tr><td><code>nong excel sheets</code></td><td>列出 worksheet</td></tr>
  <tr><td><code>nong excel read</code></td><td>读取内容</td></tr>
  <tr><td><code>nong excel create</code></td><td>从 JSON spec 创建 .xlsx</td></tr>
  <tr><td><code>nong excel to-groups</code></td><td>列转为分组 JSON</td></tr>
  <tr><td><code>nong excel restructure</code></td><td>实验表重组 + 描述统计</td></tr>
  <tr><td><code>nong excel style</code></td><td>单元格样式</td></tr>
  <tr><td><code>nong excel formula</code></td><td>公式写入</td></tr>
  <tr><td><code>nong excel pivot</code></td><td>透视表创建</td></tr>
  <tr><td><code>nong excel dissect --ingest</code></td><td>切片入库</td></tr>
</table>

<h3>ocr — 文字识别 (11 命令, 外部工具, ONNX Runtime)</h3>
<table>
  <tr><td><code>nong ocr local --ingest</code></td><td>本地 PP-OCRv6 ONNX 识别 (入库)</td></tr>
  <tr><td><code>nong ocr cloud --ingest</code></td><td>云端 PaddleOCR-VL-1.6 (入库)</td></tr>
  <tr><td><code>nong ocr to-word</code></td><td>云端 OCR 转 .docx</td></tr>
  <tr><td><code>nong ocr models</code></td><td>列出可用模型</td></tr>
  <tr><td><code>nong ocr install-model</code></td><td>从魔搭社区安装 PP-OCRv6 ONNX 模型</td></tr>
  <tr><td><code>nong ocr check-env</code></td><td>检查 OCR 环境</td></tr>
  <tr><td><code>nong ocr batch</code></td><td>批量 OCR（目录扫描）</td></tr>
  <tr><td><code>nong ocr video</code></td><td>视频帧 OCR + SRT 字幕</td></tr>
  <tr><td><code>nong ocr screen</code></td><td>屏幕区域截图 OCR</td></tr>
  <tr><td><code>nong ocr camera</code></td><td>摄像头实时 OCR</td></tr>
</table>

<h3>lit — 文献检索 (11 命令)</h3>
<table>
  <tr><td><code>nong lit parse</code></td><td>解析类 CNKI 检索式</td></tr>
  <tr><td><code>nong lit validate</code></td><td>校验检索式语法</td></tr>
  <tr><td><code>nong lit plan</code></td><td>规划文献查询</td></tr>
  <tr><td><code>nong lit search --ingest</code></td><td>检索 OpenAlex/Crossref/Unpaywall (入库)</td></tr>
  <tr><td><code>nong lit export</code></td><td>导出 JSON/Markdown/BibTeX</td></tr>
</table>

<h3>aminer (22 命令, 全部 --ingest) | metaso (3 命令, 全部 --ingest)</h3>
<p>AMiner: scholar/paper/rec/patent/org/venue/paper-info/patent-info (免费) + paper-pro/detail/citations/qa/deep-research + scholar-detail/figure/stat/papers/patents/projects + org-detail/patents + patent-detail (付费, 全部 --ingest)。</p>
<p>Metaso: search(--ingest)/reader(--ingest)/chat(--ingest)。</p>

<h3>genre / icons / skill / slice</h3>
<p>模板、图标、skill 管理、包检查等辅助命令组。</p>

<hr>

<h2>设计理念</h2>

<table>
  <tr><td><strong>1. 大模型管语义</strong></td><td>AI 选择工作流、准备 JSON spec、解读诊断结果。CLI 不猜测。</td></tr>
  <tr><td><strong>2. 确定性工作交给 .NET</strong></td><td>读取、写入、渲染、布局、统计、向量检索全部是编译后的 C# 代码。</td></tr>
  <tr><td><strong>3. JSON 优先输出</strong></td><td>每个命令支持 <code>--json</code>，专为 AI agent 和 shell 管道设计。</td></tr>
  <tr><td><strong>4. 统一错误码</strong></td><td>E001-E009 覆盖所有故障模式。</td></tr>
  <tr><td><strong>5. 模块化按需加载</strong></td><td>主 CLI 17MB，重模块独立安装。用户不用就不下载。</td></tr>
  <tr><td><strong>6. 统一入库检索</strong></td><td>31 命令支持 <code>--ingest</code>，全部产出可被 <code>nong search</code> 检索。</td></tr>
  <tr><td><strong>7. 永不引入 JavaScript</strong></td><td>从解析到渲染全链路 C#。</td></tr>
</table>

<hr>

<h2>技术栈</h2>

<ul>
  <li><strong>推理引擎</strong> — Microsoft.ML.OnnxRuntime（Embedding 搜索和 OCR 共用，按平台裁剪 nupkg）</li>
  <li><strong>Embedding 模型</strong> — jina-embeddings-v5-omni-nano Q4F16 (263MB)，来自魔搭 onnx-community</li>
  <li><strong>OCR 模型</strong> — PP-OCRv6 ONNX (medium 132MB / small 31MB / tiny 6MB)，来自魔搭 PaddlePaddle 官方</li>
  <li><strong>图片处理</strong> — SkiaSharp（OCR 预处理、图表渲染、PDF 渲染共用）</li>
  <li><strong>数据库</strong> — LiteDB（NongDb 统一文档库，向量检索用 EmbeddingEngine 计算余弦相似度）</li>
</ul>

<hr>

<h2>核心工作流</h2>

<h3>语义搜索 + 入库</h3>
<pre><code>nong word dissect paper.docx -o slice --ingest
nong lit search "水稻" --ingest
nong search "水稻产量影响因素" --limit 5 --json</code></pre>

<h3>Excel → 统计 → 图表</h3>
<pre><code>nong excel to-groups data.xlsx --group A --value B --raw &gt; groups.json
nong chart analyze groups.json --ingest --json
nong chart bar groups.json -o fig.png --json</code></pre>

<h3>论文生成 → 诊断 → 入库</h3>
<pre><code>nong inspect write-paper spec.json -o paper.docx --json
nong word dissect paper.docx -o slice --ingest --json
nong inspect diagnose paper.docx --ingest --json</code></pre>

<h3>PDF 一刀三流 + 入库</h3>
<pre><code>nong pdf check guide.pdf --json
nong pdf dissect guide.pdf -o slice --mode auto --ingest --json</code></pre>

<h3>文献检索 DSL</h3>
<pre><code>nong lit parse --query "SU=('采前'+'采前处理')*('保鲜'+'贮藏')*('果实'+'果品')" --json
nong lit search --query "SU=('水稻'+'小麦')*('产量'+'品质')" --ingest -o refs.json --json</code></pre>

<h3>OCR + 入库</h3>
<pre><code>nong ocr install-model pp-ocrv6-medium --json
nong ocr local scan.png --ingest --json
nong search "表格中的数据" --limit 5</code></pre>

<hr>

<h2>运行要求</h2>

<ul>
  <li><strong>.NET SDK 8.0</strong> 或更高（支持 9/10/11 roll-forward）</li>
  <li>Windows（优先支持）、macOS 或 Linux</li>
  <li>ONNX Runtime 原生库按平台打包在 nupkg 中，无需单独安装</li>
  <li>chart/diagram/imaging 三包当前只打包 Windows native assets；Linux/macOS 用户需从源码构建</li>
</ul>

<hr>

<h2>许可协议</h2>

<p>Apache-2.0。详见 <a href="LICENSE">LICENSE</a> 文件。</p>
