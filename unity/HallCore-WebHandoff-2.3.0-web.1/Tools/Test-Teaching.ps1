param([string]$PlayerRoot, [string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = (Select-String -LiteralPath (Join-Path $projectRoot 'Assets/THH/Runtime/HallRelease.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
if (-not $PlayerRoot) { $PlayerRoot = Join-Path $projectRoot "Builds/Teaching-$version" }
if (-not $OutputRoot) { $OutputRoot = Join-Path $projectRoot ('Logs/Teaching-' + $version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$player = Join-Path $PlayerRoot 'Hall-Teaching.exe'
$checks = @(@('smoke','-hall-smoke','THH_RUNTIME_SMOKE_PASS'), @('auto','-hall-auto-check','THH_AUTOMATIC_CHECK_PASS'), @('teaching','-hall-teaching-check','THH_TEACHING_CHECK_PASS'))
foreach ($check in $checks) {
    $folder = Join-Path $OutputRoot $check[0]
    $log = Join-Path $OutputRoot ($check[0]+'.log')
    $process = Start-Process -FilePath $player -ArgumentList @('-batchmode',$check[1],'-hall-output',('"'+$folder+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(480000)) { Stop-Process -Id $process.Id; throw "Timed out: $($check[0])" }
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $check[2] -Quiet)) { throw "Check failed: $($check[0]); inspect $log" }
    Write-Output "PASS $($check[0])"
}
Write-Output "Evidence: $OutputRoot"
