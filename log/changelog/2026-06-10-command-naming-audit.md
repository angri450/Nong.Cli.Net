# Command naming audit report

## What changed

- Added a standalone Chinese HTML audit for Nong.Toolkit.Net and Nong.Cli.Net command naming.
- Split command-surface findings into three concrete buckets:
  - command duplication;
  - missing commands;
  - commands whose names or descriptions do not fully match their behavior.
- Identified one real overlap group, the Word image extraction surface, and separated it from compatibility aliases such as `word add-*`.
- Listed missing command families that are real future features rather than documentation fixes.
- Listed command names that are usable today but should gain clearer aliases or descriptions.

## Why

The Toolkit/CLI parity report already showed that command coverage is aligned. The next decision is whether the command surface has duplicate commands, lacks important commands, or uses names that mislead users. This report gives that analysis in Chinese so it can drive the next CLI and Toolkit cleanup pass.

## Files touched

- `log/plans/2026-06-10-command-naming-audit.md`
- `log/reports/toolkit-cli-command-naming-audit.html`
- `log/plans/index.md`
- `log/changelog/index.md`

## Tests

- `nong commands --json`
- Targeted review of CLI command declarations and Toolkit SKILL routing.

## Remaining risks

- The report is an audit artifact. It does not yet implement the recommended aliases or missing commands.
- Any future alias work must update System.CommandLine registration, `Cli/Common/Manifest.cs`, CLI contract tests, and Nong.Toolkit.Net docs together.
