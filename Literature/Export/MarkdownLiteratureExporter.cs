using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;

namespace Angri450.Nong.Literature.Export;

public static class MarkdownLiteratureExporter
{
    public static string Export(IEnumerable<PaperRecord> records, string style = "gbt7714")
    {
        if (!string.Equals(style, "gbt7714", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(style, "basic", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported markdown citation style: {style}", nameof(style));

        var lines = new List<string> { "# References", "" };
        var index = 1;
        foreach (var record in records)
        {
            lines.Add(CitationFormatter.ToGbt7714Like(record, index++));
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static void Write(string path, IEnumerable<PaperRecord> records, string style = "gbt7714")
    {
        EnsureParent(path);
        File.WriteAllText(path, Export(records, style));
        JsonLiteratureExporter.ValidateArtifact(path);
    }

    static void EnsureParent(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }
}
