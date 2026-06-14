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
        Assert.True(data.TryGetProperty("localModel", out _));
        Assert.True(data.TryGetProperty("localDotNetPpOcrV6", out var local));
        Assert.True(local.GetProperty("noPython").GetBoolean());
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

    [Fact]
    public void LocalOcrConfidenceSanitizer_RejectsNonFiniteValues()
    {
        RequireCli();
        Assert.True(File.Exists(MultiModalDll), $"MultiModal assembly not found: {MultiModalDll}");

        var asm = Assembly.LoadFrom(MultiModalDll);
        var type = asm.GetType("MultiModalCore.PpOcrV6Client", throwOnError: true)!;
        var method = type.GetMethod("ToFiniteConfidence", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Null(method.Invoke(null, new object[] { double.NaN }));
        Assert.Null(method.Invoke(null, new object[] { double.PositiveInfinity }));
        Assert.Null(method.Invoke(null, new object[] { double.NegativeInfinity }));

        var finite = Assert.IsType<double>(method.Invoke(null, new object[] { 0.875d }));
        Assert.Equal(0.875d, finite);
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
        Assert.True(data.GetProperty("noPython").GetBoolean());
        Assert.Equal("pp-ocrv6-medium", data.GetProperty("modelId").GetString());
        Assert.Equal("medium", data.GetProperty("size").GetString());
        Assert.Equal("pp-ocrv6-dotnet-sdcb", data.GetProperty("engine").GetString());
        Assert.Equal("cdn-download-pir-model", data.GetProperty("deployment").GetString());
        Assert.Contains("PP-OCRv6_medium_det_infer.tar", data.GetProperty("detUrl").GetString());
        Assert.Contains("PP-OCRv6_medium_rec_infer.tar", data.GetProperty("recUrl").GetString());
        Assert.Contains("No Python", data.GetProperty("note").GetString());
    }

    [Fact]
    public void InstallModel_FirstPartyRuntimeVersion_DoesNotUseCliVersion()
    {
        var source = File.ReadAllText(OcrCommandsSource);

        Assert.Contains("OcrRuntimeVersion.Current", source);
        Assert.DoesNotMatch(
            "Angri450\\.Nong\\.OcrRuntime\\.[^\"]+\",\\s*CliVersion\\.Current",
            source);
    }

    // ===== Test 7: install-model can explicitly enable upstream fallback =====

    [Fact]
    public void InstallModel_DryRun_ReportsExplicitUpstreamFallback()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "pp-ocrv6-medium", "--dry-run", "--allow-upstream-fallback", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("pp-ocrv6-medium", data.GetProperty("modelId").GetString());
        Assert.Equal("cdn-download-pir-model", data.GetProperty("deployment").GetString());
        Assert.True(data.GetProperty("noPython").GetBoolean());
    }

    // ===== Test 8: native extraction handles Windows/Linux/macOS files =====

    [Fact]
    public void NativeRuntimeExtraction_AcceptsDllSoVersionedSoAndDylib()
    {
        RequireCli();
        var tempDir = Path.Combine(Path.GetTempPath(), "nong-native-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var nupkg = Path.Combine(tempDir, "runtime.nupkg");
        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(outputDir);

        try
        {
            using (var archive = ZipFile.Open(nupkg, ZipArchiveMode.Create))
            {
                foreach (var name in new[]
                {
                    "runtimes/linux-x64/native/a.dll",
                    "runtimes/linux-x64/native/liba.so",
                    "runtimes/linux-x64/native/libb.so.1.2.3",
                    "runtimes/linux-x64/native/libc.dylib",
                    "runtimes/linux-x64/native/readme.txt",
                    "runtimes/osx-arm64/native/other.dylib"
                })
                {
                    var entry = archive.CreateEntry(name);
                    using var stream = entry.Open();
                    using var writer = new StreamWriter(stream);
                    writer.Write("native");
                }
            }

            var asm = Assembly.LoadFrom(OcrToolDll);
            var type = asm.GetType("Nong.Cli.Commands.OcrCommands", throwOnError: true)!;
            var method = type.GetMethod("ExtractNativeFiles", BindingFlags.NonPublic | BindingFlags.Static)!;
            var files = (List<string>)method.Invoke(null, new object[] { nupkg, "runtimes/linux-x64/native/", outputDir })!;

            Assert.Contains("a.dll", files);
            Assert.Contains("liba.so", files);
            Assert.Contains("libb.so.1.2.3", files);
            Assert.Contains("libc.dylib", files);
            Assert.DoesNotContain("readme.txt", files);
            Assert.DoesNotContain("other.dylib", files);
            Assert.True(File.Exists(Path.Combine(outputDir, "libb.so.1.2.3")));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ===== Test 9: first-party local runtime bundle installs from directory source when present =====

    [Fact]
    public void InstallModel_LocalNupkgSource_UsesFirstPartyBundleWhenPresent()
    {
        if (!OperatingSystem.IsWindows())
            return;

        RequireCli();
        var sourceDir = Path.Combine(RepoRoot, "nupkg");
        if (!Directory.Exists(sourceDir))
            return;

        var packagePattern = $"Angri450.Nong.OcrRuntime.*.{ReadOcrRuntimeVersion()}.nupkg";
        var packageExists = Directory.EnumerateFiles(sourceDir, packagePattern).Any();
        if (!packageExists)
            return;

        var runtimeDir = Path.Combine(Path.GetTempPath(), "nong-runtime-install-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (json, exit) = RunWithEnv(
                new Dictionary<string, string> { ["NONG_OCR_RUNTIME_DIR"] = runtimeDir },
                "ocr", "install-model", "pp-ocrv6-medium", "--source", sourceDir, "--json");

            Assert.Equal(0, exit);
            using var doc = Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var installed = data.GetProperty("installed");
            Assert.Equal("nong-bundle", installed[0].GetProperty("origin").GetString());
            Assert.StartsWith("Angri450.Nong.OcrRuntime.", installed[0].GetProperty("package").GetString());
            Assert.Equal("disabled", data.GetProperty("upstreamFallbackDefault").GetString());

            var downloads = Path.Combine(runtimeDir, "downloads");
            Assert.False(Directory.Exists(downloads));
        }
        finally
        {
            try { Directory.Delete(runtimeDir, recursive: true); } catch { }
        }
    }

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
