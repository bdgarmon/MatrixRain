# UEM Scripts: SYSTEM context. Stops the current run only.
$ErrorActionPreference = 'Stop'
& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Stop-Workload.ps1')
