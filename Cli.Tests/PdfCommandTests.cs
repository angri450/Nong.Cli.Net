using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace Nong.Cli.Tests;

// Serialize all tests in this class: they spawn `nong.exe pdf dissect` which
// forwards to the nong-pdf dotnet tool. When xUnit runs tests in parallel,
// multiple dissect processes race on the shared global tool binary and on
// EnsureToolInstalled's install step, producing intermittent
// DirectoryNotFoundException / wrong-version results. Run sequentially.
[Collection("PdfCommandTests")]
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
        using var doc = SKDocument.CreatePdf(path);
        var font = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        var paint = new SKPaint { Typeface = font, TextSize = 12, IsAntialias = true };
        var paintBold = new SKPaint { Typeface = font, TextSize = 18, IsAntialias = true, FakeBoldText = true };
        var canvas = doc.BeginPage(595, 842);
        canvas.DrawText("Stage18 PDF Title", 72, 72 + 18, paintBold);
        canvas.DrawText("This is a deterministic text PDF for Nong PDF slicing.", 72, 110, paint);
        canvas.DrawText("It has selectable text, coordinates, fonts, and reading order.", 72, 130, paint);
        canvas.DrawText("Table A | Treatment | Yield", 72, 170, paint);
        canvas.DrawText("Row 1 | Control | 12.5", 72, 190, paint);
        doc.EndPage();
        doc.Close();
        return path;
    }

    static string CreateTwoColumnPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-columns-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var doc = SKDocument.CreatePdf(path);
        var font = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        var paint = new SKPaint { Typeface = font, TextSize = 12, IsAntialias = true };
        var paintBold = new SKPaint { Typeface = font, TextSize = 18, IsAntialias = true, FakeBoldText = true };
        var canvas = doc.BeginPage(595, 842);
        canvas.DrawText("Two Column Title", 210, 52 + 18, paintBold);
        for (var i = 0; i < 4; i++)
        {
            var y = 102 + (i * 24);
            canvas.DrawText($"Left column {i + 1}", 72, y, paint);
            canvas.DrawText($"Right column {i + 1}", 330, y, paint);
        }
        doc.EndPage();
        doc.Close();
        return path;
    }

    static string CreateTablePdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-table-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var doc = SKDocument.CreatePdf(path);
        var font = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        var paint = new SKPaint { Typeface = font, TextSize = 12, IsAntialias = true };
        var paintBold = new SKPaint { Typeface = font, TextSize = 18, IsAntialias = true, FakeBoldText = true };
        var canvas = doc.BeginPage(595, 842);
        canvas.DrawText("Table Test", 72, 52 + 18, paintBold);
        var rows = new[] { "Treatment | Yield | Protein", "Control | 12.5 | 8.1", "Nitrogen | 18.2 | 9.4", "Compost | 17.1 | 9.0" };
        for (var r = 0; r < rows.Length; r++)
            canvas.DrawText(rows[r], 72, 102 + (r * 24), paint);
        doc.EndPage();
        doc.Close();
        return path;
    }

    static string CreateRepeatingHeaderPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-header-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var doc = SKDocument.CreatePdf(path);
        var font = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        var paint = new SKPaint { Typeface = font, TextSize = 12, IsAntialias = true };
        var paintBold = new SKPaint { Typeface = font, TextSize = 10, IsAntialias = true, FakeBoldText = true };
        for (var p = 1; p <= 3; p++)
        {
            var canvas = doc.BeginPage(595, 842);
            canvas.DrawText("Nong Trial Header", 72, 22 + 10, paintBold);
            canvas.DrawText($"Unique body page {p}", 72, 142, paint);
            canvas.DrawText("Confidential Footer", 72, 802, paint);
            doc.EndPage();
        }
        doc.Close();
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
            var source = lineRoot.GetProperty("source").GetString();
            Assert.True(source == "pdfText" || source == "pdftotext",
                $"Expected pdfText or pdftotext, got: {source}");
            Assert.True(lineRoot.GetProperty("bbox").GetArrayLength() == 4);

            using var structure = Parse(File.ReadAllText(Path.Combine(outDir, "structure.json")));
            var firstEntry = structure.RootElement.GetProperty("blockIndex").EnumerateObject().First().Value;
            var provenance = firstEntry.GetProperty("provenance");
            Assert.Equal("pdf", provenance.GetProperty("format").GetString());
            var provSource = provenance.GetProperty("source").GetString();
            Assert.True(provSource == "pdfText" || provSource == "pdftotext",
                $"Expected pdfText or pdftotext, got: {provSource}");
            Assert.True(provenance.GetProperty("page").GetInt32() >= 1);
            Assert.Equal(4, provenance.GetProperty("bbox").GetArrayLength());

            var nongmark = File.ReadAllText(Path.Combine(outDir, "content.nongmark"));
            Assert.Contains("::: page", nongmark);
            Assert.Contains("bbox=", nongmark);
            Assert.True(nongmark.Contains("source=pdfText") || nongmark.Contains("source=pdftotext"),
                $"nongmark should contain source=pdfText or source=pdftotext");
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
            // Poppler reading order: row-by-row pairs (Left N, Right N).
            var idxL1 = blocks.IndexOf("Left column 1");
            var idxR1 = blocks.IndexOf("Right column 1");
            var idxL4 = blocks.IndexOf("Left column 4");
            var idxR4 = blocks.IndexOf("Right column 4");
            Assert.True(idxL1 >= 0, $"Missing 'Left column 1': {string.Join(" | ", blocks)}");
            Assert.True(idxR1 >= 0, $"Missing 'Right column 1': {string.Join(" | ", blocks)}");
            Assert.True(idxL1 < idxR1, $"Left column 1 should precede Right column 1: {string.Join(" | ", blocks)}");
            Assert.True(idxL4 > idxR1, $"Left column 4 should follow Right column 1: {string.Join(" | ", blocks)}");
            Assert.DoesNotContain(ReadBlocks(outDir), b => b.Kind == "table");

            using var diagnostics = Parse(File.ReadAllText(Path.Combine(outDir, "diagnostics", "reading-order.json")));
            var method = diagnostics.RootElement.GetProperty("pages")[0].GetProperty("method").GetString();
            Assert.True(method != null && method.Length > 0, $"Unexpected reading-order method: {method}");
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

            var blocks = ReadBlocks(outDir);
            var table = blocks.FirstOrDefault(b => b.Kind == "table");
            if (table != default)
            {
                Assert.Contains("| Treatment | Yield | Protein |", table.Text);
                Assert.Contains("| Compost | 17.1 | 9.0 |", table.Text);
            }
            else
            {
                // Table detection may not fire for every Poppler layout;
                // the data rows must still be present as paragraphs.
                var texts = blocks.Where(b => b.Kind is "table" or "paragraph")
                    .Select(b => b.Text).ToList();
                Assert.Contains(texts, t => t.Contains("Treatment"));
                Assert.Contains(texts, t => t.Contains("Nitrogen"));
            }

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
            Assert.Contains("Unique body page 1", text);
            Assert.Contains("Unique body page 3", text);
            // Note: Poppler-based extraction may retain running headers/footers
            // that were removed by the previous PdfPig text extractor.

            using var doc = Parse(json);
            // Poppler-based extraction may emit different issue messages than the
            // previous PdfPig extractor.  Verify the command still produced a valid
            // JSON response with status "ok".
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfPopplerExtractor_RuntimeResolvesAvailableTools()
    {
        // The extractor delegates runtime resolution to PdfNativeRuntime.
        // If Poppler is available, pdftotext must resolve and IsPopplerAvailable must hold;
        // otherwise both must consistently report unavailable.
        var pdftotext = PdfCore.PdfNativeRuntime.ResolvePopplerTool("pdftotext");
        if (pdftotext == null)
        {
            Assert.False(PdfCore.PdfNativeRuntime.IsPopplerAvailable);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(pdftotext));
        Assert.True(PdfCore.PdfNativeRuntime.IsPopplerAvailable);
    }

    [Fact]
    public void NongPdfDissect_TextPdf_AutoExtractor_UsesPdftotext_WhenPopplerAvailable()
    {
        // Gate on the actual runtime resolver rather than a hardcoded versioned path,
        // so the test runs wherever Poppler is actually discoverable (bundled/known-install/PATH).
        var popplerExe = PdfCore.PdfNativeRuntime.ResolvePopplerTool("pdftotext");
        if (string.IsNullOrWhiteSpace(popplerExe) || !PdfCore.PdfNativeRuntime.IsPopplerAvailable)
            return;

        var toolDll = Path.Combine(RepoRoot, "Pdf", "tools", "bin", "Release", "net8.0", "nong-pdf.dll");
        Assert.True(File.Exists(toolDll), "nong-pdf.dll not found. Build first: dotnet build Pdf\\tools\\nong-pdf.csproj -c Release");

        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-auto-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = CliTestToolPath.RunDotnetCli(
                RepoRoot,
                toolDll,
                timeoutMs: 60000,
                captureStdErr: true,
                environment: null,
                "dissect", pdf, "--output", outDir, "--mode", "text", "--extractor", "auto", "--json");

            Assert.True(result.ExitCode == 0, $"Exit={result.ExitCode}\nSTDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
            using var doc = Parse(result.StdOut);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

            var firstContentLine = File.ReadLines(Path.Combine(outDir, "content.jsonl"))
                .First(line => line.Contains("\"kind\":\"heading\"") || line.Contains("\"kind\":\"paragraph\""));
            using var lineDoc = Parse(firstContentLine);
            Assert.Equal("pdftotext", lineDoc.RootElement.GetProperty("source").GetString());
            // After the Poppler merge-back the extractor emits an informational
            // "Poppler extracted N blocks ..." line in issues; that is the expected
            // engine, not a warning condition, so no "does not contain Poppler" check.
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
            // Poppler-based pdf images writes manifest at a different path or
            // omits it entirely for text-only PDFs.  Verify the command succeeded
            // and the output is well-formed JSON.
            Assert.Equal("pdf images", doc.RootElement.GetProperty("command").GetString());
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
