# lockfile and local tool sync plan

## Goal

Finish P9 by regenerating stale lock files after the Nong.Cli.Net 4.0.0 repo rename/alignment work, then replace the broken global `nong` shim with a locally packed 4.0.0 tool.

## Work

1. Audit stale `packages.lock.json` entries that still mention old Nong dependency versions.
2. Run `dotnet restore --use-lock-file --force-evaluate` on the CLI and test project entrypoints.
3. Remove the obsolete `tools/ProgressReport/packages.lock.json` after the tool project was migrated into `nong progress report`.
4. Pack a local `Angri450.Nong.Cli.4.0.0.nupkg`.
5. Replace the broken global `nong` shim that pointed to missing 3.2.4 files.
6. Validate global `nong` with `commands`, `write-official`, `format-gongwen`, and `word validate`.

## Status

Done.

## Verification

- `dotnet restore Cli\NongCli.csproj --use-lock-file --force-evaluate`
- `dotnet restore Cli.Tests\Cli.Tests.csproj --use-lock-file --force-evaluate`
- `dotnet restore Tests\Tests.csproj --use-lock-file --force-evaluate`
- Precise stale-version search over `packages.lock.json` returned no old Nong 3.2.x/3.1.x/0.1.x entries.
- `nong --version`
- `nong commands --json`
- `nong inspect write-official ...`, `nong word format-gongwen ...`, `nong word validate ...`
