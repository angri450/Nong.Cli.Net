---
name: skill-grader
description: Nong.Toolkit.Net skill lifecycle — create, validate, test, audit, prune, package. Trigger on create skill, write SKILL.md, validate skill, scan skill, inventory plugin, test skill triggers, check description quality, merge/split/deprecate skills, or package plugin.
---

# Skill Lifecycle

The complete Nong.Toolkit.Net skill lifecycle: create → validate → scan → test → prune → inventory → package.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before running any CLI gate. Confirm `nong` is installed and the `skill` command group.

## CLI Commands

```powershell
nong skill validate <skill-dir> --json
nong skill inventory <plugin-root> --json
nong skill scan <plugin-root> --json
nong skill package <plugin-root> --json
```

## Creation & Authoring

| User wants | Where to go |
|------------|-------------|
| SKILL.md template and structure | [references/authoring.md](references/authoring.md) |
| Naming rules for skills | [references/authoring.md](references/authoring.md) |
| When to split into references | [references/authoring.md](references/authoring.md) |
| Validate the result | `nong skill validate .\<name> --json` |

## Testing & Quality Review

| User wants | Where to go |
|------------|-------------|
| Check trigger precision and description quality | [references/trigger-audit.md](references/trigger-audit.md) |
| Capture failures and feed back into skill docs | [references/feedback-loop.md](references/feedback-loop.md) |

## Pruning Decisions

| User wants | Where to go |
|------------|-------------|
| Merge two skills | [references/lifecycle.md](references/lifecycle.md) |
| Split an oversized skill | [references/lifecycle.md](references/lifecycle.md) |
| Deprecate a retired skill | [references/lifecycle.md](references/lifecycle.md) |

## Dispatch

1. `skill inventory` at the plugin root → verify discovered skills and manifests.
2. `skill validate` on each changed skill directory.
3. `skill scan` at the plugin root before packaging or release.
4. `skill package` only after validation and scan are clean.
5. Move generated `.zip` files outside the repository.

## Gates

- Validation errors block packaging.
- High or Critical scan findings block packaging.
- Medium and Low findings must be read and either fixed or documented.
