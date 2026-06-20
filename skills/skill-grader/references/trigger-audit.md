# Trigger Audit Guide

How to check whether a skill's frontmatter `description` and trigger keywords are precise enough.

## Audit Steps

1. **Read the description field.** Does it name the capability and the concrete trigger contexts? A bad description says "helps with documents". A good one says "Word document read, check, slice, generate, edit, and format audit via nong CLI".

2. **Check keyword overlap with other skills.** If two skills share trigger keywords, the model may route to the wrong one. Example: `ocr analyze-image` is NOT "image analysis" in the general sense — it's OCR image structure QA. If another skill also triggers on "image", there's a conflict.

3. **Test boundary scenarios.** Ask: "If a user says X, which skill should activate?" If the answer is ambiguous, the description or triggers need tightening.

## Common Problems

| Problem | Example | Fix |
|---------|---------|-----|
| Name too broad | `multimodal` for OCR-only | Rename to `ocr` |
| Description too vague | "helps with skills" | Rewrite to "create, maintain, and audit skills; trigger on skill creation, editing, merging, deprecation" |
| Missing boundary clause | No list of what the skill does NOT do | Add explicit "Not for..." boundaries |
| Keyword conflict | Two skills both trigger on "analyze" | Narrow each description to the specific analysis type |

## Validation

After fixing triggers, validate the skill:

```powershell
nong skill validate .\<skill-name> --json
```

If the description changed, also check `nong skill inventory . --json` to see it in the catalog.
