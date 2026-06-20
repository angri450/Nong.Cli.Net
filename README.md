<h1 align="center">Nong.Cli.Net</h1>

<p align="center">
  <strong>Pure .NET CLI toolkit for scientific document generation, inspection, charts, OCR, semantic search, and package slicing.</strong><br>
  Zero JavaScript. Modular architecture: one light router (17MB) plus 6 external dotnet tools. 179 commands.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Angri450.Nong.Cli/"><img src="https://img.shields.io/nuget/v/Angri450.Nong.Cli.svg?label=NuGet" alt="NuGet"></a>
  <a href="https://github.com/angri450/Nong.Cli.Net/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
  <a href="https://dotnet.microsoft.com/en-us/download"><img src="https://img.shields.io/badge/.NET-8.0-8A2BE2" alt=".NET 8.0"></a>
  <img src="https://img.shields.io/badge/commands-179-green" alt="179 commands">
  <a href="README.zh-CN.md"><img src="https://img.shields.io/badge/Chinese-README.zh--CN.md-orange" alt="Chinese"></a>
</p>

<hr>

<h2>Quick Install</h2>

<pre><code>dotnet tool install --global Angri450.Nong.Cli
nong commands --json</code></pre>

<p>The main <code>nong</code> package is a light router (17MB with ONNX Runtime native) plus pure .NET built-in modules. Heavy groups are split into independent dotnet tools and are routed by the same user commands: <code>nong chart ...</code>, <code>nong diagram ...</code>, <code>nong pdf ...</code>, <code>nong pptx ...</code>, <code>nong ocr ...</code>, and imaging paths under <code>nong word images</code>. No Node.js, Docker, Python, Graphviz, or Mermaid runtime is required for core local features.</p>

<p>For local OCR, install the default PP-OCRv6 ONNX model once:</p>

<pre><code>nong ocr install-model pp-ocrv6-medium --json</code></pre>

<p>PP-OCRv6 ONNX models (det+rec) are downloaded from ModelScope and run through ONNX Runtime. No PaddleInference, no native runtime packages needed. <code>Microsoft.ML.OnnxRuntime</code> is shared between embedding search and OCR — a single inference engine.</p>

<p>For semantic search, ensure an embedding model is in <code>.nong/models/jina-v5-nano/</code>:</p>

<pre><code>git clone --depth 1 https://modelscope.cn/onnx-community/jina-embeddings-v5-omni-nano-ONNX.git .nong/models/jina-v5-nano
mv .nong/models/jina-v5-nano/onnx/text_model_q4f16.onnx .nong/models/jina-v5-nano/model.onnx
mv .nong/models/jina-v5-nano/onnx/text_model_q4f16.onnx_data .nong/models/jina-v5-nano/
nong search "your query"</code></pre>

<p>The CLI targets <code>net8.0</code> and the packaged tool opts into major-version roll-forward, so current .NET 9/10 runtimes can run it.</p>

<hr>

<h2>Architecture (v12.1.0, 332 commands, 24 csproj, 22 projects)</h2>

<h3>Project Dependency Map</h3>
<pre><code>                    ┌─────────────┐
                    │ NongCli     │ (main CLI, 332 commands)
                    └──┬──────────┘
         ┌─────────────┼─────────────┬──────────┐
    ┌────▼───┐   ┌────▼───┐   ┌─────▼────┐     │
    │ Docx   │   │ Excel  │   │ Inspect  │  ...│
    │ Pptx   │   │ Pdf    │   │ Genre    │     │
    │ Chart  │   │Diagram │   │ Bioicons │     │
    │ Imaging│   │ OCR    │   │Lit/Aminer│     │
    └──┬──┬──┘   └──┬──┬──┘   └────┬─────┘     │
       │  │         │  │           │           │
    ┌──▼──▼──┐  ┌──▼──▼──┐   ┌───▼────┐      │
    │ThirdParty│ │Pandoc  │   │  Data  │      │
    │(shared) │ │(AST)   │   │(nong.db)│     │
    └─────────┘ └────────┘   └────────┘      │
                                             │
    ┌────────────────────────────────────────▼──┐
    │  外圈 dotnet tools (独立进程，首次用自动装)  │
    │  chart · diagram · pdf · pptx · ocr · imaging│
    └─────────────────────────────────────────────┘</code></pre>

<h3>Command Group → Toolkit.Net Skill</h3>
<table>
<tr><th>CLI Group</th><th>Commands</th><th>Skill</th><th>Notes</th></tr>
<tr><td><code>word</code> (+ <code>add</code>)</td><td>125</td><td><span class="tag skill">word</span></td><td>Document engine: create, dissect, format, fill, convert, to-pdf</td></tr>
<tr><td><code>aminer</code></td><td>44</td><td><span class="tag skill">aminer</span></td><td>AMiner scholar/paper/patent/org API</td></tr>
<tr><td><code>excel</code></td><td>28</td><td><span class="tag skill">excel</span></td><td>XLSX create, chart, formula evaluate, pivot</td></tr>
<tr><td><code>inspect</code> (+ <code>genre</code>)</td><td>28</td><td><span class="tag skill">inspect</span></td><td>Paper diagnosis &amp; template discovery</td></tr>
<tr><td><code>lit</code></td><td>21</td><td><span class="tag skill">literature</span></td><td>Literature search, cache, export</td></tr>
<tr><td><code>pdf</code></td><td>14</td><td><span class="tag skill">pdf</span></td><td>PDF dissect, create, form-fields, compress</td></tr>
<tr><td><code>chart</code></td><td>12</td><td><span class="tag skill">chart</span></td><td>ANOVA, Duncan MRT, bar/line/pie/scatter</td></tr>
<tr><td><code>ocr</code></td><td>12</td><td><span class="tag skill">ocr</span></td><td>Local ONNX OCR, cloud PaddleOCR, analyze-image</td></tr>
<tr><td><code>pptx</code></td><td>9</td><td><span class="tag skill">pptx</span></td><td>PPTX create, edit-slide, remove-slide</td></tr>
<tr><td><code>skill</code></td><td>8</td><td><span class="tag skill">skill-grader</span></td><td>Skill validate, scan, inventory, package</td></tr>
<tr><td><code>slice</code></td><td>8</td><td><span class="tag skill">slice</span></td><td>NongPandoc package inspect</td></tr>
<tr><td><code>nongcli</code> (+ <code>search</code>)</td><td>7</td><td><span class="tag skill">nongcli</span></td><td>Workspace init, embedding model, semantic search</td></tr>
<tr><td><code>metaso</code></td><td>5</td><td><span class="tag skill">metaso</span></td><td>Metaso AI search, reader, chat</td></tr>
<tr><td><code>diagram</code></td><td>4</td><td><span class="tag skill">diagram</span></td><td>Flowchart, network, tree</td></tr>
<tr><td><code>icons</code></td><td>3</td><td><span class="tag skill">icons</span></td><td>Bioicons scientific icon search</td></tr>
<tr><td><code>export</code></td><td>2</td><td><span class="tag skill">export</span></td><td>EPUB, LaTeX, HTML, ODF export</td></tr>
<tr><td><code>markdown</code></td><td>2</td><td><span class="tag skill">markdown</span></td><td>GFM ↔ NongMark bidirectional</td></tr>
<tr style="background:#FFF0E8"><td colspan="4"><b>Infrastructure (no Skill)</b></td></tr>
<tr><td><code>commands</code></td><td>1</td><td>—</td><td>Command manifest export</td></tr>
<tr><td><code>manifest-generate</code></td><td>1</td><td>—</td><td>Manifest source generator</td></tr>
</table>

<h3>Key metrics</h3>
<table>
<tr><th>Metric</th><th>Value</th></tr>
<tr><td>All first-party + tool <code>&lt;Version&gt;</code></td><td><b>12.1.0</b> (24 csproj unified)</td></tr>
<tr><td>Tests</td><td><b>216/216 PASS</b>, 0 skip, 0 fail</td></tr>
<tr><td>NuGet packages</td><td>18 published to nuget.org</td></tr>
<tr><td>Toolkit.Net skills</td><td>17 skills (plugin v2.0)</td></tr>
</table>

nong ocr local scan.png --ingest --json</code></pre>

<h3>Literature DSL retrieval</h3>
<pre><code>nong lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json
nong lit plan --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --sources openalex,crossref,unpaywall --json
nong lit search --query "SU=('采前'+'采前处理')*('保鲜'+'贮藏')*('果实'+'果品')" --ingest -o refs.json --json</code></pre>

<hr>

<h2>JSON Output Schema</h2>

<pre><code>{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": { "docx": "out.docx" },
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 42, "version": "4.5.0" }
}</code></pre>

<p>Error codes <code>E001</code> through <code>E009</code> cover file-not-found, unsupported format, missing argument, internal error, dependency missing, validation failed, read failed, write failed, and not implemented.</p>

<hr>

<h2>Requirements</h2>

<ul>
  <li>.NET SDK 8.0 or later for development; installed tools can roll forward to current major runtimes.</li>
  <li>Windows is the validated native-rendering path for current Chart, Diagram, and Imaging tool packages.</li>
  <li>ONNX Runtime native DLL is bundled per-platform (17MB nupkg). No separate runtime install needed.</li>
  <li>No JavaScript, npm, Python, pip, Graphviz, or Mermaid runtime is required for core local workflows.</li>
</ul>

<hr>

<h2>License</h2>

<p>Apache-2.0. See <a href="LICENSE">LICENSE</a> for details.</p>

<hr>

<h2>Chinese Documentation</h2>

<p>See <a href="README.zh-CN.md">README.zh-CN.md</a>.</p>
