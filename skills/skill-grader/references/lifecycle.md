# Skill Lifecycle Decisions

When to merge, split, or deprecate a skill.

## Merge Skills

Merge when two skills:
- Always trigger together on the same user requests
- Share the same CLI command group
- Have overlapping route tables that confuse the model

**How**: Pick the primary skill name. Move the other skill's unique references into the primary skill. Update the route table. Test both original trigger scenarios. Keep the merged skill, deprecate the absorbed one.

**Example**: A `chart-bar` and `chart-line` skill that always get triggered together should merge into `chart`.

## Split Skills

Split when one skill:
- Exceeds ~80 lines of SKILL.md body content
- Contains unrelated command groups that serve different user tasks
- Has conflicting trigger keywords causing model routing problems

**How**: Identify the distinct responsibilities. Create separate skill directories. Copy relevant references and examples to each. Narrow each description to its specific triggers. Validate all new skills. Deprecate or remove the original.

**Example**: `inspect` teaching both paper diagnosis AND document generation should split if the two paths trigger on unrelated requests.

## Deprecate Skills

Deprecate rather than delete when:
- A skill is renamed (old name should redirect)
- A skill is merged into another
- A skill's CLI commands are retired

**How**: Keep the directory but add a deprecation notice in SKILL.md pointing to the replacement. Remove from plugin.json active skill list. Mark in changelog. After one release cycle, delete the directory.

**Example**: `multimodal` renamed to `ocr` — `multimodal` directory deleted, `ocr` directory is the canonical location.

## Validation

After any lifecycle change, run the full gate:

```powershell
nong skill validate .\<changed-skill> --json
nong skill inventory . --json
nong skill scan . --json
nong skill package . --json
```
