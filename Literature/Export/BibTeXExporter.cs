using System.Text;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;

namespace Angri450.Nong.Literature.Export;

public static class BibTeXExporter
{
    public static string Export(IEnumerable<PaperRecord> records)
    {
        var builder = new StringBuilder();
        var index = 1;
        foreach (var record in records)
        {
            builder.AppendLine(CitationFormatter.ToBibTeX(record, index++));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static void Write(string path, IEnumerable<PaperRecord> records)
    {
        EnsureParent(path);
        File.WriteAllText(path, Export(records));
        JsonLiteratureExporter.ValidateArtifact(path);
    }

    static void EnsureParent(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }
}
