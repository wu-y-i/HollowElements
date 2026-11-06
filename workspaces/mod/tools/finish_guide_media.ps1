param([switch]$StartNormal)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$taskBackup=[IO.Path]::GetFullPath((Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'local/game-session/guide-media-backup.txt')).Trim())
$backupParent=[IO.Path]::GetFullPath((Join-Path $taskRoot 'local/backups'))+[IO.Path]::DirectorySeparatorChar
if(-not $taskBackup.StartsWith($backupParent,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup outside workspace'}
$gameRoot='E:\SteamLibrary\steamapps\common\Hollow Knight'
$modRoot=Join-Path $gameRoot 'hollow_knight_Data\Managed\Mods\HollowElements'
if(Get-Process hollow_knight -ErrorAction SilentlyContinue){throw 'Close the test game before restoring saves'}
$saveRoot=[IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'))
$saveBackup=Join-Path $taskBackup 'saves'
$savePrefix=$saveRoot+[IO.Path]::DirectorySeparatorChar
$recovery=Join-Path $taskBackup 'test-generated'
New-Item -ItemType Directory -Path $recovery -Force | Out-Null
$restored=@()
foreach($source in Get-ChildItem -LiteralPath $saveBackup -File -Recurse){
    if($source.Extension -eq '.log' -or $source.Name -match '^ModLog'){continue}
    $relative=[IO.Path]::GetRelativePath($saveBackup,$source.FullName)
    $target=[IO.Path]::GetFullPath((Join-Path $saveRoot $relative))
    if(-not $target.StartsWith($savePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected restore target'}
    $same=(Test-Path -LiteralPath $target) -and ((Get-FileHash -LiteralPath $source.FullName).Hash -eq (Get-FileHash -LiteralPath $target).Hash)
    if($same){continue}
    $copy=Join-Path $recovery $relative
    if(Test-Path -LiteralPath $target){New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($copy)) -Force | Out-Null;Copy-Item -LiteralPath $target -Destination $copy -Force}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $source.FullName -Destination $target -Force
    if((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Restore mismatch: $relative"}
    $restored+=$relative
}
foreach($file in Get-ChildItem -LiteralPath $saveRoot -File | Where-Object {$_.Name -match '^user3[._]'}){
    if(Test-Path -LiteralPath (Join-Path $saveBackup $file.Name)){continue}
    $resolved=[IO.Path]::GetFullPath($file.FullName)
    if(-not $resolved.StartsWith($savePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected generated save target'}
    Move-Item -LiteralPath $resolved -Destination (Join-Path $recovery $file.Name) -Force
}
$marker=[IO.Path]::GetFullPath((Join-Path $modRoot 'development-session.json'))
if((Test-Path -LiteralPath $marker) -and $marker.StartsWith([IO.Path]::GetFullPath($modRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $marker}
$report=[ordered]@{passed=$true;backup=$taskBackup;restored=$restored;developmentMarkerRemoved=(-not (Test-Path -LiteralPath $marker));dllSha256=(Get-FileHash -LiteralPath (Join-Path $modRoot 'HollowElements.dll')).Hash.ToLowerInvariant();version='0.1.9.8'}
if($StartNormal){
    $env:SteamAppId='367520';$env:SteamGameId='367520'
    $log=Join-Path $taskRoot 'local/game-session/guide-media-normal.log'
    $arguments='-screen-fullscreen 0 -screen-width 2160 -screen-height 1440 -logFile "'+$log+'"'
    $process=Start-Process -FilePath (Join-Path $gameRoot 'hollow_knight.exe') -WorkingDirectory $gameRoot -ArgumentList $arguments -WindowStyle Normal -PassThru
    $report.normalPid=$process.Id
    Set-Content -LiteralPath (Join-Path $taskRoot 'local/game-session/guide-media-normal.pid') -Value $process.Id
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot 'artifacts/acceptance/guide-media/install.json') -Encoding utf8
$report | ConvertTo-Json -Depth 8
