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

<h2>4.x Modular Line</h2>

<p>Nong.Cli.Net 4.x separates command routing from heavy native dependencies:</p>

<pre><code>nong (Angri450.Nong.Cli, 17MB)
  built in: word / excel / inspect / lit / aminer / metaso / genre / icons / slice / skill / search
  external: chart / diagram / pdf / pptx / ocr / imaging</code></pre>

<table>
  <tr><th>User surface</th><th>Tool command</th><th>PackageId</th><th>Size</th><th>Role</th></tr>
  <tr><td><code>nong</code></td><td><code>nong</code></td><td><code>Angri450.Nong.Cli</code></td><td>17 MB</td><td>Light router, ONNX Runtime, pure .NET built-ins</td></tr>
  <tr><td><code>nong chart ...</code></td><td><code>nong-chart</code></td><td><code>Angri450.Nong.Tool.Chart</code></td><td>26 MB</td><td>Statistics and charts</td></tr>
  <tr><td><code>nong diagram ...</code></td><td><code>nong-diagram</code></td><td><code>Angri450.Nong.Tool.Diagram</code></td><td>26 MB</td><td>Scientific diagrams</td></tr>
  <tr><td><code>nong pdf ...</code></td><td><code>nong-pdf</code></td><td><code>Angri450.Nong.Tool.Pdf</code></td><td>29 MB</td><td>PDF slicing, rendering, images, merge/split/OCR</td></tr>
  <tr><td><code>nong pptx ...</code></td><td><code>nong-pptx</code></td><td><code>Angri450.Nong.Tool.Pptx</code></td><td>11 MB</td><td>PowerPoint read/write and slicing</td></tr>
  <tr><td><code>nong ocr ...</code></td><td><code>nong-ocr</code></td><td><code>Angri450.Nong.Tool.Ocr</code></td><td>~15 MB</td><td>Cloud and local PP-OCRv6 (ONNX Runtime)</td></tr>
  <tr><td><code>nong word images ...</code></td><td><code>nong-imaging</code></td><td><code>Angri450.Nong.Tool.Imaging</code></td><td>26 MB</td><td>Image analysis and crop support</td></tr>
</table>

<hr>

<h2>Capability Overview (v4.5.0, 179 commands)</h2>

<pre><code>nong commands --json
nong commands --format openai-tools</code></pre>

<table>
  <tr><th>Group</th><th>Count</th><th>Notes</th></tr>
  <tr><td><code>word</code></td><td>56</td><td>DOCX creation, repair, formatting, dissect(--ingest), db import/list/block/image</td></tr>
  <tr><td><code>inspect</code></td><td>12</td><td>Paper diagnostics(--ingest) and generation</td></tr>
  <tr><td><code>excel</code></td><td>9</td><td>Read, restructure, create, style, formula, pivot, dissect(--ingest)</td></tr>
  <tr><td><code>lit</code></td><td>11</td><td>CNKI-like DSL parse/validate/plan/search(--ingest)/export + local cache</td></tr>
  <tr><td><code>aminer</code></td><td>22</td><td>Scholar, paper, patent, org, venue — all 22 commands with --ingest</td></tr>
  <tr><td><code>metaso</code></td><td>3</td><td>Search, reader, chat (RAG) — all with --ingest</td></tr>
  <tr><td><code>chart</code></td><td>11</td><td>ANOVA/Duncan + bar, line, scatter, pie, boxplot, histogram, heatmap, radar; analyze --ingest</td></tr>
  <tr><td><code>diagram</code></td><td>3</td><td>Flowchart, network, phylogenetic tree</td></tr>
  <tr><td><code>pdf</code></td><td>13</td><td>Check, dissect(--ingest), render, images, merge, split, OCR, compress, db import/list/block/image</td></tr>
  <tr><td><code>pptx</code></td><td>4</td><td>Read, slides, dissect(--ingest), create</td></tr>
  <tr><td><code>ocr</code></td><td>11</td><td>PP-OCRv6 ONNX local(--ingest), PaddleOCR-VL cloud(--ingest), model install, batch/video/screen/camera</td></tr>
  <tr><td><code>search</code></td><td>1</td><td><strong>Semantic vector search across all ingested documents</strong> (jina-embeddings-v5-omni-nano Q4F16, 263MB)</td></tr>
  <tr><td><code>slice</code></td><td>4</td><td>NongPandoc package inspection</td></tr>
  <tr><td><code>genre</code></td><td>2</td><td>Template listing and inspection</td></tr>
  <tr><td><code>icons</code></td><td>2</td><td>Scientific icon search and inventory</td></tr>
  <tr><td><code>skill</code></td><td>4</td><td>Skill validation, scan, inventory, packaging</td></tr>
  <tr><td><code>nongcli</code></td><td>2</td><td>CLI self-management: init, where</td></tr>
  <tr><td><code>commands</code></td><td>1</td><td>List all commands (--json / --format openai-tools)</td></tr>
</table>

<h3>--ingest: Unified Ingestion (31 commands)</h3>

<p>Every text-producing command supports <code>--ingest</code>. Results are written to NongDb and become searchable via <code>nong search</code>:</p>

<pre><code>nong word dissect paper.docx -o slice --ingest
nong lit search "drought tolerance" --ingest
nong inspect diagnose paper.txt --ingest
nong aminer scholar --name "John Smith" --ingest
nong metaso reader --url "https://..." --ingest
nong ocr local scan.png --ingest
nong chart analyze data.json --ingest
nong search "drought tolerance maize" --limit 5</code></pre>

<hr>

<h2>v4.5.0 Highlights</h2>

<ul>
  <li><strong>nong search</strong> — semantic vector search over all ingested document blocks. Uses jina-embeddings-v5-omni-nano Q4F16 (263MB, from ModelScope). 1511ms cold start.</li>
  <li><strong>--ingest on 31 commands</strong> — word/excel/pdf/pptx dissect, ocr local/cloud, inspect diagnose, lit/aminer/metaso search, metaso reader/chat, aminer 22 commands, chart analyze.</li>
  <li><strong>OCR ONNX unified</strong> — PP-OCRv6 runs on ONNX Runtime. PaddleInference retired. Nong.OcrRuntime repository archived. Models downloaded from ModelScope via git clone. No native runtime packages needed.</li>
  <li><strong>Single inference engine</strong> — embedding search and OCR share <code>Microsoft.ML.OnnxRuntime</code>. Main nupkg is per-platform trimmed (17MB win-x64).</li>
  <li><strong>179 commands</strong> — up from 167 in v4.3.0.</li>
</ul>

<hr>

<h2>Core Workflows</h2>

<h3>Semantic search (new in v4.5.0)</h3>
<pre><code>nong word dissect paper.docx -o slice --ingest
nong search "水稻产量影响因素" --limit 5 --json</code></pre>

<h3>Experiment workbook restructure</h3>
<pre><code>nong excel restructure experiment.spec.json -o experiment.restructured.xlsx --json
nong excel sheets experiment.restructured.xlsx --json</code></pre>

<h3>Excel to statistics to chart</h3>
<pre><code>nong excel to-groups data.xlsx --group A --value B --raw &gt; groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json</code></pre>

<h3>Paper generation and inspection</h3>
<pre><code>nong inspect write-paper spec.json -o paper.docx --json
nong word preview paper.docx --json
nong word format-audit paper.docx --json</code></pre>

<h3>Document package slicing + ingest</h3>
<pre><code>nong word dissect paper.docx -o paper.slice --ingest --json
nong slice inspect paper.slice --strict --json</code></pre>

<h3>PDF one-cut package workflow</h3>
<pre><code>nong pdf check guide.pdf --json
nong pdf dissect guide.pdf --output guide.slice --mode auto --ingest --json</code></pre>

<h3>Local OCR (ONNX Runtime)</h3>
<pre><code>nong ocr models --json
nong ocr install-model pp-ocrv6-medium --json
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
