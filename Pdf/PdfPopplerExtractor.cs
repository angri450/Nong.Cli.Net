using System.Diagnostics;
using System.Globalization;
using System.Xml;

namespace PdfCore;

/// <summary>
/// Poppler pdftotext -bbox-layout extractor. The sole text extraction engine for nong-pdf.
/// Uses Pdftotext via shell-out to produce word-level bounding boxes with correct CJK
/// character grouping and layout-preserving flow/block/line hierarchy.
/// Requires Poppler runtime bundled in Pdf/runtimes/.
/// </summary>
public static class PdfPopplerExtractor
{
    [ThreadStatic] private static List<PopplerLine>? _previousPageLines;

    /// <summary>Extract a PdfDocumentModel using Poppler pdftotext -bbox-layout.</summary>
    public static PdfDocumentModel ExtractTextModel(string pdfPath, PdfCheckResult check)
    {
        _previousPageLines = null;
        var toolPath = ResolveRequired("pdftotext");

        var fullPath = Path.GetFullPath(pdfPath);
        if (!File.Exists(fullPath))
            throw new PdfProcessingException(PdfErrorKind.FileNotFound, $"PDF not found: {pdfPath}");

        var sw = Stopwatch.StartNew();
        try
        {
            var xml = RunPdftotext(toolPath, fullPath);
            var model = ParseBboxLayout(xml, fullPath, check);
            model.Warnings.Add($"Poppler extracted {model.Blocks.Count} blocks in {sw.ElapsedMilliseconds}ms.");
            return model;
        }
        catch (PdfProcessingException) { throw; }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed,
                $"Poppler text extraction failed: {ex.Message}", ex);
        }
    }

    // 鈹€鈹€ Runtime resolution 鈹€鈹€

    static string ResolveRequired(string toolName)
    {
        return PdfNativeRuntime.ResolvePopplerTool(toolName)
            ?? throw new PdfProcessingException(PdfErrorKind.DependencyMissing,
                $"Poppler {toolName} not found. Ensure Poppler runtime is bundled in Pdf/runtimes/<rid>/native/ or installed on PATH.");
    }

    // 鈹€鈹€ Process execution 鈹€鈹€

    static string RunPdftotext(string toolPath, string pdfPath)
    {
        var psi = new ProcessStartInfo(toolPath)
        {
            ArgumentList = { "-bbox-layout", "-enc", "UTF-8", pdfPath, "-" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new PdfProcessingException(PdfErrorKind.InternalError, "Failed to start pdftotext.");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(TimeSpan.FromSeconds(30));

        if (proc.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            throw new PdfProcessingException(PdfErrorKind.ReadFailed,
                $"pdftotext exited with code {proc.ExitCode}.");

        if (string.IsNullOrWhiteSpace(stdout))
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, "pdftotext returned empty output.");

        return stdout;
    }

    // 鈹€鈹€ XHTML parsing 鈹€鈹€

    static PdfDocumentModel ParseBboxLayout(string xml, string pdfPath, PdfCheckResult check)
    {
        var model = new PdfDocumentModel
        {
            Source = new PdfSourceInfo
            {
                Path = Path.GetFileName(pdfPath),
                Sha256 = check.Sha256 ?? PdfUtilities.Sha256(pdfPath),
                PageCount = check.PageCount,
                Classification = check.Classification,
            },
            Warnings = new List<string>(check.Warnings),
        };

        using var reader = new StringReader(xml);
        var doc = new XmlDocument();
        doc.Load(reader);

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("x", "http://www.w3.org/1999/xhtml");

        var pages = doc.SelectNodes("/*[local-name()='html']/*[local-name()='body']/*[local-name()='doc']/*[local-name()='page']", nsmgr);
        if (pages == null || pages.Count == 0)
            return model; // empty document

        int blockIndex = 0;
        int paragraphIndex = 0;
        int headingIndex = 0;

        foreach (XmlNode pageNode in pages)
        {
            double pageW = ParseDouble(pageNode, "width") ?? 612;
            double pageH = ParseDouble(pageNode, "height") ?? 842;
            int pageNum = model.Pages.Count + 1;

            model.Pages.Add(new PdfPageModel
            {
                Page = pageNum,
                Width = pageW,
                Height = pageH,
                Unit = "pt",
                TextCharCount = 0,
                ImageCount = 0,
            });

            var fontSizes = new List<double>();
            var flows = pageNode.SelectNodes("*[local-name()='flow']", nsmgr);

            // First pass: collect sizes.
            if (flows != null)
            {
                foreach (XmlNode flow in flows)
                {
                    var blocks = flow.SelectNodes("*[local-name()='block']", nsmgr) ?? flow.SelectNodes("*[local-name()='block']", nsmgr);
                    if (blocks == null) continue;
                    foreach (XmlNode block in blocks)
                    {
                        var lines = block.SelectNodes("*[local-name()='line']", nsmgr);
                        if (lines == null) continue;
                        foreach (XmlNode line in lines)
                        {
                            var words = line.SelectNodes("*[local-name()='word']", nsmgr);
                            if (words == null || words.Count == 0) continue;
                            double? fs = ParseFontSize(words[0]);
                            if (fs.HasValue) fontSizes.Add(fs.Value);
                        }
                    }
                }
            }

            var headingThreshold = new PopplerHeadingThreshold(fontSizes);
            var lastY = double.NegativeInfinity;

            // ── V7: collect all lines for layout analysis ──
            var allPageLines = new List<PopplerLine>();
            if (flows != null)
            {
                foreach (XmlNode flow in flows)
                {
                    var fblocks = flow.SelectNodes("*[local-name()='block']", nsmgr);
                    if (fblocks == null) continue;
                    foreach (XmlNode blk in fblocks)
                    {
                        var blines = blk.SelectNodes("*[local-name()='line']", nsmgr);
                        if (blines == null) continue;
                        foreach (XmlNode ln in blines)
                        {
                            var wrds = ln.SelectNodes("*[local-name()='word']", nsmgr);
                            if (wrds == null || wrds.Count == 0) continue;
                            var txt = string.Join(" ", wrds.Cast<XmlNode>().Select(w => w.InnerText.Trim()).Where(t => t.Length > 0));
                            if (txt.Length == 0) continue;
                            double? fs = ParseFontSize(wrds[0]);
                            double l = ParseDouble(ln, "xMin") ?? 0;
                            double t = ParseDouble(ln, "yMin") ?? 0;
                            double r = ParseDouble(ln, "xMax") ?? 0;
                            double b = ParseDouble(ln, "yMax") ?? 0;
                            allPageLines.Add(new PopplerLine
                            {
                                X = l, Y = pageH - b, // flip Y to top-down
                                W = r - l, H = b - t,
                                BaselineY = pageH - b + (fs ?? 10),
                                Text = txt, FontSize = fs ?? 10,
                                PageNum = pageNum
                            });
                        }
                    }
                }
            }
            allPageLines = FilterNoiseLines(allPageLines, pageW, pageH);
            var columnSplitX = DetectColumnSplit(allPageLines, pageW);
            if (columnSplitX.HasValue)
                allPageLines = OrderTwoColumns(allPageLines, columnSplitX.Value);
            // Store for page model
            if (columnSplitX.HasValue)
                model.Pages[^1].ColumnSplitX = columnSplitX.Value;

            // ── V7: table detection (skip when a column split was detected:
            //       two-column body text looks identical to a 2-cell table under
            //       the baseline-Y row clustering heuristic, so column detection
            //       must win over table detection on ambiguous pages) ──
            var tableStrings = columnSplitX.HasValue
                ? new List<string>()
                : DetectTableRegions(allPageLines, pageW);

            // Collect the canonical text of every cell consumed by table detection
            // so the main XML-block loop below can skip emitting duplicate paragraph
            // blocks for the same lines. Without this dedup a document gets one
            // "table" block per row AND a duplicate "paragraph"/"heading" block per
            // cell, which breaks downstream consumers expecting one block per region.
            var tableCellTexts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trow in tableStrings)
            {
                model.Blocks.Add(new PdfContentBlock
                {
                    Id = $"t{model.Blocks.Count}",
                    BlockId = $"t{model.Blocks.Count}",
                    Index = blockIndex++,
                    Kind = "table",
                    Page = pageNum,
                    Source = "pdftotext",
                    Text = trow,
                    Confidence = "high",
                });
                // trow is a markdown table row like "| Left column 1 | Right column 1 |"
                // (or "|---|---|" for the separator). Split on the pipe to get cells.
                foreach (var raw in trow.Split('|'))
                {
                    var cell = raw.Trim();
                    // Skip the separator row cells (---) and empties.
                    if (cell.Length == 0 || cell.All(c => c == '-')) continue;
                    tableCellTexts.Add(cell);
                }
            }

            // ── V7: header/footer removal (compare with previous page) ──
            if (pageNum > 1 && _previousPageLines != null)
                RemoveRepeatedHeadersFooters(_previousPageLines, allPageLines, pageH);
            _previousPageLines = allPageLines;
            // ── end V7 layout preprocessing ──

            if (flows == null) continue;
            foreach (XmlNode flow in flows)
            {
                var blocks = flow.SelectNodes("*[local-name()='block']", nsmgr) ?? flow.SelectNodes("*[local-name()='block']", nsmgr);
                if (blocks == null) continue;

                foreach (XmlNode block in blocks)
                {
                    var lines = block.SelectNodes("*[local-name()='line']", nsmgr);
                    if (lines == null || lines.Count == 0) continue;

                    var blockWords = new List<XmlNode>();
                    foreach (XmlNode line in lines)
                    {
                        var words = line.SelectNodes("*[local-name()='word']", nsmgr);
                        if (words != null)
                            foreach (XmlNode w in words)
                                blockWords.Add(w);
                    }

                    if (blockWords.Count == 0) continue;

                    var texts = blockWords.Select(w => w.InnerText.Trim()).Where(t => t.Length > 0).ToList();
                    if (texts.Count == 0) continue;

                    string fullText = string.Join(" ", texts);

                    // V7 dedup: skip blocks whose text exactly matches a table cell
                    // already emitted as part of a table block above. Without this,
                    // every table cell also appears as a duplicate paragraph/heading.
                    if (tableCellTexts.Count > 0 && tableCellTexts.Contains(fullText))
                        continue;

                    double left = double.MaxValue, bottom = double.MaxValue, right = double.MinValue, top = double.MinValue;
                    foreach (XmlNode w in blockWords)
                    {
                        double xMin = ParseDouble(w, "xMin") ?? 0;
                        double yMin = ParseDouble(w, "yMin") ?? 0;
                        double xMax = ParseDouble(w, "xMax") ?? 0;
                        double yMax = ParseDouble(w, "yMax") ?? 0;
                        if (xMin < left) left = xMin;
                        if (yMin < bottom) bottom = yMin;
                        if (xMax > right) right = xMax;
                        if (yMax > top) top = yMax;
                    }

                    var bbox = new[] { Math.Round(left, 3), Math.Round(bottom, 3), Math.Round(right, 3), Math.Round(top, 3) };
                    double fontSize = ParseFontSize(blockWords[0]) ?? 10;
                    string kind = InferKind(block, fullText, blockWords.Count, headingThreshold, pageW, lastY, ref headingIndex, ref paragraphIndex);

                    string id;
                    if (kind == "heading")
                    {
                        headingIndex++;
                        id = $"h{headingIndex:D4}";
                    }
                    else
                    {
                        paragraphIndex++;
                        id = $"p{paragraphIndex:D4}";
                    }
                    lastY = top;

                    model.Blocks.Add(new PdfContentBlock
                    {
                        Id = id,
                        BlockId = id,
                        Index = blockIndex++,
                        Kind = kind,
                        Page = pageNum,
                        Bbox = bbox,
                        Source = "pdftotext",
                        Text = fullText,
                        Runs = BuildRuns(blockWords),
                        Format = new PdfBlockFormat
                        {
                            Size = Math.Round(fontSize, 1),
                            Align = InferAlignment(left, right, pageW),
                        },
                        Confidence = "high",
                    });
                    // V7: compute dynamic confidence (update in-place)
                    if (model.Blocks[^1].Runs.Count > 0)
                    {
                        var quality = PdfTextQuality.AnalyzeRuns(model.Blocks[^1].Runs);
                        var conf = quality.SuspiciousRatio < 0.1 ? "high" :
                                   quality.SuspiciousRatio < 0.3 ? "medium" : "low";
                        model.Blocks[^1].Confidence = conf;
                    }
                }
            }
        }

        return model;
    }

    // 鈹€鈹€ Heading inference 鈹€鈹€

    static string InferKind(XmlNode block, string text, int wordCount, PopplerHeadingThreshold th,
        double pageW, double lastY, ref int hIdx, ref int pIdx)
    {
        var blockLeft = ParseDouble(block, "xMin");
        var blockRight = ParseDouble(block, "xMax");

        // First non-empty block on first page → heading (document title / heading1).
        if (hIdx == 0 && pIdx == 0 && text.Length <= 80)
            return "heading";

        // Numeric prefix (1., 1.1, Chapter, I., A.) → heading
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[\dIVX]+[\.\)]\s") && text.Length <= 100)
            return "heading";

        // Large font relative to median
        var lines = block.SelectNodes("*[local-name()='line']", null);
        if (lines != null && lines.Count > 0)
        {
            var firstWord = lines[0]?.SelectSingleNode("*[local-name()='word']", null)
                          ?? lines[0]?.SelectNodes("*[local-name()='word']", null)?.Item(0);
            if (firstWord != null)
            {
                double? fs = ParseFontSize(firstWord);
                if (fs.HasValue && th.HasVariation && fs.Value > th.Median * 1.2 && text.Length <= 120)
                    return "heading";
            }
        }

        // Centered + short = heading
        if (text.Length <= 60 && blockLeft.HasValue && blockRight.HasValue)
        {
            double center = pageW / 2;
            double blockCenter = (blockLeft.Value + blockRight.Value) / 2;
            if (Math.Abs(blockCenter - center) < pageW * 0.12)
                return "heading";
        }

        return "paragraph";
    }

    // 鈹€鈹€ Helpers 鈹€鈹€

    static List<PdfRun> BuildRuns(List<XmlNode> words)
    {
        var runs = new List<PdfRun>();
        foreach (var w in words)
        {
            var text = w.InnerText.Trim();
            if (text.Length == 0) continue;
            double? fs = ParseFontSize(w);
            runs.Add(new PdfRun
            {
                Text = text,
                Bbox = new[]
                {
                    Math.Round(ParseDouble(w, "xMin") ?? 0, 3),
                    Math.Round(ParseDouble(w, "yMin") ?? 0, 3),
                    Math.Round(ParseDouble(w, "xMax") ?? 0, 3),
                    Math.Round(ParseDouble(w, "yMax") ?? 0, 3),
                },
                Format = new PdfRunFormat { Size = fs.HasValue ? Math.Round(fs.Value, 1) : null, },
            });
        }
        return runs;
    }

    static string InferAlignment(double left, double right, double pageW)
    {
        double center = pageW / 2;
        double blockCenter = (left + right) / 2;
        if (Math.Abs(blockCenter - center) <= pageW * 0.08) return "center";
        if (left <= pageW * 0.15) return "left";
        return "unknown";
    }

    static double? ParseFontSize(XmlNode word)
    {
        if (word.Attributes == null) return null;
        var szAttr = word.Attributes["font-size"] ?? word.Attributes["style"];
        if (szAttr != null)
        {
            var val = szAttr.Value;
            var idx = val.IndexOf("font-size:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var num = val.Substring(idx + 10).Trim();
                num = new string(num.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0)
                    return d;
            }
        }
        return null;
    }

    static double? ParseDouble(XmlNode node, string attrName)
    {
        var attr = node.Attributes?[attrName];
        if (attr == null) return null;
        return double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    struct PopplerHeadingThreshold
    {
        public double Median;
        public bool HasVariation;

        public PopplerHeadingThreshold(List<double> sizes)
        {
            if (sizes.Count == 0) { Median = 0; HasVariation = false; return; }
            var sorted = sizes.OrderBy(s => s).ToList();
            Median = sorted[sorted.Count / 2];
            HasVariation = sorted.Count >= 3 && sorted[^1] > sorted[0] * 1.08;
        }
    }

    // ════════════════════════════════════════════════════════════
    // V7 layout algorithms (ported from PdfPig patterns to Poppler bbox-layout)
    // ════════════════════════════════════════════════════════════

    struct PopplerLine
    {
        public double X, Y, W, H;
        public double BaselineY; // Y + H (bottom of text)
        public string Text;
        public double FontSize;
        public bool IsHeading;
        public int PageNum;
    }

    /// <summary>Detect column split X in a two-column layout. Returns null if single column.</summary>
    static double? DetectColumnSplit(List<PopplerLine> lines, double pageW)
    {
        // Need at least 6 lines to confidently claim two columns (≥3 lines per side
        // is the minimum useful sample; below that, a sparse page looks columnar
        // by accident). Original threshold of 10 rejected valid short two-column
        // pages like the test fixture (4 rows × 2 cols + 1 title = 9 lines).
        if (lines.Count < 6) return null;
        // Find gaps in the center 40% of page
        double center = pageW / 2;
        double leftThird = pageW * 0.30;
        double rightThird = pageW * 0.70;

        // Collect line center X positions
        var centers = lines.Select(l => l.X + l.W / 2).OrderBy(c => c).ToList();
        // Find largest gap in center region
        double bestGap = 0, bestSplit = 0;
        for (int i = 1; i < centers.Count; i++)
        {
            if (centers[i - 1] < leftThird || centers[i] > rightThird) continue;
            double gap = centers[i] - centers[i - 1];
            if (gap > bestGap && gap > pageW * 0.08) // require ≥8% page width gap
            {
                bestGap = gap;
                bestSplit = (centers[i - 1] + centers[i]) / 2;
            }
        }
        return bestGap > 0 ? bestSplit : null;
    }

    /// <summary>Reorder lines for two-column reading: left column top-to-bottom, then right column.</summary>
    static List<PopplerLine> OrderTwoColumns(List<PopplerLine> lines, double splitX)
    {
        var left = new List<PopplerLine>();
        var right = new List<PopplerLine>();
        var spanning = new List<PopplerLine>();
        foreach (var l in lines)
        {
            if (l.X < splitX - 8 && l.X + l.W < splitX + 4)
                left.Add(l);
            else if (l.X > splitX - 4 && l.X + l.W > splitX + 8)
                right.Add(l);
            else
                spanning.Add(l); // spans both columns or centered
        }
        // Sort each column by Y descending (top first)
        left.Sort((a, b) => a.Y.CompareTo(b.Y));
        right.Sort((a, b) => a.Y.CompareTo(b.Y));
        spanning.Sort((a, b) => a.Y.CompareTo(b.Y));

        var result = new List<PopplerLine>();
        result.AddRange(spanning);
        result.AddRange(left);
        result.AddRange(right);
        return result;
    }

    /// <summary>Remove annotation noise: small text at page edges, single characters, etc.</summary>
    static List<PopplerLine> FilterNoiseLines(List<PopplerLine> lines, double pageW, double pageH)
    {
        return lines.Where(l =>
        {
            // Remove tiny text at extreme edges (page numbers, running headers)
            if (l.FontSize < 5) return false;
            if (l.Text.Length <= 1 && (l.Y < 20 || l.Y > pageH - 20)) return false;
            // Remove very wide single chars (watermarks)
            if (l.Text.Length <= 2 && l.W > pageW * 0.6) return false;
            return true;
        }).ToList();
    }

    /// <summary>Detect table regions and convert to markdown table blocks.</summary>
    static List<string> DetectTableRegions(List<PopplerLine> lines, double pageW)
    {
        // Cluster lines into visual rows by baseline Y proximity
        var rows = new List<List<PopplerLine>>();
        foreach (var line in lines.OrderBy(l => l.BaselineY).ThenBy(l => l.X))
        {
            var matched = false;
            foreach (var row in rows)
            {
                if (Math.Abs(row[0].BaselineY - line.BaselineY) < Math.Max(4, row[0].FontSize * 0.6))
                { row.Add(line); matched = true; break; }
            }
            if (!matched) rows.Add(new List<PopplerLine> { line });
        }

        // For each row, build cells by X-clustering
        var tableRows = new List<List<string>>();
        var tableLines = new HashSet<PopplerLine>();
        foreach (var row in rows.OrderBy(r => r[0].BaselineY))
        {
            var sorted = row.OrderBy(l => l.X).ToList();
            var cells = new List<string>();
            var current = new List<PopplerLine>();
            double lastRight = double.MinValue;
            foreach (var l in sorted)
            {
                if (current.Count > 0 && l.X - lastRight > 8)
                {
                    cells.Add(string.Join(" ", current.Select(c => c.Text)));
                    current.Clear();
                }
                current.Add(l);
                lastRight = l.X + l.W;
            }
            if (current.Count > 0) cells.Add(string.Join(" ", current.Select(c => c.Text)));
            if (cells.Count >= 2) { tableRows.Add(cells); foreach (var l in row) tableLines.Add(l); }
        }

        // Require ≥4 rows with consistent column counts to be a table
        if (tableRows.Count < 4) return new List<string>();
        var colCounts = tableRows.Select(r => r.Count).ToList();
        var mostCommon = colCounts.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
        if (mostCommon < 2 || mostCommon > 12) return new List<string>();

        var aligned = tableRows.Where(r => r.Count == mostCommon).ToList();
        if (aligned.Count < 4) return new List<string>();

        // Build markdown table and remove original lines
        lines.RemoveAll(l => tableLines.Contains(l));
        var tableText = new List<string>();
        tableText.Add("| " + string.Join(" | ", aligned[0]) + " |");
        tableText.Add("|" + string.Join("|", aligned[0].Select(_ => "---")) + "|");
        for (int i = 1; i < aligned.Count; i++)
            tableText.Add("| " + string.Join(" | ", aligned[i]) + " |");
        return tableText;
    }

    /// <summary>Remove repeated headers/footers by fingerprinting top/bottom lines.</summary>
    static void RemoveRepeatedHeadersFooters(List<PopplerLine> firstPage, List<PopplerLine> currentPage,
        double pageH, double tolerance = 6)
    {
        if (firstPage.Count < 2 || currentPage.Count < 2) return;
        // Top 15% = header zone, bottom 15% = footer zone
        var firstHeaders = firstPage.Where(l => l.Y < pageH * 0.15).ToList();
        var firstFooters = firstPage.Where(l => l.Y > pageH * 0.85).ToList();
        // Remove current page lines that match by text + position
        if (firstHeaders.Any())
            currentPage.RemoveAll(l =>
                l.Y < pageH * 0.15 &&
                firstHeaders.Any(f => f.Text == l.Text && Math.Abs(f.X - l.X) < tolerance * 2));
        if (firstFooters.Any())
            currentPage.RemoveAll(l =>
                l.Y > pageH * 0.85 &&
                firstFooters.Any(f => f.Text == l.Text && Math.Abs(f.X - l.X) < tolerance * 2));
    }

    /// <summary>Infer block kind (heading/paragraph) with improved heuristics.</summary>
    static string InferLineKind(PopplerLine line, double pageW, double medianFontSize, bool isFirstBlock, int blockIdx)
    {
        // Title page first block → heading
        if (isFirstBlock && blockIdx == 0 && line.Text.Length <= 80)
            return "heading";
        // Large font relative to median
        if (medianFontSize > 0 && line.FontSize > medianFontSize * 1.25 && line.Text.Length <= 120)
            return "heading";
        // Centered short text → heading
        double center = line.X + line.W / 2;
        if (line.Text.Length <= 60 && Math.Abs(center - pageW / 2) < pageW * 0.1)
            return "heading";
        // Numeric prefix (1., 1.1, A., etc) → heading
        if (System.Text.RegularExpressions.Regex.IsMatch(line.Text, @"^[\dA-Za-z]+\.\s"))
            return "heading";
        return "paragraph";
    }
}
