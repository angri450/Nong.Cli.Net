using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocxCore;

namespace MultiModalCore;

/// <summary>将 API 版面分析结果转为 Word，对接 Angri450.Nong.Docx 排版引擎。</summary>
public static class LayoutToWordConverter
{
    static readonly HttpClient DownloadClient = new();

    public static void Convert(OcrResult result, string outputPath)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        // 用 Docx 包的标准样式
        var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylePart.Styles = new Styles();
        StyleBuilder.BuildAll(stylePart.Styles);

        var writer = new DocumentWriter(body, doc);

        var imgDir = Path.Combine(Path.GetTempPath(), $"ocr_imgs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(imgDir);

        try
        {
            for (int i = 0; i < result.Pages.Count; i++)
            {
                var page = result.Pages[i];
                var orderedBlocks = page.Blocks
                    .Where(b => b.BlockOrder.HasValue)
                    .OrderBy(b => b.BlockOrder)
                    .ToList();

                // 检测多栏布局
                var columns = DetectColumns(orderedBlocks, page.Width);

                if (columns > 1)
                {
                    WriteMultiColumn(writer, orderedBlocks, columns, page, result, imgDir, body, mainPart, doc);
                }
                else
                {
                    foreach (var block in orderedBlocks)
                        WriteBlock(writer, block, page, result, imgDir, body, mainPart, doc);
                }

                if (i < result.Pages.Count - 1)
                {
                    body.Append(new Paragraph(
                        new Run(new Break { Type = BreakValues.Page })));
                }
            }
        }
        finally
        {
            try { Directory.Delete(imgDir, true); } catch { }
        }

        ElementOrder.RectifyTree(body);
        mainPart.Document.Save();
    }

    static void WriteBlock(DocumentWriter writer, ParsingBlock block, OcrPage page,
        OcrResult result, string imgDir, Body body, MainDocumentPart mainPart, WordprocessingDocument doc)
    {
        switch (block.BlockLabel)
        {
            case "doc_title":
                writer.Title(block.BlockContent);
                break;
            case "paragraph_title":
                writer.Heading(block.BlockContent, 2);
                break;
            case "text":
                if (!string.IsNullOrWhiteSpace(block.BlockContent))
                    writer.Body(block.BlockContent);
                break;
            case "image":
                WriteImageBlock(block, page, result, imgDir, mainPart, body);
                break;
            case "table":
                WriteTableBlock(block, doc);
                break;
            case "vision_footnote":
                writer.Footnote(block.BlockContent);
                break;
            default:
                if (!string.IsNullOrWhiteSpace(block.BlockContent))
                    writer.Body(block.BlockContent);
                break;
        }
    }

    static void WriteImageBlock(ParsingBlock block, OcrPage page, OcrResult result,
        string imgDir, MainDocumentPart mainPart, Body body)
    {
        // 从 MarkdownImages 中匹配对应图片 URL
        var imgUrl = FindImageUrl(block, page, result);
        if (imgUrl == null)
        {
            var p = new Paragraph(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }));
            p.Append(new Run(new RunProperties(
                new RunFonts { Ascii = "宋体", HighAnsi = "宋体", EastAsia = "宋体" },
                new FontSize { Val = "20" }, new Italic()),
                new Text("[图片]") { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(p);
            return;
        }

        // 下载图片
        var ext = Path.GetExtension(imgUrl.Split('?')[0]);
        if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
        var localPath = Path.Combine(imgDir, $"img_{block.BlockId}{ext}");
        try
        {
            var imgBytes = DownloadClient.GetByteArrayAsync(imgUrl).Result;
            File.WriteAllBytes(localPath, imgBytes);
        }
        catch
        {
            return; // 下载失败静默跳过
        }

        ImageEmbedder.EmbedSingleImage(body, mainPart, localPath);
    }

    static void WriteTableBlock(ParsingBlock block, WordprocessingDocument doc)
    {
        var content = block.BlockContent;
        if (string.IsNullOrWhiteSpace(content)) return;

        var (headers, rows) = ParseHtmlTable(content);
        if (headers.Length == 0 && rows.Length == 0) return;

        var mainPart = doc.MainDocumentPart ?? throw new InvalidOperationException("文档结构缺失");
        var document = mainPart.Document ?? throw new InvalidOperationException("Document 缺失");
        var body = document.Body ?? throw new InvalidOperationException("Body 缺失");
        var writer = new DocumentWriter(body, doc);

        if (headers.Length > 0)
            writer.Table("", 0, headers, rows);
        else
            AppendRawTable(body, rows);
    }

    static void WriteMultiColumn(DocumentWriter writer, List<ParsingBlock> blocks, int numCols,
        OcrPage page, OcrResult result, string imgDir, Body body,
        MainDocumentPart mainPart, WordprocessingDocument doc)
    {
        // 用无边框表格模拟多栏：每行 = 一组同一水平位置的块
        var groups = GroupByRow(blocks, page.Height);

        var table = new Table();
        table.Append(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }),
            new TableLayout { Type = TableLayoutValues.Fixed }));

        var grid = new TableGrid();
        foreach (var _ in Enumerable.Range(0, numCols))
            grid.Append(new GridColumn());
        table.Append(grid);

        foreach (var rowBlocks in groups)
        {
            var row = new TableRow();
            for (int c = 0; c < numCols; c++)
            {
                var cellBlock = rowBlocks.FirstOrDefault(b => GetColumnIndex(b, page.Width, numCols) == c);
                var cell = new TableCell();
                if (cellBlock != null && !string.IsNullOrWhiteSpace(cellBlock.BlockContent))
                {
                    string text = cellBlock.BlockLabel switch
                    {
                        "paragraph_title" => cellBlock.BlockContent,
                        _ => cellBlock.BlockContent,
                    };
                    var para = new Paragraph();
                    var isTitle = cellBlock.BlockLabel == "paragraph_title";
                    if (isTitle)
                        para.Append(new ParagraphProperties(new SpacingBetweenLines { Before = "100", After = "50" }));
                    para.Append(new Run(new RunProperties(
                        new RunFonts { Ascii = "宋体", HighAnsi = "宋体", EastAsia = "宋体" },
                        new FontSize { Val = isTitle ? "24" : "21" },
                        isTitle ? new Bold() : null!),
                        new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
                    cell.Append(para);
                }
                else
                {
                    cell.Append(new Paragraph());
                }
                row.Append(cell);
            }
            table.Append(row);
        }
        body.Append(table);
    }

    // === 多栏检测 ===

    static int DetectColumns(List<ParsingBlock> blocks, int pageWidth)
    {
        if (pageWidth <= 0 || blocks.Count < 3) return 1;
        var xs = blocks
            .Where(b => b.BlockBbox.Length >= 2)
            .Select(b => b.BlockBbox[0])
            .OrderBy(x => x)
            .ToList();
        if (xs.Count < 3) return 1;

        // 找间距跳变：相邻块 x0 差 > 页面宽度 10% 视为栏间分隔
        var gaps = new List<double>();
        for (int i = 1; i < xs.Count; i++)
        {
            double gap = (xs[i] - xs[i - 1]) / pageWidth;
            if (gap > 0.08) gaps.Add(gap);
        }
        return gaps.Count + 1;
    }

    static int GetColumnIndex(ParsingBlock block, int pageWidth, int numCols)
    {
        if (block.BlockBbox.Length < 1) return 0;
        double colW = (double)pageWidth / numCols;
        return Math.Clamp((int)(block.BlockBbox[0] / colW), 0, numCols - 1);
    }

    static List<List<ParsingBlock>> GroupByRow(List<ParsingBlock> blocks, int pageHeight)
    {
        var sorted = blocks.OrderBy(b => b.BlockOrder).ToList();
        var rows = new List<List<ParsingBlock>>();
        var currentRow = new List<ParsingBlock>();
        double? rowTop = null;

        foreach (var b in sorted)
        {
            if (b.BlockBbox.Length < 2) continue;
            double y = b.BlockBbox[1];
            if (rowTop == null || Math.Abs(y - rowTop.Value) > pageHeight * 0.03)
            {
                if (currentRow.Count > 0) rows.Add(currentRow);
                currentRow = new List<ParsingBlock>();
                rowTop = y;
            }
            currentRow.Add(b);
        }
        if (currentRow.Count > 0) rows.Add(currentRow);
        return rows;
    }

    // === HTML 表格解析 ===

    static (string[] headers, string[][] rows) ParseHtmlTable(string html)
    {
        try
        {
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            var doc = XDocument.Parse($"<root>{html}</root>");
            var table = doc.Descendants("table").FirstOrDefault();
            if (table == null) return ([], []);

            var allRows = new List<string[]>();
            foreach (var tr in table.Elements("tr"))
            {
                var cells = tr.Elements("td").Select(e => e.Value.Trim()).ToArray();
                if (cells.Length > 0) allRows.Add(cells);
            }

            if (allRows.Count == 0) return ([], []);
            var headers = allRows[0];
            var data = allRows.Skip(1).ToArray();
            return (headers, data);
        }
        catch
        {
            return ([], []);
        }
    }

    static void AppendRawTable(Body body, string[][] rows)
    {
        if (rows.Length == 0) return;
        var maxCols = rows.Max(r => r.Length);
        var table = new Table();
        table.Append(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" }),
            new TableLayout { Type = TableLayoutValues.Fixed }));
        var grid = new TableGrid();
        foreach (var _ in Enumerable.Range(0, maxCols))
            grid.Append(new GridColumn());
        table.Append(grid);

        for (int ri = 0; ri < rows.Length; ri++)
        {
            var tr = new TableRow();
            for (int ci = 0; ci < maxCols; ci++)
            {
                var tc = new TableCell();
                var text = ci < rows[ri].Length ? rows[ri][ci] : "";
                var para = new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { Before = "20", After = "20" }));
                para.Append(new Run(new RunProperties(
                    new RunFonts { Ascii = "宋体", HighAnsi = "宋体", EastAsia = "宋体" },
                    new FontSize { Val = "20" },
                    ri == 0 ? new Bold() : null!),
                    new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
                tc.Append(para);
                tr.Append(tc);
            }
            table.Append(tr);
        }
        body.Append(table);
        body.Append(new Paragraph());
    }

    static string? FindImageUrl(ParsingBlock imageBlock, OcrPage page, OcrResult result)
    {
        // API 返回的 markdown images 的 key 里包含 bbox 信息
        // 如 "imgs/img_in_image_box_756_185_1522_685.jpg"
        if (page.MarkdownImages == null) return null;

        var bbox = imageBlock.BlockBbox;
        if (bbox.Length < 4) return null;

        foreach (var (key, url) in page.MarkdownImages)
        {
            if (key.Contains("image_box"))
            {
                // key 格式: imgs/img_in_image_box_{x0}_{y0}_{x1}_{y1}.jpg
                var match = Regex.Match(key, @"image_box_(\d+)_(\d+)_(\d+)_(\d+)");
                if (match.Success)
                {
                    int kx0 = int.Parse(match.Groups[1].Value);
                    int ky0 = int.Parse(match.Groups[2].Value);
                    int kx1 = int.Parse(match.Groups[3].Value);
                    int ky1 = int.Parse(match.Groups[4].Value);

                    if (Math.Abs(kx0 - (int)bbox[0]) < 10 &&
                        Math.Abs(ky0 - (int)bbox[1]) < 10 &&
                        Math.Abs(kx1 - (int)bbox[2]) < 10 &&
                        Math.Abs(ky1 - (int)bbox[3]) < 10)
                        return url;
                }
            }
        }
        // fallback: 返回第一张图片
        return page.MarkdownImages.Values.FirstOrDefault();
    }
}
