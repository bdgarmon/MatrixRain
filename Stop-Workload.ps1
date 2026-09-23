[CmdletBinding()]
param([string]$DataDirectory=(Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'))
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator elevation is required. Use the main-screen controls in Matrix Rain or an elevated PowerShell session.' }
} finally { $identity.Dispose() }
$runtime=Join-Path $DataDirectory 'workloads'
$manifest=Join-Path $runtime 'run.json'
if (!(Test-Path $manifest)) { Write-Output 'No workload has been recorded.'; return }
$probe=$null
try {
    $probe=[IO.File]::Open((Join-Path $runtime 'run.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
} catch [IO.IOException] { }
if ($probe) { $probe.Dispose(); Write-Output 'No live worker holds the lease. A prior RUNNING record may be stale.'; return }
$run=Get-Content $manifest -Raw | ConvertFrom-Json
if ($run.State -ne 'RUNNING') { Write-Output "No active run; last state: $($run.State)"; return }
$id=[guid]::Parse($run.RunId).ToString('N')
[IO.File]::WriteAllText((Join-Path $runtime "stop-$id"),'stop')
$polls = if ($run.Mode -eq 'office') { 1000 } else { 150 }
for ($i=0;$i -lt $polls;$i++) {
    Start-Sleep -Milliseconds 100
    $updated=Get-Content $manifest -Raw | ConvertFrom-Json
    if ($updated.RunId -ne $id -or $updated.State -ne 'RUNNING') { Write-Output "Stop processed for $id; current state: $($updated.State)"; return }
}
throw 'Stop requested, but no acknowledgement within the stop window. Check Get-WorkloadStatus.ps1 and worker logs; the process may have crashed or be blocked in disk I/O.'
