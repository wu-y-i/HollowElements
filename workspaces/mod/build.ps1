param([string]$GameDirectory, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dotnet = Join-Path $root 'local/tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $root 'local/dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $root 'local/nuget-packages'
if (-not $GameDirectory) {
    $paths = Join-Path $root 'local/paths.json'
    if (-not (Test-Path -LiteralPath $paths)) { throw 'Create local/paths.json using paths.example.json first.' }
    $GameDirectory = (Get-Content -Raw -LiteralPath $paths | ConvertFrom-Json).gameDirectory
}
$managed = Join-Path $GameDirectory 'hollow_knight_Data/Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'UnityEngine.CoreModule.dll'))) { throw 'Game Managed directory is invalid.' }
Push-Location $PSScriptRoot
try {
    $source = Join-Path $root 'local/dependencies'
    foreach ($project in @('tests/HollowElements.RuleTests.csproj', 'src/HollowElements.csproj')) {
        & $dotnet restore $project --source $source
        if ($LASTEXITCODE) { throw "Restore failed: $project" }
    }
    if (-not $SkipTests) {
        & $dotnet run --project tests/HollowElements.RuleTests.csproj -c Release --no-restore
        if ($LASTEXITCODE) { throw 'Rule tests failed.' }
    }
    & $dotnet build src/HollowElements.csproj -c Release --no-restore "-p:GameManagedDir=$managed"
    if ($LASTEXITCODE) { throw 'Mod build failed.' }
    $destination = Join-Path $root 'artifacts/mod/HollowElements'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($file in @('HollowElements.dll','HollowElements.pdb','HollowElements.xml')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('src/bin/Release/net472/' + $file)) -Destination $destination
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content/ElementGuide') -Destination $destination -Recurse -Force
    foreach($cache in @('content.zip','.content-state.json')) {
        $cachePath=Join-Path $destination ('ElementGuide/'+$cache)
        if(Test-Path -LiteralPath $cachePath) {Remove-Item -LiteralPath $cachePath}
    }
    & python (Join-Path $PSScriptRoot 'tools/optimize_guide_media.py') --target (Join-Path $destination 'ElementGuide')
    if ($LASTEXITCODE) { throw 'Guide media optimization failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'balance.defaults.json') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content/BossHints.json') -Destination $destination -Force
    $charmDestination=Join-Path $destination 'Assets/Charms'
    New-Item -ItemType Directory -Path $charmDestination -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root 'workspaces/art/source/charms') -Filter '*.png' | Copy-Item -Destination $charmDestination -Force
    & python (Join-Path $PSScriptRoot 'tools/optimize_runtime_textures.py') --staging $destination
    if ($LASTEXITCODE) { throw 'Texture optimization failed.' }
    & python (Join-Path $PSScriptRoot 'tools/pack_ui_icons.py') --staging $destination
    if ($LASTEXITCODE) { throw 'UI atlas packing failed.' }
    & python (Join-Path $PSScriptRoot 'tools/verify_release.py') --atlas-only $destination
    if ($LASTEXITCODE) { throw 'Unexpected unpacked source images in release assets.' }
    Write-Output "Built to $destination (game files were not modified)."
} finally { Pop-Location }
