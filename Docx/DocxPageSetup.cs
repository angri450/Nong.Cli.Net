using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Unified page setup: section-level properties for all or targeted sections.
/// Handles pgSz (size/orientation), pgMar (margins), cols (columns),
/// titlePg (different first page), pgNumType (page number format).
/// Pure OOXML — no COM.
/// </summary>
public static class DocxPageSetup
{
    public record PageSetupOptions
    {
        public string? PageSize { get; init; }  // "A4", "A3", "B5", "Letter", or "WxH" mm like "210x297"
        public string? Orient { get; init; }    // "portrait" or "landscape"
        public double? MarginTopMm { get; init; }
        public double? MarginBottomMm { get; init; }
        public double? MarginLeftMm { get; init; }
        public double? MarginRightMm { get; init; }
        public int? Columns { get; init; }       // >1 means multi-column
        public double? ColumnGapMm { get; init; }
        public bool? DifferentFirstPage { get; init; }
        public string? PageNumberFormat { get; init; } // "decimal", "roman", "romanUpper", "letterUpper"
        public int? SectionIndex { get; init; }  // null = all sections
    }

    public static PageSetupResult Apply(string inputPath, string outputPath, PageSetupOptions opts)
    {
        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        var sectPrs = doc.Descendants()
            .Where(e => e.Name.LocalName == "sectPr").ToList();

        var changes = new List<string>();
        int applied = 0;

        for (int si = 0; si < sectPrs.Count; si++)
        {
            if (opts.SectionIndex.HasValue && si != opts.SectionIndex.Value) continue;

            var sectPr = sectPrs[si];
            XNamespace w = sectPr.Name.Namespace;

            // --- Page size ---
            if (opts.PageSize != null || opts.Orient != null)
            {
                var pgSz = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
                if (pgSz == null)
                {
                    pgSz = new XElement(w + "pgSz");
                    sectPr.AddFirst(pgSz);
                }

                if (opts.PageSize != null)
                {
                    var mm = ParseSize(opts.PageSize);
                    long wTwips = mm.width * 567 / 10; // mm → twips (1mm = 56.7 twips)
                    long hTwips = mm.height * 567 / 10;
                    pgSz.SetAttributeValue(w + "w", wTwips);
                    pgSz.SetAttributeValue(w + "h", hTwips);
                    changes.Add($"section[{si}]:size={opts.PageSize}({wTwips}x{hTwips}twips)");
                }

                if (opts.Orient != null)
                {
                    // Swap w/h if landscape
                    if (opts.Orient == "landscape")
                    {
                        var wv = pgSz.Attribute(w + "w")?.Value;
                        var hv = pgSz.Attribute(w + "h")?.Value;
                        if (wv != null && hv != null)
                        {
                            pgSz.SetAttributeValue(w + "w", hv);
                            pgSz.SetAttributeValue(w + "h", wv);
                        }
                        pgSz.SetAttributeValue(w + "orient", "landscape");
                    }
                    else
                        pgSz.Attribute(w + "orient")?.Remove();
                    changes.Add($"section[{si}]:orient={opts.Orient}");
                }
            }

            // --- Margins ---
            var needsMargins = opts.MarginTopMm.HasValue || opts.MarginBottomMm.HasValue
                || opts.MarginLeftMm.HasValue || opts.MarginRightMm.HasValue;
            if (needsMargins)
            {
                var pgMar = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");
                if (pgMar == null)
                {
                    pgMar = new XElement(w + "pgMar");
                    sectPr.Add(pgMar);
                }

                if (opts.MarginTopMm.HasValue) SetTwips(pgMar, w + "top", opts.MarginTopMm.Value);
                if (opts.MarginBottomMm.HasValue) SetTwips(pgMar, w + "bottom", opts.MarginBottomMm.Value);
                if (opts.MarginLeftMm.HasValue) SetTwips(pgMar, w + "left", opts.MarginLeftMm.Value);
                if (opts.MarginRightMm.HasValue) SetTwips(pgMar, w + "right", opts.MarginRightMm.Value);
                changes.Add($"section[{si}]:margins updated");
            }

            // --- Columns ---
            if (opts.Columns.HasValue && opts.Columns.Value > 1)
            {
                var cols = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "cols");
                if (cols == null)
                {
                    cols = new XElement(w + "cols");
                    sectPr.Add(cols);
                }

                int colCount = opts.Columns.Value;
                long gap = 720; // default 0.5 inch = 360 twips
                if (opts.ColumnGapMm.HasValue)
                    gap = (long)(opts.ColumnGapMm.Value * 567 / 10);

                cols.SetAttributeValue(w + "num", colCount);
                cols.SetAttributeValue(w + "space", gap);
                cols.Attribute(w + "equalWidth")?.Remove(); // ensure equal

                var w1 = cols.Attribute(w + "w")?.Value;
                var sp1 = cols.Attribute(w + "space")?.Value;
                changes.Add($"section[{si}]:columns={colCount}");
            }
            else if (opts.Columns.HasValue && opts.Columns.Value <= 1)
            {
                sectPr.Elements().Where(e => e.Name.LocalName == "cols").Remove();
                changes.Add($"section[{si}]:columns=off");
            }

            // --- Different first page ---
            if (opts.DifferentFirstPage.HasValue)
            {
                var tpg = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "titlePg");
                if (opts.DifferentFirstPage.Value && tpg == null)
                {
                    sectPr.Add(new XElement(w + "titlePg"));
                    changes.Add($"section[{si}]:titlePg=on");
                }
                else if (!opts.DifferentFirstPage.Value && tpg != null)
                {
                    tpg.Remove();
                    changes.Add($"section[{si}]:titlePg=off");
                }
            }

            // --- Page number format ---
            if (opts.PageNumberFormat != null)
            {
                var pnt = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgNumType")
                    ?? new XElement(w + "pgNumType");
                pnt.SetAttributeValue(w + "fmt", opts.PageNumberFormat);
                if (!sectPr.Elements().Any(e => e.Name.LocalName == "pgNumType"))
                    sectPr.Add(pnt);
                changes.Add($"section[{si}]:pgNumType={opts.PageNumberFormat}");
            }

            applied++;
        }

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new PageSetupResult(applied, changes);
    }

    static void SetTwips(XElement el, XName attr, double mm)
    {
        el.SetAttributeValue(attr, (long)(mm * 567 / 10));
    }

    static (int width, int height) ParseSize(string size) => size switch
    {
        "A4" => (210, 297),
        "A3" => (297, 420),
        "A5" => (148, 210),
        "B5" => (176, 250),
        "B4" => (250, 353),
        "Letter" => (216, 279),
        "Legal" => (216, 356),
        _ => ParseCustomSize(size)
    };

    static (int, int) ParseCustomSize(string s)
    {
        var parts = s.Split('x', 'X', '×');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            return (w, h);
        return (210, 297); // fallback A4
    }
}

public sealed record PageSetupResult(int SectionsApplied, List<string> Changes);
