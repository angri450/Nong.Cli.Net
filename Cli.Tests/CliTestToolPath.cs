using System.Diagnostics;
using Xunit;

namespace Nong.Cli.Tests;

internal static class CliTestToolPath
{
    public static void AddLocalTools(ProcessStartInfo psi, string repoRoot)
    {
        var dirs = new[]
        {
            Path.Combine(repoRoot, "Chart", "tools", "bin", "Release", "net8.0"),
            Path.Combine(repoRoot, "Diagram", "tools", "bin", "Release", "net8.0"),
            Path.Combine(repoRoot, "Pdf", "tools", "bin", "Release", "net8.0"),
            Path.Combine(repoRoot, "Pptx", "tools", "bin", "Release", "net8.0"),
            Path.Combine(repoRoot, "MultiModal", "tools", "bin", "Release", "net8.0"),
            Path.Combine(repoRoot, "Imaging", "tools", "bin", "Release", "net8.0")
        }.Where(Directory.Exists);

        var existing = psi.Environment.TryGetValue("PATH", out var path) ? path : "";
        psi.Environment["PATH"] = string.Join(Path.PathSeparator, dirs.Append(existing));
    }

    public static (string StdOut, string StdErr, int ExitCode) RunDotnetCli(
        string repoRoot,
        string nongDll,
        int timeoutMs,
        bool captureStdErr,
        IReadOnlyDictionary<string, string>? environment,
        params string[] args)
    {
        var allArgs = new List<string> { nongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = captureStdErr,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        AddLocalTools(psi, repoRoot);

        if (environment != null)
        {
            foreach (var kvp in environment)
                psi.Environment[kvp.Key] = kvp.Value;
        }

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = captureStdErr
            ? proc.StandardError.ReadToEndAsync()
            : Task.FromResult(string.Empty);

        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            Assert.Fail($"CLI command timed out after {timeoutMs} ms: dotnet {string.Join(" ", allArgs)}");
        }

        Task.WaitAll(stdoutTask, stderrTask);
        return (stdoutTask.Result, stderrTask.Result, proc.ExitCode);
    }
}
