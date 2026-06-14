using System.IO.Compression;
using System.Text.RegularExpressions;
using Xunit;

namespace Nong.Cli.Tests;

public class OcrRuntimePackageTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NupkgDir => Path.Combine(RepoRoot, "nupkg");
    static string OcrRuntimeVersionSource => Path.Combine(RepoRoot, "Cli", "Common", "OcrRuntimeVersion.cs");

    static string ReadOcrRuntimeVersion()
    {
        var source = File.ReadAllText(OcrRuntimeVersionSource);
        var match = Regex.Match(source, "public const string Current = \"(?<version>[^\"]+)\"");
        Assert.True(match.Success, $"Could not read OCR runtime version from {OcrRuntimeVersionSource}");
        return match.Groups["version"].Value;
    }

    [Fact]
    public void OcrRuntimeVersionConstant_IsIndependentPinnedRuntimeVersion()
    {
        var version = ReadOcrRuntimeVersion();

        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", version);
    }

    [Fact]
    public void OcrRuntimePackages_AreSingleRidNativeBundles()
    {
        if (!Directory.Exists(NupkgDir))
            return;

        var packages = Directory.EnumerateFiles(NupkgDir, $"Angri450.Nong.OcrRuntime.*.{ReadOcrRuntimeVersion()}.nupkg")
            .OrderBy(p => p)
            .ToList();
        if (packages.Count == 0)
            return;

        foreach (var package in packages)
        {
            using var archive = ZipFile.OpenRead(package);
            var nativeEntries = archive.Entries
                .Select(e => e.FullName.Replace('\\', '/'))
                .Where(n => n.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(nativeEntries);
            Assert.DoesNotContain(nativeEntries, n => n.Contains("python", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(nativeEntries, n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(nativeEntries, n => n.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(nativeEntries, n => n.Contains("/models/", StringComparison.OrdinalIgnoreCase));

            var rids = nativeEntries
                .Select(n => n.Split('/'))
                .Where(parts => parts.Length >= 2)
                .Select(parts => parts[1])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Single(rids);
            var rid = rids[0];
            var names = nativeEntries.Select(Path.GetFileName).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            switch (rid)
            {
                case "win-x64":
                    Assert.Contains("paddle_inference_c.dll", names);
                    Assert.Contains("OpenCvSharpExtern.dll", names);
                    Assert.Contains("mklml.dll", names);
                    Assert.Contains("mkldnn.dll", names);
                    break;
                case "linux-x64":
                case "linux-arm64":
                    Assert.Contains("libpaddle_inference_c.so", names);
                    Assert.Contains("libOpenCvSharpExtern.so", names);
                    break;
                case "osx-x64":
                case "osx-arm64":
                    Assert.Contains("libpaddle_inference_c.dylib", names);
                    Assert.Contains("libOpenCvSharpExtern.dylib", names);
                    break;
                default:
                    Assert.Fail($"Unexpected OCR runtime RID in {Path.GetFileName(package)}: {rid}");
                    break;
            }
        }
    }
}
