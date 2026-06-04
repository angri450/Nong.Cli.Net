using System.Text.Json;
using System.Text.RegularExpressions;
using SkillManager.Cli.Models;

namespace SkillManager.Cli.Tools;

public class SecurityScanner
{
    private readonly string _skillDir;
    private readonly bool _verbose;
    private List<AllowlistEntry>? _allowlist;

    public SecurityScanner(string skillDir, bool verbose = false)
    {
        _skillDir = Path.GetFullPath(skillDir);
        _verbose = verbose;
        LoadAllowlist();
    }

    public List<SecurityFinding> Scan()
    {
        var findings = new List<SecurityFinding>();

        var files = EnumerateSkillFiles().ToList();

        foreach (var file in files)
        {
            var fullPath = Path.GetFullPath(file);
            foreach (var entry in _allowlist ?? new())
            {
                var normalizedEntryFile = entry.File.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var allowlistPath = Path.GetFullPath(Path.Combine(_skillDir, normalizedEntryFile));
                if (fullPath.StartsWith(allowlistPath, StringComparison.OrdinalIgnoreCase) || fullPath.Equals(allowlistPath, StringComparison.OrdinalIgnoreCase))
                {
                    // File is allowlisted — skip all scanning for this file
                    goto nextFile;
                }
            }
            ScanFile(file, findings);
            continue;
        nextFile: ;
        }

        return findings
            .GroupBy(f => (f.File, f.Rule))
            .Select(g => g.OrderByDescending(f => f.Severity).First())
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.File)
            .ToList();
    }

    private void LoadAllowlist()
    {
        var allowlistPath = Path.Combine(_skillDir, ".scan-allowlist.json");
        if (!File.Exists(allowlistPath))
        {
            _allowlist = new List<AllowlistEntry>();
            return;
        }

        try
        {
            var json = File.ReadAllText(allowlistPath);
            var doc = JsonSerializer.Deserialize<AllowlistConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _allowlist = doc?.Allowlist ?? new List<AllowlistEntry>();
        }
        catch
        {
            _allowlist = new List<AllowlistEntry>();
        }
    }

    private void ScanFile(string file, List<SecurityFinding> findings)
    {
        var lines = File.ReadAllLines(file);
        var ext = Path.GetExtension(file).ToLowerInvariant();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            ScanEmail(line, file, lineNum, findings);
            ScanPrivateKeys(line, file, lineNum, findings);
            ScanTokens(line, file, lineNum, findings);
            ScanAbsolutePaths(line, file, lineNum, findings);
            ScanUnsafeShell(line, file, lineNum, findings);
            ScanHttpUrls(line, file, lineNum, findings);
            ScanCdnReferences(line, file, lineNum, findings);

            if (ext == ".html" || ext == ".js" || ext == ".htm")
            {
                ScanInnerHtml(line, file, lineNum, findings);
            }
        }
    }

    private IEnumerable<string> EnumerateSkillFiles()
    {
        var excludeDirs = new HashSet<string> { "bin", "obj", ".git", "node_modules", "__pycache__", "evals", "workspace" };
        var excludeFiles = new HashSet<string> { ".scan-allowlist.json" };
        var sourceExts = new HashSet<string> { ".md", ".py", ".ps1", ".sh", ".cs", ".csproj", ".html", ".js", ".json", ".yaml", ".yml", ".xml", ".css", ".ts", ".tsx", ".jsx" };

        foreach (var file in Directory.EnumerateFiles(_skillDir, "*.*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(_skillDir, file);
            var parts = relPath.Split(Path.DirectorySeparatorChar);

            // Skip excluded directories
            if (parts.Any(p => excludeDirs.Contains(p.ToLowerInvariant())))
                continue;

            // Skip excluded files
            if (excludeFiles.Contains(Path.GetFileName(file)))
                continue;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (sourceExts.Contains(ext) || ext == "")
                yield return file;
        }
    }

    private void ScanEmail(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        var match = Regex.Match(line, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        if (match.Success && line.Trim().StartsWith("//"))
            return; // allow in comments

        if (!match.Success) return;
        var email = match.Value;
        // Exclude known safe exceptions
        if (email.Contains("@anthropic.com") && file.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            if (_verbose)
            {
                findings.Add(new SecurityFinding
                {
                    Severity = Severity.Low,
                    Rule = "EMAIL_EXPOSED",
                    File = Path.GetRelativePath(_skillDir, file),
                    Line = lineNum,
                    Detail = $"Email found (known exception): {email}"
                });
            }
            return;
        }

        findings.Add(new SecurityFinding
        {
            Severity = Severity.High,
            Rule = "EMAIL_EXPOSED",
            File = Path.GetRelativePath(_skillDir, file),
            Line = lineNum,
            Detail = $"Email address exposed: {email}"
        });
    }

    private void ScanPrivateKeys(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        if (Regex.IsMatch(line, @"-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Critical,
                Rule = "PRIVATE_KEY",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "Private key detected in source"
            });
        }
    }

    private void ScanTokens(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        // GitHub token pattern
        if (Regex.IsMatch(line, @"ghp_[a-zA-Z0-9]{36}"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Critical,
                Rule = "GITHUB_TOKEN",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "GitHub personal access token detected"
            });
        }

        // Generic API key patterns
        if (Regex.IsMatch(line, @"(?i)(api[_-]?key|apikey|secret[_-]?key|access[_-]?key)\s*[:=]\s*['""][A-Za-z0-9_\-]{20,}['""]"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Critical,
                Rule = "API_KEY",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "Possible API key/secret in source"
            });
        }

        // JWT tokens
        if (Regex.IsMatch(line, @"eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.High,
                Rule = "JWT_TOKEN",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "JWT token detected in source"
            });
        }
    }

    private void ScanAbsolutePaths(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        // Windows absolute paths with user names
        var winMatch = Regex.Match(line, @"C:\\Users\\([^\\""'\s]+)");
        if (winMatch.Success)
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.High,
                Rule = "ABSOLUTE_USER_PATH",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = $"Windows user path detected: {winMatch.Value}"
            });
        }

        // Unix user paths (but skip /tmp/ and /usr/ which are system paths)
        var unixMatch = Regex.Match(line, @"/(home|Users)/([^/""'\s]+)");
        if (unixMatch.Success)
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.High,
                Rule = "ABSOLUTE_USER_PATH",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = $"Unix user path detected: {unixMatch.Value}"
            });
        }

        // ~/username patterns
        if (Regex.IsMatch(line, @"~/\w+"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Medium,
                Rule = "HOME_PATH_REFERENCE",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "Home directory reference detected"
            });
        }
    }

    private void ScanUnsafeShell(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".sh" or ".ps1" or ".bash" or ".zsh")) return;

        if (Regex.IsMatch(line, @"rm\s+-rf\s+/[^t]"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Critical,
                Rule = "DANGEROUS_RM",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "Potentially destructive rm -rf detected"
            });
        }
    }

    private void ScanHttpUrls(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        var match = Regex.Match(line, @"http://([^\s""'<>]+)");
        if (match.Success && !match.Value.Contains("localhost") && !match.Value.Contains("127.0.0.1"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.Low,
                Rule = "HTTP_URL",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = $"HTTP (non-HTTPS) URL: {match.Value}"
            });
        }
    }

    private void ScanCdnReferences(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        var cdnDomains = new[] { "cdn.sheetjs.com", "fonts.googleapis.com", "cdnjs.cloudflare.com", "unpkg.com", "cdn.jsdelivr.net" };

        foreach (var domain in cdnDomains)
        {
            if (line.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new SecurityFinding
                {
                    Rule = "EXTERNAL_CDN",
                    File = Path.GetRelativePath(_skillDir, file),
                    Line = lineNum,
                    Detail = $"External CDN reference: {domain}. Consider bundling or using subresource integrity."
                });

                if (domain.Contains("fonts.googleapis") || domain.Contains("cdn."))
                {
                    findings.Last().Severity = Severity.High;
                }
                else
                {
                    findings.Last().Severity = Severity.Medium;
                }
            }
        }
    }

    private void ScanInnerHtml(string line, string file, int lineNum, List<SecurityFinding> findings)
    {
        if (Regex.IsMatch(line, @"\.innerHTML\s*=\s*['""]\s*['""]\s*;?"))
            return; // Setting to empty string — always safe

        if (Regex.IsMatch(line, @"\.innerHTML\s*=.*sanitize|escapeHtml|DOMPurify|textContent"))
            return; // Has sanitization guard

        if (Regex.IsMatch(line, @"\.innerHTML\s*=") && !Regex.IsMatch(line, @"\.innerHTML\s*=\s*['""]\s*['""]"))
        {
            findings.Add(new SecurityFinding
            {
                Severity = Severity.High,
                Rule = "UNSAFE_INNERHTML",
                File = Path.GetRelativePath(_skillDir, file),
                Line = lineNum,
                Detail = "innerHTML assignment without sanitization detected"
            });
        }
    }
}
