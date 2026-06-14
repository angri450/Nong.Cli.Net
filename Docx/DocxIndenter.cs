using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Paragraph indent control: firstLine, hanging, left, right indents
/// and outlineLevel. Applies by style role or blockId.
/// Pure OOXML — no COM.
/// </summary>
public static class DocxIndenter
{
    public record IndentOptions
    {
        public double? FirstLineMm { get; init; }   // null = don't change
        public double? HangingMm { get; init; }
        public double? LeftMm { get; init; }
        public double? RightMm { get; init; }
        public int? OutlineLevel { get; init; }      // 0-9, null = don't change
        public string? Role { get; init; }           // "heading", "body", "all" — role-based targeting
        public List<string>? BlockIds { get; init; } // explicit block IDs
    }

    public record IndentResult(int ParagraphsChanged, List<string> Changes);

    public static IndentResult Apply(string inputPath, string outputPath, IndentOptions opts)
    {
        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        var allParas = doc.Descendants()
            .Where(e => e.Name.LocalName == "p" && e.Parent?.Name.LocalName == "body")
            .ToList();

        int changed = 0;
        var changes = new List<string>();

        // Determine which paragraphs to target
        for (int pi = 0; pi < allParas.Count; pi++)
        {
            var para = allParas[pi];

            // Role filtering
            if (opts.Role != null && opts.Role != "all")
            {
                bool isHeading = para.Elements().Where(e => e.Name.LocalName == "pPr")
                    .SelectMany(pp => pp.Elements().Where(e => e.Name.LocalName == "pStyle"))
                    .Any(ps => ps.Attributes().Any(a => a.Name.LocalName == "val" && HeadingStyleIds.Contains(a.Value)));
                if (opts.Role == "heading" && !isHeading) continue;
                if (opts.Role == "body" && isHeading) continue;
            }

            var pPr = para.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");
            if (pPr == null)
            {
                pPr = new XElement(para.Name.Namespace + "pPr");
                para.AddFirst(pPr);
            }

            // Find or create ind element
            XNamespace w = para.Name.Namespace;
            var ind = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "ind");
            if (ind == null)
            {
                ind = new XElement(w + "ind");
                pPr.Add(ind);
            }

            bool paraChanged = false;

            if (opts.FirstLineMm.HasValue)
            {
                long val = (long)(opts.FirstLineMm.Value * 567 / 10);
                ind.SetAttributeValue(w + "firstLine", val);
                ind.Attribute(w + "hanging")?.Remove();
                paraChanged = true;
            }

            if (opts.HangingMm.HasValue)
            {
                long val = (long)(opts.HangingMm.Value * 567 / 10);
                ind.SetAttributeValue(w + "hanging", val);
                ind.Attribute(w + "firstLine")?.Remove();
                paraChanged = true;
            }

            if (opts.LeftMm.HasValue)
            {
                ind.SetAttributeValue(w + "left", (long)(opts.LeftMm.Value * 567 / 10));
                paraChanged = true;
            }

            if (opts.RightMm.HasValue)
            {
                ind.SetAttributeValue(w + "right", (long)(opts.RightMm.Value * 567 / 10));
                paraChanged = true;
            }

            if (opts.OutlineLevel.HasValue)
            {
                var ol = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "outlineLvl");
                if (ol == null)
                {
                    ol = new XElement(w + "outlineLvl");
                    pPr.Add(ol);
                }
                ol.SetAttributeValue(w + "val", opts.OutlineLevel.Value);
                paraChanged = true;
            }

            if (paraChanged) changed++;
        }

        if (changed > 0) changes.Add($"indent applied to {changed} paragraphs");
        if (opts.Role != null) changes.Add($"role={opts.Role}");
        if (opts.FirstLineMm.HasValue) changes.Add($"firstLine={opts.FirstLineMm.Value}mm");
        if (opts.HangingMm.HasValue) changes.Add($"hanging={opts.HangingMm.Value}mm");
        if (opts.LeftMm.HasValue) changes.Add($"left={opts.LeftMm.Value}mm");
        if (opts.RightMm.HasValue) changes.Add($"right={opts.RightMm.Value}mm");
        if (opts.OutlineLevel.HasValue) changes.Add($"outlineLevel={opts.OutlineLevel.Value}");

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new IndentResult(changed, changes);
    }

    // Standard heading style IDs and their Chinese/local names
    static readonly HashSet<string> HeadingStyleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "1","2","3","4","5","6","7","8","9",             // standard
        "Heading1","Heading2","Heading3","Heading4","Heading5","Heading6","Heading7","Heading8","Heading9",
        "21","31","4","5","6","7","8","9",                 // Chinese template style IDs
        "heading 1","heading 2","heading 3","heading 4","heading 5","heading 6","heading 7","heading 8","heading 9",
        "heading1","heading2","heading3","heading4","heading5","heading6","heading7","heading8","heading9",
        "标题1","标题2","标题3","标题4","标题5","标题6","标题7","标题8","标题9",
        "标题 1","标题 2","标题 3","标题 4","标题 5","标题 6","标题 7","标题 8","标题 9",
        "TOC","TOC Heading","toc 1","toc 2","toc 3"
    };
}
