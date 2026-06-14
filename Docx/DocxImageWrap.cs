using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Image wrap control: converts standalone inline drawings to floating anchors
/// with configurable wrap modes. Multi-image paragraphs (side-by-side from
/// fit-images) are kept inline to prevent overlap. Pure OOXML — no COM.
/// </summary>
public static class DocxImageWrap
{
    public record WrapOptions
    {
        public string? Mode { get; init; }
        public double? OffsetMm { get; init; }
        public string? AlignH { get; init; }
        public string? AlignV { get; init; }
    }

    public record WrapResult(int ImagesConverted, int ImagesTotal, int Skipped, List<string> Changes);

    public static WrapResult Apply(string inputPath, string outputPath, WrapOptions opts)
    {
        if (opts.Mode == "inline" || opts.Mode == null)
            return new WrapResult(0, 0, 0, new() { "no wrap mode specified" });

        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        var allParas = doc.Descendants().Where(e => e.Name.LocalName == "p").ToList();
        var allInlines = doc.Descendants().Where(e => e.Name.LocalName == "inline").ToList();

        // Skip inlines in multi-image paragraphs — they're side-by-side from fit-images
        var skipSet = new HashSet<XElement>();
        foreach (var para in allParas)
        {
            var ils = para.Descendants().Where(e => e.Name.LocalName == "inline").ToList();
            if (ils.Count >= 2)
                foreach (var il in ils) skipSet.Add(il);
        }

        int total = allInlines.Count, converted = 0, skipped = 0;
        var changes = new List<string>();
        long offsetEmu = (long)((opts.OffsetMm ?? 3) * 36000);

        foreach (var il in allInlines)
        {
            if (skipSet.Contains(il)) { skipped++; continue; }

            var drawing = il.Parent;
            if (drawing == null) continue;

            var extent = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
            var cx = extent?.Attribute("cx")?.Value ?? "0";
            var cy = extent?.Attribute("cy")?.Value ?? "0";
            var docPrId = il.Elements().FirstOrDefault(e => e.Name.LocalName == "docPr")?.Attribute("id")?.Value ?? "0";

            XNamespace wp = il.Name.Namespace;
            var anchor = new XElement(wp + "anchor",
                new XAttribute("distT", offsetEmu), new XAttribute("distB", offsetEmu),
                new XAttribute("distL", offsetEmu), new XAttribute("distR", offsetEmu),
                new XAttribute("simplePos", "0"), new XAttribute("relativeHeight", "0"),
                new XAttribute("behindDoc", opts.Mode == "behind" ? "1" : "0"),
                new XAttribute("locked", "0"), new XAttribute("layoutInCell", "1"),
                new XAttribute("allowOverlap", "1"),
                new XElement(wp + "simplePos", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(wp + "positionH", new XAttribute("relativeFrom", "column"),
                    new XElement(wp + "posOffset", "0")),
                new XElement(wp + "positionV", new XAttribute("relativeFrom", "paragraph"),
                    new XElement(wp + "posOffset", "0")),
                new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                new XElement(wp + "effectExtent",
                    new XAttribute("l", "0"), new XAttribute("t", "0"),
                    new XAttribute("r", "0"), new XAttribute("b", "0")));

            anchor.Add(opts.Mode switch
            {
                "square" => new XElement(wp + "wrapSquare", new XAttribute("wrapText", "bothSides")),
                "topAndBottom" => new XElement(wp + "wrapTopAndBottom"),
                "tight" => new XElement(wp + "wrapTight", new XAttribute("wrapText", "bothSides")),
                "through" => new XElement(wp + "wrapThrough", new XAttribute("wrapText", "bothSides")),
                "behind" => new XElement(wp + "wrapNone"),
                "inFront" => new XElement(wp + "wrapNone"),
                _ => new XElement(wp + "wrapSquare", new XAttribute("wrapText", "bothSides"))
            });

            anchor.Add(new XElement(wp + "docPr", new XAttribute("id", docPrId), new XAttribute("name", $"Pic{docPrId}")));
            anchor.Add(new XElement(wp + "cNvGraphicFramePr"));

            var graphic = il.Elements().FirstOrDefault(e => e.Name.LocalName == "graphic");
            if (graphic != null) anchor.Add(graphic);

            il.Remove();
            drawing.Add(anchor);
            converted++;
        }

        changes.Add($"converted {converted}/{total}{ (skipped > 0 ? $" (skipped {skipped} multi-image paras)" : "" )} to {opts.Mode}");
        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);
        return new WrapResult(converted, total, skipped, changes);
    }
}
