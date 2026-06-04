using System.IO.Compression;

namespace SkillManager.Cli.Tools;

public enum SkillRootKind { Skill, Plugin, Directory }

public sealed record SkillRootInfo(
    string FullPath,
    SkillRootKind Kind,
    bool HasSkillMd,
    bool HasPluginManifest,
    bool HasMarketplaceManifest,
    bool HasSkillsManifest,
    IReadOnlyList<string> SkillDirectories
);

public static class SkillRootClassifier
{
    public static SkillRootInfo Classify(string fullPath)
    {
        var hasSkillMd = File.Exists(Path.Combine(fullPath, "SKILL.md"));
        var hasPluginManifest = File.Exists(Path.Combine(fullPath, ".claude-plugin", "plugin.json"))
                                || File.Exists(Path.Combine(fullPath, "plugin.json"));
        var hasMarketplaceManifest = File.Exists(Path.Combine(fullPath, ".claude-plugin", "marketplace.json"))
                                     || File.Exists(Path.Combine(fullPath, "marketplace.json"));
        var hasSkillsManifest = File.Exists(Path.Combine(fullPath, "skills.sh.json"));

        var childSkills = new List<string>();
        if (Directory.Exists(fullPath))
        {
            foreach (var subDir in Directory.EnumerateDirectories(fullPath))
            {
                if (File.Exists(Path.Combine(subDir, "SKILL.md")))
                    childSkills.Add(subDir);
            }
        }

        SkillRootKind kind;
        if (hasSkillMd)
            kind = SkillRootKind.Skill;
        else if (hasPluginManifest || hasMarketplaceManifest || hasSkillsManifest || childSkills.Count > 0)
            kind = SkillRootKind.Plugin;
        else
            kind = SkillRootKind.Directory;

        return new SkillRootInfo(
            fullPath, kind, hasSkillMd,
            hasPluginManifest, hasMarketplaceManifest, hasSkillsManifest,
            childSkills);
    }
}

public class Packager
{
    private readonly string _rootDir;
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

    public Packager(string rootDir, string? outputDir = null)
    {
        _rootDir = Path.GetFullPath(rootDir);
        _outputDir = outputDir != null ? Path.GetFullPath(outputDir) : null;
    }

    public async Task<string> PackageAsync()
    {
        var info = SkillRootClassifier.Classify(_rootDir);
        return info.Kind switch
        {
            SkillRootKind.Skill => await PackageSkillAsync(info),
            SkillRootKind.Plugin => await PackagePluginAsync(info),
            _ => throw new InvalidOperationException("Directory is neither a skill nor a plugin root.")
        };
    }

    private string GetOutputPath(string name)
    {
        var outputDir = _outputDir ?? _rootDir;
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{name}.zip");
        if (File.Exists(outputPath))
            File.Delete(outputPath);
        return outputPath;
    }

    private async Task<string> PackageSkillAsync(SkillRootInfo info)
    {
        var name = Path.GetFileName(_rootDir);
        var outputPath = GetOutputPath(name);

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            AddDirectoryToZip(zip, _rootDir, "", outputPath);
        });

        return outputPath;
    }

    private async Task<string> PackagePluginAsync(SkillRootInfo info)
    {
        var name = Path.GetFileName(_rootDir);
        var outputPath = GetOutputPath(name);

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            // Add plugin-level manifest files at root
            AddFileToZip(zip, Path.Combine(_rootDir, ".claude-plugin", "plugin.json"), ".claude-plugin/plugin.json");
            AddFileToZip(zip, Path.Combine(_rootDir, ".claude-plugin", "marketplace.json"), ".claude-plugin/marketplace.json");

            // Add root-level files (LICENSE, README, CHANGELOG, etc.)
            foreach (var file in Directory.EnumerateFiles(_rootDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var fname = Path.GetFileName(file);
                if (fname.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ExcludeFiles.Contains(fname))
                    continue;
                if (fname.EndsWith("~") || fname.StartsWith(".#"))
                    continue;
                AddFileToZip(zip, file, fname);
            }

            // Add each child skill directory
            foreach (var skillDir in info.SkillDirectories)
            {
                var skillRelPath = Path.GetFileName(skillDir);
                AddDirectoryToZip(zip, skillDir, skillRelPath + "/", outputPath);
            }
        });

        return outputPath;
    }

    private void AddDirectoryToZip(ZipArchive zip, string sourceDir, string entryPrefix, string? skipPath)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            if (skipPath != null && string.Equals(file, skipPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                continue;

            var relPath = Path.GetRelativePath(sourceDir, file);
            var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Any(p => ExcludeDirs.Contains(p)))
                continue;

            var fname = Path.GetFileName(file);
            if (ExcludeFiles.Contains(fname))
                continue;
            if (fname.EndsWith("~") || fname.StartsWith(".#"))
                continue;

            var entryName = entryPrefix + string.Join("/", parts);
            zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }

    private static void AddFileToZip(ZipArchive zip, string filePath, string entryName)
    {
        if (!File.Exists(filePath))
            return;
        zip.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
    }

    public async Task VerifyArchiveAsync(string outputPath, SkillRootKind expectedKind)
    {
        await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(outputPath);
            if (!zip.Entries.Any())
                throw new InvalidOperationException("Generated archive is empty");

            if (expectedKind == SkillRootKind.Skill)
            {
                if (!zip.Entries.Any(e => e.Name == "SKILL.md"))
                    throw new InvalidOperationException("SKILL.md not found in archive");
            }
            else if (expectedKind == SkillRootKind.Plugin)
            {
                if (!zip.Entries.Any(e => e.Name == "SKILL.md" || e.FullName.EndsWith("/SKILL.md")))
                    throw new InvalidOperationException("No SKILL.md found in plugin archive");
            }
        });
    }

    public bool HasSecurityScanPassed()
    {
        var markerPath = Path.Combine(_rootDir, ".security-scan-passed");
        return File.Exists(markerPath);
    }
}
