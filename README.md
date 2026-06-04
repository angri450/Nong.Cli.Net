<h1 align="center">
  <a href="https://github.com/angri450/Nong.NET">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MDAiIGhlaWdodD0iMTAwIiB2aWV3Qm94PSIwIDAgNjAwIDEwMCI+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGRvbWluYW50LWJhc2VsaW5lPSJtaWRkbGUiIHRleHQtYW5jaG9yPSJtaWRkbGUiIGZvbnQtZmFtaWx5PSJzZXJpZiIgZm9udC1zaXplPSI1MiIgZmlsbD0iI0ZGRiI+Tm9uZy5ORVQ8L3RleHQ+PC9zdmc+">
      <img alt="Nong.NET" src="data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MDAiIGhlaWdodD0iMTAwIiB2aWV3Qm94PSIwIDAgNjAwIDEwMCI+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGRvbWluYW50LWJhc2VsaW5lPSJtaWRkbGUiIHRleHQtYW5jaG9yPSJtaWRkbGUiIGZvbnQtZmFtaWx5PSJzZXJpZiIgZm9udC1zaXplPSI1MiIgZmlsbD0iIzIyMiI+Tm9uZy5ORVQ8L3RleHQ+PC9zdmc+" height="80">
+  </picture>
+  </a>
+</h1>
+
+<p align="center">
+  <strong>Pure .NET CLI toolkit for scientific document generation and inspection.</strong><br>
+  Zero JavaScript. One binary. 71 commands. Cross-platform.
+</p>
+
+<p align="center">
+  <a href="https://www.nuget.org/packages/Angri450.Nong.Cli/"><img src="https://img.shields.io/nuget/v/Angri450.Nong.Cli.svg?label=NuGet" alt="NuGet"></a>
+  <a href="https://github.com/angri450/Nong.NET/blob/master/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
+  <a href="https://dotnet.microsoft.com/en-us/download"><img src="https://img.shields.io/badge/.NET-8.0-8A2BE2" alt=".NET 8.0"></a>
+  <img src="https://img.shields.io/badge/commands-71-green" alt="71 commands">
+  <a href="#中文文档"><img src="https://img.shields.io/badge/中文-README.zh--CN.md-orange" alt="中文"></a>
+</p>
+
+<hr>
+
+<h2>Quick Install</h2>
+
+<pre><code>dotnet tool install --global Angri450.Nong.Cli
+nong commands --json</code></pre>
+
+<p>That's it. No Node.js, no Python, no Docker. One <code>nong</code> command in your terminal.</p>
+
+<hr>
+
+<h2>What is Nong.NET?</h2>
+
+<p><strong>Nong</strong> (农) is a pure .NET CLI toolkit built for agricultural research papers and scientific document workflows. It replaces fragmented script chains — Word COM automation, Python chart scripts, JavaScript diagram tools — with a single, deterministic, cross-platform binary.</p>
+
+<table>
+  <tr>
+    <td width="50%">
+      <h3>The Model does semantics</h3>
+      <p>Claude / GPT chooses the workflow, prepares JSON specs, and interprets diagnostic results.</p>
+    </td>
+    <td width="50%">
+      <h3>The CLI does deterministic work</h3>
+      <p>Reading, writing, rendering, layout, statistics — all in compiled .NET code. No prompt-based guessing.</p>
+    </td>
+  </tr>
+</table>
+
+<hr>
+
+<h2>Capability Overview — 71 Commands</h2>
+
+<h3>word — Word Document Engine (30 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong word read</code></td><td>Extract plain text from .docx</td></tr>
+  <tr><td><code>nong word preview</code></td><td>7-step diagnostic report</td></tr>
+  <tr><td><code>nong word fill</code></td><td>Template fill from .docx + .json</td></tr>
+  <tr><td><code>nong word rebuild</code></td><td>Style cleanup and normalization</td></tr>
+  <tr><td><code>nong word stats</code></td><td>Count paragraphs, tables, images, footnotes</td></tr>
+  <tr><td><code>nong word fonts</code></td><td>List all fonts used in document</td></tr>
+  <tr><td><code>nong word styles</code></td><td>List all style definitions</td></tr>
+  <tr><td><code>nong word validate</code></td><td>OOXML schema validation</td></tr>
+  <tr><td><code>nong word extract</code></td><td>Extract embedded images</td></tr>
+  <tr><td><code>nong word dissect</code></td><td>Format fingerprint aggregation (nongmark/v1)</td></tr>
+  <tr><td><code>nong word merge</code></td><td>Merge multiple .docx into one</td></tr>
+  <tr><td><code>nong word outline</code></td><td>Extract document outline/headings</td></tr>
+  <tr><td><code>nong word images</code></td><td>List or extract all images</td></tr>
+  <tr><td><code>nong word comments</code></td><td>Read all comments</td></tr>
+  <tr><td><code>nong word revisions</code></td><td>List tracked revisions</td></tr>
+  <tr><td><code>nong word infer-format</code></td><td>Infer format from Chinese description</td></tr>
+  <tr><td><code>nong word fix-order</code></td><td>Fix OOXML element ordering</td></tr>
+  <tr><td><code>nong word protect</code></td><td>Document protection (readonly, comments, track-changes)</td></tr>
+  <tr><td><code>nong word embed-font</code></td><td>Embed TrueType font into document</td></tr>
+  <tr><td><code>nong word add paragraph</code></td><td>Append paragraph from JSON spec</td></tr>
+  <tr><td><code>nong word add table</code></td><td>Append table from JSON spec</td></tr>
+  <tr><td><code>nong word add footnote</code></td><td>Append footnote</td></tr>
+  <tr><td><code>nong word add endnote</code></td><td>Append endnote</td></tr>
+  <tr><td><code>nong word add image</code></td><td>Append image with optional caption</td></tr>
+  <tr><td><code>nong word add toc</code></td><td>Insert table of contents</td></tr>
+  <tr><td><code>nong word add xref</code></td><td>Insert cross-reference to bookmark</td></tr>
+  <tr><td><code>nong word add link</code></td><td>Insert hyperlink</td></tr>
+  <tr><td><code>nong word add bookmark</code></td><td>Insert bookmark</td></tr>
+  <tr><td><code>nong word add comment</code></td><td>Insert comment</td></tr>
+  <tr><td><code>nong word add math</code></td><td>Insert math equation from LaTeX</td></tr>
+</table>
+
+<h3>inspect — Paper Diagnostics &amp; Generation (10 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong inspect diagnose</code></td><td>Full paper diagnostic report</td></tr>
+  <tr><td><code>nong inspect refs</code></td><td>Reference list check</td></tr>
+  <tr><td><code>nong inspect write-paper</code></td><td>Generate paper .docx from JSON spec</td></tr>
+  <tr><td><code>nong inspect classify</code></td><td>Classify paper type (16 types)</td></tr>
+  <tr><td><code>nong inspect structure</code></td><td>Extract paper structure</td></tr>
+  <tr><td><code>nong inspect evidence</code></td><td>Evidence chain diagnosis</td></tr>
+  <tr><td><code>nong inspect data-req</code></td><td>Data requirements diagnosis</td></tr>
+  <tr><td><code>nong inspect gap</code></td><td>Gap grade assessment</td></tr>
+  <tr><td><code>nong inspect varplan</code></td><td>Variable operationalization plan</td></tr>
+  <tr><td><code>nong inspect semantics</code></td><td>Semantic/logic risk diagnosis</td></tr>
+</table>
+
+<h3>chart — Statistics &amp; Charts (7 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong chart analyze</code></td><td>ANOVA + Duncan MRT + descriptive statistics</td></tr>
+  <tr><td><code>nong chart anova</code></td><td>One-way ANOVA</td></tr>
+  <tr><td><code>nong chart duncan</code></td><td>Duncan multiple range test</td></tr>
+  <tr><td><code>nong chart bar</code></td><td>Bar chart with error bars + significance letters</td></tr>
+  <tr><td><code>nong chart line</code></td><td>Multi-series line chart</td></tr>
+  <tr><td><code>nong chart scatter</code></td><td>Scatter plot with optional trendline</td></tr>
+  <tr><td><code>nong chart pie</code></td><td>Pie chart</td></tr>
+</table>
+
+<p>Charts are rendered with <strong>ScottPlot</strong>. Statistical analysis uses simplified Q-value approximations — verify with formal tools for publication.</p>
+
+<h3>excel — Excel Data Entry (4 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong excel sheets</code></td><td>List worksheets</td></tr>
+  <tr><td><code>nong excel read</code></td><td>Read cell content</td></tr>
+  <tr><td><code>nong excel to-groups</code></td><td>Convert treatment/value columns to grouped JSON</td></tr>
+  <tr><td><code>nong excel create</code></td><td>Create .xlsx from JSON spec</td></tr>
+</table>
+
+<h3>diagram — Scientific Diagrams (3 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong diagram flowchart</code></td><td>Flowchart from JSON spec (Sugiyama layout)</td></tr>
+  <tr><td><code>nong diagram network</code></td><td>Network/relationship graph (force-directed)</td></tr>
+  <tr><td><code>nong diagram tree</code></td><td>Phylogenetic tree (Newick/JSON input)</td></tr>
+</table>
+
+<p>Rendered with <strong>MSAGL</strong> (automatic layout) + <strong>SkiaSharp</strong> (rasterization). No Graphviz, no Mermaid, no JavaScript.</p>
+
+<h3>pptx — Slide Reading (2 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong pptx read</code></td><td>Extract all slide text</td></tr>
+  <tr><td><code>nong pptx slides</code></td><td>Count shapes/elements per slide</td></tr>
+</table>
+
+<h3>ocr — Optical Character Recognition (7 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong ocr cloud</code></td><td>PaddleOCR-VL cloud OCR</td></tr>
+  <tr><td><code>nong ocr local</code></td><td>Local PP-OCRv5 recognition (E005/E009 — honest)</td></tr>
+  <tr><td><code>nong ocr check-env</code></td><td>Check OCR environment status</td></tr>
+  <tr><td><code>nong ocr analyze-image</code></td><td>Image structure analysis (no token needed)</td></tr>
+  <tr><td><code>nong ocr models</code></td><td>List available OCR models</td></tr>
+  <tr><td><code>nong ocr install-model</code></td><td>Install OCR model</td></tr>
+  <tr><td><code>nong ocr to-word</code></td><td>Cloud OCR → .docx conversion</td></tr>
+</table>
+
+<h3>genre / icons — Templates &amp; Assets (4 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong genre list</code></td><td>List available writing templates</td></tr>
+  <tr><td><code>nong genre show</code></td><td>View template content</td></tr>
+  <tr><td><code>nong icons list</code></td><td>List 40 scientific SVG icons</td></tr>
+  <tr><td><code>nong icons search</code></td><td>Search icons by keyword</td></tr>
+</table>
+
+<h3>skill — Skill Lifecycle Management (4 commands)</h3>
+
+<table>
+  <tr><th>Command</th><th>What it does</th></tr>
+  <tr><td><code>nong skill validate</code></td><td>Validate SKILL.md structure and references</td></tr>
+  <tr><td><code>nong skill scan</code></td><td>Security scan on skill/plugin directory</td></tr>
+  <tr><td><code>nong skill inventory</code></td><td>List directory contents (single skill or plugin root)</td></tr>
+  <tr><td><code>nong skill package</code></td><td>Validate + scan + package into .zip</td></tr>
+</table>
+
+<hr>
+
+<h2>Design Principles</h2>
+
+<table>
+  <tr>
+    <td><strong>1. Models handle semantics</strong></td>
+    <td>AI models choose workflows, prepare JSON specs, and interpret diagnostic results. The CLI never guesses.</td>
+  </tr>
+  <tr>
+    <td><strong>2. Deterministic work stays in .NET</strong></td>
+    <td>All reading, writing, rendering, layout, and statistics are compiled C# code. Reproducible every time.</td>
+  </tr>
+  <tr>
+    <td><strong>3. JSON-first output</strong></td>
+    <td>Every command supports <code>--json</code> for machine-readable output. Designed for AI agent consumption and shell pipelines.</td>
+  </tr>
+  <tr>
+    <td><strong>4. Unified error codes</strong></td>
+    <td>E001 through E009 cover every failure mode. Scripts and agents get predictable, parseable errors.</td>
+  </tr>
+  <tr>
+    <td><strong>5. Zero JavaScript forever</strong></td>
+    <td>No npm, no webpack, no Node.js. The entire stack is C# from parsing to rendering.</td>
+  </tr>
+</table>
+
+<hr>
+
+<h2>Core Workflows</h2>
+
+<h3>1. Excel &rarr; Statistics &rarr; Chart</h3>
+<pre><code>nong excel to-groups data.xlsx --group A --value B --raw &gt; groups.json
+nong chart analyze groups.json --json
+nong chart bar groups.json -o fig.png --json</code></pre>
+
+<h3>2. Paper Generation &rarr; Inspection</h3>
+<pre><code>nong inspect write-paper spec.json -o paper.docx --json
+nong word preview paper.docx --json
+nong word read paper.docx --json</code></pre>
+
+<h3>3. Paper Diagnostics</h3>
+<pre><code>nong word read paper.docx &gt; paper.txt
+nong inspect diagnose paper.txt --json
+nong inspect refs paper.txt --json</code></pre>
+
+<h3>4. Document Forensics</h3>
+<pre><code>nong word stats paper.docx --json
+nong word fonts paper.docx --json
+nong word dissect paper.docx -o paper.slice --json</code></pre>
+
+<h3>5. Cloud OCR Pipeline</h3>
+<pre><code>nong ocr check-env --json
+nong ocr cloud scan.png -o ocr-out/ --json
+nong ocr to-word scan.png -o out.docx --json</code></pre>
+
+<hr>
+
+<h2>JSON Output Schema</h2>
+
+<p>Every command with <code>--json</code> returns a unified structure:</p>
+
+<pre><code>{
+  "status": "ok" | "error",
+  "command": "word read",
+  "summary": "...",
+  "data": {},
+  "issues": [],
+  "artifacts": { "png": "fig.png" },
+  "metrics": { "paragraphs": 29 },
+  "errors": [],
+  "meta": { "durationMs": 42, "version": "3.2.0" }
+}</code></pre>
+
+<hr>
+
+<h2>Error Codes</h2>
+
+<table>
+  <tr><th>Code</th><th>Name</th><th>Meaning</th></tr>
+  <tr><td><code>E001</code></td><td>file_not_found</td><td>File not found</td></tr>
+  <tr><td><code>E002</code></td><td>unsupported_format</td><td>Wrong extension or unsupported format</td></tr>
+  <tr><td><code>E003</code></td><td>missing_argument</td><td>Required argument missing</td></tr>
+  <tr><td><code>E004</code></td><td>internal_error</td><td>Unexpected crash</td></tr>
+  <tr><td><code>E005</code></td><td>dependency_missing</td><td>Tool or token not installed</td></tr>
+  <tr><td><code>E006</code></td><td>validation_failed</td><td>Bad input or schema violation</td></tr>
+  <tr><td><code>E007</code></td><td>read_failed</td><td>Cannot read file</td></tr>
+  <tr><td><code>E008</code></td><td>write_failed</td><td>Cannot write file</td></tr>
+  <tr><td><code>E009</code></td><td>not_implemented</td><td>Command not implemented yet</td></tr>
+</table>
+
+<hr>
+
+<h2>Project Structure — 9 NuGet Packages</h2>
+
+<p>All packages share the version <strong>3.2.0</strong>. Each is a purpose-built library with a single responsibility.</p>
+
+<table>
+  <tr><th>Package</th><th>Purpose</th></tr>
+  <tr><td><code>Angri450.Nong.ThirdParty</code></td><td><strong>Foundation</strong> — 15 inlined open-source libraries compiled as a single DLL</td></tr>
+  <tr><td><code>Angri450.Nong.Docx</code></td><td>Word generation, template fill, paper diagnostics</td></tr>
+  <tr><td><code>Angri450.Nong.Excel</code></td><td>Chainable Excel generation API with formula validation</td></tr>
+  <tr><td><code>Angri450.Nong.Chart</code></td><td>18 chart types + ANOVA/Duncan MRT statistical analysis</td></tr>
+  <tr><td><code>Angri450.Nong.Diagram</code></td><td>Flowchart, network graph, phylogenetic tree rendering</td></tr>
+  <tr><td><code>Angri450.Nong.Pptx</code></td><td>PowerPoint generation with 10 theme presets</td></tr>
+  <tr><td><code>Angri450.Nong.MultiModal</code></td><td>PaddleOCR cloud + local OCR integration</td></tr>
+  <tr><td><code>Angri450.Nong.Bioicons</code></td><td>40 SVG scientific icons</td></tr>
+  <tr><td><code>Angri450.Nong.Skill.Manager</code></td><td>Skill lifecycle management CLI</td></tr>
+</table>
+
+<hr>
+
+<h2>Inlined Third-Party Libraries</h2>
+
+<table>
+  <tr><th>Library</th><th>License</th><th>Use</th></tr>
+  <tr><td>ClosedXML</td><td>MIT</td><td>Excel read/write</td></tr>
+  <tr><td>DocumentFormat.OpenXml</td><td>MIT</td><td>Word/PPTX OOXML handling</td></tr>
+  <tr><td>ScottPlot</td><td>MIT</td><td>Chart rendering</td></tr>
+  <tr><td>MSAGL</td><td>MIT</td><td>Automatic graph layout</td></tr>
+  <tr><td>SkiaSharp</td><td>MIT</td><td>2D graphics rasterization</td></tr>
+  <tr><td>HarfBuzzSharp</td><td>MIT</td><td>Text shaping</td></tr>
+  <tr><td>SixLabors.Fonts</td><td>Apache-2.0</td><td>Font loading and measurement</td></tr>
+</table>
+
+<hr>
+
+<h2>Requirements</h2>
+
+<ul>
+  <li><strong>.NET SDK 8.0</strong> or later (forward-compatible with 9.0, 10.0, 11.0)</li>
+  <li>Windows, macOS, or Linux</li>
+  <li>Native SkiaSharp/HarfBuzzSharp binaries (auto-installed via NuGet)</li>
+</ul>
+
+<hr>
+
+<h2>License</h2>
+
+<p>Apache-2.0. See <a href="LICENSE">LICENSE</a> for details.</p>
+
+<hr>
+
+<h2>中文文档</h2>
+
+<p>See <a href="README.zh-CN.md">README.zh-CN.md</a> for full Chinese documentation.</p>
+