# Builds CheatMenu-v2 in Release config and copies the DLL into the game's
# BepInEx/plugins/ folder. Optional -Restart kills any running game process
# and relaunches via Steam.
#
# Usage:
#   .\tools\deploy.ps1 -GameRoot "C:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era"
#   .\tools\deploy.ps1 -GameRoot "..." -Restart

param(
    [string] $GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era",
    [switch] $Restart
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $GameRoot -PathType Container)) {
    Write-Error "GameRoot not found: '$GameRoot'. Provide the full path to the game install directory."
    exit 1
}

$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

dotnet build CheatMenuV2.csproj -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$src = Join-Path $repoRoot 'bin\Release\CheatMenu-v2.dll'
$dst = Join-Path $GameRoot 'BepInEx\plugins\CheatMenu-v2.dll'
Copy-Item -Force $src $dst
Write-Host "Deployed: $dst"

if ($Restart)
{
    Get-Process -Name 'HeroesOldenEra' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    Start-Process 'steam://rungameid/3105440'  # AppId observed in LogOutput.log
    Write-Host "Game restart requested via Steam"
}
