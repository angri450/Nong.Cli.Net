using ShapeCrawler;
using Xunit;

namespace Nong.Cli.Tests;

public class PptxPictureTests
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

    static string CreateTestImage(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "pptx-img-" + name + ".png");
        if (File.Exists(path)) return path;

        // Minimal valid 1x1 red PNG
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    static string CreateBasePptx()
    {
        var outPath = Path.Combine(Path.GetTempPath(), "pptx-base-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        var spec = """{"slides":[{"kind":"title","title":"Base"}]}""";
        var specPath = Path.Combine(Path.GetTempPath(), "pptx-base-spec-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(specPath, spec);
        var result = CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null,
            "pptx", "create", specPath, "-o", outPath);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create base pptx: {result.StdErr}");
        return outPath;
    }

    [Fact]
    public void AddPicture_DefaultSignature_InsertsWithoutError()
    {
        RequireCli();
        var basePptx = CreateBasePptx();
        try
        {
            using var pres = new Presentation(basePptx);
            pres.Slides.Add(1);
            var slide = pres.Slides[pres.Slides.Count - 1];
            using var img = File.OpenRead(CreateTestImage("default"));

            // Current: throws NotImplementedException
            var ex = Record.Exception(() => slide.Shapes.AddPicture(img));
            Assert.Null(ex); // Should not throw after fix
        }
        finally { try { File.Delete(basePptx); } catch { } }
    }

    [Fact]
    public void AddPicture_WithStream_ShapeHasPictureProperty()
    {
        RequireCli();
        var basePptx = CreateBasePptx();
        try
        {
            using var pres = new Presentation(basePptx);
            pres.Slides.Add(1);
            var slide = pres.Slides[pres.Slides.Count - 1];
            using var img = File.OpenRead(CreateTestImage("prop"));

            slide.Shapes.AddPicture(img);
            var shapes = slide.Shapes.ToList();
            Assert.Contains(shapes, s => s.Picture != null);
        }
        finally { try { File.Delete(basePptx); } catch { } }
    }
}
