$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$gameRoot='E:\SteamLibrary\steamapps\common\Hollow Knight'
$modRoot=Join-Path $gameRoot 'hollow_knight_Data/Managed/Mods/HollowElements'
if(Get-Process hollow_knight -ErrorAction SilentlyContinue){throw 'Close game before acceptance deployment'}
$backup=Join-Path $taskRoot ('local/backups/boss-hints-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$saveRoot=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
Copy-Item -LiteralPath $saveRoot -Destination (Join-Path $backup 'saves') -Recurse
Copy-Item -LiteralPath $modRoot -Destination (Join-Path $backup 'PreviousMod') -Recurse
Set-Content -LiteralPath (Join-Path $taskRoot 'local/game-session/boss-hints-backup.txt') -Value $backup
Copy-Item -Path (Join-Path $taskRoot 'artifacts/mod/HollowElements/*') -Destination $modRoot -Recurse -Force
@{directory=(Join-Path $taskRoot 'local/game-session')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $modRoot 'development-session.json') -Encoding utf8
$env:SteamAppId='367520';$env:SteamGameId='367520'
$arguments='-screen-fullscreen 0 -screen-width 2160 -screen-height 1440 -logFile "'+(Join-Path $taskRoot 'local/game-session/boss-hints.log')+'"'
$process=Start-Process -FilePath (Join-Path $gameRoot 'hollow_knight.exe') -WorkingDirectory $gameRoot -ArgumentList $arguments -WindowStyle Normal -PassThru
@{backup=$backup;pid=$process.Id} | ConvertTo-Json
