# 2026-06-03 Stage 13 Guidance: Angri450.Nong Core Hardening

## Mission

This stage only works on:

```text
C:\Users\Administrator\Documents\Github\Angri450.Nong
```

Do not edit `GroundPA-Toolkit` in this stage.

The product direction is fixed:

```text
nong is the deterministic .NET 8+ CLI/tool layer for low-token AI-agent workflows.
```

This stage is not a small patch. Treat it as a full hardening pass for the current `nong skill` migration and release surface. Use ClaudeCode multi-agent parallel development to reduce main-context load. The main conversation should only need to read the final audit report.

## Non-Negotiable Scope

Allowed:

```text
Cli/
SkillManagerCore/
SkillManager/
Tests/
CAPABILITY.md
README.md
README.zh-CN.md
release-checklist.md
log/
changelog/
```

Not allowed:

```text
Do not edit GroundPA-Toolkit.
Do not merge external repositories.
Do not refactor unrelated third-party source.
Do not publish NuGet packages.
Do not change git branches.
Do not delete user work.
Do not mark completion based only on changelog claims.
```

## Current Audit Findings To Fix

### A. `nong skill package` cannot package plugin roots

Observed command:

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll skill package C:\Users\Administrator\Documents\Github\GroundPA-Toolkit --json
```

Current behavior:

```json
{
  "status": "error",
  "command": "skill package",
  "errors": [
    {
      "code": "E006",
      "name": "validation_failed",
      "message": "SKILL.md: SKILL.md not found in skill directory"
    }
  ]
}
```

This is wrong for product usage. `skill package` must support both:

```text
single skill directory: contains SKILL.md
plugin root directory: contains .claude-plugin/plugin.json or skills.sh.json and child directories with SKILL.md
```

Required behavior:

```text
nong skill package <single-skill-dir> --json
nong skill package <plugin-root-dir> --json
```

For single skill:

1. Validate the skill.
2. Scan the skill.
3. Block on Critical/High findings.
4. Create zip.
5. Verify zip exists, nonzero, and contains `SKILL.md`.
6. Return `artifacts.zip`.

For plugin root:

1. Detect plugin root.
2. Inventory child skills.
3. Validate every child skill.
4. Scan the plugin root.
5. Block on Critical/High findings.
6. Create one plugin zip containing:
   - `.claude-plugin/plugin.json` if present
   - `.claude-plugin/marketplace.json` if present
   - `skills.sh.json` if present
   - skill directories and their resource files
7. Exclude dev/build/runtime junk:
   - `.git/`
   - `bin/`
   - `obj/`
   - `node_modules/`
   - `__pycache__/`
   - `workspace/`
   - `tests/`
   - `TestResults/`
   - `.vs/`
   - `temp/`
   - existing `*.zip`
8. Verify zip exists, nonzero, and contains at least one `SKILL.md`.
9. Return `artifacts.zip`, `data.packageType = "plugin"`, `metrics.skillCount`.

If a directory is neither a single skill nor a plugin root, return:

```text
E006 validation_failed
EXIT:1
```

Do not return `E004` for ordinary invalid input.

### B. `skill inventory` reports plugin manifests incorrectly

Current code checks:

```text
hasPluginManifest = File.Exists(Path.Combine(fullDir, "marketplace.json"))
hasMarketplaceManifest = File.Exists(Path.Combine(fullDir, "marketplace.json"))
```

This is wrong.

Required detection:

```text
hasPluginManifest =
  .claude-plugin/plugin.json exists
  OR plugin.json exists

hasMarketplaceManifest =
  .claude-plugin/marketplace.json exists
  OR marketplace.json exists

hasSkillsManifest =
  skills.sh.json exists
```

Required JSON data:

```json
{
  "root": "...",
  "rootType": "skill" | "plugin" | "directory",
  "skills": [],
  "skillCount": 17,
  "totalFiles": 111,
  "hasPluginManifest": true,
  "hasMarketplaceManifest": true,
  "hasSkillsManifest": true
}
```

`rootType` rules:

```text
skill     root contains SKILL.md
plugin    root has plugin/marketplace/skills manifest or child skill dirs
directory otherwise
```

### C. Old `SkillManager` global tool remains a release hazard

`SkillManager/SkillManager.Cli.csproj` currently still has:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>skill-manager</ToolCommandName>
<PackageId>Angri450.Nong.Skill.Manager</PackageId>
```

The product decision is:

```text
Do not promote old skill-manager global tool.
Use nong skill instead.
```

Required action:

1. Keep legacy project buildable for local compatibility if useful.
2. Remove it from normal publish/release path.
3. Prefer:

```xml
<IsPackable>false</IsPackable>
<PackAsTool>false</PackAsTool>
```

4. Its usage text must say legacy/deprecated and point to:

```text
nong skill validate <dir>
nong skill scan <dir>
nong skill inventory <dir>
nong skill package <dir>
```

5. `release-checklist.md` must explicitly say:

```text
Do not pack or publish Angri450.Nong.Skill.Manager in 3.1.x/2.0.0 migration.
Only Angri450.Nong.Cli is the public tool entry.
```

### D. Floating dependency in `SkillManagerCore`

Current:

```xml
<PackageReference Include="YamlDotNet" Version="*" />
```

For `nong` release stability this is too loose.

Required action:

1. Pin YamlDotNet to the version currently restored in the environment.
2. If lock file or package cache makes this unclear, inspect build output or `obj/project.assets.json`.
3. Do not upgrade dependencies opportunistically.
4. Record the chosen version and reason in the result report.

### E. CLI contract and tests are not strong enough

Add focused tests or a deterministic verification harness for `nong skill`.

Required coverage:

```text
skill validate empty path -> E003 + EXIT:1
skill validate missing dir -> E001 + EXIT:1
skill validate valid skill -> status ok + EXIT:0
skill validate invalid skill -> E006 + EXIT:1
skill scan no High/Critical -> status ok + EXIT:0
skill scan High/Critical -> status error + E006 + EXIT:1
skill inventory single skill -> rootType skill + skillCount 1
skill inventory plugin root -> rootType plugin + manifest booleans correct
skill inventory empty directory -> rootType directory + skillCount 0 + EXIT:0
skill package single skill -> zip exists, nonzero, contains SKILL.md
skill package plugin root -> zip exists, nonzero, contains child SKILL.md and plugin manifests
skill package invalid dir -> E006 + EXIT:1
commands --json -> includes 24 implemented commands
commands --all --json -> includes stubs
stub command -> E009 + EXIT:1
```

Tests can be:

```text
real xUnit tests in Tests/
or a PowerShell verification script under tests-output/stage13/
```

But the result report must include exact commands run and key outputs.

## Multi-Agent Development Plan

Use ClaudeCode subagents. The main ClaudeCode thread coordinates only; subagents do code reading, implementation, and local verification.

### Agent A: CLI Contract And Command Behavior

Files:

```text
Cli/Commands/SkillCommands.cs
Cli/Common/CliHelpers.cs
Cli/Common/ErrorCodes.cs
Cli/Common\JsonOutput.cs
Cli/Common\Manifest.cs
Cli/Program.cs
```

Tasks:

1. Refactor `SkillCommands` to share path classification:
   - empty path
   - missing path
   - single skill root
   - plugin root
   - ordinary directory
2. Ensure all expected invalid inputs return `E001`, `E003`, or `E006`, not `E004`.
3. Ensure every error path sets nonzero exit code.
4. Ensure every JSON response has:
   - `status`
   - `command`
   - `data`
   - `issues`
   - `artifacts`
   - `metrics`
   - `errors`
   - `meta.durationMs`
   - `meta.version`
5. Ensure command names are exact:
   - `skill validate`
   - `skill scan`
   - `skill inventory`
   - `skill package`

Agent A must not implement eval/scaffold/serve/optimizer commands.

### Agent B: SkillManagerCore Packaging And Inventory

Files:

```text
SkillManagerCore/Tools/Packager.cs
SkillManagerCore/Tools/InventoryRunner.cs
SkillManagerCore/Tools/SkillValidator.cs
SkillManagerCore/Tools/SecurityScanner.cs
SkillManagerCore/Models/*.cs
```

Tasks:

1. Add clean support for plugin-root inventory and packaging.
2. Avoid putting plugin-root logic only in `SkillCommands` if it belongs in core.
3. Keep package exclusion rules centralized and testable.
4. Verify archive content:
   - single skill archive must contain root-level `SKILL.md`
   - plugin archive must contain at least one `*/SKILL.md`
   - plugin archive should include `.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json` when present
5. Keep namespace stable for now if broad rename would increase risk.

Do not move or rewrite unrelated eval/optimizer/viewer code.

### Agent C: Release Surface And Legacy Tool Safety

Files:

```text
SkillManager/SkillManager.Cli.csproj
SkillManager/Program.cs
SkillManager/README.md
Cli/NongCli.csproj
CAPABILITY.md
README.md
README.zh-CN.md
release-checklist.md
```

Tasks:

1. Make legacy `SkillManager` non-packable or otherwise impossible to accidentally publish in the normal release path.
2. Keep it buildable if retained.
3. Update user-facing Angri450.Nong docs so public install path is:

   ```powershell
   dotnet tool install --global Angri450.Nong.Cli
   nong skill ...
   ```

4. Remove Angri450.Nong-side recommendations to install `Angri450.Nong.Skill.Manager`.
5. Update release checklist with the new release rule.
6. Confirm no `net11` or preview requirement is introduced in `Cli`, `SkillManagerCore`, or `SkillManager`.

Do not edit GroundPA docs in this stage.

### Agent D: Tests And Behavioral Verification

Files:

```text
Tests/
tests-output/
log/
changelog/
```

Tasks:

1. Create deterministic test fixtures under a stage-specific temporary folder.
2. Include:
   - valid skill
   - invalid skill without frontmatter
   - plugin root with `.claude-plugin/plugin.json`
   - plugin root with `.claude-plugin/marketplace.json`
   - plugin root with two child skills
   - scan target containing a deliberate HIGH finding
3. Run Release build.
4. Run CLI behavior tests using the built `nong.dll`, not global `nong`.
5. Capture exit codes.
6. Capture zip content checks.
7. Keep generated outputs out of source directories unless they are test fixtures.

Use this invocation pattern:

```powershell
dotnet C:\Users\Administrator\Documents\Github\Angri450.Nong\Cli\bin\Release\net8.0\nong.dll <args>
Write-Output "EXIT:$LASTEXITCODE"
```

### Agent E: Final Internal Audit Report

Files:

```text
changelog/
log/
```

Tasks:

1. Read all subagent results.
2. Re-run the final acceptance commands independently.
3. Create one final report:

```text
changelog/2026-06-03-016-stage13-nong-core-hardening-result.md
```

4. The report must be concise but audit-ready:

```text
Goal
Files changed
Architecture changes
Commands run
Build/test results
Behavior verification table
Known remaining issues
What was deliberately not changed
Next recommended stage
```

5. Include a `Reviewer Checklist` section with exact commands for Codex/user to re-run.

## Required Final Acceptance Commands

Run from:

```powershell
C:\Users\Administrator\Documents\Github\Angri450.Nong
```

Build:

```powershell
dotnet build .\Cli\NongCli.csproj -c Release
dotnet build .\SkillManager\SkillManager.Cli.csproj -c Release
```

Command discovery:

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json
```

Core skill commands:

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate <valid-skill-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill scan <plugin-root-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill inventory <single-skill-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill inventory <plugin-root-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill package <single-skill-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill package <plugin-root-dir> --json
```

Error behavior:

```powershell
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate "" --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate C:\missing\nong-skill --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill package <invalid-dir> --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word extract --json
```

For each error command, report:

```text
errors[0].code
status
EXIT
```

Artifact checks:

```powershell
Test-Path <zip>
(Get-Item <zip>).Length
```

Zip content checks:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead("<zip>").Entries | Select-Object FullName,Length
```

## Expected Behavior Summary

### `skill inventory <GroundPA-like plugin root> --json`

Expected:

```json
{
  "status": "ok",
  "command": "skill inventory",
  "data": {
    "rootType": "plugin",
    "skillCount": 2,
    "hasPluginManifest": true,
    "hasMarketplaceManifest": true,
    "hasSkillsManifest": true
  }
}
```

### `skill package <GroundPA-like plugin root> --json`

Expected:

```json
{
  "status": "ok",
  "command": "skill package",
  "data": {
    "packageType": "plugin",
    "skillCount": 2
  },
  "artifacts": {
    "zip": "..."
  }
}
```

### `skill package <invalid-dir> --json`

Expected:

```json
{
  "status": "error",
  "command": "skill package",
  "errors": [
    {
      "code": "E006",
      "name": "validation_failed"
    }
  ]
}
```

Exit code must be 1.

## Implementation Notes

Prefer adding small domain helpers over duplicating conditions in every command. Suggested internal concepts:

```text
SkillRootKind.Skill
SkillRootKind.Plugin
SkillRootKind.Directory
```

Suggested helper output:

```csharp
sealed record SkillRootInfo(
    string FullPath,
    SkillRootKind Kind,
    bool HasSkillMd,
    bool HasPluginManifest,
    bool HasMarketplaceManifest,
    bool HasSkillsManifest,
    IReadOnlyList<string> SkillDirectories);
```

This helper can live in `Cli/Commands/SkillCommands.cs` if kept private, or in `SkillManagerCore` if reused by packager/inventory.

Use `Path.GetFullPath` only after guarding empty/whitespace paths.

Use `CliHelpers.CheckArtifact` after creating zips.

For plugin zips, also verify archive content. `CheckArtifact` alone is not enough.

## Contract Guardrails

Do not regress these established behaviors:

```text
nong commands --json lists only implemented commands.
nong commands --all --json lists implemented + stub commands.
All stubs return E009 + EXIT:1.
Missing file/dir returns E001 + EXIT:1.
Malformed/invalid content returns E006 + EXIT:1.
Internal exceptions return E004 + EXIT:1.
Generated artifacts must exist and be nonzero before returning ok.
```

## What Not To Do In Stage 13

Do not implement:

```text
nong skill eval
nong skill eval serve
nong skill scaffold
nong skill optimize-description
nong skill run-loop
```

Do not touch:

```text
GroundPA-Toolkit
PPTX/OCR implementation
Word/Inspect/Chart expansion
```

This stage is about making the Angri450.Nong core reliable enough before syncing skill-layer docs.

## Completion Definition

This stage is complete only when:

1. `Cli` Release build passes.
2. legacy `SkillManager` still builds or is explicitly documented as intentionally not built.
3. `nong skill validate/scan/inventory/package` pass the behavior matrix.
4. plugin-root packaging works.
5. inventory manifest booleans are correct.
6. old `SkillManager` cannot be accidentally published through normal release.
7. YamlDotNet is pinned.
8. final report exists at:

```text
changelog/2026-06-03-016-stage13-nong-core-hardening-result.md
```

9. final report includes exact commands and exit codes.

If any acceptance command fails, do not write "completed". Write `PARTIAL` and list the failing command.
