# Create a New Skill

## What the user wants

Create a skill for Excel-to-Chart data pipeline routing.

## What was done

1. Created the skill directory and SKILL.md:
```
excel-chart/
  SKILL.md
```

2. Wrote SKILL.md following the template from [authoring.md](../references/authoring.md):
```markdown
---
name: excel-chart
description: Route Excel data into Chart statistical analysis. Trigger on Excel to chart, data to ANOVA, or worksheet to bar chart.
---

# Excel to Chart

Route Excel data through the Chart statistical pipeline.

## Route Table

| User wants | Command |
|------------|---------|
| Convert Excel to grouped JSON | `nong excel to-groups <file.xlsx> --json` |
| Run ANOVA on groups | `nong chart anova <groups.json> --json` |
| Create bar chart with error bars | `nong chart bar <groups.json> -o <chart.png> --json` |

## Boundaries

- Does not create Excel worksheets. Route to `excel` skill for that.
- Does not do non-statistical chart types. Route to `diagram` skill for flowcharts and network graphs.
```

3. Validated:
```powershell
nong skill validate .\excel-chart --json
```

## Result

Validation passed. The skill is now listed in `nong skill inventory . --json`.

## Key takeaways

- Start with a concise SKILL.md (~20-30 lines). Don't pre-write long references.
- Use the route table pattern: user intent → exact CLI command.
- Add boundaries immediately: what this skill does NOT do, and where to go instead.
- Validate early. Fix errors before adding references or examples.
