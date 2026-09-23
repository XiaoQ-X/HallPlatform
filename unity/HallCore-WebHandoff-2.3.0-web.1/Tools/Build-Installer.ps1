$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = (Select-String -LiteralPath (Join-Path $projectRoot 'Assets/THH/Runtime/HallRelease.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$archive = Join-Path $projectRoot "Releases/HallTeaching-$version.zip"
$destination = Join-Path $projectRoot "Releases/HallTeaching-$version-Setup.exe"
if (Test-Path -LiteralPath $destination) { throw 'Installer already exists; use a new version or retain it before rebuilding.' }
if (-not (Test-Path -LiteralPath $archive)) { throw 'Run Package-Teaching.ps1 first.' }
$scratch = Join-Path $projectRoot "Tools/.runtime/installer-$version"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$source = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'InstallerBuild/Setup.cs') -Raw).Replace('__VERSION__',$version).Replace('__PAYLOAD_SHA256__',(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash)
$sourcePath = Join-Path $scratch 'Setup.cs'
$stub = Join-Path $scratch 'Setup.exe'
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$stub" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:Microsoft.CSharp.dll $sourcePath
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$output = [IO.File]::Create($destination)
try {
    foreach ($part in @($stub,$archive)) {
        $inputStream = [IO.File]::OpenRead($part)
        try { $inputStream.CopyTo($output) } finally { $inputStream.Dispose() }
    }
    $lengthBytes = [BitConverter]::GetBytes([long](Get-Item -LiteralPath $archive).Length)
    $output.Write($lengthBytes,0,$lengthBytes.Length)
    $marker = [Text.Encoding]::ASCII.GetBytes('HALL_TEACHING_SETUP_PAYLOAD_V1')
    $output.Write($marker,0,$marker.Length)
} finally { $output.Dispose() }
$verify = Start-Process -FilePath $destination -ArgumentList '/verify' -WindowStyle Hidden -Wait -PassThru
if ($verify.ExitCode -ne 0) { throw 'Embedded ZIP integrity verification failed.' }
Get-FileHash -LiteralPath $destination -Algorithm SHA256
