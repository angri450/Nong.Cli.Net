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
    /// <summary>Extract a PdfDocumentModel using Poppler pdftotext -bbox-layout.</summary>
    public static PdfDocumentModel ExtractTextModel(string pdfPath, PdfCheckResult check)
    {
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

        if (hIdx == 0 && pIdx == 0 && text.Length <= 60)
            return "heading";
        if (text.Length <= 60 && wordCount >= 2)
            return "heading";

        var lines = block.SelectNodes("*[local-name()='line']", null);
        if (lines != null && lines.Count > 0)
        {
            var firstWord = lines[0]?.SelectSingleNode("*[local-name()='word']", null) ?? lines[0]?.SelectNodes("*[local-name()='word']", null)?.Item(0);
            if (firstWord != null)
            {
                double? fs = ParseFontSize(firstWord);
                if (fs.HasValue && th.HasVariation && fs.Value > th.Median * 1.2 && text.Length <= 120)
                    return "heading";
            }
        }

        if (text.Length <= 50 && blockLeft.HasValue && blockRight.HasValue)
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
}
