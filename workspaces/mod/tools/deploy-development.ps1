param([string]$GameDirectory, [switch]$InstallApi)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $GameDirectory) { $GameDirectory = (Get-Content -Raw -LiteralPath (Join-Path $root 'local/paths.json') | ConvertFrom-Json).gameDirectory }
$gamePath = [IO.Path]::GetFullPath($GameDirectory)
$managed = Join-Path $gamePath 'hollow_knight_Data/Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (Get-Process -Name hollow_knight -ErrorAction SilentlyContinue) { throw 'Close Hollow Knight before deploying.' }
$backup = Join-Path $root ('local/backups/development-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$savePath = Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
if (Test-Path -LiteralPath $savePath) { Copy-Item -LiteralPath $savePath -Destination (Join-Path $backup 'saves') -Recurse }
$report = [ordered]@{ gameDirectory=$gamePath; backup=$backup; managed=@() }
if ($InstallApi) {
    $apiSource = Join-Path $root 'local/dependencies/api-v77'
    New-Item -ItemType Directory -Path (Join-Path $backup 'Managed') -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $apiSource -File | Where-Object { $_.Extension -eq '.dll' }) {
        $target = Join-Path $managed $file.Name
        $existed = Test-Path -LiteralPath $target
        $oldHash = $null
        if ($existed) { $oldHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash; Copy-Item -LiteralPath $target -Destination (Join-Path $backup ('Managed/' + $file.Name)) }
        $report.managed += [ordered]@{ name=$file.Name; existed=$existed; oldSha256=$oldHash; newSha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
$destination = Join-Path $managed 'Mods/HollowElements'
if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination (Join-Path $backup 'PreviousMod') -Recurse }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $root 'artifacts/mod/HollowElements/*') -Destination $destination -Recurse -Force
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $backup 'deployment.json') -Encoding utf8
Write-Output "Deployed to $destination; original files and saves backed up at $backup"
