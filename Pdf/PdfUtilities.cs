using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfCore;

internal static class PdfUtilities
{
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static readonly JsonSerializerOptions JsonlOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static void ValidatePdfPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "PDF path is required.");
        if (!File.Exists(path))
            throw new PdfProcessingException(PdfErrorKind.FileNotFound, $"File not found: {path}");
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new PdfProcessingException(PdfErrorKind.UnsupportedFormat, $"Expected .pdf file, got: {Path.GetExtension(path)}");

        Span<byte> header = stackalloc byte[5];
        using var fs = File.OpenRead(path);
        if (fs.Read(header) < 5 || Encoding.ASCII.GetString(header) != "%PDF-")
            throw new PdfProcessingException(PdfErrorKind.UnsupportedFormat, "File does not start with a PDF header.");
    }

    internal static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    internal static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, value.GetType(), JsonOpts), Encoding.UTF8);
    }

    internal static string Preview(string? text, int length = 100)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= length ? normalized : normalized[..length] + "...";
    }

    internal static string FormatDouble(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static string FormatBbox(IReadOnlyList<double> bbox) =>
        string.Join(",", bbox.Select(FormatDouble));

    internal static double[] UnionBbox(IEnumerable<double[]> boxes)
    {
        var valid = boxes.Where(b => b.Length >= 4).ToList();
        if (valid.Count == 0) return Array.Empty<double>();
        return
        [
            valid.Min(b => b[0]),
            valid.Min(b => b[1]),
            valid.Max(b => b[2]),
            valid.Max(b => b[3]),
        ];
    }

    internal static string SanitizeText(string value) =>
        value.Replace('\u0000', ' ').Trim();
}
