using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Cell-level formatting: borders, shading, margins, vertical alignment, merge/split.
/// Pure OOXML — no COM.
/// </summary>
public static class DocxCellFormatter
{
    public record CellFormatOptions
    {
        public int? TableIndex { get; init; }   // 0-based, null=all tables
        public int? RowIndex { get; init; }     // 0-based, null=all rows in target table
        public int? ColIndex { get; init; }     // 0-based, null=all columns in target row
        public string? Shading { get; init; }   // hex color e.g. "2A7A65" or "none" to remove
        public double? BorderTopMm { get; init; }
        public double? BorderBottomMm { get; init; }
        public double? BorderLeftMm { get; init; }
        public double? BorderRightMm { get; init; }
        public string? BorderColor { get; init; }  // hex, default "2A7A65"
        public string? VAlign { get; init; }       // "top", "center", "bottom"
        public double? PaddingTopMm { get; init; }
        public double? PaddingLeftMm { get; init; }
        public double? PaddingBottomMm { get; init; }
        public double? PaddingRightMm { get; init; }
    }

    public record CellFormatResult(int CellsChanged, List<string> Changes);

    public static CellFormatResult Apply(string inputPath, string outputPath, CellFormatOptions opts)
    {
        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var tables = doc.Descendants().Where(e => e.Name.LocalName == "tbl").ToList();
        int changed = 0;
        var changes = new List<string>();

        string borderColor = opts.BorderColor ?? "2A7A65";

        for (int ti = 0; ti < tables.Count; ti++)
        {
            if (opts.TableIndex.HasValue && ti != opts.TableIndex.Value) continue;
            var rows = tables[ti].Elements().Where(e => e.Name.LocalName == "tr").ToList();

            for (int ri = 0; ri < rows.Count; ri++)
            {
                if (opts.RowIndex.HasValue && ri != opts.RowIndex.Value) continue;
                var cells = rows[ri].Elements().Where(e => e.Name.LocalName == "tc").ToList();

                for (int ci = 0; ci < cells.Count; ci++)
                {
                    if (opts.ColIndex.HasValue && ci != opts.ColIndex.Value) continue;

                    var tc = cells[ci];
                    var tcPr = tc.Elements().FirstOrDefault(e => e.Name.LocalName == "tcPr");
                    if (tcPr == null)
                    {
                        tcPr = new XElement(w + "tcPr");
                        tc.AddFirst(tcPr);
                    }

                    bool cellChanged = false;

                    // --- Shading ---
                    if (opts.Shading != null)
                    {
                        var shd = tcPr.Elements().FirstOrDefault(e => e.Name.LocalName == "shd");
                        if (opts.Shading == "none")
                        {
                            shd?.Remove();
                        }
                        else
                        {
                            if (shd == null) { shd = new XElement(w + "shd"); tcPr.Add(shd); }
                            shd.SetAttributeValue(w + "val", "clear");
                            shd.SetAttributeValue(w + "color", "auto");
                            shd.SetAttributeValue(w + "fill", opts.Shading);
                        }
                        cellChanged = true;
                    }

                    // --- Vertical alignment ---
                    if (opts.VAlign != null)
                    {
                        var va = tcPr.Elements().FirstOrDefault(e => e.Name.LocalName == "vAlign");
                        if (va == null) { va = new XElement(w + "vAlign"); tcPr.Add(va); }
                        va.SetAttributeValue(w + "val", opts.VAlign.ToLower());
                        cellChanged = true;
                    }

                    // --- Cell margins ---
                    bool hasPad = opts.PaddingTopMm.HasValue || opts.PaddingLeftMm.HasValue
                        || opts.PaddingBottomMm.HasValue || opts.PaddingRightMm.HasValue;
                    if (hasPad)
                    {
                        var mar = tcPr.Elements().FirstOrDefault(e => e.Name.LocalName == "tcMar");
                        if (mar == null) { mar = new XElement(w + "tcMar"); tcPr.Add(mar); }
                        if (opts.PaddingTopMm.HasValue) SetMargin(mar, w + "top", opts.PaddingTopMm.Value);
                        if (opts.PaddingLeftMm.HasValue) SetMargin(mar, w + "left", opts.PaddingLeftMm.Value);
                        if (opts.PaddingBottomMm.HasValue) SetMargin(mar, w + "bottom", opts.PaddingBottomMm.Value);
                        if (opts.PaddingRightMm.HasValue) SetMargin(mar, w + "right", opts.PaddingRightMm.Value);
                        cellChanged = true;
                    }

                    // --- Cell borders ---
                    bool hasBorders = opts.BorderTopMm.HasValue || opts.BorderBottomMm.HasValue
                        || opts.BorderLeftMm.HasValue || opts.BorderRightMm.HasValue;
                    if (hasBorders)
                    {
                        var borders = tcPr.Elements().FirstOrDefault(e => e.Name.LocalName == "tcBorders");
                        if (borders == null) { borders = new XElement(w + "tcBorders"); tcPr.Add(borders); }
                        var sz8 = (long)(8 * 12700 / 254); // 8 pt in EMU

                        if (opts.BorderTopMm.HasValue) SetBorder(borders, w + "top", opts.BorderTopMm.Value, borderColor);
                        if (opts.BorderBottomMm.HasValue) SetBorder(borders, w + "bottom", opts.BorderBottomMm.Value, borderColor);
                        if (opts.BorderLeftMm.HasValue) SetBorder(borders, w + "left", opts.BorderLeftMm.Value, borderColor);
                        if (opts.BorderRightMm.HasValue) SetBorder(borders, w + "right", opts.BorderRightMm.Value, borderColor);
                        cellChanged = true;
                    }

                    if (cellChanged) changed++;
                }
            }
        }

        if (changed > 0) changes.Add($"cells formatted: {changed}");
        if (opts.Shading != null) changes.Add($"shading={opts.Shading}");
        if (opts.VAlign != null) changes.Add($"vAlign={opts.VAlign}");
        if (opts.BorderColor != null) changes.Add($"borderColor={opts.BorderColor}");

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new CellFormatResult(changed, changes);
    }

    static void SetMargin(XElement mar, XName name, double mm)
    {
        var el = mar.Elements().FirstOrDefault(e => e.Elements().Any());
        var e = new XElement(name,
            new XAttribute("w", (long)(mm * 567 / 10)),
            new XAttribute("type", "dxa"));
        var existing = mar.Elements().FirstOrDefault(x => x.Name == name);
        if (existing != null) existing.ReplaceWith(e);
        else mar.Add(e);
    }

    static void SetBorder(XElement borders, XName name, double mm, string color)
    {
        long sz = (long)(mm * 12700 / 25.4); // mm → EMU
        var b = new XElement(name,
            new XAttribute("val", "single"),
            new XAttribute("sz", (int)(sz / 12700)),
            new XAttribute("color", color),
            new XAttribute("space", "0"));
        var existing = borders.Elements().FirstOrDefault(e => e.Name == name);
        if (existing != null) existing.ReplaceWith(b);
        else borders.Add(b);
    }
}
