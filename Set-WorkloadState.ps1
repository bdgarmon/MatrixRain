[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('IDLE','RUNNING','PAUSED','COMPLETED','FAILED')][string]$State,
    [string]$WorkloadLabel,
    [switch]$NewExperiment,
    [string]$Path = (Join-Path $PSScriptRoot 'state.json')
)
$ErrorActionPreference = 'Stop'
$Path = [IO.Path]::GetFullPath($Path)
$start = [DateTime]::UtcNow.ToString('o')
$label = $WorkloadLabel
if ((Test-Path $Path) -and !$NewExperiment) {
    $previous = Get-Content $Path -Raw | ConvertFrom-Json
    if ($previous.ExperimentStartedUtc) { $start = $previous.ExperimentStartedUtc }
    if (!$PSBoundParameters.ContainsKey('WorkloadLabel')) { $label = $previous.WorkloadLabel }
}
if ($label -and ($label.Length -gt 70 -or $label -match '[\x00-\x1f]')) { throw 'WorkloadLabel must be at most 70 characters without control characters.' }
$value = [ordered]@{ State=$State.ToUpperInvariant(); ExperimentStartedUtc=$start; WorkloadLabel=$label }
$directory = Split-Path $Path -Parent
if (!(Test-Path $directory)) { New-Item $directory -ItemType Directory -Force | Out-Null }
$temp = Join-Path $directory ([IO.Path]::GetRandomFileName())
try {
    [IO.File]::WriteAllText($temp, ($value | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    if (Test-Path $Path) { [IO.File]::Replace($temp, $Path, [NullString]::Value) } else { [IO.File]::Move($temp, $Path) }
} finally { if (Test-Path $temp) { Remove-Item $temp -Force } }
Write-Output "Reported $State in $Path"
