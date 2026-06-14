# 2026-06-10 Toolkit shared resources packaging

## Goal

Support Nong.Toolkit.Net 2.4.0 shared plugin-level resources, especially `references/shared/nong-cli-preflight.md`, so duplicated SKILL.md preflight blocks can be replaced safely.

## Work

1. Update `SkillValidator` so a skill can reference plugin-level shared resource directories through `../references/...` without false missing-directory warnings.
2. Update `Packager` so plugin-level shared resource directories such as `references/` are included in plugin zip output.
3. Keep single-skill packaging behavior unchanged.
4. Validate against Nong.Toolkit.Net with local `Cli/bin/Release/net8.0/nong.exe`.

## Risk

Packaging more root directories can accidentally include development-only files if the allowlist is too broad. Only known resource directories should be included, and existing exclude rules must still apply.

## Status

Done.

## Verification

- `dotnet build SkillManagerCore\SkillManagerCore.csproj -c Release --no-restore -maxcpucount:1`
- `nong skill package . --json`
- Verified the generated plugin zip contains `references/shared/nong-cli-preflight.md`.
