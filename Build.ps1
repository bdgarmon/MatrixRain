[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Build.ps1 requires Windows. Use the included prebuilt EXE on the laptops.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw '.NET Framework 4.x x64 compiler not found.' }
$output = Join-Path $PSScriptRoot 'MatrixRainLab.exe'
$shared = Join-Path $PSScriptRoot 'shared\OfficeModel.cs'
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo "/win32icon:$PSScriptRoot\MatrixRainLab.ico" /target:winexe /platform:x64 /optimize+ /warnaserror+ /r:System.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/win32manifest:$PSScriptRoot\display.manifest" "/out:$output" $sources $shared
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Output "Built $output"

$workerSources = Get-ChildItem (Join-Path $PSScriptRoot 'workloads\src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$workerSources = @($workerSources) + @(Join-Path $PSScriptRoot 'src\PowerDiagnostics.cs')
$workerOutput = Join-Path $PSScriptRoot 'MatrixLabWorker.exe'
& $compiler /nologo "/win32icon:$PSScriptRoot\MatrixRainLab.ico" /target:exe /platform:x64 /optimize+ /warnaserror+ /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "/win32manifest:$PSScriptRoot\worker.manifest" "/out:$workerOutput" $workerSources $shared
if ($LASTEXITCODE -ne 0) { throw "Worker build failed: $LASTEXITCODE" }
Write-Output "Built $workerOutput"

$officeSources = Get-ChildItem (Join-Path $PSScriptRoot 'office\src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll /r:System.Management.dll "/win32icon:$PSScriptRoot\MatrixRainLab.ico" "/win32manifest:$PSScriptRoot\office.manifest" "/out:$PSScriptRoot\MatrixOfficeActor.exe" $officeSources $shared
if ($LASTEXITCODE -ne 0) { throw "Office helper build failed: $LASTEXITCODE" }
