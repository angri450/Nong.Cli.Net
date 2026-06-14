# official command surface alignment plan

## Goal

Close the Toolkit/CLI parity gap where Toolkit documentation discussed official-document drafting and gongwen formatting but Nong.Cli.Net only exposed the underlying library classes.

## Work

1. Add a first-class Word command for existing-DOCX gongwen formatting.
2. Add a first-class Inspect command for official-document DOCX generation from JSON spec.
3. Register both commands in `nong commands --json`.
4. Add command-level regression coverage.
5. Update Nong.Toolkit.Net word, inspect, genre, and skill docs so the skill layer routes to implemented CLI commands.
6. Re-run bidirectional command coverage audits and Toolkit package gates.

## Status

Done.

## Verification

- `dotnet build SkillManagerCore\SkillManagerCore.csproj -c Release --no-restore -maxcpucount:1`
- `dotnet build Cli\NongCli.csproj -c Release --no-restore -maxcpucount:1`
- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-build`
- Toolkit `skill validate` for `word`, `inspect`, `genre`, and `skill`
- Toolkit `skill inventory`, `skill scan`, and `skill package`
