using System.Text;
using System.Text.RegularExpressions;

namespace SkillManager.Cli.Tools;

public static class Scaffolder
{
    static string PH(int s) => $"<<< FILL FROM Step {s} of workflow.md >>>";

    public static int Run(string name, string tool, string targetDir, bool noConfig = false, bool force = false)
    {
        var t = Path.GetFullPath(targetDir);
        if (!Directory.Exists(t)) { Console.Error.WriteLine($"error: {t} not found"); return 1; }
        var dir = Path.Combine(t, name);
        if (Directory.Exists(dir) && !force && Directory.EnumerateFileSystemEntries(dir).Any()) { Console.Error.WriteLine($"error: {dir} exists, use --force"); return 1; }
        var slug = S(name);
        Console.WriteLine($"Scaffolding: {name}");
        W(dir, "", "SKILL.md", Md(name, tool, slug));
        W(dir, "scripts", $"install_{slug}.sh", InstallSh(name, tool, slug, slug.ToUpper()), x: true);
        W(dir, "scripts", "diagnose.sh", DiagnoseSh(name, tool, slug), x: true);
        W(dir, "references", "installation_flow.md", R("Installation Flow", "Fill from mining Step 1a.", IF()));
        W(dir, "references", "credentials_setup.md", R("Credentials Setup", "Fill from mining Step 1b.", CS()));
        W(dir, "references", "known_issues.md", R("Known Issues", "Fill from mining Step 1c.", KI(tool)));
        W(dir, "references", "best_practices.md", R("Best Practices", "Fill from mining Step 1d.", BP(tool)));
        if (!noConfig) W(dir, "config-template", $"{slug}.json.example", ConfigJson());
        Console.WriteLine($"Done: {dir}");
        return 0;
    }

    static string Md(string n, string t, string sl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {n}");
        sb.AppendLine($"description: {PH(3)} Describe in 4-8 sentences. Use mined bug strings as literal triggers.");
        sb.AppendLine("---");
        sb.AppendLine($"# {n}");
        sb.AppendLine();
        sb.AppendLine("## Principles");
        sb.AppendLine($"- Never vendor {t} files here.");
        sb.AppendLine("- Repairs at runtime: instructions in known_issues.md.");
        sb.AppendLine("- Ask before touching upstream files.");
        sb.AppendLine();
        sb.AppendLine("## Capabilities");
        sb.AppendLine($"| Install {t} | scripts/install_{sl}.sh |");
        sb.AppendLine("| Credentials | inline workflow |");
        sb.AppendLine("| Diagnose & fix | scripts/diagnose.sh + known_issues.md |");
        sb.AppendLine();
        sb.AppendLine($"## Install ({PH(4)})");
        sb.AppendLine("```bash");
        sb.AppendLine($"bash scripts/install_{sl}.sh");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"## Credentials ({PH(7)})");
        sb.AppendLine();
        sb.AppendLine($"## Diagnose & fix ({PH(5)})");
        sb.AppendLine();
        sb.AppendLine("## Refuses to");
        sb.AppendLine("- Vendor upstream files");
        sb.AppendLine("- Pin upstream versions in SKILL.md");
        sb.AppendLine("- Silently patch upstream");
        return sb.ToString();
    }

    static string InstallSh(string n, string t, string sl, string u)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine($"# install_{sl}.sh — Install {t}");
        sb.AppendLine($"# {PH(4)}");
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();
        sb.AppendLine("BASE_URL=\"<FILL>\"");
        sb.AppendLine($"STAGING_ROOT=\"/tmp/{n}-staging\"");
        sb.AppendLine("STAGING_DIR=\"${STAGING_ROOT}/$(date +%s)-$$\"");
        sb.AppendLine("cleanup() { [ -n \"${STAGING_DIR:-}\" ] && [ -d \"$STAGING_DIR\" ] && command rm -rf \"$STAGING_DIR\"; }");
        sb.AppendLine("trap cleanup EXIT");
        sb.AppendLine();
        sb.AppendLine("echo 'TODO: fill install logic per workflow.md Step 4' >&2");
        sb.AppendLine("exit 1");
        return sb.ToString();
    }

    static string DiagnoseSh(string n, string t, string sl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine($"# diagnose.sh — Read-only health check for {t}");
        sb.AppendLine($"# {PH(6)}");
        sb.AppendLine("set -uo pipefail");
        sb.AppendLine();
        sb.AppendLine("status_ok()   { echo \"OK $1\"; }");
        sb.AppendLine("status_warn() { echo \"WARN $1\"; }");
        sb.AppendLine("status_fail() { echo \"FAIL $1\"; }");
        sb.AppendLine($"echo \"=== {n} diagnostic ===\"");
        sb.AppendLine();
        sb.AppendLine("echo 'TODO: fill diagnose per workflow.md Step 6' >&2");
        sb.AppendLine("exit 2");
        return sb.ToString();
    }

    static string R(string t, string top, string b) => $"# {t}\n\n{top}\n\n{b}\n";
    static string IF() => "Why wrapper installer exists.\nPrerequisites: curl, unzip, npx, Node >= 18.\nAgent detection.\nVersion override.\nLayout after install.\nUninstall.\nTroubleshooting.\n";
    static string CS() => "Where credentials go (XDG, modes).\nEnv var fallback.\nHow to obtain.\nLiveness test.\nRotation.\n";
    static string KI(string t) => "Agent: 1. explain 2. AskUserQuestion for strategy 3. execute (backup first) 4. re-run diagnose 5. remind upstream upgrades.\n\n<<< Add ISSUE-001, ... one per bug from mining Step 1c. >>>\n";
    static string BP(string t) => "Non-obvious patterns from session.\nRecommended defaults.\nCommon pitfalls.\nWhen vs alternatives.\n";
    static string ConfigJson() => "{\n  \"_comment_field1\": \"<<< FILL-STEP-8 >>>\",\n  \"field1\": [\"placeholder\"]\n}\n";
    static string S(string n) { var s = Regex.Replace(n, "[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant(); return string.IsNullOrEmpty(s) ? "wrapper" : s; }
    static void W(string dir, string sub, string f, string c, bool x = false) { var d = string.IsNullOrEmpty(sub) ? dir : Path.Combine(dir, sub); Directory.CreateDirectory(d); var p = Path.Combine(d, f); File.WriteAllText(p, c, Encoding.UTF8); if (x && !OperatingSystem.IsWindows()) try { File.SetUnixFileMode(p, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); } catch { } Console.WriteLine($"  OK {(string.IsNullOrEmpty(sub) ? "" : sub + "/")}{f}"); }
}
