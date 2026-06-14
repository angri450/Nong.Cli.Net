# Final blueprint

## What changed

- Added a standalone Chinese blueprint HTML that explains Nong's intended end-state around `一刀三流`, unified slice contracts, controlled reassembly, and loss boundaries.
- Reframed the final three-layer architecture so:
  - `Nong.NanoBot.Net` orchestrates tasks,
  - `Nong.Toolkit.Net` teaches AI how to route and judge evidence,
  - `Nong.Cli.Net` performs the actual cut/read/reassemble work.
- Defined Nong's core philosophy for complex objects:
  - cut first;
  - split into content, structure, and format/assets streams;
  - let AI read the package, not the raw file;
  - reassemble with controlled loss instead of flattening everything to plain text.
- Added the breeding-style product criterion that rebuilt objects must keep their key traits consistent and must not suffer `性状分离` after reconstruction.
- Reworked the naming guidance so command words line up with the cut/read/reassemble model instead of being only a flat command catalog.
- Kept the clearer-alias recommendations, but placed them under the broader document-object blueprint.

## Why

The earlier audit answered whether commands were duplicated, missing, or poorly named. What was still missing was Nong's real center: a plain-language blueprint that treats complex documents and similar objects as things to dissect into AI-readable streams and rebuild with controlled loss.

## Files touched

- `log/plans/2026-06-10-final-blueprint.md`
- `log/reports/nong-final-blueprint.html`
- `log/plans/index.md`
- `log/changelog/index.md`

## Tests

- `nong commands --json`
- `nong progress report --project-root . --json`

## Remaining risks

- This blueprint does not yet implement the recommended aliases or missing commands.
- If the command surface changes later, the slice-contract explanations, naming dictionary, and Toolkit teaching docs must stay in sync.
