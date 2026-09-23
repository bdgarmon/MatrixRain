#Requires -Version 5.0
<#
Workspace ONE UEM application detection for Matrix Rain 2.5.0.
Success exit code: 0. Not detected / check error: 1.
Read-only: no installation, process launch, elevation, or configuration changes.
Checks the seven file criteria from the 2.5.0 deployment guide. No additional
registry-marker requirement. Works in 32-bit or 64-bit Windows PowerShell.
#>
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$expectedVersion = '2.5.0.0'

try {
    if ($env:OS -ne 'Windows_NT') {
        throw 'Windows is required.'
    }

    # ProgramFiles can point to Program Files (x86) in a 32-bit UEM host.
    # ProgramW6432 identifies the native 64-bit application directory.
    $nativeProgramFiles = $env:ProgramW6432
    if ([string]::IsNullOrWhiteSpace($nativeProgramFiles)) {
        $nativeProgramFiles = $env:ProgramFiles
    }
    if ([string]::IsNullOrWhiteSpace($nativeProgramFiles) -or
        [string]::IsNullOrWhiteSpace($env:ProgramData)) {
        throw 'Program Files or ProgramData location is unavailable.'
    }

    $installDirectory = Join-Path $nativeProgramFiles 'AfterSix\MatrixRainLab'
    $dataDirectory = Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'
    $failures = @()

    foreach ($name in @('MatrixRainLab.exe', 'MatrixLabWorker.exe', 'MatrixOfficeActor.exe')) {
        $path = Join-Path $installDirectory $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            $failures += "Missing file: $path"
            continue
        }
        $actualVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion
        if ($actualVersion -ne $expectedVersion) {
            $failures += "Version mismatch: $path (expected $expectedVersion; found '$actualVersion')"
        }
    }

    $requiredFiles = @(
        (Join-Path $dataDirectory 'config.json')
        (Join-Path $installDirectory 'Start-Workload.ps1')
        (Join-Path $installDirectory 'Stop-Workload.ps1')
        (Join-Path $installDirectory 'Get-WorkloadStatus.ps1')
    )
    foreach ($path in $requiredFiles) {
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            $failures += "Missing file: $path"
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Output "NOT DETECTED: $_" }
        exit 1
    }

    Write-Output 'DETECTED: Matrix Rain 2.5.0; all seven file criteria passed.'
    exit 0
} catch {
    Write-Output "NOT DETECTED: $($_.Exception.Message)"
    exit 1
}
