$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if(Get-Process hollow_knight -ErrorAction SilentlyContinue){throw 'Close the test game before redeploying'}
if(-not(Test-Path -LiteralPath (Join-Path $taskRoot 'local/game-session/rule-update-backup.txt'))){throw 'Fresh session backup required'}
$gameRoot='E:\SteamLibrary\steamapps\common\Hollow Knight'
$modRoot=Join-Path $gameRoot 'hollow_knight_Data/Managed/Mods/HollowElements'
Copy-Item -Path (Join-Path $taskRoot 'artifacts/mod/HollowElements/*') -Destination $modRoot -Recurse -Force
$env:SteamAppId='367520';$env:SteamGameId='367520'
$arguments='-screen-fullscreen 0 -screen-width 2160 -screen-height 1440 -logFile "'+(Join-Path $taskRoot 'local/game-session/rule-update.log')+'"'
$process=Start-Process -FilePath (Join-Path $gameRoot 'hollow_knight.exe') -WorkingDirectory $gameRoot -ArgumentList $arguments -WindowStyle Normal -PassThru
Write-Output $process.Id
