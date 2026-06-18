param([Parameter(Mandatory=$true)] [string]$NupkgPath)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $NupkgPath)) { Write-Warning "nupkg not found: $NupkgPath"; exit 0 }

Add-Type -AssemblyName System.IO.Compression.FileSystem

$rid = & {
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    $a = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    if ($a -eq 'X86') { 'win-x86' } elseif ($a -eq 'Arm64') { 'win-arm64' } else { 'win-x64' }
  } elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
    $a = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    if ($a -eq 'Arm64') { 'linux-arm64' } else { 'linux-x64' }
  } elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
    $a = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    if ($a -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' }
  } else { 'win-x64' }
}

$tmp = $NupkgPath + '.tmp'
$src = [System.IO.Compression.ZipFile]::OpenRead($NupkgPath)
$dst = [System.IO.Compression.ZipFile]::Open($tmp, 'Create')
$kept = 0; $removed = 0

foreach ($e in $src.Entries) {
    $keep = $true
    if ($e.FullName -like '*onnxruntime*' -and $e.FullName -like '*runtimes/*/native/*' -and $e.FullName -notlike "*runtimes/$rid/native/*") {
        $keep = $false
    }
    if ($keep) {
        $ne = $dst.CreateEntry($e.FullName)
        $es = $e.Open(); $nd = $ne.Open()
        $es.CopyTo($nd); $nd.Close(); $es.Close()
        $kept++
    } else { $removed++ }
}
$src.Dispose(); $dst.Dispose()
Move-Item $tmp $NupkgPath -Force
Write-Host "[trim] platform=$rid kept=$kept removed=$removed foreign onnxruntime files"
