# Run after UEM installation, in Windows PowerShell 5+ as the normal desktop user.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows is required.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this check without elevation, as the desktop user.' }
$pf = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
$app = Join-Path $pf 'AfterSix\MatrixRainLab'
$data = Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'
foreach ($name in @('MatrixRainLab.exe','MatrixLabWorker.exe')) {
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app $name)).FileVersion -ne '2.6.0.0') { throw "Wrong version: $name" }
}
Get-Content -LiteralPath (Join-Path $data 'config.json') -Raw | ConvertFrom-Json | Out-Null
$runtime = Join-Path $data 'workloads'
Get-ChildItem -LiteralPath $runtime -Force | Out-Null
foreach ($name in @('run.json','last-result.json')) {
    $file = Join-Path $runtime $name
    if (Test-Path -LiteralPath $file) { Get-Content -LiteralPath $file -Raw | ConvertFrom-Json | Out-Null }
}
$probe = Join-Path $runtime ('permission-probe-' + [guid]::NewGuid().ToString('N'))
$denied = $false
try { [IO.File]::WriteAllText($probe, 'probe') } catch [UnauthorizedAccessException] { $denied = $true }
if (!$denied) {
    Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
    throw 'Standard user can write worker control files. Repair using the 2.6.0 installer.'
}
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Matrix Rain.lnk'))
$exe = Join-Path $app 'MatrixRainLab.exe'
if ($link.TargetPath -ine $exe -or $link.IconLocation -ine "$exe,0") { throw 'Start menu shortcut target or icon is incorrect.' }
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Icon]::ExtractAssociatedIcon($exe)
if ($null -eq $icon) { throw 'Cannot extract app icon.' }
$icon.Dispose()
Write-Output 'PASS: 2.6.0 binaries, standard-user status read access, protected worker control files and Start menu icon.'
Write-Output 'Next: open the display, confirm v2.6.0, cancel Go once, then approve Go with a different admin account and verify baseline -> workload -> completion. Export logs as the desktop user.'
