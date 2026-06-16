namespace Angri450.Nong;

/// <summary>
/// Unified Nong data directory. All cache, config, generated files go here.
/// Default: ~/Documents/workplace/
/// Override: NONG_WORKPLACE env var (absolute path, must exist and be writable)
/// </summary>
public static class NongWorkplace
{
    static readonly string Root;

    static NongWorkplace()
    {
        var env = Environment.GetEnvironmentVariable("NONG_WORKPLACE");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (!Path.IsPathRooted(env))
                throw new InvalidOperationException($"NONG_WORKPLACE must be an absolute path, got: {env}");
            if (!Directory.Exists(env))
                throw new InvalidOperationException($"NONG_WORKPLACE directory does not exist: {env}");
            var normalized = Path.GetFullPath(env).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Root = normalized;
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

    /// <summary>
    /// Resolve output path. If given path is relative (just a filename), prefix with Output dir.
    /// Rejects path traversal (../..) in bare filenames.
    /// </summary>
    public static string ResolveOutput(string fileOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileOrPath))
            throw new ArgumentException("Output path must not be empty.", nameof(fileOrPath));

        // Absolute paths allowed as-is — caller explicitly controls location.
        if (Path.IsPathRooted(fileOrPath))
            return fileOrPath;

        // Has directory component (relative path) — resolve from CWD.
        if (fileOrPath.Contains(Path.DirectorySeparatorChar) || fileOrPath.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFullPath(fileOrPath);

        // Bare filename — put in Output dir. Reject traversal via filename injection.
        var fileName = Path.GetFileName(fileOrPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != fileOrPath)
            throw new ArgumentException($"Invalid output filename: {fileOrPath}", nameof(fileOrPath));

        return Path.Combine(Output, fileName);
    }

    /// <summary>Validate that a given absolute path resolves under Root. Throws on failure.</summary>
    public static void RequireUnderRoot(string absolutePath)
    {
        var normalized = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Path is outside NongWorkplace root: {absolutePath}. Root: {root}");
        }
    }

    /// <summary>Create a safe cache file path under Cache/. Rejects traversal.</summary>
    public static string CacheFile(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Contains("..") || safeName != fileName)
            throw new ArgumentException($"Invalid cache filename: {fileName}", nameof(fileName));
        return Path.Combine(Cache, safeName);
    }

    static string EnsureDir(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains("..") || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException($"Invalid subdirectory name: {name}", nameof(name));
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
