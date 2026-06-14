using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Estimates page breaks and measures blank space by walking document body
/// paragraphs + tables and accumulating content heights against section page dimensions.
///
/// Not a Word layout engine — estimates line heights from spacing/spacingLine values,
/// uses fixed-width line approximation for text, and treats inline images as blocks.
/// Designed to flag pages with excessive blank space for compaction.
/// </summary>
public static class DocxPageEstimator
{
    const long EmuPerMm = 36000;
    const long FallbackTextAreaHeightEmu = 220 * EmuPerMm; // A4: 297 - 2x(2.54+2.54)cm ≈ 220mm

    public static PageEstimateResult Estimate(string docxPath)
    {
        using var zip = ZipFile.OpenRead(docxPath);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        long textAreaH = GetTextAreaHeightEmu(doc);
        var body = doc.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "body");
        if (body == null) return new PageEstimateResult(0, 0, textAreaH, new());

        var pages = new List<PageInfo>();
        var bodyElements = body.Elements().ToList();

        long accumulatedH = 0;
        int pageNo = 1;
        int itemCount = 0;
        bool pageHasImage = false;
        bool pageHasTable = false;
        int keepNextCount = 0;    // keepNext paragraphs on this page
        int cantSplitCount = 0;   // cantSplit rows on this page

        for (int idx = 0; idx < bodyElements.Count; idx++)
        {
            var el = bodyElements[idx];
            long elH = EstimateElementHeight(el);
            bool isImage = el.Name.LocalName == "p" && el.Descendants()
                .Any(e => e.Name.LocalName == "inline");
            bool isTable = el.Name.LocalName == "tbl";

            // Detect keepNext on this paragraph
            bool hasKeepNext = el.Name.LocalName == "p" && el.Elements()
                .Where(e => e.Name.LocalName == "pPr")
                .Any(pp => pp.Elements().Any(k => k.Name.LocalName == "keepNext"));
            if (hasKeepNext) keepNextCount++;

            // Detect cantSplit in table rows
            if (el.Name.LocalName == "tbl")
            {
                cantSplitCount += el.Descendants()
                    .Where(e => e.Name.LocalName == "trPr")
                    .Count(trp => trp.Elements().Any(c => c.Name.LocalName == "cantSplit"));
            }

            bool wouldOverflow = accumulatedH + elH > textAreaH && accumulatedH > 0 && itemCount > 0;

            // keepNext de-optimizes page break: if previous para has keepNext,
            // don't break here — carry over to the next page
            if (wouldOverflow && idx > 0)
            {
                var prevEl = bodyElements[idx - 1];
                bool prevHasKeepNext = prevEl.Name.LocalName == "p" && prevEl.Elements()
                    .Where(e => e.Name.LocalName == "pPr")
                    .Any(pp => pp.Elements().Any(k => k.Name.LocalName == "keepNext"));

                if (prevHasKeepNext)
                {
                    // Roll back the last item(s) with keepNext — they must stay together
                    // Simple: if prev has keepNext, don't break — push current to next page
                    wouldOverflow = false;
                }
            }

            if (wouldOverflow)
            {
                // Page break — flush current page
                long wasteEmu = textAreaH - accumulatedH;
                pages.Add(new PageInfo(pageNo, itemCount, accumulatedH, textAreaH,
                    wasteEmu, Math.Round((double)wasteEmu / textAreaH * 100, 1),
                    pageHasImage, pageHasTable, keepNextCount, cantSplitCount));

                pageNo++;
                accumulatedH = 0;
                itemCount = 0;
                pageHasImage = false;
                pageHasTable = false;
                keepNextCount = 0;
                cantSplitCount = 0;
            }

            accumulatedH += elH;
            itemCount++;
            if (isImage) pageHasImage = true;
            if (isTable) pageHasTable = true;
        }

        // Flush last page
        if (itemCount > 0)
        {
            long wasteEmu = textAreaH - accumulatedH;
            pages.Add(new PageInfo(pageNo, itemCount, accumulatedH, textAreaH,
                wasteEmu, Math.Round((double)wasteEmu / textAreaH * 100, 1),
                pageHasImage, pageHasTable, keepNextCount, cantSplitCount));
        }

        return new PageEstimateResult(pages.Count, pages.Sum(p => p.ItemCount),
            textAreaH, pages);
    }

    static long EstimateElementHeight(XElement el)
    {
        return el.Name.LocalName switch
        {
            "p" => EstimateParagraphHeight(el),
            "tbl" => EstimateTableHeight(el),
            _ => 0
        };
    }

    static long EstimateParagraphHeight(XElement para)
    {
        // Image paragraph — sum image extents + 2mm padding
        var inlines = para.Descendants().Where(e => e.Name.LocalName == "inline").ToList();
        if (inlines.Count > 0)
        {
            long maxH = 0;
            foreach (var il in inlines)
            {
                var ext = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
                if (ext != null && long.TryParse(ext.Attribute("cy")?.Value, out long cy))
                    if (cy > maxH) maxH = cy;
            }
            if (maxH > 0) return maxH + 4 * EmuPerMm; // 4mm caption padding
        }

        // Text paragraph — use spacing/line value, fallback 360 twips
        long lineH = 360 * 635; // 360 twips * 635 EMU/twip
        var sp = para.Elements().Where(e => e.Name.LocalName == "pPr")
            .SelectMany(pp => pp.Elements().Where(e => e.Name.LocalName == "spacing"))
            .FirstOrDefault();
        if (sp != null)
        {
            var lv = sp.Attribute("line");
            if (lv != null && long.TryParse(lv.Value, out long lh))
                lineH = lh * 635; // twips → EMU
        }

        // Count text lines
        int chars = para.Descendants().Where(e => e.Name.LocalName == "t")
            .Sum(t => t.Value.Length);
        int lines = Math.Max(1, (chars + 50) / 51); // ~51 chars per A4 line with 24pt body

        return lineH * lines;
    }

    static long EstimateTableHeight(XElement tbl)
    {
        // Sum row heights from trHeight or row count * default
        long total = 0;
        foreach (var tr in tbl.Descendants().Where(e => e.Name.LocalName == "tr"))
        {
            var trH = tr.Elements().Where(e => e.Name.LocalName == "trPr")
                .SelectMany(tp => tp.Elements().Where(e => e.Name.LocalName == "trHeight"))
                .FirstOrDefault();
            long rh = 400 * 635; // default ~400 twips twips→EMU
            if (trH != null)
            {
                var hv = trH.Attribute("val");
                if (hv != null && long.TryParse(hv.Value, out long tv)) rh = tv * 635;
            }
            total += rh;
        }
        return total > 0 ? total : 10 * EmuPerMm;
    }

    static long GetTextAreaHeightEmu(XDocument doc)
    {
        var sectPr = doc.Descendants().Where(e => e.Name.LocalName == "sectPr").LastOrDefault();
        if (sectPr == null) return FallbackTextAreaHeightEmu;

        long pageH = 16838; // A4 twips
        long topM = 1440, bottomM = 1440;

        var pgSz = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
        if (pgSz != null)
            foreach (var a in pgSz.Attributes())
                if (a.Name.LocalName == "h") long.TryParse(a.Value, out pageH);

        var pgMar = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");
        if (pgMar != null)
            foreach (var a in pgMar.Attributes())
            {
                if (a.Name.LocalName == "top") long.TryParse(a.Value, out topM);
                if (a.Name.LocalName == "bottom") long.TryParse(a.Value, out bottomM);
            }

        long textH = pageH - topM - bottomM;
        return textH > 0 ? textH * 635 : FallbackTextAreaHeightEmu; // twips → EMU
    }
}

public sealed record PageInfo(
    int PageNumber,
    int ItemCount,
    long ContentHeightEmu,
    long TextAreaHeightEmu,
    long WasteEmu,
    double WastePercent,
    bool HasImage,
    bool HasTable,
    int KeepNextCount,
    int CantSplitCount)
{
    public double ContentMm => Math.Round(ContentHeightEmu / 36000.0, 1);
    public double WasteMm => Math.Round(WasteEmu / 36000.0, 1);
    public double TextAreaMm => Math.Round(TextAreaHeightEmu / 36000.0, 1);
    public bool IsProblem => WastePercent > 30;
}

public sealed record PageEstimateResult(
    int PageCount, int TotalItems,
    long TextAreaHeightEmu,
    List<PageInfo> Pages)
{
    public double TextAreaMm => Math.Round(TextAreaHeightEmu / 36000.0, 1);
    public int ProblemPages => Pages.Count(p => p.IsProblem);
    public double WasteTotalMm => Math.Round(Pages.Sum(p => p.WasteEmu) / 36000.0, 1);
}
