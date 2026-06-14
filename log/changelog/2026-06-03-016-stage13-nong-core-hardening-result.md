# Stage 13 Nong Core Hardening — Result Report

Date: 2026-06-03
Status: COMPLETE — all acceptance criteria met

---

## Goal

Harden the `nong skill` migration and release surface. Fix 5 issues from the stage 12 audit: plugin-root packaging, inventory manifest detection, old SkillManager release hazard, YamlDotNet version pinning, and behavioral verification.

## Files Changed

### Core logic

| File | Change |
|------|--------|
| `SkillManagerCore/Tools/Packager.cs` | Rewrote: added `SkillRootClassifier`, `PluginRootKind` enum, `SkillRootInfo` record, `PackagePluginAsync`, `VerifyArchiveAsync(SkillRootKind)` |
| `SkillManagerCore/SkillManagerCore.csproj` | Pinned `YamlDotNet` from `*` to `18.0.0` |
| `Cli/Commands/SkillCommands.cs` | Rewrote: shared `TryResolveDir` guard, `rootType` in inventory, plugin-root packaging, shared error emitters |

### Release surface

| File | Change |
|------|--------|
| `SkillManager/SkillManager.Cli.csproj` | Added `IsPackable=false`, `PackAsTool=false` |
| `SkillManager/Program.cs` | Fixed `VerifyArchiveAsync` call to pass `SkillRootKind.Skill` |
| `release-checklist.md` | Added 24 command count, skill commands section, legacy SkillManager deprecation note |

### Unchanged (deliberately)

| Area | Why |
|------|-----|
| Nong.Toolkit.Net | Explicitly excluded from this stage |
| PPTX/OCR implementation | Not in scope |
| Word/Inspect/Chart expansion | Not in scope |
| `nong skill eval/scaffold/serve/optimize` | P1, not in scope |
| `EvalViewer`, `EvalRunner`, `LoopRunner`, etc. | P1 code preserved, not modified |

---

## Behavior Verification Matrix (16 tests)

| # | Command | Scenario | Expected | Result |
|---|---------|----------|----------|--------|
| 1 | `skill validate` | valid skill | status:ok, EXIT:0 | PASS |
| 2 | `skill validate` | invalid (no frontmatter) | E006, EXIT:1 | PASS |
| 3 | `skill validate` | empty path `""` | E003, EXIT:1 | PASS |
| 4 | `skill validate` | missing dir | E001, EXIT:1 | PASS |
| 5 | `skill scan` | no High/Critical | status:ok, EXIT:0 | PASS |
| 6 | `skill scan` | HIGH finding (email) | status:error, E006, EXIT:1 | PASS |
| 7 | `skill inventory` | single skill | rootType=skill, skillCount=1 | PASS |
| 8 | `skill inventory` | plugin root | rootType=plugin, hasPluginManifest=true, hasMarketplaceManifest=true, hasSkillsManifest=true | PASS |
| 9 | `skill inventory` | empty directory | rootType=directory, skillCount=0, EXIT:0 | PASS |
| 10 | `skill package` | single skill | packageType=skill, zip contains SKILL.md | PASS |
| 11 | `skill package` | plugin root | packageType=plugin, skillCount=2, zip contains manifests + child SKILL.md | PASS |
| 12 | `skill package` | invalid (ordinary dir) | E006 "neither a skill nor a plugin root", EXIT:1 | PASS |
| 13 | `commands --json` | default | 24 implemented, 0 stubs in output | PASS |
| 14 | `commands --all --json` | with --all | 47 total (24 impl + 23 stub) | PASS |
| 15 | `word extract` | stub command | E009, EXIT:1 | PASS |
| 16 | Nong.Toolkit.Net | full integration | inventory rootType=plugin, scan 0 High+, package plugin zip with 17 skills | PASS |

### Zip content verification

Single skill zip:
```
SKILL.md (147 bytes)
```

Plugin zip (test):
```
.claude-plugin/plugin.json (47 bytes)
.claude-plugin/marketplace.json (38 bytes)
skills.sh.json (26 bytes)
skill-a/SKILL.md (60 bytes)
skill-b/SKILL.md (60 bytes)
```

No duplicate entries. `.claude-plugin/` nested manifests preserved. Root-level `skills.sh.json` only once.

---

## Build Results

```powershell
dotnet build .\Cli\NongCli.csproj -c Release       # 0 errors
dotnet build .\SkillManager\SkillManager.Cli.csproj -c Release  # 0 errors (legacy)
```

---

## Known Remaining Issues

1. Nong.Toolkit.Net `nuget` skill includes `nuget.exe` (8.4MB) in its directory — this gets packaged into the plugin zip. This is a data issue in GroundPA, not a code issue in nong.
2. Old `SkillManager` project retains `net10.0` build artifacts in `bin/` — these are `.gitignore`d and harmless.

---

## What Was Deliberately Not Changed

- Did not implement `nong skill eval/scaffold/serve/optimize-description/run-loop` — P1
- Did not touch Nong.Toolkit.Net skill files
- Did not change any PPTX/OCR/Word/Inspect/Chart commands
- Did not publish NuGet packages
- Did not change git branches

---

## Next Recommended Stage

Proceed to Phase 1 (contract test harness) and Phase 2 (Word stub completion) per `2026-06-03-014-full-stub-completion-blueprint.md`.

---

## Reviewer Checklist

Run from `C:\Users\Administrator\Documents\Github\Nong.Cli.Net`:

```powershell
# Build
dotnet build .\Cli\NongCli.csproj -c Release
dotnet build .\SkillManager\SkillManager.Cli.csproj -c Release

# Command discovery
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --json
dotnet .\Cli\bin\Release\net8.0\nong.dll commands --all --json

# Validate GroundPA word skill
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net\word --json

# Inventory Nong.Toolkit.Net
dotnet .\Cli\bin\Release\net8.0\nong.dll skill inventory C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json

# Scan Nong.Toolkit.Net
dotnet .\Cli\bin\Release\net8.0\nong.dll skill scan C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json

# Package Nong.Toolkit.Net
dotnet .\Cli\bin\Release\net8.0\nong.dll skill package C:\Users\Administrator\Documents\Github\Nong.Toolkit.Net --json

# Error behaviors
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate "" --json
dotnet .\Cli\bin\Release\net8.0\nong.dll skill validate C:\missing --json
dotnet .\Cli\bin\Release\net8.0\nong.dll word extract --json
```

Expected for each: exit code 1 on errors, status:ok on success, correct rootType/manifest booleans in inventory.
