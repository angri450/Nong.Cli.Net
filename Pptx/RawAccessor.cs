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
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        foreach (var (name, xml) in _entries)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(xml);
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
