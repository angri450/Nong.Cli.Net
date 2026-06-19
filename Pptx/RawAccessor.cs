using System.IO.Compression;
using System.Text;

namespace PptxCore;

/// <summary>
/// L3 raw OOXML access for operations SlideBuilder cannot express.
/// Read or modify any part inside a .pptx (which is a ZIP file), save as a new file.
/// Use only as a last resort — prefer SlideBuilder first.
/// </summary>
public sealed class RawAccessor : IDisposable
{
    private readonly Dictionary<string, string> _entries;
    private bool _disposed;
    private static readonly System.Xml.Linq.SaveOptions SaveOptions = System.Xml.Linq.SaveOptions.DisableFormatting;

    /// <summary>Opens a .pptx file for raw access.</summary>
    public RawAccessor(string path)
    {
        _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            _entries[entry.FullName] = reader.ReadToEnd();
        }
    }

    /// <summary>Opens a presentation object for raw access (saves to temp first).</summary>
    public RawAccessor(ShapeCrawler.IPresentation pres) : this(SaveToTemp(pres)) { }

    private static string SaveToTemp(ShapeCrawler.IPresentation pres)
    {
        var tmp = Path.GetTempFileName() + ".pptx";
        pres.Save(tmp);
        return tmp;
    }

    /// <summary>Saves the modified package back to the presentation.</summary>
    public void SaveTo(ShapeCrawler.IPresentation pres, string outputPath)
    {
        SaveAs(outputPath);
    }

    /// <summary>Lists all part paths in the package.</summary>
    public IReadOnlyList<string> ListParts() => _entries.Keys.ToList();

    /// <summary>Gets raw XML content of a part.</summary>
    /// <param name="partPath">e.g. "/ppt/slides/slide1.xml", "/ppt/presentation.xml", "/[Content_Types].xml"</param>
    public string GetPart(string partPath)
    {
        // Normalize path
        string key = partPath.TrimStart('/');
        if (_entries.TryGetValue(key, out var xml))
            return xml;
        throw new KeyNotFoundException($"Part not found: '{partPath}'. Use ListParts() to see available parts.");
    }

    /// <summary>Sets (overwrites) raw XML content of a part.</summary>
    public void SetPart(string partPath, string xml)
    {
        string key = partPath.TrimStart('/');
        if (_entries.ContainsKey(key))
            _entries[key] = xml;
        else
            throw new KeyNotFoundException($"Part not found: '{partPath}'. Create new parts with AddPart().");
    }

    /// <summary>Adds a new part to the package.</summary>
    public void AddPart(string partPath, string xml)
    {
        string key = partPath.TrimStart('/');
        _entries[key] = xml;
    }

    /// <summary>Removes a part from the package.</summary>
    public void RemovePart(string partPath)
    {
        string key = partPath.TrimStart('/');
        _entries.Remove(key);
    }

    /// <summary>Saves the modified package to a new file.</summary>
    public void SaveAs(string outputPath)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        foreach (var (name, xml) in _entries)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(xml);
        }
    }

    /// <summary>Removes a slide by index and updates all references. Uses string replacement to preserve original namespace declarations.</summary>
    public void RemoveSlide(int index)
    {
        var slidePath = $"ppt/slides/slide{index + 1}.xml";
        var relsPath = $"ppt/slides/_rels/slide{index + 1}.xml.rels";
        var bundlePath = $"ppt/slides/slide{index + 1}.xml.bundle";
        RemovePart(slidePath);
        if (_entries.ContainsKey(relsPath)) RemovePart(relsPath);
        if (_entries.ContainsKey(bundlePath)) RemovePart(bundlePath);

        // Remove Nth sldId element from ppt/presentation.xml (by position, not rId)
        if (_entries.TryGetValue("ppt/presentation.xml", out var presXml))
        {
            var sldIdPattern = @"<p:sldId[^>]*?/>";
            var matches = System.Text.RegularExpressions.Regex.Matches(presXml, sldIdPattern);
            if (index < matches.Count)
            {
                var m = matches[index];
                presXml = presXml.Remove(m.Index, m.Length);
            }
            _entries["ppt/presentation.xml"] = presXml;
        }

        // Remove relationship for slideN.xml from ppt/_rels/presentation.xml.rels
        if (_entries.TryGetValue("ppt/_rels/presentation.xml.rels", out var relsXml))
        {
            var target = $"slides/slide{index + 1}.xml";
            var pattern = $@"<Relationship[^>]*?\bTarget=""{target}""[^>]*?/>";
            relsXml = System.Text.RegularExpressions.Regex.Replace(relsXml, pattern, "");
            _entries["ppt/_rels/presentation.xml.rels"] = relsXml;
        }

        // Remove Override from [Content_Types].xml
        if (_entries.TryGetValue("[Content_Types].xml", out var ctXml))
        {
            var partName = $"/ppt/slides/slide{index + 1}.xml";
            var pattern = $@"<Override[^>]*?\bPartName=""{System.Text.RegularExpressions.Regex.Escape(partName)}""[^>]*?/>";
            ctXml = System.Text.RegularExpressions.Regex.Replace(ctXml, pattern, "");
            _entries["[Content_Types].xml"] = ctXml;
        }
    }

    /// <summary>Moves a slide from one position to another using position-based string replacement.</summary>
    public void MoveSlide(int fromIndex, int toIndex)
    {
        if (_entries.TryGetValue("ppt/presentation.xml", out var presXml))
        {
            var sldIdPattern = @"<p:sldId[^>]*?/>";
            var matches = System.Text.RegularExpressions.Regex.Matches(presXml, sldIdPattern);
            if (fromIndex < matches.Count && toIndex < matches.Count)
            {
                var fromStr = matches[fromIndex].Value;
                // Remove from original position
                var removeIdx = matches[fromIndex].Index;
                presXml = presXml.Remove(removeIdx, fromStr.Length);
                // Re-find target position (indexes shift after removal)
                var newMatches = System.Text.RegularExpressions.Regex.Matches(presXml, sldIdPattern);
                if (toIndex >= fromIndex) toIndex--; // shift down since we removed one before it
                if (toIndex < newMatches.Count)
                {
                    var insertIdx = newMatches[toIndex].Index;
                    presXml = presXml.Insert(insertIdx, fromStr);
                }
            }
            _entries["ppt/presentation.xml"] = presXml;
        }
        if (_entries.TryGetValue("ppt/_rels/presentation.xml.rels", out var relsXml))
        {
            var fromTarget = $"slides/slide{fromIndex + 1}.xml";
            var toTarget = $"slides/slide{toIndex + 1}.xml";
            var fromPat = $@"<Relationship[^>]*?\bTarget=""{fromTarget}""[^>]*?/>";
            var toPat = $@"<Relationship[^>]*?\bTarget=""{toTarget}""[^>]*?/>";
            var fromMatch = System.Text.RegularExpressions.Regex.Match(relsXml, fromPat);
            if (fromMatch.Success)
            {
                var fromStr = fromMatch.Value;
                relsXml = System.Text.RegularExpressions.Regex.Replace(relsXml, fromPat, "");
                var toMatch = System.Text.RegularExpressions.Regex.Match(relsXml, toPat);
                if (toMatch.Success)
                    relsXml = relsXml.Insert(toIndex >= fromIndex ? toMatch.Index + toMatch.Length : toMatch.Index, fromStr);
            }
            _entries["ppt/_rels/presentation.xml.rels"] = relsXml;
        }
    }

    /// <summary>Duplicates a slide by index.</summary>
    public void DuplicateSlide(int index)
    {
        var srcPath = $"ppt/slides/slide{index + 1}.xml";
        var slideCount = _entries.Keys.Count(k => k.StartsWith("ppt/slides/slide") && k.EndsWith(".xml") && !k.Contains("_rels"));
        var newIdx = slideCount + 1;
        if (_entries.TryGetValue(srcPath, out var slideXml))
        {
            // Rename shape IDs to avoid conflicts
            var doc = System.Xml.Linq.XDocument.Parse(slideXml);
            var ns = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/presentationml/2006/main");
            foreach (var nvPr in doc.Descendants(ns + "cNvPr"))
            {
                var idAttr = nvPr.Attribute("id");
                if (idAttr != null && uint.TryParse(idAttr.Value, out var id))
                    idAttr.Value = (id + 1000).ToString();
            }
            AddPart($"ppt/slides/slide{newIdx}.xml", doc.ToString(SaveOptions));
        }
        // Update presentation.xml
        if (_entries.TryGetValue("ppt/presentation.xml", out var presXml))
        {
            var pdoc = System.Xml.Linq.XDocument.Parse(presXml);
            var pns = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/presentationml/2006/main");
            var rNs = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            var last = pdoc.Descendants(pns + "sldId").LastOrDefault();
            var newId = last != null ? new System.Xml.Linq.XElement(pns + "sldId",
                new System.Xml.Linq.XAttribute("id", "256"),
                new System.Xml.Linq.XAttribute(rNs + "id", $"rId{100 + newIdx}")) : null;
            if (newId != null) pdoc.Descendants(pns + "sldIdLst").First().Add(newId);
            _entries["ppt/presentation.xml"] = pdoc.ToString(SaveOptions);
        }
        // Update [Content_Types].xml
        if (_entries.TryGetValue("[Content_Types].xml", out var ctXml))
        {
            var cdoc = System.Xml.Linq.XDocument.Parse(ctXml);
            var ctNs = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types");
            cdoc.Root?.Add(new System.Xml.Linq.XElement(ctNs + "Override",
                new System.Xml.Linq.XAttribute("PartName", $"/ppt/slides/slide{newIdx}.xml"),
                new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.presentationml.slide+xml")));
            _entries["[Content_Types].xml"] = cdoc.ToString(SaveOptions);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _entries.Clear();
            _disposed = true;
        }
    }
}
