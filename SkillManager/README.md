# Angri450.Nong.Skill.Manager

CLI tool for managing Claude Code skills. Validate, scan, package, evaluate, and scaffold skills.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Skill.Manager)](https://www.nuget.org/packages/Angri450.Nong.Skill.Manager)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet tool install -g Angri450.Nong.Skill.Manager
```

## Commands

```bash
# Validate SKILL.md format and structure
skill-manager validate ./my-skill/

# Security scan — check for common vulnerabilities
skill-manager scan ./my-skill/

# Package skill as distributable .zip
skill-manager package ./my-skill/

# Blind evaluation — run eval suite
skill-manager eval ./my-skill/

# Scaffold a new skill from template
skill-manager scaffold my-new-skill

# List all files in a skill directory
skill-manager inventory ./my-skill/
```

## Quick Start

```bash
# Create a new skill
skill-manager scaffold hello-world
cd hello-world

# Edit SKILL.md, add your code

# Validate
skill-manager validate .

# Package for distribution
skill-manager package .
```

## Dependencies

- `YamlDotNet` — YAML parsing for SKILL.md frontmatter

## API Reference

| Command | Description |
|---------|-------------|
| `validate <dir>` | Check SKILL.md structure, required fields, reference integrity |
| `scan <dir>` | Security analysis: dependency checks, injection risks, permission audit |
| `package <dir>` | Bundle skill into .zip with manifest |
| `eval <dir>` | Run blind evaluation suite, output score report |
| `scaffold <name>` | Generate new skill from template with proper structure |
| `inventory <dir>` | List all files with sizes and types |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
