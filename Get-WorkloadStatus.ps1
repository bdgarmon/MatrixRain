[CmdletBinding()]
param([string]$DataDirectory=(Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'))
$ErrorActionPreference='Stop'
$path=Join-Path $DataDirectory 'workloads\run.json'
if (!(Test-Path $path)) { Write-Output 'No workload has been recorded.'; return }
$run=Get-Content $path -Raw | ConvertFrom-Json
$age=([DateTime]::UtcNow-([DateTime]$run.UpdatedUtc).ToUniversalTime()).TotalSeconds
$run | Add-Member -NotePropertyName HeartbeatAgeSeconds -NotePropertyValue ([math]::Round($age,1))
$run | Add-Member -NotePropertyName Stale -NotePropertyValue ($run.State -eq 'RUNNING' -and $age -gt 20)
$run | ConvertTo-Json -Depth 8
