# Run in NON-ELEVATED Windows PowerShell on the pilot after installation.
[CmdletBinding()]
param([switch]$Portable)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this particular test from a non-elevated PowerShell window.' }
} finally { $identity.Dispose() }
$root=Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab'
if ($Portable) { $root=Split-Path $PSScriptRoot -Parent }
$exe=Join-Path $root 'MatrixLabWorker.exe'
if (!(Test-Path $exe)) { throw "Worker missing: $exe" }
$data=Join-Path $env:TEMP ('MatrixElevationCheck-'+[guid]::NewGuid().ToString('N'))
$info=New-Object Diagnostics.ProcessStartInfo
$info.FileName=$exe
$info.Arguments='--mode baseline --seconds 1 --data "{0}"' -f $data
$info.UseShellExecute=$false
$info.CreateNoWindow=$true
$blocked=$false
try {
    $p=[Diagnostics.Process]::Start($info)
    $p.WaitForExit()
    throw 'Expected Windows to reject non-elevated CreateProcess for this executable.'
} catch [ComponentModel.Win32Exception] {
    if ($_.Exception.NativeErrorCode -eq 740 -or $_.Exception.InnerException.NativeErrorCode -eq 740) { $blocked=$true }
    else { throw }
}
if (!$blocked) { throw 'Elevation boundary not confirmed.' }
Write-Output 'PASS: Windows refused direct non-elevated worker startup (740, elevation required).'
Write-Output 'Now test Go -> cancel UAC, Go -> approve UAC, and Stop in the main screen.'
