# command naming audit plan

## Goal

Write a second Chinese HTML audit for Toolkit/CLI alignment that focuses on command naming, semantic clarity, workflow boundaries, and whether each command group is functionally sufficient for current Nong.Toolkit.Net routing.

## Work

1. Use `nong commands --json` as the current command surface baseline.
2. Compare command names and descriptions against Toolkit SKILL routing.
3. Classify findings into three buckets:
   - names that are clear enough and should stay stable;
   - names that are usable but need stronger docs or aliases;
   - real feature gaps that should not be solved by wording alone.
4. Produce a standalone Chinese report under `log/reports/`.
5. Add a changelog entry.

## Status

Done.

## Verification

- `nong commands --json`
- Targeted reads of `Cli/Commands/*`, `Cli/Common/Manifest.cs`, and Nong.Toolkit.Net SKILL files.
