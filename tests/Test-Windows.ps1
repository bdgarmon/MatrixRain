# Run this on one Latitude before expanding the UEM assignment.
[CmdletBinding()]
param([switch]$Installed)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($env:OS -ne 'Windows_NT') { throw 'This smoke test requires Windows.' }
$exe = Join-Path $root 'MatrixRainLab.exe'
$config = Join-Path $root 'config.json'
if ($Installed) {
    $exe = Join-Path $env:ProgramFiles 'AfterSix\MatrixRainLab\MatrixRainLab.exe'
    $config = Join-Path $env:ProgramData 'AfterSix\MatrixRainLab\config.json'
}
$p = Start-Process $exe -ArgumentList @('--diagnostics', ('--config "{0}"' -f $config)) -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "Windows telemetry failed with code $($p.ExitCode). Check %LOCALAPPDATA%\AfterSix\MatrixRainLab\last-error.txt" }
$report = Join-Path $env:LOCALAPPDATA 'AfterSix\MatrixRainLab\diagnostics.json'
Get-Content $report -Raw
Write-Output 'PASS: Windows CPU and physical RAM sampled. GPU null means counters are unavailable; check the driver/counter access.'
Write-Output 'Next: launch the display, verify fullscreen/Esc/F11/Space and compare live readings with Task Manager.'
