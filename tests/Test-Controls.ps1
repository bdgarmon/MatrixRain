$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -Path @((Join-Path $root 'src/WorkloadRequest.cs'),(Join-Path $root 'shared/OfficeModel.cs'))
$count=0
function Assert($value,$name) { if (!$value) { throw "FAIL: $name" }; $script:count++; Write-Output "PASS: $name" }
function Reject([scriptblock]$action,$name) { $failed=$false; try { & $action } catch { $failed=$true }; Assert $failed $name }
$r=New-Object MatrixRainLab.WorkloadRequest
foreach ($mode in [MatrixRainLab.WorkloadRequest]::Modes) { $r.Mode=$mode; $r.Validate(); Assert $true "$mode preset validates" }
$r.Mode='cpu & calc'
Reject { $r.Validate() } 'Command-like workload names rejected'
$r.Mode='cpu'; $r.Seconds=3601
Reject { $r.Validate() } 'Unbounded duration rejected'
$r.Seconds=600; $r.Intensity=100
Reject { $r.Validate() } 'Intensity limit enforced'
$r.Intensity=50; $r.DiskMiB=64; $r.WriteLimitMiB=4
Reject { $r.Validate() } 'Write budget below scratch size rejected'
$r.WriteLimitMiB=1024
Assert ([MatrixRainLab.WorkloadRequest]::Quote('C:\Lab Space\') -eq '"C:\Lab Space\\"') 'Trailing slash in quoted path escaped'
Assert ([MatrixRainLab.WorkloadRequest]::Quote('a"b') -eq '"a\"b"') 'Embedded quote escaped'
$id=[guid]::NewGuid()
$argsText=$r.Arguments('C:\Lab Space',$id)
Assert ($argsText.Contains('--data "C:\Lab Space"') -and $argsText.Contains($id.ToString('N'))) 'Arguments preserve data path and command ID'
$r.Threads=33
Reject { $r.Validate() } 'Excessive thread count rejected'
[xml]$display=Get-Content (Join-Path $root 'display.manifest')
[xml]$worker=Get-Content (Join-Path $root 'worker.manifest')
Assert ($display.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level -eq 'asInvoker') 'Display manifest does not request elevation'
Assert ($worker.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level -eq 'requireAdministrator') 'Worker manifest requires administrator'
Write-Output "TOTAL: $count control checks passed."

foreach ($seed in 1..100) {
    $rotation=[MatrixRainLab.TestRotation]::new($seed,'cpu')
    $last='cpu'
    foreach ($cycle in 1..3) {
        $bag=@()
        foreach ($i in 1..5) {
            $next=$rotation.Next()
            if ($next -eq $last) { throw "Repeated adjacent test for seed $seed" }
            $bag+=$next; $last=$next
        }
        if (@($bag | Select-Object -Unique).Count -ne 5 -or $bag -contains 'baseline') { throw 'Rotation lost coverage' }
    }
}
Assert $true '100 seeds: every five draws cover the workloads; baseline is automatic, with no adjacent repeats'
Add-Type -Path (Join-Path $root 'src/DisplayAwake.cs')
$script:flags=[System.Collections.Generic.List[uint32]]::new()
$script:response=[uint32]2147483648
$lease=[MatrixRainLab.DisplayAwake]::new([Func[uint32,uint32]]{ param($flag) $script:flags.Add($flag); return $script:response })
Assert ($lease.Renew()) 'Windows accepted awake request'
Assert ($flags[0] -eq [uint32]2147483651) 'Request includes continuous, display and system flags'
$script:response=0
Assert (!$lease.Renew()) 'Failed awake request is reported honestly'
$script:response=[uint32]2147483648
Assert ($lease.Renew()) 'Request can recover on renewal/resume'
$lease.Dispose(); $lease.Dispose()
Assert ($flags.Count -eq 4 -and $flags[3] -eq [uint32]2147483648) 'Closing releases power request exactly once'
Assert (!$lease.Renew() -and $flags.Count -eq 4) 'Disposed display cannot renew power request'
$r=[MatrixRainLab.WorkloadRequest]::new()
Assert ($r.Arguments('C:\Data',[guid]::NewGuid()).Contains('--baseline-seconds 60')) 'Inline Go requests a default 60-second baseline'
$r.Mode='baseline'
Assert ($r.Arguments('C:\Data',[guid]::NewGuid()).Contains('--baseline-seconds 0')) 'Standalone baseline is not doubled'
$r.Mode='cpu'; $r.Seconds=3600
Reject { $r.Validate() } 'Combined experiment duration is bounded at one hour'
Write-Output "FINAL TOTAL: $count control checks passed."

foreach ($manifest in @('display.manifest','worker.manifest')) {
    Assert ((Get-Content (Join-Path $root $manifest) -Raw).Contains('8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a')) "$manifest declares Windows 10/11 compatibility"
}
Write-Output "RELEASE TOTAL: $count control checks passed."
