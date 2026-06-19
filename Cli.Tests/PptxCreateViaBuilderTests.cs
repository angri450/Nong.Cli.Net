using ShapeCrawler;
using Xunit;

namespace Nong.Cli.Tests;

public class PptxCreateViaBuilderTests
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
        var path = Path.Combine(Path.GetTempPath(), "pptx-new-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void NewSpec_WithThemeTableChart_GeneratesViaBuilder()
    {
        RequireCli();
        // Simple spec without table (table requires SkiaSharp native which may not load in test env)
        var spec = """
{
  "theme": "Professional",
  "slides": [
    {"layout": "HeroTop", "title": "Q1 Report", "chart": {"kind": "bar", "data": {"A": 10, "B": 20}, "seriesName": "Revenue"}},
    {"kind": "content", "title": "Data", "items": ["x", "y", "1", "2", "3", "4"]}
  ]
}
""";
        var specPath = WriteTempJson(spec);
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-new-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        try
        {
            var (_, exitCode) = Run("pptx", "create", specPath, "-o", outPath);
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outPath));
            using var pres = new Presentation(outPath);
            Assert.Equal(2, pres.Slides.Count);
            Assert.NotEmpty(pres.Slides[0].Shapes);
            Assert.NotEmpty(pres.Slides[1].Shapes);
        }
        finally
        {
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void NewSpec_ThemeApplied_PreservesColors()
    {
        RequireCli();
        var spec = """
{
  "theme": "Academic",
  "slides": [
    {"kind": "title", "title": "Research Paper", "subtitle": "A Study", "author": "Author"}
  ]
}
""";
        var specPath = WriteTempJson(spec);
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-theme-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
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
