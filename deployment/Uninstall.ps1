[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows is required.' }
if (![Environment]::Is64BitProcess) {
    $p = Start-Process "$env:WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File', ('"{0}"' -f $PSCommandPath)) -Wait -PassThru
    exit $p.ExitCode
}
try {
    $admin = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Uninstall requires SYSTEM or administrator.' }
    $target = Join-Path $env:ProgramFiles 'AfterSix\MatrixRainLab'
    $exe = Join-Path $target 'MatrixRainLab.exe'
    $stopScript = Join-Path $target 'Stop-Workload.ps1'
    if (Test-Path $stopScript) { & $stopScript -DataDirectory (Join-Path $env:ProgramData 'AfterSix\MatrixRainLab') }
    $workerPath = Join-Path $target 'MatrixLabWorker.exe'
    $workers = @(Get-Process -Name MatrixLabWorker -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $workerPath })
    foreach ($worker in $workers) { if (!$worker.WaitForExit(5000)) { throw 'Worker has not stopped; uninstall aborted.' } }
    Remove-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name AfterSixMatrixRainLab -ErrorAction SilentlyContinue
    Get-Process -Name MatrixRainLab -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $exe } | ForEach-Object {
        Stop-Process -Id $_.Id -Force
        if (!$_.WaitForExit(5000)) { throw 'The installed display did not exit within five seconds.' }
    }
    $shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Matrix Rain.lnk'
    if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
    foreach ($actor in @(Get-Process -Name MatrixOfficeActor -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq (Join-Path $target 'MatrixOfficeActor.exe') })) {
        if (!$actor.WaitForExit(100000)) { throw 'Office helper is still closing documents; uninstall aborted.' }
    }
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    if (Test-Path 'HKLM:\SOFTWARE\AfterSix\MatrixRainLab') { Remove-Item 'HKLM:\SOFTWARE\AfterSix\MatrixRainLab' -Recurse -Force }
    Write-Output 'Uninstalled. Experiment configuration/state and per-user logs retained.'
    exit 0
} catch { Write-Error -Message $_ -ErrorAction Continue; exit 1 }
