using System.Reflection;

var sharedDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".dotnet", "tools", "nong-runtimes");

Directory.CreateDirectory(sharedDir);

// Copy all native dlls from the tool's published directory into the shared runtime dir.
var publishDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
foreach (var pattern in new[] { "*.dll", "*.so", "*.dylib" })
{
    foreach (var f in Directory.GetFiles(publishDir, pattern, SearchOption.AllDirectories))
    {
        var dest = Path.Combine(sharedDir, Path.GetFileName(f));
        try { File.Copy(f, dest, overwrite: true); } catch { }
    }
}

Console.WriteLine($"Nong shared runtime installed to: {sharedDir}");
Console.WriteLine($"Files: {Directory.GetFiles(sharedDir).Length}");
