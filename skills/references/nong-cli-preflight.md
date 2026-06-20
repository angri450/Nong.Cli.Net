# Nong CLI Preflight

Before running any `nong` command, confirm the CLI is installed and working. Run once per session:

```powershell
nong --version
```

Expected: `nong v4.3.0` or later.

If `nong` is not found:

```powershell
dotnet tool install --global Angri450.Nong.Cli
```

If already installed but stale:

```powershell
dotnet tool update --global Angri450.Nong.Cli
```

## Verify Command Group

After confirming the CLI, verify the needed command group:

```powershell
nong commands --json | nong commands --json  # list all 157 commands
```

Or spot-check the specific group:

```powershell
nong <group> --help
```

## JSON Output Contract

All commands support `--json`. Output structure:

```json
{
  "status": "ok",
  "command": "<group> <action>",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": {},
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 42, "version": "4.3.0" }
}
```

If `status` is not `"ok"`, inspect `errors[]` before proceeding.
