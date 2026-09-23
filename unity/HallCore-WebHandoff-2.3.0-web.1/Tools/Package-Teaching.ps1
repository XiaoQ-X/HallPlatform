$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = (Select-String -LiteralPath (Join-Path $projectRoot 'Assets/THH/Runtime/HallRelease.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$buildRoot = Join-Path $projectRoot "Builds/Teaching-$version"
$releaseRoot = Join-Path $projectRoot 'Releases'
$archive = Join-Path $releaseRoot "HallTeaching-$version.zip"
$allowed = @('Hall-Teaching.exe', 'Hall-Teaching_Data', 'MonoBleedingEdge', 'UnityPlayer.dll',
    'UnityCrashHandler64.exe', 'Licenses', 'THIRD_PARTY_NOTICES.md', '使用与来源说明.md', '使用指南.md', 'version.txt')
if (Test-Path -LiteralPath $archive) { throw 'Release archive already exists; retain it and use a new version.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs/教学版使用指南.md') -Destination (Join-Path $buildRoot '使用指南.md') -Force
foreach ($name in $allowed) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildRoot $name))) { throw "Missing release component: $name" }
}
$unexpected = Get-ChildItem -LiteralPath $buildRoot -Force | Where-Object { $_.Name -notin $allowed }
if ($unexpected) { throw ('Unexpected release entries: ' + ($unexpected.Name -join ', ')) }
$files = @(Get-ChildItem -LiteralPath $buildRoot -Recurse -File)
foreach ($file in $files) {
    if ($file.Extension -in @('.pdf','.jpg','.jpeg','.png','.svg','.fbx','.obj','.blend','.ttf','.otf') -or
        $file.FullName -match '参考资料|原始资料|实物照片') { throw "Unreviewed loose media: $($file.FullName)" }
}
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$manifest = @($files | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{path=$_.FullName.Substring($buildRoot.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $releaseRoot "HallTeaching-$version-files.json")
$paths = @($allowed | ForEach-Object { Join-Path $buildRoot $_ })
Compress-Archive -LiteralPath $paths -DestinationPath $archive -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $entries = @($zip.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
    if ($entries.Count -ne $files.Count) { throw 'Archive entry count mismatch' }
    foreach ($entry in $entries) {
        $relative=$entry.FullName.Replace('\','/')
        $record=@($manifest | Where-Object { $_.path -ceq $relative })
        if($record.Count -ne 1 -or $record[0].bytes -ne $entry.Length) { throw "Archive mismatch: $relative" }
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try { $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose();$sha.Dispose() }
        if($hash -ne $record[0].sha256) { throw "Archive hash mismatch: $relative" }
    }
} finally { $zip.Dispose() }
$summary = [pscustomobject]@{result='PASS';files=$files.Count;archive=$archive;sha256=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash;researchOriginalsIncluded=$false}
$summary | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $releaseRoot "HallTeaching-$version-verification.json")
$summary | Format-List
