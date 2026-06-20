# Skill Authoring Guide

How to create a well-structured Nong.Toolkit.Net skill.

## SKILL.md Template

```markdown
---
name: <short-name>
description: <what it does>. Trigger on <concrete trigger keywords>.
---

# <Title>

<One-sentence description>.

## Nong CLI Preflight

Read [../../../.claude/references/nong-cli-preflight.md]...

## Route Table

| User wants | Command / Reference |
|------------|---------------------|
| <scenario A> | <command or reference link> |
| <scenario B> | <command or reference link> |

## Boundaries

- <what this skill does NOT do>
- <when to route to a different skill>
```

## When to Split into References

- SKILL.md exceeds ~40 lines: move detailed rules into `references/`.
- A single reference exceeds ~80 lines: consider splitting further.
- Shared rules used by multiple skills: put in `references/shared/`.

## When to Add Examples

- Every skill MUST have at least 1 successful example in `examples/`.
- High-frequency or complex skills SHOULD have at least 1 failure example.
- Use the standard example format: What the user wants → What was done → Result → Key takeaways.

## Naming Rules

| Skill type | Naming rule | Example |
|------------|------------|---------|
| CLI-mirror skill | Same name as the CLI command group | `word`, `pdf`, `ocr`, `chart` |
| Governance skill | Role-based name describing the policy area | `skill-manager` |
| Workflow skill | Verb-noun describing the process | `progress-report` |

## Relationship with `nong skill`

- `nong skill validate <path>`: checks SKILL.md structure, frontmatter, and cross-references. Run before committing.
- `nong skill scan <root>`: runs security scan (credential leaks, unsafe patterns). Fix findings before packaging.
- `nong skill inventory <root>`: lists all skills with counts of references, examples, and formats.
- `nong skill package <root>`: runs validate + scan, then creates a zip for distribution.

**skill-manager is policy** — it teaches HOW to design skills. **nong skill is the gate** — it deterministically checks correctness.
