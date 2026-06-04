using YamlDotNet.Serialization;

namespace SkillManager.Cli.Models;

public class SkillFrontmatter
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "description")]
    public object? Description { get; set; }

    [YamlMember(Alias = "context")]
    public string? Context { get; set; }

    [YamlMember(Alias = "agent")]
    public string? Agent { get; set; }

    [YamlMember(Alias = "model")]
    public string? Model { get; set; }

    [YamlMember(Alias = "license")]
    public string? License { get; set; }

    [YamlMember(Alias = "allowed-tools")]
    public object? AllowedTools { get; set; }

    [YamlMember(Alias = "user-invocable")]
    public object? UserInvocable { get; set; }

    [YamlMember(Alias = "disable-model-invocation")]
    public object? DisableModelInvocation { get; set; }

    [YamlMember(Alias = "argument-hint")]
    public string? ArgumentHint { get; set; }

    [YamlMember(Alias = "hooks")]
    public object? Hooks { get; set; }

    [YamlMember(Alias = "metadata")]
    public object? Metadata { get; set; }

    [YamlMember(Alias = "version")]
    public string? Version { get; set; }
}
