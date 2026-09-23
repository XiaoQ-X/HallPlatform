param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = (Select-String -LiteralPath (Join-Path $projectRoot 'Assets/THH/Runtime/HallRelease.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
if (Test-Path -LiteralPath (Join-Path $projectRoot "Releases/HallTeaching-$version.zip")) { throw 'This version is already packaged; increment HallRelease.Version before rebuilding a release.' }
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'Logs') | Out-Null
$log = Join-Path $projectRoot "Logs/build-$version.log"
$process = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-quit','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','HallLab.Editor.THHPlayerBuild.BuildTeaching','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(900000)) { Stop-Process -Id $process.Id; throw 'Unity build timed out.' }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'THH_PLAYER_BUILD_PASS' -Quiet)) { throw "Unity build failed; inspect $log" }
& (Join-Path $PSScriptRoot 'Test-Teaching.ps1')
& (Join-Path $PSScriptRoot 'Package-Teaching.ps1')
& (Join-Path $PSScriptRoot 'Build-Installer.ps1')
