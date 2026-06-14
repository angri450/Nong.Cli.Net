using System.Reflection;
using System.Runtime.InteropServices;

namespace PdfCore;

public static class PdfNativeRuntime
{
    static int registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref registered, 1) == 1)
            return;

        var assembly = typeof(PdfNativeRuntime).Assembly;
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
        }
        catch (InvalidOperationException)
        {
            // Another host may have already registered a resolver for PdfCore.
        }
    }

    static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "pdfium", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var candidate in EnumeratePdfiumCandidates())
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    static IEnumerable<string> EnumeratePdfiumCandidates()
    {
        var fileName = GetNativeFileName();
        var baseDirs = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(PdfNativeRuntime).Assembly.Location) ?? AppContext.BaseDirectory,
            Environment.CurrentDirectory
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

    static string GetNativeFileName()
    {
        if (OperatingSystem.IsWindows())
            return "pdfium.dll";
        if (OperatingSystem.IsMacOS())
            return "pdfium.dylib";
        return "pdfium.so";
    }
}
