using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nong.Cli.Tests;

public class CliContractTests
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
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30000);
        var json = proc.StandardOutput.ReadToEnd();
        return (json, proc.ExitCode);
    }

    static string TempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    // ===== Manifest tests =====

    [Fact]
    public void Commands_Json_ReturnsOnlyImplemented()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("commands", root.GetProperty("command").GetString());

        foreach (var cmd in root.GetProperty("data").EnumerateArray())
        {
            var status = cmd.GetProperty("status").GetString();
            Assert.Equal("implemented", status);
        }
    }

    [Fact]
    public void Commands_Json_UsesCanonicalWordAddNames()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var names = doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("word add paragraph", names);
        Assert.Contains("word add table", names);
        Assert.Contains("word add math", names);
        Assert.DoesNotContain("word add-paragraph", names);
        Assert.DoesNotContain("word add-table", names);
        Assert.DoesNotContain("word add-math", names);
    }

    [Fact]
    public void Commands_All_ReturnsAllCommands()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--all", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetArrayLength() >= 46, "commands --all should return 46+ entries");
        bool hasImpl = false;
        foreach (var cmd in data.EnumerateArray())
        {
            var s = cmd.GetProperty("status").GetString();
            if (s == "implemented") hasImpl = true;
        }
        Assert.True(hasImpl, "commands --all should include implemented");
    }

    [Fact]
    public void Commands_All_EveryEntryHasRequiredFields()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--all", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        foreach (var cmd in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.True(cmd.TryGetProperty("name", out _));
            Assert.True(cmd.TryGetProperty("description", out _));
            Assert.True(cmd.TryGetProperty("group", out _));
            Assert.True(cmd.TryGetProperty("status", out _));
        }
    }

    // ===== Stub / honest-error commands =====

    [Fact]
    public void OcrLocal_Returns_E005()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "local", "test.png", "--json");
        Assert.NotEqual(0, exit);
        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        var code = doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
        Assert.True(code is "E005" or "E009", $"Expected E005 or E009, got {code}");
    }

    // ===== Newly implemented commands: missing file → E001 =====

    [Theory]
    [InlineData("word", "stats", "nonexistent.docx")]
    [InlineData("word", "fonts", "nonexistent.docx")]
    [InlineData("word", "styles", "nonexistent.docx")]
    [InlineData("word", "dissect", "nonexistent.docx")]
    [InlineData("chart", "line", "nonexistent.json", "-o", "out.png")]
    [InlineData("chart", "scatter", "nonexistent.json", "-o", "out.png")]
    [InlineData("chart", "pie", "nonexistent.json", "-o", "out.png")]
    [InlineData("diagram", "tree", "nonexistent.json", "-o", "out.png")]
    [InlineData("pptx", "read", "nonexistent.pptx")]
    [InlineData("pptx", "slides", "nonexistent.pptx")]
    public void NewCommand_MissingFile_Returns_E001(params string[] args)
    {
        RequireCli();
        var allArgs = new List<string>(args) { "--json" };
        var (json, exit) = Run(allArgs.ToArray());
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Error path tests =====

    [Fact]
    public void MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("chart", "anova", "nonexistent_file.json", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void InvalidSpec_Returns_E006()
    {
        RequireCli();
        var specPath = TempFile("not valid json {{{");
        try
        {
            var outPath = Path.Combine(Path.GetTempPath(), "test-out.docx");
            var (json, exit) = Run("inspect", "write-paper", specPath, "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { File.Delete(specPath); }
    }

    // ===== JSON schema tests =====

    static readonly string[] RequiredFields =
        { "status", "command", "summary", "data", "issues", "artifacts", "metrics", "errors", "meta" };

    [Theory]
    [InlineData("word", "preview", "nonexistent.docx")]
    [InlineData("chart", "anova", "nonexistent.json")]
    [InlineData("chart", "duncan", "nonexistent.json")]
    public void ErrorResponse_HasAllSchemaFields(string group, string sub, string fileArg)
    {
        RequireCli();
        var (json, exit) = Run(group, sub, fileArg, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        foreach (var field in RequiredFields)
            Assert.True(root.TryGetProperty(field, out _), $"Missing field: {field}");

        Assert.True(root.TryGetProperty("meta", out var meta));
        Assert.True(meta.TryGetProperty("durationMs", out _), "Missing meta.durationMs");
        Assert.True(meta.TryGetProperty("version", out _), "Missing meta.version");
    }

    // ===== skill commands =====

    [Fact]
    public void SkillValidate_EmptyPath_Returns_E003()
    {
        RequireCli();
        var (json, exit) = Run("skill", "validate", "", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E003", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillValidate_MissingDir_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("skill", "validate", "C:\\nonexistent\\path", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillScan_EmptyDir_ReturnsOk()
    {
        RequireCli();
        var emptyDir = Path.Combine(Path.GetTempPath(), "nong-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var (json, exit) = Run("skill", "scan", emptyDir, "--json");
            Assert.Equal(0, exit);
            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { Directory.Delete(emptyDir); }
    }

    [Fact]
    public void SkillInventory_MissingDir_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("skill", "inventory", "C:\\nonexistent\\path", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillPackage_InvalidDir_Returns_E006()
    {
        RequireCli();
        var emptyDir = Path.Combine(Path.GetTempPath(), "nong-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var (json, exit) = Run("skill", "package", emptyDir, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { Directory.Delete(emptyDir); }
    }

    // ===== Excel pipeline tests =====

    [Fact]
    public void ExcelCreate_Sheets_Read_ToGroups_RoundTrip()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-excel-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var specPath = Path.Combine(dir, "spec.json");
            var xlsxPath = Path.Combine(dir, "test.xlsx");
            File.WriteAllText(specPath, @"{""sheets"":[{""name"":""Data"",""headers"":[""Treatment"",""Yield""],""rows"":[[""A"",1.2],[""A"",1.3],[""B"",2.1]]}]}");

            // Create xlsx
            var (createJson, createExit) = Run("excel", "create", specPath, "-o", xlsxPath, "--json");
            Assert.Equal(0, createExit);
            Assert.Contains("ok", createJson);

            // sheets
            var (sheetsJson, sheetsExit) = Run("excel", "sheets", xlsxPath, "--json");
            Assert.Equal(0, sheetsExit);

            // read
            var (readJson, readExit) = Run("excel", "read", xlsxPath, "--json");
            Assert.Equal(0, readExit);

            // to-groups --raw (pipeline mode)
            var (tgJson, tgExit) = Run("excel", "to-groups", xlsxPath, "--group", "A", "--value", "B", "--raw", "--json");
            Assert.Equal(0, tgExit);
            var tgDoc = Parse(tgJson);
            Assert.True(tgDoc.RootElement.TryGetProperty("A", out _));
            Assert.True(tgDoc.RootElement.TryGetProperty("B", out _));

            // to-groups --json (structured mode) with sheet name
            var (tgJson2, tgExit2) = Run("excel", "to-groups", xlsxPath, "--sheet", "Data", "--group", "A", "--value", "B", "--json");
            Assert.Equal(0, tgExit2);
            var tgDoc2 = Parse(tgJson2);
            Assert.Equal("excel to-groups", tgDoc2.RootElement.GetProperty("command").GetString());
            Assert.Equal("ok", tgDoc2.RootElement.GetProperty("status").GetString());

            // to-groups with non-existent sheet → E006
            var (tgErr, tgErrExit) = Run("excel", "to-groups", xlsxPath, "--sheet", "NoSuchSheet", "--group", "A", "--value", "B", "--json");
            Assert.NotEqual(0, tgErrExit);
            var tgErrDoc = Parse(tgErr);
            Assert.Equal("E006", tgErrDoc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
