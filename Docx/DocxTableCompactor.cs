using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Compacts tables by removing fixed row heights, adjusting column widths,
/// centering on the page, and letting cells flow to their natural content
/// height. Also preserves and applies OOXML pagination controls:
/// keepNext (keep with next), keepLines (keep lines together),
/// cantSplit (don't split row across pages).
/// Pure OOXML manipulation — no COM, no SkiaSharp.
/// </summary>
public static class DocxTableCompactor
{
    public static TableCompactResult Compact(string inputPath, string outputPath, bool autoHeight = false)
    {
        File.Copy(inputPath, outputPath, true);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;

        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        // Get page text width
        long textWidthTwips = GetTextWidthTwips(doc);

        var tables = doc.Descendants()
            .Where(e => e.Name.LocalName == "tbl")
            .ToList();

        var modified = new List<TableCompactInfo>();
        int tIdx = 0;

        foreach (var tbl in tables)
        {
            var info = CompactTable(tbl, textWidthTwips, tIdx, autoHeight);
            modified.Add(info);
            tIdx++;
        }

        // Write back
        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open())
            doc.Save(ws);

        return new TableCompactResult(modified);
    }

    static TableCompactInfo CompactTable(XElement tbl, long textWidthTwips, int index, bool autoHeight)
    {
        var changes = new List<string>();
        int rowsBefore = 0, rowsAfter = 0;

        // 1. Fix table width to 100% page width
        var tblPr = tbl.Elements().FirstOrDefault(e => e.Name.LocalName == "tblPr");
        if (tblPr == null)
        {
            tblPr = new XElement(XName.Get("tblPr",
                "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));
            tbl.AddFirst(tblPr);
        }

        // Set width to 100% (5000 fiftieths-of-percent)
        var tblW = tblPr.Elements().FirstOrDefault(e => e.Name.LocalName == "tblW");
        if (tblW == null) tblW = new XElement(XName.Get("tblW",
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));
        tblW.SetAttributeValue(XName.Get("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"), "5000");
        tblW.SetAttributeValue(XName.Get("type", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"), "pct");
        if (!tblPr.Elements().Any(e => e.Name.LocalName == "tblW"))
            tblPr.Add(tblW);
        changes.Add("tableWidth=100%");

        // 2. Center table
        var jc = tblPr.Elements().FirstOrDefault(e => e.Name.LocalName == "jc");
        if (jc == null) jc = new XElement(XName.Get("jc",
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));
        jc.SetAttributeValue(XName.Get("val", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"), "center");
        if (!tblPr.Elements().Any(e => e.Name.LocalName == "jc"))
            tblPr.Add(jc);
        changes.Add("centered");

        // 3. Count rows
        var rows = tbl.Descendants().Where(e => e.Name.LocalName == "tr").ToList();
        rowsBefore = rows.Count;

        // 4. Remove fixed row heights — change exact to atLeast
        int fixedRows = 0;
        foreach (var tr in rows)
        {
            var trPr = tr.Elements().FirstOrDefault(e => e.Name.LocalName == "trPr");
            if (trPr != null)
            {
                var trHeight = trPr.Elements().FirstOrDefault(e => e.Name.LocalName == "trHeight");
                if (trHeight != null)
                {
                    var rule = trHeight.Attribute(XName.Get("hRule",
                        "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));
                    var rule2 = trHeight.Attribute(XName.Get("rule",
                        "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));

                    if (rule?.Value == "exact" || rule2?.Value == "exact")
                    {
                        if (rule != null) rule.Value = autoHeight ? "auto" : "atLeast";
                        if (rule2 != null) rule2.Value = autoHeight ? "auto" : "atLeast";
                        if (!autoHeight)
                        {
                            var val = trHeight.Attribute(XName.Get("val",
                                "http://schemas.openxmlformats.org/wordprocessingml/2006/main"));
                            if (val != null && long.TryParse(val.Value, out long hv))
                            { hv = Math.Max(200, hv / 2); val.Value = hv.ToString(); }
                        }
                        fixedRows++;
                    }
                    else if (autoHeight)
                    {
                        // atLeast → auto: remove height constraint entirely
                        if (rule != null && rule.Value == "atLeast") rule.Value = "auto";
                        if (rule2 != null && rule2.Value == "atLeast") rule2.Value = "auto";
                        fixedRows++;
                    }
                }
            }
        }
        if (fixedRows > 0) changes.Add($"fixedRows→atLeast:{fixedRows}");

        // 5. Re-equalize column widths — find max columns per row, set equal widths
        int maxCols = rows.Max(r => r.Elements()
            .Where(e => e.Name.LocalName == "tc").Count());
        if (maxCols > 1 && textWidthTwips > 0)
        {
            long colWidth = textWidthTwips / maxCols;
            foreach (var tr in rows)
            {
                foreach (var tc in tr.Elements().Where(e => e.Name.LocalName == "tc"))
                {
                    var tcPr = tc.Elements().FirstOrDefault(e => e.Name.LocalName == "tcPr");
                    if (tcPr == null) continue;
                    var tcW = tcPr.Elements().FirstOrDefault(e => e.Name.LocalName == "tcW");
                    if (tcW != null)
                    {
                        tcW.SetAttributeValue(XName.Get("w",
                            "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                            colWidth.ToString());
                        tcW.SetAttributeValue(XName.Get("type",
                            "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
                            "dxa");
                    }
                }
            }
            changes.Add($"equalColumns:{maxCols}x{colWidth}dxa");
        }

        // 6. Pagination control: preserve + enhance keepNext / cantSplit
        var paginationChanges = ApplyPaginationControl(tbl, rows);
        changes.AddRange(paginationChanges);

        rowsAfter = rows.Count;
        return new TableCompactInfo(index, rowsBefore, rowsAfter, changes);
    }

    /// <summary>
    /// Preserves existing keepNext/keepLines on all cell paragraphs, and
    /// adds keepNext to header-row cells so they stay glued to the next row.
    /// Also marks fragile rows with cantSplit (don't split across pages).
    /// </summary>
    static List<string> ApplyPaginationControl(XElement tbl, List<XElement> rows)
    {
        var changes = new List<string>();
        int keepNextExisting = 0, keepLinesExisting = 0;
        int keepNextAdded = 0, cantSplitAdded = 0;

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        for (int r = 0; r < rows.Count; r++)
        {
            var cells = rows[r].Elements().Where(e => e.Name.LocalName == "tc").ToList();

            // Detect header rows: bold text in first cell, or table has <w:tblHeader/> repeat
            bool isHeader = false;
            if (r == 0 && tbl.Descendants().Any(e => e.Name.LocalName == "tblHeader"))
                isHeader = true;

            if (cells.Count > 0)
            {
                var firstCellText = string.Concat(
                    cells[0].Descendants().Where(e => e.Name.LocalName == "t")
                    .Select(t => t.Value));

                var firstCellHasBold = cells[0].Descendants()
                    .Any(e => e.Name.LocalName == "b");

                if (firstCellHasBold && !string.IsNullOrWhiteSpace(firstCellText))
                    isHeader = true;
            }

            // For each cell paragraph: preserve existing keepNext/keepLines
            foreach (var tc in cells)
            {
                foreach (var para in tc.Elements().Where(e => e.Name.LocalName == "p"))
                {
                    var pPr = para.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");

                    // Count existing
                    if (pPr != null)
                    {
                        if (pPr.Elements().Any(e => e.Name.LocalName == "keepNext"))
                            keepNextExisting++;
                        if (pPr.Elements().Any(e => e.Name.LocalName == "keepLines"))
                            keepLinesExisting++;
                    }
                }
            }

            // Add keepNext to header cells (glue header to next row)
            if (isHeader && r < rows.Count - 1)
            {
                foreach (var tc in cells)
                {
                    foreach (var para in tc.Elements().Where(e => e.Name.LocalName == "p"))
                    {
                        var pPr = para.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");
                        if (pPr == null)
                        {
                            pPr = new XElement(w + "pPr");
                            para.AddFirst(pPr);
                        }
                        if (!pPr.Elements().Any(e => e.Name.LocalName == "keepNext"))
                        {
                            pPr.Add(new XElement(w + "keepNext"));
                            keepNextAdded++;
                        }
                    }
                }
            }

            // Add cantSplit to rows that are fragile (multi-line content in cells)
            bool hasMultiLineCells = cells.Any(tc =>
            {
                var paraCount = tc.Elements().Where(e => e.Name.LocalName == "p").Count();
                var textLength = string.Concat(
                    tc.Descendants().Where(e => e.Name.LocalName == "t")
                    .Select(t => t.Value)).Length;
                return paraCount > 1 || textLength > 200;
            });

            if (hasMultiLineCells)
            {
                var trPr = rows[r].Elements().FirstOrDefault(e => e.Name.LocalName == "trPr");
                if (trPr == null)
                {
                    trPr = new XElement(w + "trPr");
                    rows[r].AddFirst(trPr);
                }
                if (!trPr.Elements().Any(e => e.Name.LocalName == "cantSplit"))
                {
                    trPr.Add(new XElement(w + "cantSplit"));
                    cantSplitAdded++;
                }
            }
        }

        if (keepNextExisting > 0) changes.Add($"keepNext-preserved:{keepNextExisting}");
        if (keepLinesExisting > 0) changes.Add($"keepLines-preserved:{keepLinesExisting}");
        if (keepNextAdded > 0) changes.Add($"keepNext-added:{keepNextAdded}");
        if (cantSplitAdded > 0) changes.Add($"cantSplit-added:{cantSplitAdded}");

        return changes;
    }

    static long GetTextWidthTwips(XDocument doc)
    {
        var sectPr = doc.Descendants()
            .Where(e => e.Name.LocalName == "sectPr").LastOrDefault();
        if (sectPr == null) return 8788; // A4 default

        long pw = 11906, ml = 1701, mr = 1417;
        var pgSz = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
        if (pgSz != null)
            foreach (var a in pgSz.Attributes())
                if (a.Name.LocalName == "w") long.TryParse(a.Value, out pw);
        var pgMar = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");
        if (pgMar != null)
            foreach (var a in pgMar.Attributes())
            {
                if (a.Name.LocalName == "left") long.TryParse(a.Value, out ml);
                if (a.Name.LocalName == "right") long.TryParse(a.Value, out mr);
            }

        return pw - ml - mr;
    }
}

public sealed record TableCompactInfo(
    int TableIndex, int RowsBefore, int RowsAfter, List<string> Changes);

public sealed record TableCompactResult(List<TableCompactInfo> Tables)
{
    public int TablesModified => Tables.Count(t => t.Changes.Count > 0);
    public int FixedRowsTotal => Tables.Sum(t => t.Changes.Count(c => c.StartsWith("fixedRows")));
}
