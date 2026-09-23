[CmdletBinding()]
param([string]$Config = (Join-Path $PSScriptRoot 'config.json'), [switch]$Windowed, [switch]$Diagnostics)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'The display requires Windows.' }
$exe = Join-Path $PSScriptRoot 'MatrixRainLab.exe'
if (!(Test-Path $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$resolved = (Resolve-Path $Config).Path
$arguments = @('--config', ('"{0}"' -f $resolved))
if ($Windowed) { $arguments += '--windowed' }
if ($Diagnostics) { $arguments += '--diagnostics' }
$p = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "Display exited with code $($p.ExitCode). See %LOCALAPPDATA%\AfterSix\MatrixRainLab\last-error.txt" }
