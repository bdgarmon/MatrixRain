[CmdletBinding()]
param([switch]$Installed)
$ErrorActionPreference='Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Requires Windows.' }
if (Get-Process -Name MatrixLabWorker -ErrorAction SilentlyContinue) { throw 'Stop the current workload before running smoke tests.' }
$root=Split-Path $PSScriptRoot -Parent
if ($Installed) { $root=Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab' }
$temp=Join-Path ([IO.Path]::GetTempPath()) ('MatrixWorkerSmoke-'+[guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory | Out-Null
try {
    foreach ($mode in @('Cpu','Memory','Science','Disk','Baseline','Visualization')) {
        & (Join-Path $root 'Start-Workload.ps1') -Mode $mode -DurationSeconds 2 -Threads 1 -Intensity 10 -MemoryMiB 8 -DiskMiB 4 -WriteLimitMiB 4 -DataDirectory $temp -Wait
        $result=Get-Content (Join-Path $temp 'workloads\run.json') -Raw | ConvertFrom-Json
        if ($result.State -ne 'COMPLETED') { throw "$mode did not complete: $($result.Detail)" }
        Write-Output "PASS: $mode / Windows worker lifecycle"
    }
    & (Join-Path $root 'Start-Workload.ps1') -Mode Cpu -DurationSeconds 30 -Threads 1 -Intensity 10 -DataDirectory $temp
    $rejected=$false
    try { & (Join-Path $root 'Start-Workload.ps1') -Mode Cpu -DurationSeconds 1 -DataDirectory $temp } catch { $rejected=$true }
    if (!$rejected) { throw 'Duplicate launcher was not rejected.' }
    & (Join-Path $root 'Stop-Workload.ps1') -DataDirectory $temp
    $result=Get-Content (Join-Path $temp 'workloads\run.json') -Raw | ConvertFrom-Json
    if ($result.State -ne 'IDLE') { throw 'Cancellation not acknowledged.' }
    Write-Output 'PASS: duplicate rejection and stop command'
    Get-ChildItem (Join-Path $temp 'workloads') -Filter 'result-*.json' | ForEach-Object { Get-Content $_.FullName }
} finally {
    # On a failure, retain evidence; do not remove files while a worker may still be using them.
    Write-Output "Windows smoke-test evidence: $temp"
}
