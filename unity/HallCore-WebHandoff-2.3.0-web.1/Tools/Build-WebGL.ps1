param([string]$UnityEditor = 'D:/UnitySetup/EditorCN/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$log=Join-Path $projectRoot 'Logs/webgl-build.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-quit','-projectPath',('"'+$projectRoot+'"'),'-buildTarget','WebGL','-executeMethod','HallLab.Editor.WebPlayerBuild.Build','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -SimpleMatch 'HALL_WEBGL_BUILD_PASS' -Quiet)){throw "Build failed: $log"}
Write-Output "WebGL ready: $projectRoot/Builds/WebGL"
