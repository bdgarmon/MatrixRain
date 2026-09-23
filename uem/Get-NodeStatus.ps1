# UEM Scripts: SYSTEM context. Read-only JSON status.
$ErrorActionPreference = 'Stop'
& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Get-WorkloadStatus.ps1')
