# Lockfile and local tool sync

## What changed

- Regenerated stale lock files with `dotnet restore --use-lock-file --force-evaluate`.
- Removed the obsolete `tools/ProgressReport/packages.lock.json` after the progress-report project was migrated into the main CLI.
- Packed `Angri450.Nong.Cli.4.0.0.nupkg` to `..\Nong.Cli_archive\local-tools`.
- Backed up the broken global `nong.exe` shim that pointed to a missing 3.2.4 tool store path.
- Installed the local 4.0.0 tool globally and verified it exposes 96 commands.

## Why

The lock files still carried old Nong dependency ranges even though the project graph had moved to 4.0.0 project references. The global `nong` command was also broken because its shim referenced a deleted 3.2.4 tool install.

## Files touched

- `Cli/packages.lock.json`
- `Cli.Tests/packages.lock.json`
- `Inspect/packages.lock.json`
- `MultiModal/packages.lock.json`
- `Tests/packages.lock.json`
- `tools/ProgressReport/packages.lock.json`

## Tests

- `dotnet restore Cli\NongCli.csproj --use-lock-file --force-evaluate`
- `dotnet restore Cli.Tests\Cli.Tests.csproj --use-lock-file --force-evaluate`
- `dotnet restore Tests\Tests.csproj --use-lock-file --force-evaluate`
- Precise stale-version scan over all `packages.lock.json`
- `nong --version`
- `nong commands --json`
- Global `nong inspect write-official` -> `nong word format-gongwen` -> `nong word validate`

## Remaining risks

- The local global tool is installed from `..\Nong.Cli_archive\local-tools\Angri450.Nong.Cli.4.0.0.nupkg`, not NuGet.org. Publish a new public package before relying on fresh machines to have these commands.
