using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using D = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocxCore;

/// <summary>
/// 图片嵌入器。将 PNG/JPEG/GIF/BMP/TIFF 嵌入 docx 并以合理布局排列。
/// 前两张图自动 1x2 无边框表格并排，后续图片纵向排列。
/// </summary>
public static class ImageEmbedder
{
    static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".tiff"] = "image/tiff",
        [".tif"] = "image/tiff",
    };

    /// <summary>
    /// 嵌入图片到文档。前两张图自动 1x2 并排（无边框表格），第3张起纵向排列。
    /// </summary>
    public static string[] EmbedImages(W.Body body, MainDocumentPart mainPart, string[] imagePaths, double maxWidthCm = 15.5)
    {
        if (imagePaths.Length == 0) return Array.Empty<string>();
        var ids = new List<string>();

        if (imagePaths.Length >= 2)
            EmbedSideBySide(body, mainPart, imagePaths[0], imagePaths[1], maxWidthCm, ids, 1);

        int start = imagePaths.Length >= 2 ? 2 : 0;
        for (int i = start; i < imagePaths.Length; i++)
            EmbedSingle(body, mainPart, imagePaths[i], maxWidthCm, ids, i + 1);

        return ids.ToArray();
    }

    /// <summary>嵌入单张图片到文档，返回 image part ID。</summary>
    public static string EmbedSingleImage(W.Body body, MainDocumentPart mainPart, string imagePath, string? caption = null, double maxWidthCm = 15.5)
    {
        var ids = new List<string>();
        EmbedSingle(body, mainPart, imagePath, maxWidthCm, ids, 1, caption);
        return ids[0];
    }

    static void EmbedSideBySide(W.Body body, MainDocumentPart mainPart, string path1, string path2, double maxCm, List<string> ids, int startNum)
    {
        var id1 = AddImagePart(mainPart, path1);
        var id2 = AddImagePart(mainPart, path2);
        ids.Add(id1); ids.Add(id2);

        var (w1, h1) = GetImageSize(path1);
        var (w2, h2) = GetImageSize(path2);
        double halfCm = maxCm / 2.0;
        var emu1 = FitToWidth(w1, h1, halfCm);
        var emu2 = FitToWidth(w2, h2, halfCm);

        var table = new W.Table(
            new W.TableProperties(
                new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.None }, new W.BottomBorder { Val = W.BorderValues.None },
                    new W.LeftBorder { Val = W.BorderValues.None }, new W.RightBorder { Val = W.BorderValues.None },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.None }, new W.InsideVerticalBorder { Val = W.BorderValues.None }),
                new W.TableLayout { Type = W.TableLayoutValues.Fixed }),
            new W.TableGrid(
                new W.GridColumn { Width = "4500" },
                new W.GridColumn { Width = "4500" }),
            new W.TableRow(
                ImageCell(id1, "图" + startNum, emu1.cx, emu1.cy),
                ImageCell(id2, "图" + (startNum + 1), emu2.cx, emu2.cy)));
        body.Append(table);
        body.Append(new W.Paragraph());
    }

    static void EmbedSingle(W.Body body, MainDocumentPart mainPart, string path, double maxCm, List<string> ids, int figNum, string? caption = null)
    {
        var id = AddImagePart(mainPart, path);
        ids.Add(id);

        var (pw, ph) = GetImageSize(path);
        var (cx, cy) = FitToWidth(pw, ph, maxCm);

        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
            ImageRun(id, $"Figure{figNum}", cx, cy)));

        var capText = caption ?? $"图 {figNum}";
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = "BodyTextNoIndent" },
                new W.Justification { Val = W.JustificationValues.Center }),
            new W.Run(new W.RunProperties(
                new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new W.FontSize { Val = "16" }), new W.Text(capText))));
        body.Append(new W.Paragraph());
    }

    static string AddImagePart(MainDocumentPart mainPart, string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        if (!ContentTypes.TryGetValue(ext, out var contentType))
            throw new NotSupportedException($"Unsupported image format: {ext}. Supported: png, jpg, gif, bmp, tiff");

        var part = mainPart.AddImagePart(contentType);
        using var stream = File.OpenRead(path);
        part.FeedData(stream);
        return mainPart.GetIdOfPart(part);
    }

    static (long cx, long cy) FitToWidth(int pixelW, int pixelH, double maxCm)
    {
        double maxEmu = maxCm * 360000;
        double aspect = pixelH / (double)pixelW;
        return ((long)maxEmu, (long)(maxEmu * aspect));
    }

    static (int w, int h) GetImageSize(string path)
    {
        try { return ImageHeaderReader.GetDimensions(path); }
        catch { return (800, 600); }
    }

    /// <summary>创建图片 Run（供外部使用，如 DocxTemplate）。</summary>
    public static W.Run ImageRun(string imagePartId, string name, long cx, long cy)
    {
        var inline = new D.Inline(
            new D.Extent { Cx = cx, Cy = cy },
            new D.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new D.DocProperties { Id = (uint)Math.Abs(name.GetHashCode() % 0x7FFFFFFF), Name = name },
            new D.NonVisualGraphicFrameDrawingProperties(new GraphicFrameLocks { NoChangeAspect = true }),
            new Graphic(new GraphicData(
                new Picture(
                    new NonVisualPictureProperties(
                        new NonVisualDrawingProperties { Id = 0, Name = name },
                        new NonVisualPictureDrawingProperties()),
                    new BlipFill(
                        new Blip { Embed = imagePartId },
                        new Stretch(new FillRectangle())),
                    new ShapeProperties(
                        new Transform2D(new Offset { X = 0L, Y = 0L }, new Extents { Cx = cx, Cy = cy }),
                        new PresetGeometry(new AdjustValueList()) { Preset = ShapeTypeValues.Rectangle }))
            ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0
        };
        return new W.Run(new W.Drawing(inline));
    }

    static W.TableCell ImageCell(string imagePartId, string name, long cx, long cy)
    {
        var tc = new W.TableCell(
            new W.TableCellProperties(
                new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center },
                new W.TableCellBorders(
                    new W.TopBorder { Val = W.BorderValues.None },
                    new W.BottomBorder { Val = W.BorderValues.None },
                    new W.LeftBorder { Val = W.BorderValues.None },
                    new W.RightBorder { Val = W.BorderValues.None })));
        tc.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
            ImageRun(imagePartId, name, cx, cy),
            new W.Run(new W.RunProperties(
                new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new W.FontSize { Val = "16" }), new W.Text(name))));
        return tc;
    }
}
