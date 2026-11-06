param([Parameter(Mandatory=$true)][string]$BackupDirectory)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$backupRoot=[IO.Path]::GetFullPath($BackupDirectory)
$backupPrefix=[IO.Path]::GetFullPath((Join-Path $taskRoot 'local/backups'))+[IO.Path]::DirectorySeparatorChar
if(-not $backupRoot.StartsWith($backupPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup outside workspace'}
if(Get-Process hollow_knight -ErrorAction SilentlyContinue){throw 'Close the test game before restoring settings'}
$gameRoot=(Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'local/paths.json') | ConvertFrom-Json).gameDirectory
$modRoot=[IO.Path]::GetFullPath((Join-Path $gameRoot 'hollow_knight_Data/Managed/Mods/HollowElements'))
$saveRoot=[IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'))
$savePrefix=$saveRoot+[IO.Path]::DirectorySeparatorChar
$saved=Join-Path $backupRoot 'saves'
if(-not(Test-Path -LiteralPath $saved)){throw 'Save backup missing'}
$recovery=Join-Path $backupRoot 'ui-v1-test-generated'
$restored=@();$checks=@()
foreach($source in Get-ChildItem -LiteralPath $saved -File -Recurse){
    if($source.Extension -eq '.log' -or $source.Name -match '^ModLog'){continue}
    $relative=[IO.Path]::GetRelativePath($saved,$source.FullName)
    $target=[IO.Path]::GetFullPath((Join-Path $saveRoot $relative))
    if(-not $target.StartsWith($savePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Restore path escaped save directory'}
    $expected=(Get-FileHash -LiteralPath $source.FullName).Hash
    $same=(Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -eq $expected
    if(-not $same){
        $copy=Join-Path $recovery $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($copy)) -Force | Out-Null
        if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination $copy -Force}
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath $source.FullName -Destination $target -Force
        $restored+=$relative
    }
    $matched=(Get-FileHash -LiteralPath $target).Hash -eq $expected
    if(-not $matched){throw "Restore mismatch: $relative"}
    $checks+=@{path=$relative;unchanged=$matched;sha256=$expected.ToLowerInvariant()}
}
$marker=[IO.Path]::GetFullPath((Join-Path $modRoot 'development-session.json'))
if(Test-Path -LiteralPath $marker){Remove-Item -LiteralPath $marker}
$dll=Join-Path $modRoot 'HollowElements.dll'
$digest=(Get-FileHash -LiteralPath $dll).Hash.ToLowerInvariant()
if($digest -ne (Get-FileHash -LiteralPath (Join-Path $taskRoot 'artifacts/mod/HollowElements/HollowElements.dll')).Hash.ToLowerInvariant()){throw 'Installed DLL differs from tested staging build'}
$report=@{passed=$true;version='1.0';assemblyVersion=[Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString();backup=$backupRoot;restored=$restored;savesUnchanged=$true;saves=$checks;developmentMarkerRemoved=(-not(Test-Path -LiteralPath $marker));dllSha256=$digest}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot 'artifacts/acceptance/ui-v1/install.json') -Encoding utf8
Write-Output "Restored $($restored.Count) files; verified $($checks.Count) original save/settings files; development marker removed."
