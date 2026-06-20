# PPTX Read Reference

Nong.Toolkit.Net routes PPTX inspection through the `nong` CLI only. Use `--json` whenever the result will feed another tool, model decision, or report.

## Extract Slide Text

```powershell
nong pptx read deck.pptx --json
```

Use this for a text-first view of a deck. The command is appropriate when the user asks what the slides say, needs a deck summary, or wants content copied into another workflow.

## Inspect Slide Structure

```powershell
nong pptx slides deck.pptx --json
```

Use this when the task needs slide inventory or structure, such as slide count, titles, notes, and shape-level information returned by the CLI.

## Slice Deck Package

```powershell
nong pptx dissect deck.pptx -o deck.slice --json
```

Use this when the task needs the unified NongPandoc package contract. After slicing, use `nong slice inspect`, `slice blocks`, and `slice block <id>` for package-level evidence.

## Contract

- Do not parse PPTX OOXML directly from Nong.Toolkit.Net.
- Do not call PPTX libraries as an alternate path.
- Treat `status: "error"` as a failed command even if partial text appears in console output.
- If a sample deck is unavailable, report fixture unavailable rather than inventing a successful smoke result.
