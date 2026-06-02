namespace SkillManager.Cli.Tools;

public class InventoryRunner
{
    private readonly string _skillDir;

    public InventoryRunner(string skillDir)
    {
        _skillDir = Path.GetFullPath(skillDir);
    }

    public InventoryResult Run()
    {
        var result = new InventoryResult
        {
            SkillName = Path.GetFileName(_skillDir),
            SkillPath = _skillDir
        };

        // SKILL.md
        var skillMdPath = Path.Combine(_skillDir, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            result.HasSkillMd = true;
            result.SkillMdSizeBytes = new FileInfo(skillMdPath).Length;
            var lines = File.ReadAllLines(skillMdPath);
            result.SkillMdLineCount = lines.Length;
        }

        // Resources
        result.References = InventoryDirectory("references");
        result.Scripts = InventoryDirectory("scripts");
        result.Assets = InventoryDirectory("assets");
        result.Agents = InventoryDirectory("agents");
        result.Workflows = InventoryDirectory("workflows");
        result.EvalViewerFiles = InventoryDirectory("eval-viewer");
        result.Templates = InventoryDirectory("templates");
        result.Tools = InventoryDirectory("tools");
        result.TestFiles = InventoryDirectory("tests");

        // Evals
        var evalsDir = Path.Combine(_skillDir, "evals");
        if (Directory.Exists(evalsDir))
        {
            result.HasEvals = true;
            result.EvalsFiles = Directory.GetFiles(evalsDir, "*.*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_skillDir, f))
                .ToList();
        }

        // .NET tools
        var toolsDir = Path.Combine(_skillDir, "tools");
        if (Directory.Exists(toolsDir))
        {
            result.DotNetTools = Directory.GetFiles(toolsDir, "*.csproj", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_skillDir, f))
                .ToList();
        }

        return result;
    }

    private List<string> InventoryDirectory(string dirName)
    {
        var dir = Path.Combine(_skillDir, dirName);
        if (!Directory.Exists(dir))
            return new List<string>();

        return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_skillDir, f))
            .ToList();
    }
}

public class InventoryResult
{
    public string? SkillName { get; set; }
    public string SkillPath { get; set; } = "";
    public bool HasSkillMd { get; set; }
    public long SkillMdSizeBytes { get; set; }
    public int SkillMdLineCount { get; set; }
    public bool HasEvals { get; set; }
    public List<string> EvalsFiles { get; set; } = new();
    public List<string> References { get; set; } = new();
    public List<string> Scripts { get; set; } = new();
    public List<string> Assets { get; set; } = new();
    public List<string> Agents { get; set; } = new();
    public List<string> Workflows { get; set; } = new();
    public List<string> EvalViewerFiles { get; set; } = new();
    public List<string> Templates { get; set; } = new();
    public List<string> Tools { get; set; } = new();
    public List<string> TestFiles { get; set; } = new();
    public List<string> DotNetTools { get; set; } = new();

    public int TotalFileCount =>
        References.Count + Scripts.Count + Assets.Count + Agents.Count +
        Workflows.Count + EvalViewerFiles.Count + Templates.Count + Tools.Count +
        TestFiles.Count + EvalsFiles.Count + (HasSkillMd ? 1 : 0);
}
