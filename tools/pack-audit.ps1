param(
    [string[]] $Path = @("nupkg"),
    [int] $Top = 10,
    [switch] $Json,
    [switch] $FailOnWarning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-NupkgFiles {
    param([string[]] $InputPaths)

    $files = New-Object System.Collections.Generic.List[string]
    foreach ($item in $InputPaths) {
        if ([string]::IsNullOrWhiteSpace($item)) {
            continue
        }

        $resolved = Resolve-Path -LiteralPath $item -ErrorAction SilentlyContinue
        if ($null -eq $resolved) {
            throw "Path not found: $item"
        }

        foreach ($pathInfo in $resolved) {
            $full = $pathInfo.ProviderPath
            if (Test-Path -LiteralPath $full -PathType Container) {
                Get-ChildItem -LiteralPath $full -Filter *.nupkg -File | ForEach-Object { $files.Add($_.FullName) }
            }
            elseif ($full.EndsWith(".nupkg", [StringComparison]::OrdinalIgnoreCase)) {
                $files.Add($full)
            }
        }
    }

    return $files | Sort-Object -Unique
}

function Get-PackageMetadata {
    param([System.IO.Compression.ZipArchive] $Zip, [string] $FallbackName)

    $nuspec = $Zip.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($null -eq $nuspec) {
        return [ordered]@{
            Id = [System.IO.Path]::GetFileNameWithoutExtension($FallbackName)
            Version = ""
        }
    }

    $reader = [System.IO.StreamReader]::new($nuspec.Open())
    try {
        [xml] $xml = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    return [ordered]@{
        Id = [string] $xml.package.metadata.id
        Version = [string] $xml.package.metadata.version
    }
}

function Get-Threshold {
    param([string] $PackageId)

    switch ($PackageId) {
        "Angri450.Nong.Cli" {
            return [ordered]@{ Category = "cli"; WarnMb = 15; FailMb = 15; FailOnWarn = $true }
        }
        { $_ -in @("Angri450.Nong.Tool.Chart", "Angri450.Nong.Tool.Diagram", "Angri450.Nong.Tool.Imaging", "Angri450.Nong.Tool.Pdf") } {
            return [ordered]@{ Category = "native-tool"; WarnMb = 50; FailMb = 100; FailOnWarn = $false }
        }
        { $_ -in @("Angri450.Nong.Tool.Pptx", "Angri450.Nong.Tool.Ocr") } {
            return [ordered]@{ Category = "light-tool"; WarnMb = 20; FailMb = 100; FailOnWarn = $false }
        }
        default {
            return [ordered]@{ Category = "package"; WarnMb = 50; FailMb = 100; FailOnWarn = $false }
        }
    }
}

function Format-SizeMb {
    param([long] $Bytes)
    return [math]::Round($Bytes / 1MB, 2)
}

$nupkgs = @(Get-NupkgFiles -InputPaths $Path)
if ($nupkgs.Count -eq 0) {
    throw "No .nupkg files found under: $($Path -join ', ')"
}

$results = New-Object System.Collections.Generic.List[object]
$hasFailure = $false
$hasWarning = $false

foreach ($file in $nupkgs) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($file)
    try {
        $metadata = Get-PackageMetadata -Zip $zip -FallbackName $file
        $threshold = Get-Threshold -PackageId $metadata.Id
        $packageSize = (Get-Item -LiteralPath $file).Length
        $sizeMb = Format-SizeMb $packageSize

        $topEntries = @(
            $zip.Entries |
                Where-Object { $_.Length -gt 0 } |
                Sort-Object Length -Descending |
                Select-Object -First $Top |
                ForEach-Object {
                    [ordered]@{
                        path = $_.FullName
                        sizeMb = Format-SizeMb $_.Length
                    }
                }
        )

        $status = "ok"
        if ($sizeMb -gt [double] $threshold.FailMb) {
            $status = "fail"
            $hasFailure = $true
        }
        elseif ($sizeMb -gt [double] $threshold.WarnMb) {
            $status = "warning"
            $hasWarning = $true
            if ($threshold.FailOnWarn -or $FailOnWarning) {
                $hasFailure = $true
            }
        }

        $results.Add([ordered]@{
            packageId = $metadata.Id
            version = $metadata.Version
            file = $file
            sizeMb = $sizeMb
            category = $threshold.Category
            warnMb = $threshold.WarnMb
            failMb = $threshold.FailMb
            status = $status
            topEntries = $topEntries
        })
    }
    finally {
        $zip.Dispose()
    }
}

if ($Json) {
    [ordered]@{
        status = if ($hasFailure) { "fail" } elseif ($hasWarning) { "warning" } else { "ok" }
        packageCount = $results.Count
        results = $results
    } | ConvertTo-Json -Depth 8
}
else {
    foreach ($result in $results) {
        $line = "{0,-34} {1,8:N2} MB  {2,-11} warn>{3} fail>{4}  {5}" -f `
            $result.packageId, $result.sizeMb, $result.status, $result.warnMb, $result.failMb, $result.file
        Write-Output $line
        foreach ($entry in $result.topEntries) {
            Write-Output ("  {0,8:N2} MB  {1}" -f $entry.sizeMb, $entry.path)
        }
    }
}

if ($hasFailure) {
    exit 1
}
