using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Character-level (run) formatting: underline, strikethrough, color, highlight,
/// character spacing, superscript/subscript. Targets runs by text content or
/// regex pattern. Pure OOXML — no COM.
/// </summary>
public static class DocxRunFormatter
{
    public record RunFormatOptions
    {
        public string? Underline { get; init; }     // "single", "double", "none"
        public string? UnderlineColor { get; init; } // hex, default auto
        public bool? Strikethrough { get; init; }
        public string? Color { get; init; }          // hex e.g. "FF0000" or "none"
        public string? Highlight { get; init; }      // "yellow", "cyan", "none", etc.
        public double? SpacingMm { get; init; }      // character spacing in mm
        public bool? Superscript { get; init; }
        public bool? Subscript { get; init; }
        public string? Pattern { get; init; }        // regex to match text content
        public string? Role { get; init; }           // "heading", "body", "all"
    }

    public record RunFormatResult(int RunsChanged, List<string> Changes);

    public static RunFormatResult Apply(string inputPath, string outputPath, RunFormatOptions opts)
    {
        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        int changed = 0;
        var changes = new List<string>();

        var regex = opts.Pattern != null ? new System.Text.RegularExpressions.Regex(opts.Pattern) : null;

        var allParas = doc.Descendants().Where(e => e.Name.LocalName == "p" && e.Parent?.Name.LocalName == "body").ToList();
        // Role detection (same as indent)
        var hIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "1","2","3","4","5","6","7","8","9","21","31","Heading1","Heading2","Heading3","Heading4","Heading5","Heading6",
          "heading 1","heading 2","heading 3","标题1","标题2","标题3","标题 1","标题 2","标题 3" };

        foreach (var para in allParas)
        {
            // Role filter
            if (opts.Role != null && opts.Role != "all")
            {
                bool isHeading = para.Elements().Where(e => e.Name.LocalName == "pPr")
                    .SelectMany(pp => pp.Elements().Where(e => e.Name.LocalName == "pStyle"))
                    .Any(ps => ps.Attributes().Any(a => a.Name.LocalName == "val" && hIds.Contains(a.Value)));
                if (opts.Role == "heading" && !isHeading) continue;
                if (opts.Role == "body" && isHeading) continue;
            }

            var runs = para.Elements().Where(e => e.Name.LocalName == "r").ToList();
            foreach (var run in runs)
            {
                // Get text content for pattern matching
                var text = string.Concat(run.Elements().Where(e => e.Name.LocalName == "t")
                    .Select(t => t.Value));
                if (regex != null && !regex.IsMatch(text)) continue;

                var rPr = run.Elements().FirstOrDefault(e => e.Name.LocalName == "rPr");
                if (rPr == null)
                {
                    rPr = new XElement(w + "rPr");
                    run.AddFirst(rPr);
                }

                bool rChanged = false;

                if (opts.Underline != null)
                {
                    var u = rPr.Elements().FirstOrDefault(e => e.Name.LocalName == "u");
                    if (opts.Underline == "none") { u?.Remove(); }
                    else
                    {
                        if (u == null) { u = new XElement(w + "u"); rPr.Add(u); }
                        u.SetAttributeValue(w + "val", opts.Underline);
                        if (opts.UnderlineColor != null) u.SetAttributeValue(w + "color", opts.UnderlineColor);
                    }
                    rChanged = true;
                }

                if (opts.Strikethrough.HasValue)
                {
                    var st = rPr.Elements().FirstOrDefault(e => e.Name.LocalName == "strike");
                    if (opts.Strikethrough.Value && st == null) { rPr.Add(new XElement(w + "strike")); rChanged = true; }
                    else if (!opts.Strikethrough.Value && st != null) { st.Remove(); rChanged = true; }
                }

                if (opts.Color != null)
                {
                    var c = rPr.Elements().FirstOrDefault(e => e.Name.LocalName == "color");
                    if (opts.Color == "none") { c?.Remove(); }
                    else
                    {
                        if (c == null) { c = new XElement(w + "color"); rPr.Add(c); }
                        c.SetAttributeValue(w + "val", opts.Color);
                    }
                    rChanged = true;
                }

                if (opts.Highlight != null)
                {
                    var h = rPr.Elements().FirstOrDefault(e => e.Name.LocalName == "highlight");
                    if (opts.Highlight == "none") { h?.Remove(); }
                    else
                    {
                        if (h == null) { h = new XElement(w + "highlight"); rPr.Add(h); }
                        h.SetAttributeValue(w + "val", opts.Highlight);
                    }
                    rChanged = true;
                }

                if (opts.SpacingMm.HasValue)
                {
                    var sp = rPr.Elements().FirstOrDefault(e => e.Name.LocalName == "spacing");
                    if (sp == null) { sp = new XElement(w + "spacing"); rPr.Add(sp); }
                    sp.SetAttributeValue(w + "val", (int)(opts.SpacingMm.Value * 567 / 10));
                    rChanged = true;
                }

                if (opts.Superscript.HasValue && opts.Superscript.Value)
                {
                    if (!rPr.Elements().Any(e => e.Name.LocalName == "vertAlign" && e.Attribute("val")?.Value == "superscript"))
                    {
                        rPr.Add(new XElement(w + "vertAlign", new XAttribute(w + "val", "superscript")));
                        rChanged = true;
                    }
                }

                if (opts.Subscript.HasValue && opts.Subscript.Value)
                {
                    if (!rPr.Elements().Any(e => e.Name.LocalName == "vertAlign" && e.Attribute("val")?.Value == "subscript"))
                    {
                        rPr.Add(new XElement(w + "vertAlign", new XAttribute(w + "val", "subscript")));
                        rChanged = true;
                    }
                }

                if (rChanged) changed++;
            }
        }

        if (changed > 0) changes.Add($"formatted {changed} runs");
        if (opts.Underline != null) changes.Add($"underline={opts.Underline}");
        if (opts.Color != null) changes.Add($"color={opts.Color}");
        if (opts.Role != null) changes.Add($"role={opts.Role}");

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new RunFormatResult(changed, changes);
    }
}
