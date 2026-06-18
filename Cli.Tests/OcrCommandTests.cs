using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Nong.Cli.Tests;

public class OcrCommandTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");
    static string OcrToolDir => Path.Combine(RepoRoot, "MultiModal", "tools", "bin", "Release", "net8.0");
    static string OcrToolDll => Path.Combine(OcrToolDir, "nong-ocr.dll");
    static string MultiModalDll => Path.Combine(OcrToolDir, "MultiModalCore.dll");
    static string OcrRuntimeVersionSource => Path.Combine(RepoRoot, "Cli", "Common", "OcrRuntimeVersion.cs");
    static string OcrCommandsSource => Path.Combine(RepoRoot, "Cli", "Commands", "OcrCommands.cs");

    (string json, int exitCode) Run(params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: false,
            environment: null,
            args);
        return (result.StdOut, result.ExitCode);
    }

    (string stdout, string stderr, int exitCode) RunWithStderr(params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: true,
            environment: null,
            args);
        return (result.StdOut, result.StdErr, result.ExitCode);
    }

    (string json, int exitCode) RunWithEnv(IReadOnlyDictionary<string, string> env, params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: false,
            environment: env,
            args);
        return (result.StdOut, result.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
        Assert.True(File.Exists(OcrToolDll),
            "nong-ocr.dll not found. Build first: dotnet build MultiModal/tools/nong-ocr.csproj -c Release");
    }

    static string ReadOcrRuntimeVersion()
    {
        var source = File.ReadAllText(OcrRuntimeVersionSource);
        var match = Regex.Match(source, "public const string Current = \"(?<version>[^\"]+)\"");
        Assert.True(match.Success, $"Could not read OCR runtime version from {OcrRuntimeVersionSource}");
        return match.Groups["version"].Value;
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
        Assert.True(data.TryGetProperty("ocrV6Onnx", out var ocr));
        Assert.True(ocr.GetProperty("noPython").GetBoolean());
        Assert.Equal("pp-ocrv6-onnx", ocr.GetProperty("engine").GetString());
        Assert.Equal("onnxruntime", ocr.GetProperty("runtime").GetString());
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

    // ===== Test 3: local OCR native runtime internals =====

    [Fact(Skip = "ONNX migration — old PpOcrV6Client deleted")]
    public void LocalOcrConfidenceSanitizer_RejectsNonFiniteValues() { }

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
        Assert.True(models.GetArrayLength() >= 1);
        Assert.True(models[0].GetProperty("noPython").GetBoolean());
    }

    // ===== Test 6: install-model pp-ocrv6-medium dry-run returns OK =====

    [Fact]
    public void InstallModel_PpOcrV6Medium_DryRun_ReturnsOk()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "pp-ocrv6-medium", "--dry-run", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr install-model", root.GetProperty("command").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("pp-ocrv6-medium", data.GetProperty("modelId").GetString());
        Assert.True(data.TryGetProperty("modelDir", out _));
        Assert.True(data.TryGetProperty("det", out var det));
        Assert.Equal("det.onnx", det.GetProperty("file").GetString());
        Assert.Contains("det_onnx", det.GetProperty("url").GetString());
        Assert.Equal("ONNX Runtime (Microsoft.ML.OnnxRuntime, already included in nong CLI)", data.GetProperty("runtime").GetString());
    }

    [Fact(Skip = "ONNX migration — old install-model deleted")]
    public void InstallModel_FirstPartyRuntimeVersion_DoesNotUseCliVersion() { }

    // ===== Test 7: install-model can explicitly enable upstream fallback (skip — old path deleted) =====

    [Fact(Skip = "ONNX migration — old --allow-upstream-fallback deleted")]
    public void InstallModel_DryRun_ReportsExplicitUpstreamFallback() { }

    // ===== Test 8: native extraction (skip — old code deleted) =====

    [Fact(Skip = "ONNX migration — old native runtime extraction deleted")]
    public void NativeRuntimeExtraction_AcceptsDllSoVersionedSoAndDylib() { }

    // ===== Test 9: first-party nupkg bundle (skip — old code deleted) =====

    [Fact(Skip = "ONNX migration — old NuGet/runtime bundle deleted")]
    public void InstallModel_LocalNupkgSource_UsesFirstPartyBundleWhenPresent() { }

    // ===== Test 10: install-model invalid-id returns E006 =====

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

    // ===== Test 11: to-word with missing file returns E001 =====

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

    // ===== Test 12: Error messages do not leak token values =====

    [Fact]
    public void OcrErrors_DoNotLeakToken()
    {
        RequireCli();

        // Run several OCR error paths and verify no token-like patterns in output
        var commands = new[]
        {
            new[] { "ocr", "cloud", "missing.png", "-o", "out", "--json" },
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
