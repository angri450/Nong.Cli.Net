using System.CommandLine;

namespace Nong.Cli.Common;

/// <summary>
/// V8: Reflections-based manifest generation. Walks the command tree.
/// Uses [CommandCategory] attribute on command classes for grouping.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CommandCategoryAttribute(string group) : Attribute
{
    public string Group { get; } = group;
}

public static class ManifestBuilder
{
    /// <summary>Build manifest from a root command by reflecting its subcommand tree.</summary>
    public static List<Manifest.CommandInfo> Build(Command root)
    {
        var result = new List<Manifest.CommandInfo>();

        foreach (var sub in root.Subcommands)
        {
            WalkCommand(sub, sub.Name, result);
        }

        return result;
    }

    static void WalkCommand(Command cmd, string group, List<Manifest.CommandInfo> result)
    {
        if (cmd.Subcommands.Count == 0)
        {
            // Leaf command
            result.Add(new Manifest.CommandInfo(
                Name: cmd.Name,
                Description: cmd.Description ?? "",
                Group: group,
                Aliases: cmd.Aliases.ToArray(),
                Parameters: GetParams(cmd),
                Status: "implemented"
            ));
        }
        else
        {
            // Group node — walk children, using this command's name as group
            foreach (var sub in cmd.Subcommands)
            {
                WalkCommand(sub, cmd.Name, result);
            }
        }
    }

    static Manifest.ParamDef[]? GetParams(Command cmd)
    {
        var list = new List<Manifest.ParamDef>();

        foreach (var arg in cmd.Arguments)
        {
            list.Add(new Manifest.ParamDef(
                Name: arg.Name,
                Type: MapType(arg.ValueType),
                Description: arg.Description ?? "",
                Required: true
            ));
        }

        foreach (var opt in cmd.Options)
        {
            if (opt.Name == "--json" || opt.Name == "--verbose") continue; // global
            list.Add(new Manifest.ParamDef(
                Name: opt.Name,
                Type: MapType(opt.ValueType),
                Description: opt.Description ?? "",
                Required: opt.IsRequired
            ));
        }

        return list.Count > 0 ? list.ToArray() : null;
    }

    static string MapType(Type t)
    {
        if (t == typeof(string)) return "string";
        if (t == typeof(bool)) return "boolean";
        if (t == typeof(int) || t == typeof(long)) return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
        return "string";
    }
}
