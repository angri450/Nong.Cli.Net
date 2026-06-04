using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nong.Cli.Tests;

public class OcrCommandTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, int exitCode) Run(params string[] args)
    {
        var allArgs = new List<string> { NongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30000);
        var json = proc.StandardOutput.ReadToEnd();
        return (json, proc.ExitCode);
    }

    (string stdout, string stderr, int exitCode) RunWithStderr(params string[] args)
    {
        var allArgs = new List<string> { NongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30000);
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        return (stdout, stderr, proc.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    // ===== Test 1: check-env returns environment status =====

    [Fact]
    public void CheckEnv_ReturnsOk_WithEnvFields()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "check-env", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr check-env", root.GetProperty("command").GetString());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("imageAnalyzer", out _));
        Assert.True(data.TryGetProperty("cloudToken", out _));
        Assert.True(data.TryGetProperty("localModel", out _));
        Assert.True(data.TryGetProperty("pythonFallback", out _));
    }

    // ===== Test 2: analyze-image with missing file returns E001 =====

    [Fact]
    public void AnalyzeImage_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "analyze-image", "missing.png", "-o", "out", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 3: local returns E005 (model missing) or E009 (not yet implemented) =====

    [Fact]
    public void OcrLocal_Returns_E005_Or_E009()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "local", "test.png", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        var code = doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
        Assert.True(code is "E005" or "E009", $"Expected E005 or E009, got {code}");
    }

    // ===== Test 4: cloud with missing file returns E001 =====

    [Fact]
    public void OcrCloud_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "cloud", "missing.png", "-o", "out", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 5: models returns empty array =====

    [Fact]
    public void Models_ReturnsOk_WithModelsArray()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "models", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr models", root.GetProperty("command").GetString());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("models", out var models));
        Assert.Equal(JsonValueKind.Array, models.ValueKind);
    }

    // ===== Test 6: install-model pp-ocrv5-mobile returns E009 =====

    [Fact]
    public void InstallModel_PpOcrV5Mobile_Returns_E009()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "pp-ocrv5-mobile", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E009", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 7: install-model invalid-id returns E006 =====

    [Fact]
    public void InstallModel_InvalidId_Returns_E006()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "invalid-id", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 8: to-word with missing file returns E001 =====

    [Fact]
    public void ToWord_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "to-word", "missing.png", "-o", "out.docx", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 9: Error messages do not leak token values =====

    [Fact]
    public void OcrErrors_DoNotLeakToken()
    {
        RequireCli();

        // Run several OCR error paths and verify no token-like patterns in output
        var commands = new[]
        {
            new[] { "ocr", "cloud", "missing.png", "-o", "out", "--json" },
            new[] { "ocr", "local", "test.png", "--json" },
            new[] { "ocr", "analyze-image", "missing.png", "-o", "out", "--json" },
            new[] { "ocr", "to-word", "missing.png", "-o", "out.docx", "--json" },
            new[] { "ocr", "install-model", "invalid-id", "--json" },
        };

        foreach (var args in commands)
        {
            var (stdout, stderr, _) = RunWithStderr(args);
            var combined = stdout + stderr;

            // API tokens commonly start with "sk-" or contain "bearer"
            Assert.DoesNotContain("sk-", combined, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bearer", combined, StringComparison.OrdinalIgnoreCase);
        }
    }
}
