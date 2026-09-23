# Runs on Windows PowerShell 5.1 or PowerShell 7; no test framework needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $root 'src/Core.cs')
$script:passed = 0
function Assert($Condition, [string]$Message) {
    if (!$Condition) { throw "FAIL: $Message" }
    $script:passed++
    Write-Output "PASS: $Message"
}
function Rejects([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert $rejected $Message
}
Assert ([MatrixRainLab.Metrics]::CpuPercent(100,200,100,150,300,200) -eq 75) 'CPU subtracts idle from kernel+user delta'
Assert ([MatrixRainLab.Metrics]::CpuPercent(0,0,0,200,200,0) -eq 0) 'Idle CPU is zero'
Assert ($null -eq [MatrixRainLab.Metrics]::CpuPercent(10,20,30,10,20,30)) 'Zero sampling interval is unavailable'
Assert ($null -eq [MatrixRainLab.Metrics]::CpuPercent(100,200,100,10,20,10)) 'Counter reset is unavailable'
$gpu = New-Object 'System.Collections.Generic.Dictionary[string,double]'
$gpu.Add('pid_1_luid_0x00000000_0x00001234_phys_0_eng_0_engtype_3D',30)
$gpu.Add('pid_2_luid_0x00000000_0x00001234_phys_0_eng_0_engtype_3D',40)
$gpu.Add('pid_2_luid_0x00000000_0x00001234_phys_0_eng_1_engtype_Copy',60)
Assert ([MatrixRainLab.Metrics]::BusiestEngine($gpu) -eq 70) 'GPU sums processes per engine then selects peak, not total across engines'
$gpu.Add('pid_3_luid_0x00000000_0x00001234_phys_0_eng_0_engtype_3D',80)
Assert ([MatrixRainLab.Metrics]::BusiestEngine($gpu) -eq 100) 'GPU percentage is clamped'
$gpu.Clear()
Assert ($null -eq [MatrixRainLab.Metrics]::BusiestEngine($gpu)) 'Missing GPU is unavailable, not zero'
$gpu.Add('pid_1_luid_0x1_phys_0_eng_0_engtype_3D',0)
Assert ([MatrixRainLab.Metrics]::BusiestEngine($gpu) -eq 0) 'Available idle GPU remains a valid zero'
Assert ([MatrixRainLab.Metrics]::Duration(360000) -eq '100:00:00') 'Elapsed hours do not wrap after one day'
Assert ([MatrixRainLab.Metrics]::Duration(-1) -eq '00:00:00') 'Negative elapsed time clamps to zero'
Assert ([MatrixRainLab.Metrics]::Csv('a,"b"') -eq '"a,""b"""') 'CSV escaping handles commas and quotes'
Assert ([MatrixRainLab.Metrics]::ProcessName('worker.compute.exe') -eq 'worker.compute') 'Process suffix handling preserves embedded dots'
Assert ([MatrixRainLab.Metrics]::ProcessName('worker.compute') -eq 'worker.compute') 'Extensionless process name with dot is preserved'
Rejects { [MatrixRainLab.Settings]::ValidateKeys([string[]]@('FramePerSecond')) } 'Config typos are rejected'
$defaults = New-Object MatrixRainLab.Settings
$defaults.Validate()
Assert ($defaults.FramesPerSecond -eq 15 -and $defaults.KeepAwake -and !$defaults.EnableCsv) 'Bounded rendering and awake defaults'
$defaults.FramesPerSecond = 100
Rejects { $defaults.Validate() } 'Out-of-range frame cap is rejected'
$defaults.FramesPerSecond = 15
$defaults.WatchProcess = 'C:\work\test.exe'
Rejects { $defaults.Validate() } 'Process paths are rejected'
$configs = @(Get-Item (Join-Path $root 'config.json'))
foreach ($file in $configs) {
    $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
    $settings = New-Object MatrixRainLab.Settings
    foreach ($property in $json.PSObject.Properties) { $settings.($property.Name) = $property.Value }
    $settings.Validate()
    Assert ($settings.WatchProcess -eq '') "$($file.Name) validates and does not infer a running workload"
}
$state = New-Object MatrixRainLab.WorkloadState
$state.State = 'RUNNING'; $state.ExperimentStartedUtc = '2026-01-01T00:00:00Z'; $state.Validate()
Assert ($state.StartUtc().Kind -eq [DateTimeKind]::Utc) 'State timestamp is UTC'
$state.ExperimentStartedUtc = '2026-01-01T00:00:00'
Rejects { $state.Validate() } 'Ambiguous local timestamp is rejected'
$state.ExperimentStartedUtc = [DateTime]::UtcNow.AddDays(1).ToString('o')
Rejects { $state.Validate() } 'Future experiment start is rejected'
$state.ExperimentStartedUtc = $null; $state.State = 'MADE UP'
Rejects { $state.Validate() } 'Unknown state is rejected'
$heartbeat = New-Object MatrixRainLab.WorkloadState
$heartbeat.State = 'RUNNING'
$heartbeat.HeartbeatUtc = [DateTime]::UtcNow.AddSeconds(-25).ToString('o')
Assert ($heartbeat.IsStale([DateTime]::UtcNow)) 'Missing worker heartbeat is stale after 20 seconds'
$heartbeat.State = 'COMPLETED'
Assert (!$heartbeat.IsStale([DateTime]::UtcNow)) 'Completed run is not falsely marked stale'
$heartbeat.State = 'RUNNING'; $heartbeat.HeartbeatUtc = [DateTime]::UtcNow.ToString('o')
Assert (!$heartbeat.IsStale([DateTime]::UtcNow)) 'Fresh worker heartbeat remains healthy'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('Matrix Lab tests ' + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory | Out-Null
try {
    $path = Join-Path $temp 'state.json'
    & (Join-Path $root 'Set-WorkloadState.ps1') -State RUNNING -WorkloadLabel 'CPU "burst", node 03' -NewExperiment -Path $path | Out-Null
    $first = Get-Content $path -Raw | ConvertFrom-Json
    & (Join-Path $root 'Set-WorkloadState.ps1') -State PAUSED -Path $path | Out-Null
    $second = Get-Content $path -Raw | ConvertFrom-Json
    Assert ($second.ExperimentStartedUtc -eq $first.ExperimentStartedUtc) 'State update preserves experiment origin'
    Assert ($second.WorkloadLabel -eq $first.WorkloadLabel -and $second.State -eq 'PAUSED') 'Atomic update preserves label and updates state'
    & (Join-Path $root 'Set-WorkloadState.ps1') -State RUNNING -NewExperiment -Path $path | Out-Null
    $third = Get-Content $path -Raw | ConvertFrom-Json
    Assert ($third.ExperimentStartedUtc -ne $second.ExperimentStartedUtc) 'New experiment resets timestamp'
    Assert (@(Get-ChildItem $temp).Count -eq 1) 'State writer leaves no temporary files'
    Rejects { & (Join-Path $root 'Set-WorkloadState.ps1') -State RUNNING -WorkloadLabel ('x' * 71) -Path $path } 'Oversized label is rejected'
} finally { Remove-Item $temp -Recurse -Force }
foreach ($script in Get-ChildItem $root -Recurse -Filter '*.ps1') {
    $tokens = $null; $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert ($parseErrors.Count -eq 0) "$($script.Name) parses cleanly"
}
Write-Output "TOTAL: $script:passed checks passed."
