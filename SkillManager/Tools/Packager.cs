using System.IO.Compression;

namespace SkillManager.Cli.Tools;

public class Packager
{
    private readonly string _skillDir;
    private readonly string? _outputDir;

    private static readonly HashSet<string> ExcludeDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules", "__pycache__", "evals", "workspace",
        "tests", "TestResults", ".vs", "temp",
        ".security-scan-passed"
    };

    private static readonly HashSet<string> ExcludeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", "Thumbs.db", ".gitignore", ".security-scan-passed"
    };

    public Packager(string skillDir, string? outputDir = null)
    {
        _skillDir = Path.GetFullPath(skillDir);
        _outputDir = outputDir != null ? Path.GetFullPath(outputDir) : null;
    }

    public async Task<string> PackageAsync()
    {
        var skillName = Path.GetFileName(_skillDir);
        var outputDir = _outputDir ?? _skillDir;
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"{skillName}.zip");

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            foreach (var file in Directory.EnumerateFiles(_skillDir, "*.*", SearchOption.AllDirectories))
            {
                if (string.Equals(file, outputPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relPath = Path.GetRelativePath(_skillDir, file);
                var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (parts.Any(p => ExcludeDirs.Contains(p)))
                    continue;

                if (ExcludeFiles.Contains(Path.GetFileName(file)))
                    continue;

                if (Path.GetFileName(file).EndsWith("~") || Path.GetFileName(file).StartsWith(".#"))
                    continue;

                var entryName = string.Join("/", parts);
                zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        });

        return outputPath;
    }

    public async Task VerifyArchiveAsync(string outputPath)
    {
        await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(outputPath);
            if (!zip.Entries.Any())
                throw new InvalidOperationException("Generated archive is empty");

            if (!zip.Entries.Any(e => e.Name == "SKILL.md"))
                throw new InvalidOperationException("SKILL.md not found in archive");
        });
    }

    public bool HasSecurityScanPassed()
    {
        var markerPath = Path.Combine(_skillDir, ".security-scan-passed");
        return File.Exists(markerPath);
    }
}
