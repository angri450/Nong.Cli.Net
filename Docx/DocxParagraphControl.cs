using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Paragraph pagination control: keepNext, keepLines, pageBreakBefore, widowControl.
/// Applies by style role (heading/body/all).
/// Pure OOXML — no COM.
/// </summary>
public static class DocxParagraphControl
{
    public record PaginationOptions
    {
        public bool? KeepNext { get; init; }         // 与下段同页
        public bool? KeepLines { get; init; }         // 段中不分页
        public bool? PageBreakBefore { get; init; }   // 段前分页
        public bool? WidowControl { get; init; }      // 孤行控制
        public string? Role { get; init; }            // "heading", "body", "all"
    }

    public record PaginationResult(int ParagraphsChanged, List<string> Changes);

    public static PaginationResult Apply(string inputPath, string outputPath, PaginationOptions opts)
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
        if (allParas.Count == 0) return new PaginationResult(0, changes);
        XNamespace w = allParas.First().Name.Namespace;
        // Same heading style detection as DocxIndenter
        var hIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "1","2","3","4","5","6","7","8","9","21","31","Heading1","Heading2","Heading3","Heading4","Heading5","Heading6",
          "heading 1","heading 2","heading 3","标题1","标题2","标题3","标题 1","标题 2","标题 3" };

        foreach (var para in allParas)
        {
            // Role filtering
            if (opts.Role != null && opts.Role != "all")
            {
                bool isHeading = para.Elements().Where(e => e.Name.LocalName == "pPr")
                    .SelectMany(pp => pp.Elements().Where(e => e.Name.LocalName == "pStyle"))
                    .Any(ps => ps.Attributes().Any(a => a.Name.LocalName == "val" && hIds.Contains(a.Value)));
                if (opts.Role == "heading" && !isHeading) continue;
                if (opts.Role == "body" && isHeading) continue;
            }

            var pPr = para.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");
            if (pPr == null)
            {
                pPr = new XElement(w + "pPr");
                para.AddFirst(pPr);
            }

            bool paraChanged = false;

            if (opts.KeepNext.HasValue)
            {
                var el = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "keepNext");
                if (opts.KeepNext.Value && el == null) { pPr.Add(new XElement(w + "keepNext")); paraChanged = true; }
                else if (!opts.KeepNext.Value && el != null) { el.Remove(); paraChanged = true; }
            }

            if (opts.KeepLines.HasValue)
            {
                var el = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "keepLines");
                if (opts.KeepLines.Value && el == null) { pPr.Add(new XElement(w + "keepLines")); paraChanged = true; }
                else if (!opts.KeepLines.Value && el != null) { el.Remove(); paraChanged = true; }
            }

            if (opts.PageBreakBefore.HasValue)
            {
                var el = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pageBreakBefore");
                if (opts.PageBreakBefore.Value && el == null) { pPr.Add(new XElement(w + "pageBreakBefore")); paraChanged = true; }
                else if (!opts.PageBreakBefore.Value && el != null) { el.Remove(); paraChanged = true; }
            }

            if (opts.WidowControl.HasValue)
            {
                var el = pPr.Elements().FirstOrDefault(e => e.Name.LocalName == "widowControl");
                if (opts.WidowControl.Value && el == null) { pPr.Add(new XElement(w + "widowControl")); paraChanged = true; }
                else if (!opts.WidowControl.Value && el != null) { el.Remove(); paraChanged = true; }
            }

            if (paraChanged) changed++;
        }

        if (opts.KeepNext.HasValue) changes.Add($"keepNext={opts.KeepNext.Value}:{changed}p");
        if (opts.KeepLines.HasValue) changes.Add($"keepLines={opts.KeepLines.Value}");
        if (opts.PageBreakBefore.HasValue) changes.Add($"pageBreakBefore={opts.PageBreakBefore.Value}");
        if (opts.WidowControl.HasValue) changes.Add($"widowControl={opts.WidowControl.Value}");
        if (opts.Role != null) changes.Add($"role={opts.Role}");

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new PaginationResult(changed, changes);
    }
}
