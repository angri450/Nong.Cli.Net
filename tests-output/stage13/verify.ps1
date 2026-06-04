# Stage 13 Verification Script
# Run: pwsh -File tests-output/stage13/verify.ps1
# Requires: .NET 8.0 SDK

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$nong = Join-Path $repo "Cli\bin\Release\net8.0\nong.dll"
$fixtures = Join-Path $env:TEMP "nong-stage13-verify"

$pass = 0
$fail = 0

function Test-Case($name, $cmd, $expectedStatus, $expectedExit, $checkJson) {
    Write-Host -NoNewline "  $name ... "
    try {
        $result = Invoke-Expression "$cmd 2>`$null" -OutVariable stdout
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne $expectedExit) {
            Write-Host "FAIL (exit=$exitCode, expected=$expectedExit)" -ForegroundColor Red
            $script:fail++
            return
        }
        if ($checkJson) {
            $json = $stdout | ConvertFrom-Json
            $ok = & $checkJson $json
            if (-not $ok) {
                Write-Host "FAIL (JSON check)" -ForegroundColor Red
                $script:fail++
                return
            }
        }
        Write-Host "PASS" -ForegroundColor Green
        $script:pass++
    }
    catch {
        Write-Host "FAIL ($_)" -ForegroundColor Red
        $script:fail++
    }
}

# === Build ===
Write-Host "`n=== Build ===" -ForegroundColor Cyan
dotnet build (Join-Path $repo "Cli\NongCli.csproj") -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "  Build: PASS"

# === Fixtures ===
Write-Host "`n=== Creating fixtures ===" -ForegroundColor Cyan
Remove-Item $fixtures -Recurse -Force -ErrorAction SilentlyContinue
$skillDir   = Join-Path $fixtures "valid-skill";      New-Item -ItemType Directory $skillDir -Force | Out-Null
$invalidDir = Join-Path $fixtures "invalid-skill";    New-Item -ItemType Directory $invalidDir -Force | Out-Null
$pluginDir  = Join-Path $fixtures "test-plugin";      New-Item -ItemType Directory $pluginDir -Force | Out-Null
$scanDir    = Join-Path $fixtures "scan-high";         New-Item -ItemType Directory $scanDir -Force | Out-Null
$emptyDir   = Join-Path $fixtures "empty-dir";         New-Item -ItemType Directory $emptyDir -Force | Out-Null

Set-Content -Path (Join-Path $skillDir "SKILL.md") -Value "---`nname: valid-skill`ndescription: test`n---`n# Valid"
Set-Content -Path (Join-Path $invalidDir "SKILL.md") -Value "no frontmatter"
$claudeDir = Join-Path $pluginDir ".claude-plugin"; New-Item -ItemType Directory $claudeDir -Force | Out-Null
Set-Content -Path (Join-Path $claudeDir "plugin.json") -Value '{ "name": "test" }'
Set-Content -Path (Join-Path $claudeDir "marketplace.json") -Value '{ "skills": ["a","b"] }'
Set-Content -Path (Join-Path $pluginDir "skills.sh.json") -Value '[ "a", "b" ]'
$sa = Join-Path $pluginDir "skill-a"; New-Item -ItemType Directory $sa -Force | Out-Null
$sb = Join-Path $pluginDir "skill-b"; New-Item -ItemType Directory $sb -Force | Out-Null
Set-Content -Path (Join-Path $sa "SKILL.md") -Value "---`nname: skill-a`ndescription: a`n---`n# A"
Set-Content -Path (Join-Path $sb "SKILL.md") -Value "---`nname: skill-b`ndescription: b`n---`n# B"
Set-Content -Path (Join-Path $scanDir "readme.md") -Value "contact: secret@example.com"
Write-Host "  Fixtures: PASS"

# === Tests ===
Write-Host "`n=== Validate ===" -ForegroundColor Cyan
Test-Case "valid skill"       "dotnet $nong skill validate $skillDir --json" "ok" 0 { param($j) $j.status -eq "ok" -and $j.data.valid }
Test-Case "invalid skill"     "dotnet $nong skill validate $invalidDir --json" "error" 1 { param($j) $j.errors[0].code -eq "E006" }
Test-Case "empty path"        "dotnet $nong skill validate `"`" --json" "error" 1 { param($j) $j.errors[0].code -eq "E003" }
Test-Case "missing dir"       "dotnet $nong skill validate C:\nonexistent --json" "error" 1 { param($j) $j.errors[0].code -eq "E001" }

Write-Host "`n=== Scan ===" -ForegroundColor Cyan
Test-Case "no High/Critical"  "dotnet $nong skill scan $skillDir --json" "ok" 0 { param($j) $j.data.critical -eq 0 -and $j.data.high -eq 0 }
Test-Case "HIGH finding"      "dotnet $nong skill scan $scanDir --json" "error" 1 { param($j) $j.data.high -gt 0 -and $j.status -eq "error" }

Write-Host "`n=== Inventory ===" -ForegroundColor Cyan
Test-Case "single skill"      "dotnet $nong skill inventory $skillDir --json" "ok" 0 { param($j) $j.data.rootType -eq "skill" -and $j.data.skillCount -eq 1 }
Test-Case "plugin root"       "dotnet $nong skill inventory $pluginDir --json" "ok" 0 { param($j) $j.data.rootType -eq "plugin" -and $j.data.hasPluginManifest -and $j.data.hasMarketplaceManifest -and $j.data.hasSkillsManifest }
Test-Case "empty directory"   "dotnet $nong skill inventory $emptyDir --json" "ok" 0 { param($j) $j.data.rootType -eq "directory" -and $j.data.skillCount -eq 0 }

Write-Host "`n=== Package ===" -ForegroundColor Cyan
$pkg1 = Join-Path $skillDir "valid-skill.zip"; Remove-Item $pkg1 -Force -ErrorAction SilentlyContinue
$pkg2 = Join-Path $pluginDir "test-plugin.zip"; Remove-Item $pkg2 -Force -ErrorAction SilentlyContinue
Test-Case "single skill"      "dotnet $nong skill package $skillDir --json" "ok" 0 { param($j) $j.data.packageType -eq "skill" -and (Test-Path $pkg1) -and (Get-Item $pkg1).Length -gt 0 }
Test-Case "plugin root"       "dotnet $nong skill package $pluginDir --json" "ok" 0 { param($j) $j.data.packageType -eq "plugin" -and (Test-Path $pkg2) -and (Get-Item $pkg2).Length -gt 0 }
Test-Case "invalid dir"       "dotnet $nong skill package $emptyDir --json" "error" 1 { param($j) $j.errors[0].code -eq "E006" }

Write-Host "`n=== Contract ===" -ForegroundColor Cyan
Test-Case "commands --json (24 impl)"  "dotnet $nong commands --json" "ok" 0 { param($j) $j.data.Count -eq 24 }
Test-Case "commands --all (47 total)"  "dotnet $nong commands --all --json" "ok" 0 { param($j) $j.data.Count -eq 47 }
Test-Case "stub -> E009"               "dotnet $nong word extract --json" "error" 1 { param($j) $j.errors[0].code -eq "E009" }

# === Summary ===
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  PASS: $pass" -ForegroundColor Green
Write-Host "  FAIL: $fail" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
Write-Host "========================================" -ForegroundColor Cyan

if ($fail -gt 0) { exit 1 } else { exit 0 }
