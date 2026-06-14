# progress-report CLI migration plan

## Goal

Move the project progress report generator from the ad hoc `tools/ProgressReport` project into the main `nong` command surface.

## Scope

- Add `nong progress report` under `Cli/Commands/`.
- Register the command in `Cli/Program.cs` and `Cli/Common/Manifest.cs`.
- Preserve the existing `log/plans`, `log/changelog`, `log/debug`, and `log/guidance` HTML report behavior.
- Remove the misplaced `.claude/skills/progress-report` and `tools/ProgressReport` directories from Nong.Cli.Net after the command works.
- Keep the Toolkit-side `progress-report` skill as the user-facing routing layer.

## Verification

- `dotnet build SkillManagerCore\SkillManagerCore.csproj -c Release`
- `dotnet build Cli\NongCli.csproj -c Release --no-restore`
- `Cli\bin\Release\net8.0\nong.exe progress report --project-root . --json`
- Toolkit skill inventory, validate, scan, and package gates after the CLI binary is rebuilt.

## Status

Done.
