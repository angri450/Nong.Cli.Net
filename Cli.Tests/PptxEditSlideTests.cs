using ShapeCrawler;
using Xunit;

namespace Nong.Cli.Tests;

public class PptxEditSlideTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    static string CreateTestPptx(string title)
    {
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-edit-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        var spec = $"{{\"slides\":[{{\"kind\":\"title\",\"title\":\"{title}\",\"subtitle\":\"Sub\",\"author\":\"A\"}}]}}";
        var specPath = Path.Combine(Path.GetTempPath(), "pptx-edit-spec-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(specPath, spec);
        var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
            "pptx", "create", specPath, "-o", outPath);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create test pptx: {result.StdErr}");
        return outPath;
    }

    static string CreateTestImage()
    {
        var path = Path.Combine(Path.GetTempPath(), "pptx-testimg-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void PptxEditSlideCommand_ReplaceText()
    {
        RequireCli();
        var pptx = CreateTestPptx("Old Title");
        try
        {
            var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "edit-slide", pptx, "--index", "1", "--replace-text", "Old Title|New Title");
            Assert.Equal(0, result.ExitCode);
            using var pres = new Presentation(pptx);
            Assert.Contains(pres.Slides[0].Shapes, s => s.TextBox?.Text.Contains("New Title") == true);
        }
        finally { try { File.Delete(pptx); } catch { } }
    }

    [Fact]
    public void PptxRemoveSlideCommand_DecreasesCount()
    {
        RequireCli();
        var pptx = CreateTestPptx("S1");
        try
        {
            // Add a second slide by re-creating with 2 slides
            var spec2 = """{"slides":[{"kind":"title","title":"S1"},{"kind":"content","title":"S2","items":["a"]}]}""";
            var specPath = Path.GetTempFileName() + ".json";
            File.WriteAllText(specPath, spec2);
            CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "create", specPath, "-o", pptx);

            var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "remove-slide", pptx, "--index", "2");
            Assert.Equal(0, result.ExitCode);
            // Verify slide count via RawAccessor (avoids OpenXML SDK embedded resource issue)
            using var raw = new PptxCore.RawAccessor(pptx);
            var slideParts = raw.ListParts().Count(p => p.StartsWith("ppt/slides/slide") && p.EndsWith(".xml") && !p.Contains("_rels"));
            Assert.Equal(1, slideParts);
        }
        finally { try { File.Delete(pptx); } catch { } }
    }

    [Fact]
    public void PptxMoveSlideCommand_ReordersSlides()
    {
        RequireCli();
        var pptx = CreateTestPptx("First");
        try
        {
            var spec2 = """{"slides":[{"kind":"title","title":"First"},{"kind":"content","title":"Second","items":["b"]},{"kind":"content","title":"Third","items":["c"]}]}""";
            var specPath = Path.GetTempFileName() + ".json";
            File.WriteAllText(specPath, spec2);
            CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "create", specPath, "-o", pptx);

            var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "move-slide", pptx, "--from", "3", "--to", "1");
            Assert.Equal(0, result.ExitCode);
            // Verify slide count via RawAccessor
            using var raw = new PptxCore.RawAccessor(pptx);
            var slideParts = raw.ListParts().Count(p => p.StartsWith("ppt/slides/slide") && p.EndsWith(".xml") && !p.Contains("_rels"));
            Assert.Equal(3, slideParts);
        }
        finally { try { File.Delete(pptx); } catch { } }
    }

    [Fact]
    public void PptxAddImageCommand_InsertsImageIntoSlide()
    {
        RequireCli();
        var pptx = CreateTestPptx("With Image");
        var img = CreateTestImage();
        try
        {
            var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
                "pptx", "add-image", pptx, "--slide", "1", "--image", img, "--x", "100", "--y", "100");
            Assert.Equal(0, result.ExitCode);
            using var pres = new Presentation(pptx);
            Assert.Contains(pres.Slides[0].Shapes, s => s.Picture != null);
        }
        finally { try { File.Delete(pptx); } catch { } try { File.Delete(img); } catch { } }
    }
}
