using System.Text.Json;
using ShapeCrawler;
using Xunit;

namespace Nong.Cli.Tests;

public class PptxCreateBackCompatTests
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

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    static string WriteTempJson(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "pptx-backcompat-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void OldSpec_KindTitleSubtitleItems_GeneratesSameSlideCount()
    {
        RequireCli();
        var spec = """
{
  "slides": [
    {"kind": "title", "title": "T", "subtitle": "S", "author": "A"},
    {"kind": "content", "title": "C", "items": ["a", "b"]}
  ]
}
""";
        var specPath = WriteTempJson(spec);
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-old-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        try
        {
            var (_, exitCode) = Run("pptx", "create", specPath, "-o", outPath);
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outPath));
            using var pres = new Presentation(outPath);
            Assert.Equal(2, pres.Slides.Count);

            // Verify title text is present
            var allText = string.Join(" ", pres.Slides[0].Shapes
                .Select(s => s.TextBox?.Text ?? ""));
            Assert.Contains("T", allText);
        }
        finally
        {
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void OldSpec_SingleTitleSlide_OpensWithoutError()
    {
        RequireCli();
        var spec = """
{"slides":[{"kind":"title","title":"Only Title","subtitle":"","author":""}]}
""";
        var specPath = WriteTempJson(spec);
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-single-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        try
        {
            var (_, exitCode) = Run("pptx", "create", specPath, "-o", outPath);
            Assert.Equal(0, exitCode);
            using var pres = new Presentation(outPath);
            Assert.Single(pres.Slides);
        }
        finally
        {
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void OldSpec_ContentOnlySlide_GeneratesSlide()
    {
        RequireCli();
        var spec = """
{"slides":[{"kind":"content","title":"Bullet Slide","items":["item1","item2","item3"]}]}
""";
        var specPath = WriteTempJson(spec);
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-content-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        try
        {
            var (_, exitCode) = Run("pptx", "create", specPath, "-o", outPath);
            Assert.Equal(0, exitCode);
            using var pres = new Presentation(outPath);
            Assert.Single(pres.Slides);
        }
        finally
        {
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }
}
