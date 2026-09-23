# UEM Scripts: SYSTEM context; execution timeout at least 12 minutes.
$ErrorActionPreference = 'Stop'
$script = Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Start-Workload.ps1'
& $script -NodeId '05' -DurationSeconds 600 -Wait
