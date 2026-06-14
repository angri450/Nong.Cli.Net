namespace Angri450.Nong;

/// <summary>
/// Unified Nong data directory. All cache, config, generated files go here.
/// Default: ~/Documents/workplace/
/// Override: NONG_WORKPLACE env var (absolute path)
/// </summary>
public static class NongWorkplace
{
    static readonly string Root;

    static NongWorkplace()
    {
        var env = Environment.GetEnvironmentVariable("NONG_WORKPLACE");
        if (!string.IsNullOrWhiteSpace(env) && Path.IsPathRooted(env))
        {
            Root = env;
        }
        else
        {
            Root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "workplace");
        }
        Directory.CreateDirectory(Root);
    }

    /// <summary>Root directory. All Nong data lives under here.</summary>
    public static string Dir => Root;

    /// <summary>LiteDB and other cache files.</summary>
    public static string Cache => EnsureDir("Cache");

    /// <summary>Generated Word/PDF/PPT/excel output.</summary>
    public static string Output => EnsureDir("Output");

    /// <summary>Literature LiteDB database file.</summary>
    public static string LiteratureDb => Path.Combine(Cache, "literature.db");

    /// <summary>Resolve output path. If given path is relative (just a filename), prefix with Output dir.</summary>
    public static string ResolveOutput(string fileOrPath)
    {
        if (Path.IsPathRooted(fileOrPath)) return fileOrPath;
        // Has directory component? Use as-is relative to CWD — caller knows what they're doing.
        if (fileOrPath.Contains(Path.DirectorySeparatorChar) || fileOrPath.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFullPath(fileOrPath);
        // Bare filename — put in Output dir.
        return Path.Combine(Output, fileOrPath);
    }

    static string EnsureDir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
