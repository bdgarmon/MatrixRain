# Run as SYSTEM or administrator. No UI, no download, no reboot.
[CmdletBinding()]
param([ValidatePattern('^0[1-6]$')][string]$NodeId, [switch]$ReplaceConfig, [switch]$NoAutoStart)
$ErrorActionPreference = 'Stop'
# UEM can invoke 32-bit PowerShell; keep paths and registry in the 64-bit view.
if ($env:OS -ne 'Windows_NT') { throw 'Windows is required.' }
if (![Environment]::Is64BitProcess) {
    $params = @('-NoProfile','-ExecutionPolicy','Bypass','-File', ('"{0}"' -f $PSCommandPath))
    if ($NodeId) { $params += @('-NodeId', $NodeId) }
    if ($ReplaceConfig) { $params += '-ReplaceConfig' }
    if ($NoAutoStart) { $params += '-NoAutoStart' }
    $p = Start-Process "$env:WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $params -Wait -PassThru
    exit $p.ExitCode
}
. (Join-Path $PSScriptRoot 'Set-DataPermissions.ps1')
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $env:ProgramFiles 'AfterSix\MatrixRainLab'
$data = Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'
$key = 'HKLM:\SOFTWARE\AfterSix\MatrixRainLab'
$runKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
$stage = "$target.stage-$([guid]::NewGuid().ToString('N'))"
$backup = "$target.backup-$([guid]::NewGuid().ToString('N'))"
$swapped = $false
$transcribing = $false
try {
    $logDirectory = Join-Path $data 'deployment-logs'
    New-Item $logDirectory -ItemType Directory -Force | Out-Null
    Get-ChildItem $logDirectory -Filter 'install-*.txt' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip 4 | Remove-Item -Force
    Start-Transcript -Path (Join-Path $logDirectory ('install-' + [guid]::NewGuid().ToString('N') + '.txt')) -Force | Out-Null
    $transcribing = $true
    $admin = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Install requires SYSTEM or administrator.' }
    # No hostname lookup. The display selects tests locally at runtime.
    $sourceConfig = Join-Path $root 'config.json'
    if ($NodeId) { Write-Warning 'NodeId is deprecated and ignored; select the test in the display.' }
    New-Item $stage -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $root 'MatrixRainLab.exe') $stage
    Copy-Item (Join-Path $root 'MatrixRainLab.exe.config') $stage
    Copy-Item (Join-Path $root 'MatrixRainLab.ico') $stage
    foreach ($name in @('MatrixLabWorker.exe','MatrixLabWorker.exe.config','MatrixOfficeActor.exe','MatrixOfficeActor.exe.config','Start-Workload.ps1','Stop-Workload.ps1','Get-WorkloadStatus.ps1','WORKLOADS.md','OFFICE-WORKDAY.md')) { Copy-Item (Join-Path $root $name) $stage }
    Copy-Item (Join-Path $root 'Set-WorkloadState.ps1') $stage
    Copy-Item (Join-Path $root 'README.md') $stage
    Copy-Item (Join-Path $PSScriptRoot 'Uninstall.ps1') $stage
    Copy-Item (Join-Path $PSScriptRoot 'Detect.ps1') $stage
    $configPath = Join-Path $data 'config.json'
    $writeConfig = !(Test-Path $configPath) -or $ReplaceConfig
    $checkConfig = if ($writeConfig) { $sourceConfig } else { $configPath }
    $p = Start-Process (Join-Path $stage 'MatrixRainLab.exe') -ArgumentList @('--validate', ('--config "{0}"' -f $checkConfig)) -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "Config validation failed ($($p.ExitCode))." }
    $exe = Join-Path $target 'MatrixRainLab.exe'
    $stopScript = Join-Path $target 'Stop-Workload.ps1'
    if (Test-Path $stopScript) { & $stopScript -DataDirectory $data }
    $workerPath = Join-Path $target 'MatrixLabWorker.exe'
    $workers = @(Get-Process -Name MatrixLabWorker -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $workerPath })
    foreach ($worker in $workers) { if (!$worker.WaitForExit(5000)) { throw 'Worker has not stopped; upgrade aborted.' } }
    Get-Process -Name MatrixRainLab -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $exe } | ForEach-Object {
        Stop-Process -Id $_.Id -Force
        if (!$_.WaitForExit(5000)) { throw 'The installed display did not exit within five seconds.' }
    }
    foreach ($actor in @(Get-Process -Name MatrixOfficeActor -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq (Join-Path $target 'MatrixOfficeActor.exe') })) {
        if (!$actor.WaitForExit(100000)) { throw 'Office helper is still closing documents; upgrade aborted.' }
    }
    if (Test-Path $target) { Move-Item $target $backup }
    Move-Item $stage $target
    $swapped = $true
    New-Item $data -ItemType Directory -Force | Out-Null
    if ($writeConfig) { Copy-Item $sourceConfig $configPath -Force }
    $sitesPath = Join-Path $data 'browser-sites.json'
    if (!(Test-Path -LiteralPath $sitesPath) -or $ReplaceConfig) { Copy-Item (Join-Path $root 'browser-sites.json') $sitesPath -Force }
    New-Item (Join-Path $data 'workloads') -ItemType Directory -Force | Out-Null
    Set-MatrixDataPermissions -Path $data
    if ($NoAutoStart) {
        Remove-ItemProperty $runKey -Name AfterSixMatrixRainLab -ErrorAction SilentlyContinue
    } else {
        New-ItemProperty $runKey -Name AfterSixMatrixRainLab -Value ('"{0}" --config "{1}"' -f $exe, $configPath) -PropertyType String -Force | Out-Null
    }
    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Matrix Rain.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.Arguments = '--config "{0}"' -f $configPath
    $shortcut.WorkingDirectory = $target
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = 'Matrix Rain 2.5.0'
    $shortcut.Save()
    # Migrate only shortcuts that actually target this installation.
    foreach ($folder in @([Environment]::GetFolderPath('CommonPrograms'),[Environment]::GetFolderPath('CommonDesktopDirectory'))) {
        $oldLink = Join-Path $folder 'Matrix Rain Lab.lnk'
        if (Test-Path -LiteralPath $oldLink) {
            $oldShortcut = $shell.CreateShortcut($oldLink)
            if ($oldShortcut.TargetPath -ieq $exe) {
                $newLink = Join-Path $folder 'Matrix Rain.lnk'
                $newShortcut = $shell.CreateShortcut($newLink)
                $newShortcut.TargetPath = $exe
                $newShortcut.Arguments = '--config "{0}"' -f $configPath
                $newShortcut.WorkingDirectory = $target
                $newShortcut.IconLocation = "$exe,0"
                $newShortcut.Description = 'Matrix Rain 2.5.0'
                $newShortcut.Save()
                Remove-Item -LiteralPath $oldLink -Force
            }
        }
    }
    # Marker is written only after files, config, shortcut and startup settings succeed.
    New-Item $key -Force | Out-Null
    New-ItemProperty $key -Name InstallPath -Value $target -PropertyType String -Force | Out-Null
    New-ItemProperty $key -Name DisplayVersion -Value '2.5.0' -PropertyType String -Force | Out-Null
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    Write-Output 'Matrix Rain 2.5.0 installed. Starts at next user sign-in, or from the Start menu.'
    exit 0
} catch {
    Write-Error -Message $_ -ErrorAction Continue
    # Never advertise a partially configured installation as successful.
    Remove-Item $key -Recurse -Force -ErrorAction SilentlyContinue
    if ($swapped -and (Test-Path $backup)) {
        Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
        Move-Item $backup $target -ErrorAction SilentlyContinue
    } elseif (!(Test-Path $target) -and (Test-Path $backup)) {
        Move-Item $backup $target -ErrorAction SilentlyContinue
    }
    exit 1
} finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
    if ($transcribing) { Stop-Transcript | Out-Null }
}
