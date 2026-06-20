namespace Nong.Cli.Common;

public static class NongSharedRuntime
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet", "tools", "nong-runtimes");

    public static bool IsInstalled => Directory.Exists(Dir)
        && Directory.GetFiles(Dir, "*.dll").Length >= 3; // at least Skia + HarfBuzz + pdfium

    /// <summary>Print guidance if the shared runtime is missing. Called at tool startup.</summary>
    public static void Ensure(string toolName)
    {
        if (IsInstalled) return;

        Console.Error.WriteLine($"[{toolName}] Shared native runtime not found.");
        Console.Error.WriteLine($"  Expected: {Dir}");
        Console.Error.WriteLine($"  Install:  dotnet tool install Angri450.Nong.Runtime --global");
        Console.Error.WriteLine($"  Then retry your command.");
        Environment.Exit(1);
    }
}
