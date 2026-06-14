using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Nong.Cli.Common;

public static class NativeRenderWorkerHost
{
    const int DefaultTimeoutMs = 120_000;

    public static void Run(string command, bool json, IReadOnlyList<string> args)
    {
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.DependencyMissing with { Message = "Cannot locate nong.dll for native render worker startup." },
                json);
            return;
        }

        var timeoutMs = GetTimeoutMs();
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(assemblyPath);
        psi.ArgumentList.Add("__render-worker");
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add("--json");

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.DependencyMissing with { Message = "Failed to start native render worker process." },
                    json);
                return;
            }

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                CliHelpers.WriteError(command,
                    ErrorCodes.InternalError with { Message = $"Native render worker timed out after {timeoutMs} ms." },
                    json);
                return;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            WriteWorkerResult(command, proc.ExitCode, stdout, stderr, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.InternalError with { Message = $"Native render worker failed to start: {ex.Message}" },
                json);
        }
    }

    public static void AddOption(List<string> args, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        args.Add(name);
        args.Add(value);
    }

    static void WriteWorkerResult(string command, int exitCode, string stdout, string stderr, bool json)
    {
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            var trimmed = stdout.TrimEnd();
            if (json)
            {
                Console.WriteLine(trimmed);
            }
            else if (!TryWriteHumanResult(trimmed, exitCode))
            {
                Console.WriteLine(trimmed);
            }

            Environment.ExitCode = exitCode == 0 ? 0 : 1;
            return;
        }

        var stderrText = string.IsNullOrWhiteSpace(stderr) ? "" : $" Stderr: {stderr.Trim()}";
        CliHelpers.WriteError(command,
            ErrorCodes.InternalError with { Message = $"Native render worker crashed or exited without output. Exit code: {exitCode}.{stderrText}" },
            json);
    }

    static bool TryWriteHumanResult(string stdout, int exitCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                var summary = root.TryGetProperty("summary", out var summaryElement)
                    ? summaryElement.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(summary))
                    Console.WriteLine(summary);
                return true;
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    var code = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : "E004";
                    var name = error.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "internal_error";
                    var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "Native render worker failed.";
                    Console.Error.WriteLine($"[{code}] {name}: {message}");
                }
                return true;
            }
        }
        catch
        {
            return false;
        }

        return exitCode == 0;
    }

    static int GetTimeoutMs()
    {
        var value = Environment.GetEnvironmentVariable("NONG_RENDER_WORKER_TIMEOUT_MS");
        if (int.TryParse(value, out var parsed) && parsed >= 1_000)
            return parsed;
        return DefaultTimeoutMs;
    }
}
