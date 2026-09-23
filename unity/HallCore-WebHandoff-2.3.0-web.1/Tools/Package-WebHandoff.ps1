param([string]$Destination)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(!$Destination){$Destination=Join-Path $projectRoot ('Releases/HallCore-WebHandoff-2.3.0-web.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw "Destination already exists: $Destination"}
$web=Join-Path $projectRoot 'Builds/WebGL'
if(!(Test-Path (Join-Path $web 'index.html'))){throw 'WebGL build missing'}
$browserResult=Join-Path $projectRoot 'Logs/WebBrowserChecks/result.json'
if(!(Test-Path $browserResult) -or !(Get-Content $browserResult -Raw | ConvertFrom-Json).passed){throw 'Passing browser validation required'}
New-Item -ItemType Directory -Path $Destination | Out-Null
$source=Join-Path $Destination 'Source'
New-Item -ItemType Directory -Path $source | Out-Null
foreach($name in @('Assets','Packages','ProjectSettings')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $source -Recurse}
foreach($name in @('README.md','THIRD_PARTY_NOTICES.md','TEACHING_RELEASE.md','.gitignore','.gitattributes')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $source}
$sourceTools=Join-Path $source 'Tools'
New-Item -ItemType Directory -Path $sourceTools | Out-Null
Get-ChildItem (Join-Path $projectRoot 'Tools') -File | Where-Object {$_.Extension -in @('.ps1','.cjs')} | Copy-Item -Destination $sourceTools
New-Item -ItemType Directory -Path (Join-Path $sourceTools 'Web') | Out-Null
Get-ChildItem (Join-Path $projectRoot 'Tools/Web') -File | Copy-Item -Destination (Join-Path $sourceTools 'Web')
New-Item -ItemType Directory -Path (Join-Path $sourceTools 'InstallerBuild') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/InstallerBuild/Setup.cs') -Destination (Join-Path $sourceTools 'InstallerBuild')
$docs=Join-Path $Destination 'Docs'
New-Item -ItemType Directory -Path $docs | Out-Null
Get-ChildItem (Join-Path $projectRoot 'Docs') -Filter 'Web交接-*.md' -File | Copy-Item -Destination $docs
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs/重建基准.md') -Destination $docs
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs') -Destination $source -Recurse
Copy-Item -LiteralPath $web -Destination (Join-Path $Destination 'WebGL') -Recurse
New-Item -ItemType Directory -Path (Join-Path $Destination 'Tools/Web') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Web/serve.cjs') -Destination (Join-Path $Destination 'Tools/Web')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Web/HANDOFF-README.md') -Destination (Join-Path $Destination 'README.md')
$validation=Join-Path $Destination 'Validation'
New-Item -ItemType Directory -Path $validation | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Logs/WebModelChecks/result.txt') -Destination (Join-Path $validation 'model-checks.txt')
Get-ChildItem (Join-Path $projectRoot 'Logs/WebBrowserChecks') -File | Where-Object {$_.Name -match '^(0[123]-.+\.png|four-direction-operation\.webm|session-export\.json|result\.json|integration-button-check\.json|pointer-check\.json|pointer-wiring\.png|validation-summary\.md)$'} | Copy-Item -Destination $validation
$manifest=Get-ChildItem $Destination -File -Recurse | ForEach-Object {
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($Destination,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $Destination 'manifest-sha256.json') -Encoding utf8
$zip=$Destination+'.zip'
if(Test-Path -LiteralPath $zip){throw "ZIP already exists: $zip"}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($Destination,$zip,[IO.Compression.CompressionLevel]::Optimal,$true)
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Output "Handoff: $zip"
