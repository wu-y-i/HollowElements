param([Parameter(Mandatory=$true)][string]$Json,[int]$TimeoutSeconds=15)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$directory=Join-Path $root 'local/game-session'
$command=$Json | ConvertFrom-Json
$id=[Guid]::NewGuid().ToString('N')
$command | Add-Member -MemberType NoteProperty -Name id -Value $id -Force
$temp=Join-Path $directory 'command.tmp'
$path=Join-Path $directory 'command.json'
[IO.File]::WriteAllText($temp,($command | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temp -Destination $path -Force
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 150
    $responsePath=Join-Path $directory 'response.json'
    if(Test-Path -LiteralPath $responsePath) {
        try { $result=Get-Content -Raw -LiteralPath $responsePath | ConvertFrom-Json; if($result.id -eq $id) { $result | ConvertTo-Json -Depth 20; exit 0 } } catch { }
    }
} while([DateTime]::UtcNow -lt $deadline)
throw "Game command timed out: $($command.command)"
