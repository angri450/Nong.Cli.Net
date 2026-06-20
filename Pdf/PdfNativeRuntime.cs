using System.Reflection;
using System.Runtime.InteropServices;

namespace PdfCore;

public static class PdfNativeRuntime
{
    static int pdfiumRegistered;

    // ── pdfium DLL resolution ──

    /// <summary>Backward-compatible alias for EnsurePdfiumRegistered.</summary>
    [Obsolete("Use EnsurePdfiumRegistered()")]
    public static void EnsureRegistered() => EnsurePdfiumRegistered();

    public static void EnsurePdfiumRegistered()
    {
        if (Interlocked.Exchange(ref pdfiumRegistered, 1) == 1)
            return;

        var assembly = typeof(PdfNativeRuntime).Assembly;
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, ResolvePdfium);
        }
        catch (InvalidOperationException)
        {
            // Another host may have already registered a resolver for PdfCore.
        }
    }

    static IntPtr ResolvePdfium(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "pdfium", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var candidate in EnumerateRuntimes(PdfiumFileName))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    // ── Poppler tool discovery ──

    static readonly Dictionary<string, string> _popplerCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolve a Poppler tool (pdftotext, pdfinfo, pdfimages) to full path, or null.</summary>
    public static string? ResolvePopplerTool(string toolName)
    {
        if (_popplerCache.TryGetValue(toolName, out var cached))
            return cached;

        var exeName = OperatingSystem.IsWindows() ? $"{toolName}.exe" : toolName;

        // 1. Bundled runtime — Pdf/runtimes/<rid>/native/
        foreach (var candidate in EnumerateRuntimes(exeName))
        {
            if (File.Exists(candidate))
            {
                _popplerCache[toolName] = candidate;
                return candidate;
            }
        }

        // 2. Known install paths
        var known = new[]
        {
            $@"C:\tools\poppler\Library\bin\{exeName}",
            $@"C:\tools\poppler\bin\{exeName}",
            $@"C:\Program Files\poppler\bin\{exeName}",
        };
        foreach (var c in known)
        {
            if (File.Exists(c))
            {
                _popplerCache[toolName] = c;
                return c;
            }
        }

        // 3. System PATH
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exeName, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit(5000);
                if (proc.ExitCode == 0 || !string.IsNullOrWhiteSpace(proc.StandardOutput.ReadToEnd()))
                {
                    _popplerCache[toolName] = exeName; // on PATH, use bare name
                    return exeName;
                }
            }
        }
        catch { /* not on PATH */ }

        return null;
    }

    /// <summary>Check whether all required Poppler tools are available.</summary>
    public static bool IsPopplerAvailable =>
        ResolvePopplerTool("pdftotext") != null
        && ResolvePopplerTool("pdfinfo") != null
        && ResolvePopplerTool("pdfimages") != null;

    // ── shared runtime directory enumeration ──

    static IEnumerable<string> EnumerateRuntimes(string fileName)
    {
        var baseDirs = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(PdfNativeRuntime).Assembly.Location) ?? AppContext.BaseDirectory,
            // Shared runtime dir — install via: dotnet tool install Angri450.Nong.Runtime --global
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "nong-runtimes"),
        }.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var baseDir in baseDirs)
        {
            yield return Path.Combine(baseDir, fileName);

            foreach (var rid in GetRuntimeIds())
                yield return Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        }
    }

    static IEnumerable<string> GetRuntimeIds()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows())
        {
            if (arch == Architecture.X64) yield return "win-x64";
            if (arch == Architecture.X86) yield return "win-x86";
            if (arch == Architecture.Arm64) yield return "win-arm64";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            if (arch == Architecture.Arm64) yield return "osx-arm64";
            if (arch == Architecture.X64) yield return "osx-x64";
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            if (arch == Architecture.X64) yield return "linux-x64";
            if (arch == Architecture.Arm64) yield return "linux-arm64";
            if (arch == Architecture.Arm) yield return "linux-arm";
            yield return "linux";
        }
    }

    static string PdfiumFileName
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "pdfium.dll";
            if (OperatingSystem.IsMacOS()) return "pdfium.dylib";
            return "pdfium.so";
        }
    }
}
