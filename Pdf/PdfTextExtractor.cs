using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfCore;

public static class PdfTextExtractor
{
    public static PdfDocumentModel ExtractTextModel(string pdfPath, PdfCheckResult check)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        var fullPath = Path.GetFullPath(pdfPath);

        try
        {
            using var document = PdfDocument.Open(fullPath);
            var model = new PdfDocumentModel
            {
                Source = new PdfSourceInfo
                {
                    Path = Path.GetFileName(pdfPath),
                    Sha256 = check.Sha256 ?? PdfUtilities.Sha256(fullPath),
                    PageCount = document.NumberOfPages,
                    Classification = check.Classification,
                },
                Warnings = new List<string>(check.Warnings),
            };

            var pages = new List<PdfPageExtraction>();
            foreach (var page in document.GetPages())
            {
                var words = page.GetWords()
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .OrderByDescending(w => w.BoundingBox.Bottom)
                    .ThenBy(w => w.BoundingBox.Left)
                    .ToList();
                var quality = PdfTextQuality.AnalyzeWords(words);
                var lines = BuildLineGroups(page, words, quality);

                if (quality.SuspectFonts.Count > 0)
                {
                    model.Warnings.Add($"Page {page.Number}: possible custom-encoded font(s): {string.Join(", ", quality.SuspectFonts.Take(6))}.");
                }

                if (lines.Count > 0 && lines.Count(l => l.IsGibberish) / (double)lines.Count > 0.60)
                {
                    model.Warnings.Add($"Page {page.Number}: most extracted text looks suspicious; verify with pdf render or OCR.");
                }

                pages.Add(new PdfPageExtraction
                {
                    Page = page.Number,
                    Width = page.Width,
                    Height = page.Height,
                    TextCharCount = PdfDocumentInspector.CountMeaningfulChars(page.Text),
                    ImageCount = page.NumberOfImages,
                    Lines = lines,
                });
            }

            var repeatedHeaderFooter = BuildRepeatedHeaderFooterFingerprints(pages);
            var skippedRepeatingLines = 0;
            var contentIndex = 0;
            var pageBreakIndex = 0;
            var headingIndex = 0;
            var paragraphIndex = 0;
            var tableIndex = 0;

            foreach (var page in pages)
            {
                var pageLines = page.Lines
                    .Where(line =>
                    {
                        if (!ShouldSkipRepeatedHeaderFooter(line, page, repeatedHeaderFooter))
                            return true;

                        skippedRepeatingLines++;
                        return false;
                    })
                    .ToList();

                page.Lines = pageLines;
                var orderedLines = OrderLinesForReading(page);
                model.Pages.Add(new PdfPageModel
                {
                    Page = page.Page,
                    Width = page.Width,
                    Height = page.Height,
                    TextCharCount = page.TextCharCount,
                    ImageCount = page.ImageCount,
                    ReadingOrderMethod = page.ReadingOrderMethod,
                    ColumnSplitX = page.ColumnSplitX,
                });

                if (page.Page > 1)
                {
                    pageBreakIndex++;
                    contentIndex++;
                    model.Blocks.Add(new PdfContentBlock
                    {
                        Id = $"pb{pageBreakIndex:D4}",
                        BlockId = $"pb{pageBreakIndex:D4}",
                        Index = contentIndex - 1,
                        Kind = "pageBreak",
                        Page = page.Page,
                        Bbox = [0, 0, page.Width, page.Height],
                        Source = "inferred",
                        Confidence = "high",
                    });
                }

                var tables = DetectTables(orderedLines, page.Width, page.ColumnSplitX).ToList();
                var tableByFirstLine = tables.ToDictionary(t => t.FirstLine);
                var tableLines = tables.SelectMany(t => t.Lines).ToHashSet();
                var headingThresholds = HeadingThresholds.From(orderedLines.Where(l => !tableLines.Contains(l)));

                foreach (var line in orderedLines)
                {
                    if (tableByFirstLine.TryGetValue(line, out var table))
                    {
                        tableIndex++;
                        contentIndex++;
                        var tableId = $"t{tableIndex:D4}";
                        model.Blocks.Add(new PdfContentBlock
                        {
                            Id = tableId,
                            BlockId = tableId,
                            Index = contentIndex - 1,
                            Kind = "table",
                            Page = page.Page,
                            Bbox = ToBbox(table.BoundingBox),
                            Source = "pdfTableHeuristic",
                            Text = table.Markdown,
                            Format = new PdfBlockFormat
                            {
                                Align = InferAlignment(table.BoundingBox, page.Width),
                            },
                            Confidence = "medium",
                        });
                        continue;
                    }

                    if (tableLines.Contains(line))
                        continue;

                    var text = line.Text;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var kind = InferKind(line, text, paragraphIndex + headingIndex, headingThresholds);
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

                    contentIndex++;
                    var block = new PdfContentBlock
                    {
                        Id = id,
                        BlockId = id,
                        Index = contentIndex - 1,
                        Kind = kind,
                        Page = page.Page,
                        Bbox = ToBbox(line.BoundingBox),
                        Source = "pdfText",
                        Text = text,
                        Runs = BuildRuns(line.Words),
                        Format = new PdfBlockFormat
                        {
                            Font = line.Font,
                            Size = line.FontSize,
                            Align = InferAlignment(line.BoundingBox, page.Width),
                        },
                        Confidence = line.IsGibberish ? "low" : "medium",
                    };
                    if (line.IsGibberish)
                        block.Warnings.Add("Extracted text looks suspicious; verify with rendered page or OCR.");
                    model.Blocks.Add(block);
                }
            }

            if (skippedRepeatingLines > 0)
            {
                model.Warnings.Add($"Removed {skippedRepeatingLines} repeated header/footer line(s) from the PDF text stream.");
            }

            if (model.Blocks.All(b => b.Kind == "pageBreak"))
            {
                model.Warnings.Add("No extractable text blocks were found. Use --mode ocr when local OCR runtime is installed, or use ocr cloud/to-word when a cloud key exists.");
            }

            return model;
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to extract PDF text: {ex.Message}", ex);
        }
    }

    static List<PdfLineGroup> BuildLineGroups(Page page, List<Word> words, PdfTextQualitySummary quality)
    {
        var baselineGroups = new List<PdfLineGroup>();
        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;
            var tolerance = Math.Max(2.0, MedianLetterSize(word) * 0.45);
            var line = baselineGroups.FirstOrDefault(l => Math.Abs(l.BaselineY - y) <= tolerance);
            if (line == null)
            {
                line = new PdfLineGroup { BaselineY = y };
                baselineGroups.Add(line);
            }

            line.Words.Add(word);
        }

        var fragments = new List<PdfLineGroup>();
        foreach (var baseline in baselineGroups)
        {
            baseline.Words = baseline.Words.OrderBy(w => w.BoundingBox.Left).ToList();
            foreach (var wordsInFragment in SplitWordsByLargeGaps(baseline.Words, page.Width))
            {
                var fragment = new PdfLineGroup
                {
                    BaselineY = baseline.BaselineY,
                    Words = wordsInFragment,
                };
                FinalizeLine(fragment, quality);
                fragments.Add(fragment);
            }
        }

        return fragments
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .OrderByDescending(l => l.BoundingBox.Top)
            .ThenBy(l => l.BoundingBox.Left)
            .ToList();
    }

    static IEnumerable<List<Word>> SplitWordsByLargeGaps(List<Word> words, double pageWidth)
    {
        if (words.Count <= 1)
        {
            yield return words;
            yield break;
        }

        var medianSize = words
            .Select(MedianLetterSize)
            .OrderBy(s => s)
            .ElementAt(words.Count / 2);
        var threshold = Math.Max(medianSize * 3.0, pageWidth * 0.10);
        var current = new List<Word> { words[0] };

        for (var i = 1; i < words.Count; i++)
        {
            var previous = words[i - 1];
            var word = words[i];
            var gap = word.BoundingBox.Left - previous.BoundingBox.Right;
            if (gap > threshold)
            {
                yield return current;
                current = new List<Word>();
            }
            current.Add(word);
        }

        yield return current;
    }

    static void FinalizeLine(PdfLineGroup line, PdfTextQualitySummary quality)
    {
        line.Words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();
        line.BoundingBox = Union(line.Words.Select(w => w.BoundingBox));
        line.Text = PdfUtilities.SanitizeText(string.Join(" ", line.Words.Select(w => w.Text)));
        line.Font = MostCommonFont(line.Words);
        line.FontSize = MedianPointSize(line.Words);
        line.IsBold = line.Words.Any(w => LooksBold(w.FontName));
        line.IsItalic = line.Words.Any(w => LooksItalic(w.FontName));
        line.GibberishScore = PdfTextQuality.ScoreText(line.Text, line.Font, quality.SuspectFonts);
        line.IsGibberish = line.GibberishScore > 0.25;
    }

    static HashSet<string> BuildRepeatedHeaderFooterFingerprints(List<PdfPageExtraction> pages)
    {
        if (pages.Count < 3)
            return new HashSet<string>(StringComparer.Ordinal);

        var pageNumbersByFingerprint = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var line in page.Lines)
            {
                var fp = Fingerprint(line.Text);
                if (fp.Length is < 3 or > 120)
                    continue;

                if (!pageNumbersByFingerprint.TryGetValue(fp, out var pageNumbers))
                {
                    pageNumbers = new HashSet<int>();
                    pageNumbersByFingerprint[fp] = pageNumbers;
                }
                pageNumbers.Add(page.Page);
            }
        }

        var threshold = Math.Max(2, pages.Count / 2);
        return pageNumbersByFingerprint
            .Where(kvp => kvp.Value.Count >= threshold)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    static bool ShouldSkipRepeatedHeaderFooter(PdfLineGroup line, PdfPageExtraction page, HashSet<string> repeated)
    {
        if (repeated.Count == 0 || !repeated.Contains(Fingerprint(line.Text)))
            return false;

        return line.BoundingBox.Top >= page.Height * 0.88 || line.BoundingBox.Bottom <= page.Height * 0.12;
    }

    static string Fingerprint(string text)
    {
        var parts = text
            .Trim()
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    static List<PdfLineGroup> OrderLinesForReading(PdfPageExtraction page)
    {
        var candidates = page.Lines
            .Where(l => l.BoundingBox.Width < page.Width * 0.62)
            .ToList();
        var split = DetectColumnSplit(candidates, page.Width);
        if (split == null)
        {
            page.ReadingOrderMethod = "single-column-y-desc-x-asc";
            return page.Lines
                .OrderByDescending(l => l.BoundingBox.Top)
                .ThenBy(l => l.BoundingBox.Left)
                .ToList();
        }

        page.ReadingOrderMethod = "two-column-left-then-right";
        page.ColumnSplitX = Math.Round(split.Value, 3);
        return OrderTwoColumnLines(page.Lines, split.Value, page.Width);
    }

    static double? DetectColumnSplit(List<PdfLineGroup> lines, double pageWidth)
    {
        if (lines.Count < 6)
            return null;

        var leftClusters = BuildLeftEdgeClusters(lines, pageWidth)
            .Where(c => c.Count >= 3)
            .OrderBy(c => c.Center)
            .ToList();
        if (leftClusters.Count >= 2)
        {
            var left = leftClusters.First();
            var right = leftClusters.Last();
            if (right.Center - left.Center >= pageWidth * 0.18)
                return (left.Center + right.Center) / 2.0;
        }

        var xs = lines.Select(l => l.XMid).OrderBy(x => x).ToList();
        var minGap = pageWidth * 0.10;
        double? bestSplit = null;
        var bestGap = 0d;
        for (var i = 1; i < xs.Count; i++)
        {
            var gap = xs[i] - xs[i - 1];
            if (gap <= minGap || gap <= bestGap)
                continue;

            var leftCount = xs.Take(i).Count();
            var rightCount = xs.Skip(i).Count();
            if (leftCount < 3 || rightCount < 3)
                continue;

            bestGap = gap;
            bestSplit = (xs[i - 1] + xs[i]) / 2.0;
        }

        return bestSplit;
    }

    static List<ColumnCluster> BuildLeftEdgeClusters(List<PdfLineGroup> lines, double pageWidth)
    {
        var tolerance = Math.Max(18, pageWidth * 0.04);
        var clusters = new List<List<double>>();
        foreach (var x in lines.Select(l => l.BoundingBox.Left).OrderBy(x => x))
        {
            var cluster = clusters.LastOrDefault();
            if (cluster != null && x - cluster.Last() <= tolerance)
                cluster.Add(x);
            else
                clusters.Add(new List<double> { x });
        }

        return clusters
            .Select(c => new ColumnCluster(c.Average(), c.Count))
            .ToList();
    }

    static List<PdfLineGroup> OrderTwoColumnLines(List<PdfLineGroup> lines, double splitX, double pageWidth)
    {
        var remaining = lines.ToHashSet();
        var fullWidth = lines
            .Where(l => IsFullWidthLine(l, splitX, pageWidth))
            .OrderByDescending(l => l.BoundingBox.Top)
            .ThenBy(l => l.BoundingBox.Left)
            .ToList();

        var ordered = new List<PdfLineGroup>();
        var upperTop = double.PositiveInfinity;

        void AddColumnSegment(IEnumerable<PdfLineGroup> segment)
        {
            var list = segment.Where(remaining.Contains).ToList();
            var left = list
                .Where(l => l.XMid <= splitX)
                .OrderByDescending(l => l.BoundingBox.Top)
                .ThenBy(l => l.BoundingBox.Left);
            var right = list
                .Where(l => l.XMid > splitX)
                .OrderByDescending(l => l.BoundingBox.Top)
                .ThenBy(l => l.BoundingBox.Left);

            foreach (var line in left.Concat(right))
            {
                if (remaining.Remove(line))
                    ordered.Add(line);
            }
        }

        foreach (var line in fullWidth)
        {
            AddColumnSegment(lines.Where(l => l.BoundingBox.Top < upperTop && l.BoundingBox.Top > line.BoundingBox.Top));
            if (remaining.Remove(line))
                ordered.Add(line);
            upperTop = line.BoundingBox.Top;
        }

        AddColumnSegment(lines.Where(l => l.BoundingBox.Top < upperTop));
        return ordered;
    }

    static bool IsFullWidthLine(PdfLineGroup line, double splitX, double pageWidth)
    {
        var crossesSplit = line.BoundingBox.Left < splitX - 4 && line.BoundingBox.Right > splitX + 4;
        var wide = line.BoundingBox.Width > pageWidth * 0.60;
        var centeredHeading = Math.Abs(line.XMid - splitX) < pageWidth * 0.08 && line.BoundingBox.Width > pageWidth * 0.25;
        return crossesSplit || wide || centeredHeading;
    }

    static IEnumerable<PdfTableRegion> DetectTables(List<PdfLineGroup> orderedLines, double pageWidth, double? columnSplitX)
    {
        var visualRows = BuildVisualRows(orderedLines);
        var rows = visualRows
            .Select(row => new TableRowCandidate(row.OrderBy(l => l.BoundingBox.Left).First(), row, BuildTableCells(row, columnSplitX)))
            .ToList();

        var i = 0;
        while (i < rows.Count)
        {
            if (rows[i].Cells == null)
            {
                i++;
                continue;
            }

            var j = i + 1;
            while (j < rows.Count && rows[j].Cells != null)
                j++;

            if (j - i >= 4 && TryBuildTable(rows.GetRange(i, j - i), pageWidth, out var table))
                yield return table;

            i = Math.Max(j, i + 1);
        }
    }

    static List<List<PdfLineGroup>> BuildVisualRows(List<PdfLineGroup> lines)
    {
        var rows = new List<List<PdfLineGroup>>();
        foreach (var line in lines.OrderByDescending(l => l.BoundingBox.Top).ThenBy(l => l.BoundingBox.Left))
        {
            var row = rows.FirstOrDefault(r =>
            {
                var baseline = r[0].BaselineY;
                var tolerance = Math.Max(3, Math.Max(r[0].BoundingBox.Height, line.BoundingBox.Height) * 0.45);
                return Math.Abs(baseline - line.BaselineY) <= tolerance;
            });
            if (row == null)
            {
                row = new List<PdfLineGroup>();
                rows.Add(row);
            }
            row.Add(line);
        }

        foreach (var row in rows)
            row.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

        return rows
            .OrderByDescending(r => r.Max(l => l.BoundingBox.Top))
            .ToList();
    }

    static List<PdfTableCell>? BuildTableCells(List<PdfLineGroup> row, double? columnSplitX)
    {
        if (row.Count < 2)
            return null;

        if (columnSplitX is { } split && row.Count == 2)
        {
            var ordered = row.OrderBy(l => l.BoundingBox.Left).ToList();
            if (ordered[0].XMid < split && ordered[1].XMid > split)
                return null;
        }

        var cells = row
            .OrderBy(l => l.BoundingBox.Left)
            .Select(line => new PdfTableCell
            {
                Left = line.BoundingBox.Left,
                Right = line.BoundingBox.Right,
                Text = line.Text,
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Text))
            .ToList();
        return cells.Count >= 2 ? cells : null;
    }

    static bool TryBuildTable(List<TableRowCandidate> candidates, double pageWidth, out PdfTableRegion table)
    {
        table = default!;
        var rows = candidates
            .Where(c => c.Cells is { Count: >= 2 })
            .Select(c => c.Cells!)
            .ToList();
        if (rows.Count < 4)
            return false;

        var bands = BuildColumnBands(rows, pageWidth);
        if (bands.Count is < 2 or > 12)
            return false;

        var slotRows = rows.Select(r => RowSlots(r, bands)).ToList();
        var alignedRows = slotRows.Count(r => r.Count(s => !string.IsNullOrWhiteSpace(s)) >= Math.Min(2, bands.Count));
        if (alignedRows < 3)
            return false;

        var nCols = bands.Count;
        var markdown = BuildMarkdownTable(slotRows, nCols);
        table = new PdfTableRegion
        {
            FirstLine = candidates[0].FirstLine,
            Lines = candidates.SelectMany(c => c.Lines).ToList(),
            BoundingBox = Union(candidates.SelectMany(c => c.Lines).Select(l => l.BoundingBox)),
            Markdown = markdown,
        };
        return true;
    }

    static List<ColumnBand> BuildColumnBands(List<List<PdfTableCell>> rows, double pageWidth)
    {
        var tolerance = Math.Max(12, pageWidth * 0.015);
        var xs = rows.SelectMany(r => r.Select(c => c.Left)).OrderBy(x => x).ToList();
        var groups = new List<List<double>>();
        foreach (var x in xs)
        {
            var group = groups.LastOrDefault();
            if (group != null && x - group.Last() <= tolerance)
                group.Add(x);
            else
                groups.Add(new List<double> { x });
        }

        var centers = groups
            .Where(g => g.Count >= 2)
            .Select(g => g.Average())
            .OrderBy(x => x)
            .ToList();

        var bands = new List<ColumnBand>();
        for (var i = 0; i < centers.Count; i++)
        {
            var min = i == 0 ? double.NegativeInfinity : (centers[i - 1] + centers[i]) / 2;
            var max = i == centers.Count - 1 ? double.PositiveInfinity : (centers[i] + centers[i + 1]) / 2;
            bands.Add(new ColumnBand(min, max));
        }
        return bands;
    }

    static List<string> RowSlots(List<PdfTableCell> row, List<ColumnBand> bands)
    {
        var slots = Enumerable.Repeat("", bands.Count).ToList();
        foreach (var cell in row)
        {
            var index = BandIndex(bands, cell.Left);
            if (string.IsNullOrWhiteSpace(slots[index]))
                slots[index] = cell.Text;
            else
                slots[index] += " " + cell.Text;
        }
        return slots;
    }

    static int BandIndex(List<ColumnBand> bands, double x)
    {
        for (var i = 0; i < bands.Count; i++)
        {
            if (x >= bands[i].Min && x < bands[i].Max)
                return i;
        }
        return Math.Max(0, bands.Count - 1);
    }

    static string BuildMarkdownTable(List<List<string>> rows, int columns)
    {
        static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();

        var header = rows[0].Take(columns).Select(Escape).ToList();
        while (header.Count < columns) header.Add("");
        var lines = new List<string>
        {
            "| " + string.Join(" | ", header) + " |",
            "| " + string.Join(" | ", Enumerable.Repeat("---", columns)) + " |",
        };

        foreach (var row in rows.Skip(1))
        {
            var cells = row.Take(columns).Select(Escape).ToList();
            while (cells.Count < columns) cells.Add("");
            lines.Add("| " + string.Join(" | ", cells) + " |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    static List<PdfRun> BuildRuns(List<Word> words) =>
        words.Select(w => new PdfRun
        {
            Text = w.Text,
            Bbox = ToBbox(w.BoundingBox),
            Format = new PdfRunFormat
            {
                Font = w.FontName,
                Size = MedianPointSize(w.Letters),
                Bold = LooksBold(w.FontName),
                Italic = LooksItalic(w.FontName),
            }
        }).ToList();

    static string InferKind(PdfLineGroup line, string text, int priorTextBlocks, HeadingThresholds thresholds)
    {
        if (priorTextBlocks == 0 && text.Length <= 100)
            return "heading";

        if (line.FontSize is { } size)
        {
            if (thresholds.P95 > 0 && size >= thresholds.P95 * 0.97 && text.Length <= 120)
                return "heading";
            if (thresholds.Median > 0 && size > thresholds.Median * 1.25 && text.Length <= 120)
                return "heading";
        }

        if (line.IsBold && line.FontSize is { } boldSize && boldSize > thresholds.Median * 1.05 && text.Length <= 120)
            return "heading";

        return "paragraph";
    }

    static string InferAlignment(PdfRectangle bbox, double pageWidth)
    {
        var center = (bbox.Left + bbox.Right) / 2.0;
        if (Math.Abs(center - pageWidth / 2.0) <= pageWidth * 0.08)
            return "center";
        if (bbox.Left <= pageWidth * 0.12)
            return "left";
        return "unknown";
    }

    static string? MostCommonFont(IEnumerable<Word> words) =>
        words.Select(w => w.FontName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .GroupBy(f => f)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    static double? MedianPointSize(IEnumerable<Word> words) =>
        MedianPointSize(words.SelectMany(w => w.Letters));

    static double? MedianPointSize(IEnumerable<Letter> letters)
    {
        var sizes = letters
            .Select(l => l.PointSize > 0 ? l.PointSize : l.FontSize)
            .Where(double.IsFinite)
            .Where(s => s > 0)
            .OrderBy(s => s)
            .ToList();
        if (sizes.Count == 0) return null;
        return sizes[sizes.Count / 2];
    }

    static double MedianLetterSize(Word word) =>
        MedianPointSize(word.Letters) ?? Math.Max(1, word.BoundingBox.Height);

    static bool LooksBold(string? font) =>
        !string.IsNullOrWhiteSpace(font) && font.Contains("Bold", StringComparison.OrdinalIgnoreCase);

    static bool LooksItalic(string? font) =>
        !string.IsNullOrWhiteSpace(font) &&
        (font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || font.Contains("Oblique", StringComparison.OrdinalIgnoreCase));

    static PdfRectangle Union(IEnumerable<PdfRectangle> boxes)
    {
        var list = boxes.ToList();
        if (list.Count == 0) return new PdfRectangle(0, 0, 0, 0);
        return new PdfRectangle(
            list.Min(b => b.Left),
            list.Min(b => b.Bottom),
            list.Max(b => b.Right),
            list.Max(b => b.Top));
    }

    internal static double[] ToBbox(PdfRectangle rectangle) =>
    [
        Math.Round(rectangle.Left, 3),
        Math.Round(rectangle.Bottom, 3),
        Math.Round(rectangle.Right, 3),
        Math.Round(rectangle.Top, 3),
    ];

    sealed class PdfPageExtraction
    {
        public int Page { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int TextCharCount { get; set; }
        public int ImageCount { get; set; }
        public string ReadingOrderMethod { get; set; } = "single-column-y-desc-x-asc";
        public double? ColumnSplitX { get; set; }
        public List<PdfLineGroup> Lines { get; set; } = new();
    }

    sealed class PdfLineGroup
    {
        public double BaselineY { get; set; }
        public List<Word> Words { get; set; } = new();
        public PdfRectangle BoundingBox { get; set; } = new(0, 0, 0, 0);
        public string Text { get; set; } = "";
        public string? Font { get; set; }
        public double? FontSize { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public double GibberishScore { get; set; }
        public bool IsGibberish { get; set; }
        public double XMid => (BoundingBox.Left + BoundingBox.Right) / 2.0;
    }

    sealed class PdfTableCell
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public string Text { get; set; } = "";
    }

    sealed record TableRowCandidate(PdfLineGroup FirstLine, List<PdfLineGroup> Lines, List<PdfTableCell>? Cells);

    sealed class PdfTableRegion
    {
        public PdfLineGroup FirstLine { get; set; } = null!;
        public List<PdfLineGroup> Lines { get; set; } = new();
        public PdfRectangle BoundingBox { get; set; } = new(0, 0, 0, 0);
        public string Markdown { get; set; } = "";
    }

    readonly record struct ColumnBand(double Min, double Max);

    readonly record struct ColumnCluster(double Center, int Count);

    readonly record struct HeadingThresholds(double Median, double P95)
    {
        public static HeadingThresholds From(IEnumerable<PdfLineGroup> lines)
        {
            var sizes = lines
                .Select(l => l.FontSize)
                .Where(s => s is > 0)
                .Select(s => s!.Value)
                .OrderBy(s => s)
                .ToList();
            if (sizes.Count == 0)
                return new HeadingThresholds(0, 0);

            var median = sizes[sizes.Count / 2];
            var p95 = sizes[(int)Math.Floor((sizes.Count - 1) * 0.95)];
            return new HeadingThresholds(median, p95);
        }
    }
}
