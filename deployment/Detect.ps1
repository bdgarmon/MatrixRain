# Read-only detection: output version and exit 0 only when registry, EXE and config agree.
$ErrorActionPreference = 'Stop'
try {
    $hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $key = $hklm.OpenSubKey('SOFTWARE\AfterSix\MatrixRainLab')
        if ($null -eq $key) { exit 1 }
        try { $version = $key.GetValue('DisplayVersion'); $installPath = $key.GetValue('InstallPath') } finally { $key.Dispose() }
    } finally { $hklm.Dispose() }
    $exe = Join-Path $installPath 'MatrixRainLab.exe'
    if ($version -ne '2.5.0' -or !(Test-Path $exe)) { exit 1 }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne '2.5.0.0') { exit 1 }
    if (!(Test-Path (Join-Path $env:ProgramData 'AfterSix\MatrixRainLab\config.json'))) { exit 1 }
    $worker = Join-Path $installPath 'MatrixLabWorker.exe'
    if (!(Test-Path $worker) -or [Diagnostics.FileVersionInfo]::GetVersionInfo($worker).FileVersion -ne '2.5.0.0') { exit 1 }
    $office = Join-Path $installPath 'MatrixOfficeActor.exe'
    if (!(Test-Path $office) -or [Diagnostics.FileVersionInfo]::GetVersionInfo($office).FileVersion -ne '2.5.0.0') { exit 1 }
    foreach ($name in @('Start-Workload.ps1','Stop-Workload.ps1','Get-WorkloadStatus.ps1')) {
        if (!(Test-Path (Join-Path $installPath $name))) { exit 1 }
    }
    Write-Output $version
    exit 0
} catch { exit 1 }
