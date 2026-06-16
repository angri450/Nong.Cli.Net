using System.Text.Json;
using Nong.Cli.Common;
using PdfCore;

namespace Nong.Cli.Adapters;

public sealed class PdfOcrRecognizerAdapter : IPdfOcrRecognizer
{
    public PdfOcrRecognizeResult Recognize(string imagePath, int pageNumber)
    {
        var (exitCode, stdout, stderr) = CliHelpers.RunToolCapture(
            "nong-ocr",
            ToolPackages.Ocr,
            new[] { "local", imagePath, "--force", "--json" });

        if (exitCode != 0)
        {
            return new PdfOcrRecognizeResult
            {
                Page = pageNumber,
                Engine = "nong-ocr",
                ModelId = "external",
                Warnings = { string.IsNullOrWhiteSpace(stderr) ? "nong-ocr local failed." : stderr.Trim() }
            };
        }

        try
        {
            // The native PaddleOCR runtime may write diagnostic messages (e.g.
            // "ReduceMeanCheckIfOneDNNSupport") to stdout before the JSON output.
            // Scan for the opening brace so JsonDocument.Parse succeeds.
            var jsonStart = stdout.IndexOf('{');
            var json = jsonStart >= 0 ? stdout[jsonStart..] : stdout;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var output = new PdfOcrRecognizeResult
            {
                Page = pageNumber,
                Width = TryGetInt(data, "width"),
                Height = TryGetInt(data, "height"),
                Engine = TryGetString(data, "engine") ?? "nong-ocr",
                ModelId = TryGetString(data, "modelId") ?? "external",
            };

            if (data.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    output.Blocks.Add(new PdfOcrRecognizedBlock
                    {
                        Id = TryGetString(block, "id") ?? "",
                        Text = TryGetString(block, "text") ?? "",
                        Confidence = TryGetDouble(block, "confidence"),
                        Bbox = TryGetDoubleArray(block, "bbox"),
                        ConfidenceValid = TryGetBool(block, "confidenceValid", true),
                        GeometryValid = TryGetBool(block, "geometryValid", true),
                        NumericIssue = TryGetString(block, "numericIssue")
                    });
                }
            }

            return output;
        }
        catch (Exception ex)
        {
            return new PdfOcrRecognizeResult
            {
                Page = pageNumber,
                Engine = "nong-ocr",
                ModelId = "external",
                Warnings = { $"Failed to parse nong-ocr output: {ex.Message}" }
            };
        }
    }

    static string? TryGetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    static int TryGetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return 0;
        return value.TryGetInt32(out var i) ? i : 0;
    }

    static double? TryGetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;
    }

    static bool TryGetBool(JsonElement element, string name, bool fallback)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    static double[] TryGetDoubleArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<double>();

        var list = new List<double>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.TryGetDouble(out var d) && double.IsFinite(d))
                list.Add(d);
        }
        return list.ToArray();
    }
}
