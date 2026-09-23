[CmdletBinding(DefaultParameterSetName='Mode')]
param(
    [Parameter(Mandatory=$true,ParameterSetName='Mode')]
    [ValidateSet('Baseline','Visualization','Cpu','Memory','Disk','Science')][string]$Mode,
    [Parameter(Mandatory=$true,ParameterSetName='Node')][ValidatePattern('^0[1-6]$')][string]$NodeId,
    [ValidateRange(1,3600)][int]$DurationSeconds=600,
    [ValidateRange(0,600)][int]$BaselineSeconds=0,
    [ValidateRange(1,95)][int]$Intensity=50,
    [ValidateRange(1,32)][int]$Threads=2,
    [ValidateRange(8,4096)][int]$MemoryMiB=512,
    [ValidateRange(4,256)][int]$DiskMiB=64,
    [ValidateRange(4,4096)][int]$WriteLimitMiB=1024,
    [ValidateRange(1,256)][int]$DiskMiBPerSecond=10,
    [string]$DataDirectory=(Join-Path $env:ProgramData 'AfterSix\MatrixRainLab'),
    [switch]$Wait
)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator elevation is required. Use the main-screen controls in Matrix Rain or an elevated PowerShell session.' }
} finally { $identity.Dispose() }
if ($env:OS -ne 'Windows_NT') { throw 'This launcher requires Windows.' }
if ($NodeId) { $Mode = @('Baseline','Visualization','Cpu','Memory','Disk','Science')[[int]$NodeId-1] }
if ($Mode -eq 'Baseline') { $BaselineSeconds=0 }
if ($DurationSeconds + $BaselineSeconds -gt 3600) { throw 'Baseline plus workload duration must be at most 3600 seconds.' }
if ($WriteLimitMiB -lt $DiskMiB) { throw 'WriteLimitMiB must be at least DiskMiB.' }
$DataDirectory=[IO.Path]::GetFullPath($DataDirectory)
if ($DataDirectory.Contains('"')) { throw 'Invalid data path.' }
$runtime=Join-Path $DataDirectory 'workloads'
New-Item $runtime -ItemType Directory -Force | Out-Null
$exe=Join-Path $PSScriptRoot 'MatrixLabWorker.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Worker not installed: $exe" }
# Prevent parallel launchers while establishing the worker lease and acknowledgement.
$launchLease=[IO.File]::Open((Join-Path $runtime 'launch.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $probe=$null
    try { $probe=[IO.File]::Open((Join-Path $runtime 'run.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
    catch { throw 'A workload is already running. Use Stop-Workload.ps1 first.' }
    finally { if ($probe) { $probe.Dispose() } }
    # Bound stdout/stderr retention; result records are separately bounded by the worker.
    foreach ($pattern in @('worker-*.out.txt','worker-*.err.txt')) {
        Get-ChildItem $runtime -Filter $pattern | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip 19 | Remove-Item -Force
    }
    $runId=[guid]::NewGuid().ToString('N')
    $stdout=Join-Path $runtime "worker-$runId.out.txt"
    $stderr=Join-Path $runtime "worker-$runId.err.txt"
    $arguments='--mode {0} --seconds {1} --intensity {2} --threads {3} --memory-mib {4} --disk-mib {5} --write-limit-mib {6} --data "{7}" --run-id {8} --disk-rate-mib {9} --baseline-seconds {10}' -f $Mode.ToLowerInvariant(),$DurationSeconds,$Intensity,$Threads,$MemoryMiB,$DiskMiB,$WriteLimitMiB,$DataDirectory.TrimEnd('\'),$runId,$DiskMiBPerSecond,$BaselineSeconds
    $process=Start-Process $exe -ArgumentList $arguments -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
    $acknowledged=$false
    for ($i=0; $i -lt 100; $i++) {
        $manifest=Join-Path $runtime 'run.json'
        if (Test-Path $manifest) {
            try { $run=Get-Content $manifest -Raw | ConvertFrom-Json; if ($run.RunId -eq $runId) { $acknowledged=$true; break } } catch { }
        }
        if ($process.HasExited) { break }
        Start-Sleep -Milliseconds 100
    }
    if (!$acknowledged) {
        # A delayed worker will see this request as soon as it starts; no orphan run on acknowledgement timeout.
        [IO.File]::WriteAllText((Join-Path $runtime "stop-$runId"),'stop')
        $errorText=if (Test-Path $stderr) { Get-Content $stderr -Raw } else { '' }
        throw "Worker did not acknowledge startup. $errorText See $stderr"
    }
    if ($run.State -eq 'FAILED') { throw "Workload failed: $($run.Detail). See $stderr" }
    Write-Output "Started $Mode; run ID $runId; maximum $($DurationSeconds + $BaselineSeconds) seconds including baseline."
    Write-Output "Status/results: $runtime"
} finally { $launchLease.Dispose() }
if ($Wait) {
    $process.WaitForExit()
    Get-Content $stdout
    if ($process.ExitCode -ne 0) { throw "Workload failed. $(Get-Content $stderr -Raw)" }
}
