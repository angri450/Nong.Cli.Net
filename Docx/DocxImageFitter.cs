using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Merges nearby standalone image paragraphs and scales their inline drawings
/// to fit side-by-side within page text width. Pure OOXML — no COM, no SkiaSharp.
///
/// Operates in two passes:
/// 1. Find consecutive image paragraphs (at most 1 caption apart), merge drawing runs
/// 2. Scale all multi-drawing paragraphs to fit page width with a configurable gap
/// </summary>
public static class DocxImageFitter
{
    public static DocxFitResult FitImages(string inputPath, string outputPath, double gapMm = 2.0)
    {
        return FitImagesCore(inputPath, outputPath, gapMm, 3);
    }

    /// <summary>Wider scan (up to 10 paragraphs apart) for cross-section orphan pairing.</summary>
    public static DocxFitResult FitImagesWide(string inputPath, string outputPath, double gapMm = 2.0, int maxGap = 10)
    {
        return FitImagesCore(inputPath, outputPath, gapMm, maxGap);
    }

    static DocxFitResult FitImagesCore(string inputPath, string outputPath, double gapMm, int maxGap)
    {
        File.Copy(inputPath, outputPath, true);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;

        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        // 1. Calculate page text width in EMU
        long textWidthEmu = GetTextWidthEmu(doc);

        // 2. Get all body paragraphs
        var bodyParas = doc.Descendants()
            .Where(e => e.Name.LocalName == "p" && e.Parent?.Name.LocalName == "body")
            .ToList();

        long gapEmu = (long)(gapMm * 36000);

        var modified = new List<FitParagraphResult>();
        int i = 0;

        while (i < bodyParas.Count)
        {
            // Skip paragraphs already removed by a previous merge
            if (bodyParas[i].Parent == null) { i++; continue; }

            // Is this an image-only paragraph?
            var inlines = bodyParas[i].Descendants()
                .Where(e => e.Name.LocalName == "inline").ToList();

            if (inlines.Count == 0) { i++; continue; }

            // Phase A: scan for nearby image paragraphs and their captions (no tree mutation)
            List<int> imageIndices = new() { i };
            List<string> captions = new();
            List<int> captionIndices = new();
            int j = i + 1;

            // Check caption after first image
            var cap = FindCaptionAfter(bodyParas, i);
            if (cap != null)
            {
                captions.Add(cap.CaptionText);
                captionIndices.Add(i + 1);
            }

            while (j < bodyParas.Count && j <= i + maxGap && imageIndices.Count < 5)
            {
                var nextInlines = bodyParas[j].Descendants()
                    .Where(e => e.Name.LocalName == "inline").ToList();

                if (nextInlines.Count >= 1)
                {
                    imageIndices.Add(j);
                    // Check caption after this image
                    var cap2 = FindCaptionAfter(bodyParas, j);
                    if (cap2 != null && !captionIndices.Contains(j + 1))
                    {
                        captions.Add(cap2.CaptionText);
                        captionIndices.Add(j + 1);
                    }
                    j++;
                }
                else if (nextInlines.Count == 0 && !HasText(bodyParas[j]) && imageIndices.Count == 1)
                {
                    j++; // skip empty separators
                }
                else
                {
                    if (imageIndices.Count > 1) break;
                    j++;
                }
            }

            // Phase B: perform mutations (if we found images to merge)
            if (imageIndices.Count >= 2)
            {
                // Collect drawing runs from secondary image paragraphs
                List<XElement> drawingsToMerge = new();
                for (int k = 1; k < imageIndices.Count; k++)
                {
                    var secInlines = bodyParas[imageIndices[k]].Descendants()
                        .Where(e => e.Name.LocalName == "inline").ToList();
                    foreach (var il in secInlines)
                        drawingsToMerge.Add(il.Parent?.Parent!);
                    // Mark for removal (remove later, after collecting all)
                }

                // Remove caption paragraphs (from highest index to lowest)
                foreach (int ci in captionIndices.OrderByDescending(x => x))
                    bodyParas[ci].Remove();

                // Remove secondary image paragraphs (highest to lowest)
                for (int k = imageIndices.Count - 1; k >= 1; k--)
                    bodyParas[imageIndices[k]].Remove();

                // Move drawing runs into the first image paragraph
                foreach (var r in drawingsToMerge)
                    bodyParas[i].Add(r);

                // Append combined caption
                if (captions.Count > 0)
                {
                    var captionPara = BuildCaptionParagraph(captions);
                    bodyParas[i].AddAfterSelf(captionPara);
                }

                // Re-collect inlines for scaling
                inlines = bodyParas[i].Descendants()
                    .Where(e => e.Name.LocalName == "inline").ToList();
            }

            // Scale if needed
            if (inlines.Count >= 2)
            {
                var dims = new List<(long, long)>();
                foreach (var il in inlines)
                {
                    var ext = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
                    if (ext == null) continue;
                    if (long.TryParse(ext.Attribute("cx")?.Value, out long cx) &&
                        long.TryParse(ext.Attribute("cy")?.Value, out long cy) && cx > 0)
                        dims.Add((cx, cy));
                }

                long totalWidth = dims.Sum(d => d.Item1);
                if (totalWidth > textWidthEmu && dims.Count >= 2)
                {
                    long totalGap = gapEmu * (dims.Count - 1);
                    double scale = (double)(textWidthEmu - totalGap) / totalWidth;

                    if (scale > 0 && scale < 1.0)
                    {
                        var scaled = new List<(long, long, long, long)>();
                        foreach (var d in dims)
                        {
                            long nw = (long)(d.Item1 * scale);
                            long nh = (long)(d.Item2 * scale);
                            scaled.Add((nw, nh, d.Item1, d.Item2));
                        }

                        int mIdx = 0;
                        foreach (var il in inlines)
                        {
                            if (mIdx >= scaled.Count) break;
                            var (nw, nh, _, _) = scaled[mIdx++];

                            var ext = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
                            if (ext != null)
                            {
                                ext.SetAttributeValue("cx", nw);
                                ext.SetAttributeValue("cy", nh);
                            }

                            var xfrmExt = il.Descendants()
                                .FirstOrDefault(e => e.Name.LocalName == "ext"
                                    && e.Parent?.Name.LocalName == "xfrm");
                            if (xfrmExt != null)
                            {
                                xfrmExt.SetAttributeValue("cx", nw);
                                xfrmExt.SetAttributeValue("cy", nh);
                            }
                        }

                        modified.Add(new FitParagraphResult(i, dims.Count,
                            totalWidth, textWidthEmu, scale, scaled.ToArray()));
                    }
                }
            }

            i++; // advance past current paragraph (even if it was modified)
        }

        // 3. Write back
        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open())
            doc.Save(ws);

        return new DocxFitResult(modified);
    }

    static bool HasText(XElement para)
    {
        var text = para.Descendants()
            .Where(e => e.Name.LocalName == "t")
            .Select(t => t.Value)
            .ToList();
        return text.Any(t => !string.IsNullOrWhiteSpace(t));
    }

    class CaptionHit { public string CaptionText; public XElement Element; }

    static CaptionHit? FindCaptionAfter(List<XElement> paras, int imageIdx)
    {
        if (imageIdx + 1 >= paras.Count) return null;
        var next = paras[imageIdx + 1];
        var text = string.Concat(next.Descendants()
            .Where(e => e.Name.LocalName == "t")
            .Select(t => t.Value));
        if (string.IsNullOrWhiteSpace(text)) return null;
        bool isCaption = System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(^|\s)(图|[Ff]ig(?:ure)?\.?|[Tt]able)\s*\d+");
        if (!isCaption) return null;
        return new CaptionHit { CaptionText = text.Trim(), Element = next };
    }

    static XElement BuildCaptionParagraph(List<string> captions)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var para = new XElement(w + "p",
            new XElement(w + "pPr",
                new XElement(w + "jc", new XAttribute(w + "val", "center")),
                new XElement(w + "spacing",
                    new XAttribute(w + "before", "80"),
                    new XAttribute(w + "after", "240"))));
        for (int c = 0; c < captions.Count; c++)
        {
            if (c > 0)
            {
                para.Add(new XElement(w + "r",
                    new XElement(w + "rPr",
                        new XElement(w + "sz", new XAttribute(w + "val", "9"))),
                    new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), "    ")));
            }
            para.Add(new XElement(w + "r",
                new XElement(w + "rPr",
                    new XElement(w + "rFonts",
                        new XAttribute(w + "ascii", "宋体"),
                        new XAttribute(w + "hAnsi", "宋体"),
                        new XAttribute(w + "eastAsia", "宋体")),
                    new XElement(w + "i"),
                    new XElement(w + "color", new XAttribute(w + "val", "507070")),
                    new XElement(w + "sz", new XAttribute(w + "val", "21"))),
                new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), captions[c])));
        }
        return para;
    }

    static long GetTextWidthEmu(XDocument doc)
    {
        var sectPr = doc.Descendants()
            .Where(e => e.Name.LocalName == "sectPr")
            .LastOrDefault();
        if (sectPr == null) return 5_580_380;

        var pgSz = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
        var pgMar = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");

        long pw = 11906, ml = 1701, mr = 1417;
        if (pgSz != null)
            foreach (var a in pgSz.Attributes())
                if (a.Name.LocalName == "w") long.TryParse(a.Value, out pw);
        if (pgMar != null)
            foreach (var a in pgMar.Attributes())
            {
                if (a.Name.LocalName == "left") long.TryParse(a.Value, out ml);
                if (a.Name.LocalName == "right") long.TryParse(a.Value, out mr);
            }

        long tw = pw - ml - mr;
        return tw > 0 ? tw * 635 : 5_580_380;
    }
}

public sealed record FitParagraphResult(
    int ParagraphIndex, int ImageCount,
    long OriginalTotalEmu, long PageTextEmu,
    double ScaleFactor,
    (long NewWidth, long NewHeight, long OldWidth, long OldHeight)[] Dimensions);

public sealed record DocxFitResult(List<FitParagraphResult> Modified)
{
    public int ParagraphsModified => Modified.Count;
    public int ImagesScaled => Modified.Sum(m => m.ImageCount);
}
