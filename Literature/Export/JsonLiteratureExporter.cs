using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Export;

public static class JsonLiteratureExporter
{
    public static string Export(LiteratureSearchResult result) => JsonSerializer.Serialize(result, Options());

    public static string ExportRecords(IEnumerable<PaperRecord> records) => JsonSerializer.Serialize(records, Options());

    public static void Write(string path, LiteratureSearchResult result)
    {
        EnsureParent(path);
        File.WriteAllText(path, Export(result));
        ValidateArtifact(path);
    }

    public static void WriteRecords(string path, IEnumerable<PaperRecord> records)
    {
        EnsureParent(path);
        File.WriteAllText(path, ExportRecords(records));
        ValidateArtifact(path);
    }

    public static LiteratureSearchResult ReadResultOrRecords(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var records = document.RootElement.Deserialize<List<PaperRecord>>(Options()) ?? new List<PaperRecord>();
            return new LiteratureSearchResult { Records = records };
        }

        var result = document.RootElement.Deserialize<LiteratureSearchResult>(Options());
        if (result is not null)
            return result;

        if (document.RootElement.TryGetProperty("records", out var recordsElement))
        {
            var records = recordsElement.Deserialize<List<PaperRecord>>(Options()) ?? new List<PaperRecord>();
            return new LiteratureSearchResult { Records = records };
        }

        return new LiteratureSearchResult();
    }

    static JsonSerializerOptions Options() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static void EnsureParent(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public static void ValidateArtifact(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Literature export artifact was not created.", path);
        if (new FileInfo(path).Length == 0)
            throw new InvalidOperationException($"Literature export artifact is empty: {path}");
    }
}
