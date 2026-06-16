using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nong.Cli.Tests;

public class LitCommandsJsonTests
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

    (string json, int exitCode) RunWithEnv(IReadOnlyDictionary<string, string> environment, params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: true,
            environment,
            args);
        return (result.StdOut, result.ExitCode);
    }

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    [Fact]
    public void Commands_Json_IncludesLitCommands()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);
        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("lit parse", names);
        Assert.Contains("lit validate", names);
        Assert.Contains("lit plan", names);
        Assert.Contains("lit search", names);
        Assert.Contains("lit export", names);
        Assert.Contains("lit batch", names);
        Assert.Contains("lit cache-import", names);
        Assert.Contains("lit cache-query", names);
        Assert.Contains("lit cache-stats", names);
        Assert.Contains("lit cache-export", names);
        Assert.Contains("lit word", names);
    }

    [Fact]
    public void LitParseValidatePlan_WorkOffline()
    {
        RequireCli();
        var query = "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')";

        var (parseJson, parseExit) = Run("lit", "parse", "--query", query, "--json");
        Assert.Equal(0, parseExit);
        using (var doc = JsonDocument.Parse(parseJson))
        {
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("lit parse", doc.RootElement.GetProperty("command").GetString());
        }

        var (validateJson, validateExit) = Run("lit", "validate", "--query", "AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)", "--json");
        Assert.Equal(0, validateExit);
        using (var doc = JsonDocument.Parse(validateJson))
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

        var (planJson, planExit) = Run("lit", "plan", "--query", query, "--sources", "openalex,crossref,unpaywall", "--json");
        Assert.Equal(0, planExit);
        using (var doc = JsonDocument.Parse(planJson))
        {
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("data").GetProperty("providers").GetArrayLength());
        }
    }

    [Fact]
    public void LitValidate_UnsupportedOperator_ReturnsE006()
    {
        RequireCli();
        // '/NEAR' (and /SEN /PREV /AFT /PRG) became supported CNKI proximity operators in
        // the v4.3 22-operator DSL, so they no longer trigger E006. Use an unregistered
        // slash operator (/BOGUS) which CnkiLexer.ReadSlash tokenizes as Unsupported -> E006.
        // lit validate reports validity in data.valid (status stays "ok"); the contract under
        // test is: an unsupported operator yields data.valid=false with an E006 issue.
        var (json, exit) = Run("lit", "validate", "--query", "TI=humic/BOGUS acid", "--json");
        Assert.Equal(0, exit);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        Assert.False(doc.RootElement.GetProperty("data").GetProperty("valid").GetBoolean());
        Assert.Equal("E006", doc.RootElement.GetProperty("data").GetProperty("issues")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void LitExport_WritesMarkdownAndBibtexArtifacts()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-lit-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = Path.Combine(dir, "refs.fixture.json");
            File.WriteAllText(input, """
{
  "records": [
    {
      "title": "Humic acid and rare earth",
      "authors": ["Qian W"],
      "year": 2007,
      "venue": "Chem Geol",
      "doi": "10.1016/j.chemgeo.2007.05.018"
    }
  ]
}
""");
            var md = Path.Combine(dir, "refs.md");
            var bib = Path.Combine(dir, "refs.bib");

            var (mdJson, mdExit) = Run("lit", "export", "--input", input, "--format", "markdown", "-o", md, "--json");
            Assert.Equal(0, mdExit);
            using (var doc = JsonDocument.Parse(mdJson))
                Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

            var (bibJson, bibExit) = Run("lit", "export", "--input", input, "--format", "bibtex", "-o", bib, "--json");
            Assert.Equal(0, bibExit);
            using (var doc = JsonDocument.Parse(bibJson))
                Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

            Assert.True(new FileInfo(md).Length > 0);
            Assert.True(new FileInfo(bib).Length > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void LitCacheImport_ImportsFixtureIntoLocalCache()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-lit-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var workplace = Path.Combine(dir, "workplace");
            Directory.CreateDirectory(workplace);
            var input = Path.Combine(dir, "refs.fixture.json");
            File.WriteAllText(input, """
{
  "records": [
    {
      "title": "Humic acid and rare earth",
      "authors": ["Qian W"],
      "year": 2007,
      "venue": "Chem Geol",
      "doi": "10.1016/j.chemgeo.2007.05.018"
    }
  ]
}
""");

            var env = new Dictionary<string, string> { ["NONG_WORKPLACE"] = workplace };

            var (importJson, importExit) = RunWithEnv(env, "lit", "cache-import", "--input", input, "--json");
            Assert.Equal(0, importExit);
            using (var doc = JsonDocument.Parse(importJson))
            {
                Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
                Assert.Equal("lit cache-import", doc.RootElement.GetProperty("command").GetString());
                Assert.Equal(1, doc.RootElement.GetProperty("data").GetProperty("imported").GetInt32());
            }

            var (statsJson, statsExit) = RunWithEnv(env, "lit", "cache-stats", "--json");
            Assert.Equal(0, statsExit);
            using var statsDoc = JsonDocument.Parse(statsJson);
            Assert.Equal("ok", statsDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, statsDoc.RootElement.GetProperty("data").GetProperty("totalRecords").GetInt32());

            var (queryJson, queryExit) = RunWithEnv(env, "lit", "cache-query", "--title", "Humic acid", "--json");
            Assert.Equal(0, queryExit);
            using var queryDoc = JsonDocument.Parse(queryJson);
            Assert.Equal("ok", queryDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, queryDoc.RootElement.GetProperty("data").GetProperty("count").GetInt32());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
