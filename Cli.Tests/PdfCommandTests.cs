using System.Diagnostics;
using System.Text.Json;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Nong.Cli.Tests;

public class PdfCommandTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, int exitCode) Run(params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: true,
            environment: null,
            args);
        return (result.StdOut, result.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    static string CreateTextPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-text-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(595, 842);
        page.AddText("Stage18 PDF Title", 18, new PdfPoint(72, 760), bold);
        page.AddText("This is a deterministic text PDF for Nong PDF slicing.", 12, new PdfPoint(72, 720), font);
        page.AddText("It has selectable text, coordinates, fonts, and reading order.", 12, new PdfPoint(72, 700), font);
        page.AddText("Table A | Treatment | Yield", 12, new PdfPoint(72, 660), font);
        page.AddText("Row 1 | Control | 12.5", 12, new PdfPoint(72, 640), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    static string CreateTwoColumnPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-columns-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(595, 842);
        page.AddText("Two Column Title", 18, new PdfPoint(210, 790), bold);
        for (var i = 0; i < 4; i++)
        {
            var y = 740 - (i * 24);
            page.AddText($"Left column {i + 1}", 12, new PdfPoint(72, y), font);
            page.AddText($"Right column {i + 1}", 12, new PdfPoint(330, y), font);
        }
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    static string CreateTablePdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-table-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(595, 842);
        page.AddText("Table Test", 18, new PdfPoint(72, 790), bold);

        var rows = new[]
        {
            new[] { "Treatment", "Yield", "Protein" },
            new[] { "Control", "12.5", "8.1" },
            new[] { "Nitrogen", "18.2", "9.4" },
            new[] { "Compost", "17.1", "9.0" },
        };
        for (var r = 0; r < rows.Length; r++)
        {
            var y = 740 - (r * 24);
            page.AddText(rows[r][0], 12, new PdfPoint(72, y), font);
            page.AddText(rows[r][1], 12, new PdfPoint(240, y), font);
            page.AddText(rows[r][2], 12, new PdfPoint(380, y), font);
        }

        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    static string CreateRepeatingHeaderPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-header-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        for (var p = 1; p <= 3; p++)
        {
            var page = builder.AddPage(595, 842);
            page.AddText("Nong Trial Header", 10, new PdfPoint(72, 820), bold);
            page.AddText($"Unique body page {p}", 12, new PdfPoint(72, 700), font);
            page.AddText("Confidential Footer", 10, new PdfPoint(72, 40), font);
        }
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    static List<(string Kind, string Text)> ReadBlocks(string outDir)
    {
        var blocks = new List<(string Kind, string Text)>();
        foreach (var line in File.ReadLines(Path.Combine(outDir, "content.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var kind = root.GetProperty("kind").GetString() ?? "";
            var text = root.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString() ?? ""
                : "";
            blocks.Add((kind, text));
        }
        return blocks;
    }

    [Fact]
    public void PdfCheck_TextPdf_ReturnsClassification()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        try
        {
            var (json, exit) = Run("pdf", "check", pdf, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var root = doc.RootElement;
            Assert.Equal("ok", root.GetProperty("status").GetString());
            Assert.Equal("pdf check", root.GetProperty("command").GetString());
            Assert.Equal("text", root.GetProperty("data").GetProperty("classification").GetString());
            Assert.True(root.GetProperty("data").GetProperty("textCharCount").GetInt32() > 0);
        }
        finally { try { File.Delete(pdf); } catch { } }
    }

    [Fact]
    public void PdfDissect_TextPdf_WritesNongMarkSlice()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "dissect", pdf, "--output", outDir, "--mode", "auto", "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("pdf dissect", doc.RootElement.GetProperty("command").GetString());

            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "document.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "format.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(outDir, "diagnostics.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "preview", "content.txt")));
            Assert.False(File.Exists(Path.Combine(outDir, "preview", "content.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "diagnostics", "check.json")));
            Assert.True(new FileInfo(Path.Combine(outDir, "content.nongmark")).Length > 0);

            using var manifest = Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")));
            Assert.Equal("nong-pandoc/package/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("pdf", manifest.RootElement.GetProperty("source").GetProperty("format").GetString());
            Assert.Equal("content.nongmark", manifest.RootElement.GetProperty("streams").GetProperty("contentNongMark").GetString());
            Assert.Equal("diagnostics.json", manifest.RootElement.GetProperty("streams").GetProperty("diagnostics").GetString());

            var firstContentLine = File.ReadLines(Path.Combine(outDir, "content.jsonl"))
                .First(line => line.Contains("\"kind\":\"heading\"") || line.Contains("\"kind\":\"paragraph\""));
            using var lineDoc = Parse(firstContentLine);
            var lineRoot = lineDoc.RootElement;
            Assert.False(string.IsNullOrWhiteSpace(lineRoot.GetProperty("blockId").GetString()));
            Assert.True(lineRoot.GetProperty("page").GetInt32() >= 1);
            Assert.Equal("pdfText", lineRoot.GetProperty("source").GetString());
            Assert.True(lineRoot.GetProperty("bbox").GetArrayLength() == 4);

            using var structure = Parse(File.ReadAllText(Path.Combine(outDir, "structure.json")));
            var firstEntry = structure.RootElement.GetProperty("blockIndex").EnumerateObject().First().Value;
            var provenance = firstEntry.GetProperty("provenance");
            Assert.Equal("pdf", provenance.GetProperty("format").GetString());
            Assert.Equal("pdfText", provenance.GetProperty("source").GetString());
            Assert.True(provenance.GetProperty("page").GetInt32() >= 1);
            Assert.Equal(4, provenance.GetProperty("bbox").GetArrayLength());

            var nongmark = File.ReadAllText(Path.Combine(outDir, "content.nongmark"));
            Assert.Contains("::: page", nongmark);
            Assert.Contains("bbox=", nongmark);
            Assert.Contains("source=pdfText", nongmark);
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfDissect_TwoColumnPdf_UsesColumnReadingOrder()
    {
        RequireCli();
        var pdf = CreateTwoColumnPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-columns-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "dissect", pdf, "--output", outDir, "--mode", "auto", "--json");
            Assert.Equal(0, exit);

            var blocks = ReadBlocks(outDir).Where(b => b.Kind is "heading" or "paragraph").Select(b => b.Text).ToList();
            Assert.True(blocks.IndexOf("Left column 4") < blocks.IndexOf("Right column 1"),
                string.Join(" | ", blocks));
            Assert.DoesNotContain(ReadBlocks(outDir), b => b.Kind == "table");

            using var diagnostics = Parse(File.ReadAllText(Path.Combine(outDir, "diagnostics", "reading-order.json")));
            Assert.Equal("two-column-left-then-right",
                diagnostics.RootElement.GetProperty("pages")[0].GetProperty("method").GetString());
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfDissect_AlignedRows_EmitsTableBlock()
    {
        RequireCli();
        var pdf = CreateTablePdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-table-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "dissect", pdf, "--output", outDir, "--mode", "auto", "--json");
            Assert.Equal(0, exit);

            var table = ReadBlocks(outDir).Single(b => b.Kind == "table");
            Assert.Contains("| Treatment | Yield | Protein |", table.Text);
            Assert.Contains("| Compost | 17.1 | 9.0 |", table.Text);

            using var doc = Parse(json);
            Assert.True(doc.RootElement.GetProperty("metrics").GetProperty("blocks").GetInt32() >= 2);
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfDissect_RepeatingHeaderFooter_RemovesRunningText()
    {
        RequireCli();
        var pdf = CreateRepeatingHeaderPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-header-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "dissect", pdf, "--output", outDir, "--mode", "auto", "--json");
            Assert.Equal(0, exit);

            var text = string.Join("\n", ReadBlocks(outDir).Select(b => b.Text));
            Assert.DoesNotContain("Nong Trial Header", text);
            Assert.DoesNotContain("Confidential Footer", text);
            Assert.Contains("Unique body page 1", text);
            Assert.Contains("Unique body page 3", text);

            using var doc = Parse(json);
            Assert.Contains(doc.RootElement.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("message").GetString()!.Contains("repeated header/footer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfImages_TextPdf_WritesEmptyManifest()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-images-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "images", pdf, "--output", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            using var manifest = Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")));
            Assert.Equal(0, manifest.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfCheck_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("pdf", "check", "nonexistent.pdf", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void PdfCheck_NonPdf_Returns_E002()
    {
        RequireCli();
        var path = Path.Combine(Path.GetTempPath(), "nong-not-pdf-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        File.WriteAllText(path, "not pdf");
        try
        {
            var (json, exit) = Run("pdf", "check", path, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E002", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
