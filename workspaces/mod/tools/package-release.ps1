param([string]$ReleaseName='mod_v0.1.3')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$stage=Join-Path $root 'artifacts/mod/HollowElements'
$version=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $stage 'HollowElements.dll')).Version.ToString(3)
$releases=Join-Path $root 'artifacts/releases'
$release=Join-Path $releases $ReleaseName
if($ReleaseName -notmatch '^mod_v[0-9]+\.[0-9]+(?:\.[0-9]+)?$') { throw 'Invalid release name.' }
if(Test-Path -LiteralPath $release) { throw "Release already exists: $release" }
if(Test-Path -LiteralPath ($release+'.zip')) { throw 'Release archive already exists.' }
$reports=@('build.json','runtime-reactions.json','native-attacks.json','boss-control.json','player-states.json','ui-workshop.json','effects-lifecycle.json','multiple-boss-bars.json','unload-audit.json','normal-install.json')
if([Version]$version -ge [Version]'0.1.1') { $reports += 'presentation-update.json' }
if([Version]$version -ge [Version]'0.1.2') { $reports += 'outline-update.json' }
$checks=@()
foreach($name in $reports) {
    $path=Join-Path $root ('artifacts/acceptance/'+$name)
    $report=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    if($report.passed -ne $true) { throw "Acceptance report has not passed: $name" }
    $checks += [ordered]@{name=$name;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$persistence=Get-Content -Raw -LiteralPath (Join-Path $root 'artifacts/acceptance/loadout-persistence.json') | ConvertFrom-Json
if(-not($persistence.ok -and $persistence.result.saved -and $persistence.result.loaded -and $persistence.result.matched)) { throw 'Loadout persistence not verified.' }
foreach($name in @('loadout-persistence.json','saves-unchanged.json')) {
    $path=Join-Path $root ('artifacts/acceptance/'+$name)
    $checks += [ordered]@{name=$name;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$saves=@(Get-Content -Raw -LiteralPath (Join-Path $root 'artifacts/acceptance/saves-unchanged.json') | ConvertFrom-Json)
if($saves.Count -ne 13 -or @($saves | Where-Object { $_.unchanged -ne $true }).Count -ne 0) { throw 'Original saves changed or were not verified.' }
$build=Get-Content -Raw -LiteralPath (Join-Path $root 'artifacts/acceptance/build.json') | ConvertFrom-Json
if($build.dllSha256 -ne (Get-FileHash -LiteralPath (Join-Path $stage 'HollowElements.dll') -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Build verification refers to another DLL.' }
$normal=Get-Content -Raw -LiteralPath (Join-Path $root 'artifacts/acceptance/normal-install.json') | ConvertFrom-Json
if($normal.version -ne $version -or $normal.dllSha256 -ne $build.dllSha256 -or $normal.debugEnabled -ne $false -or $normal.developmentMarker -ne $false) { throw 'Normal install verification refers to another build or mode.' }
New-Item -ItemType Directory -Path $releases -Force | Out-Null
$temporary=Join-Path $releases ('.building-'+[Guid]::NewGuid().ToString('N'))
foreach($candidate in @($temporary,$release)) {
    if(-not([IO.Path]::GetFullPath($candidate).StartsWith([IO.Path]::GetFullPath($releases)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))) { throw 'Release path escapes workspace.' }
}
$mod=Join-Path $temporary 'HollowElements'
New-Item -ItemType Directory -Path $mod -Force | Out-Null
foreach($name in @('HollowElements.dll','HollowElements.pdb')) { Copy-Item -LiteralPath (Join-Path $stage $name) -Destination $mod }
Copy-Item -LiteralPath (Join-Path $stage 'Assets') -Destination $mod -Recurse
Copy-Item -LiteralPath (Join-Path $root 'workspaces/mod/balance.defaults.json') -Destination (Join-Path $mod 'balance.defaults.json')
Copy-Item -LiteralPath (Join-Path $root 'docs/mod_v0.1使用说明.md') -Destination (Join-Path $temporary '安装与使用.md')
Copy-Item -LiteralPath (Join-Path $root 'docs/mod_v0.1验收清单.md') -Destination (Join-Path $temporary '验收记录.md')
$fx=Get-ChildItem -LiteralPath (Join-Path $mod 'Assets/Effects') -Directory
$frames=0
foreach($folder in $fx) { $clip=Get-Content -Raw -LiteralPath (Join-Path $folder.FullName 'manifest.json') | ConvertFrom-Json; $frames+=$clip.frames; if(-not(Test-Path -LiteralPath (Join-Path $folder.FullName 'atlas.png'))) { throw 'Missing atlas.' } }
if($fx.Count -ne 10 -or $frames -ne 249) { throw 'Incomplete effects.' }
if((Get-ChildItem -LiteralPath (Join-Path $mod 'Assets/Audio') -Filter '*.wav').Count -ne 7) { throw 'Incomplete audio.' }
if((Get-ChildItem -LiteralPath (Join-Path $mod 'Assets/Icons') -Filter '*.png').Count -ne 5) { throw 'Incomplete icons.' }
if(Get-ChildItem -LiteralPath $temporary -Recurse -File | Where-Object { $_.Name -eq 'development-session.json' -or $_.Name -like 'Assembly-CSharp*' -or $_.Name -like 'user*.dat' }) { throw 'Development or game files leaked into package.' }
$validation=[ordered]@{version=$version;game='1.5.78.11833';api=77;runtime='net472';language='zh-CN';effects=$fx.Count;frames=$frames;audio=7;icons=5;debugDefault=$false;reports=$checks}
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $temporary 'verification.json') -Encoding utf8
$files=@()
foreach($file in Get-ChildItem -LiteralPath $temporary -Recurse -File | Sort-Object FullName) {
    $relative=[IO.Path]::GetRelativePath($temporary,$file.FullName).Replace('\','/')
    $files += [ordered]@{path=$relative;bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest=[ordered]@{version=$version;files=$files}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $temporary 'manifest.json') -Encoding utf8
Move-Item -LiteralPath $temporary -Destination $release
Compress-Archive -Path (Join-Path $release '*') -DestinationPath ($release+'.zip') -CompressionLevel Optimal
Get-FileHash -LiteralPath ($release+'.zip') -Algorithm SHA256 | Select-Object Hash,Path
Write-Output "Packaged $release"
