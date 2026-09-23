$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot '.runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$archive = Join-Path $runtime 'resvg-2.6.2.tgz'
$package = Join-Path $runtime 'resvg'
Invoke-WebRequest -Uri 'https://registry.npmjs.org/@resvg/resvg-js-win32-x64-msvc/-/resvg-js-win32-x64-msvc-2.6.2.tgz' -OutFile $archive
$metadata = Invoke-RestMethod -Uri 'https://registry.npmjs.org/@resvg/resvg-js-win32-x64-msvc/2.6.2'
$algorithm = [Security.Cryptography.SHA512]::Create()
try { $integrity = 'sha512-' + [Convert]::ToBase64String($algorithm.ComputeHash([IO.File]::ReadAllBytes($archive))) } finally { $algorithm.Dispose() }
if ($integrity -cne $metadata.dist.integrity) { throw 'Icon runtime integrity mismatch.' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
& tar -xf $archive -C $package
if ($LASTEXITCODE -ne 0) { throw 'Icon runtime extraction failed.' }
& node (Join-Path $PSScriptRoot 'render-icons.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Icon rendering failed.' }
