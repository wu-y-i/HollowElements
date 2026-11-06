param([switch]$SkipSdk)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lock = Get-Content -Raw -LiteralPath (Join-Path $root 'dependencies.lock.json') | ConvertFrom-Json
$dependencyDir = Join-Path $root 'local/dependencies'
$toolDir = Join-Path $root 'local/tools'
New-Item -ItemType Directory -Path $dependencyDir, $toolDir -Force | Out-Null

function Fetch-Verified([string]$Url, [string]$Path, [string]$Hash, [string]$Algorithm = 'SHA256') {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (-not $absolute.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Download destination escapes workspace.' }
    if (Test-Path -LiteralPath $absolute) {
        if ((Get-FileHash -LiteralPath $absolute -Algorithm $Algorithm).Hash -ieq $Hash) { return }
        throw "Existing download has wrong hash: $absolute"
    }
    $pending = $absolute + '.download'
    try { Invoke-WebRequest -Uri $Url -OutFile $pending -TimeoutSec 120 }
    catch { Invoke-WebRequest -Uri $Url -Proxy 'http://localhost:7890' -OutFile $pending -TimeoutSec 120 }
    if ((Get-FileHash -LiteralPath $pending -Algorithm $Algorithm).Hash -ine $Hash) { throw "Checksum mismatch: $Url" }
    Move-Item -LiteralPath $pending -Destination $absolute
}

$apiSource = Join-Path $root 'workspaces/api'
if (-not (Test-Path -LiteralPath (Join-Path $apiSource '.git'))) {
    & git -c http.sslBackend=openssl clone --depth 1 --branch $lock.api.tag $lock.api.repository $apiSource
    if ($LASTEXITCODE) {
        & git -c http.sslBackend=openssl -c http.proxy=http://localhost:7890 clone --depth 1 --branch $lock.api.tag $lock.api.repository $apiSource
        if ($LASTEXITCODE) { throw 'API clone failed.' }
    }
}
$head = & git -C $apiSource rev-parse HEAD
if ($LASTEXITCODE -or $head -ne $lock.api.commit) { throw 'API checkout does not match dependency lock; no automatic reset was performed.' }
$apiZip = Join-Path $dependencyDir $lock.api.asset
Fetch-Verified ('https://github.com/hk-modding/api/releases/download/' + $lock.api.tag + '/' + $lock.api.asset) $apiZip $lock.api.sha256
Expand-Archive -LiteralPath $apiZip -DestinationPath (Join-Path $dependencyDir 'api-v77') -Force
Fetch-Verified ('https://github.com/PrashantMohta/Satchel/releases/download/' + $lock.satchel.tag + '/' + $lock.satchel.asset) (Join-Path $dependencyDir $lock.satchel.asset) $lock.satchel.sha256
foreach ($package in $lock.referencePackages) {
    $name = $package.id + '.' + $package.version + '.nupkg'
    Fetch-Verified ('https://globalcdn.nuget.org/packages/' + $name) (Join-Path $dependencyDir $name) $package.sha256
}
if (-not $SkipSdk) {
    $dotnet = Join-Path $toolDir 'dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $dotnet) {
        $version = & $dotnet --version
        if ($LASTEXITCODE -or $version -ne $lock.sdk.version) { throw 'Local SDK version differs from the lock.' }
    } else {
        $sdkZip = Join-Path $toolDir 'dotnet-sdk.zip'
        Fetch-Verified $lock.sdk.url $sdkZip $lock.sdk.sha512 'SHA512'
        Expand-Archive -LiteralPath $sdkZip -DestinationPath (Join-Path $toolDir 'dotnet') -Force
    }
}
$paths = Join-Path $root 'local/paths.json'
if (-not (Test-Path -LiteralPath $paths)) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'paths.example.json') -Destination $paths }
Write-Output 'Dependencies are pinned and staged. No game files or Unity Editor settings were changed.'
