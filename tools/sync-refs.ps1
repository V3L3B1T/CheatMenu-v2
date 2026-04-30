# Copies BepInEx core + game interop DLLs into ./refs/ for project references.
# Usage: .\tools\sync-refs.ps1 -GameRoot "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era"

param(
    [Parameter(Mandatory=$true)] [string] $GameRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$refsDir  = Join-Path $repoRoot 'refs'
New-Item -ItemType Directory -Force -Path $refsDir | Out-Null

$bepinex = @(
    'BepInEx\core\BepInEx.Core.dll',
    'BepInEx\core\BepInEx.Unity.IL2CPP.dll',
    'BepInEx\core\BepInEx.Unity.Common.dll',
    'BepInEx\core\0Harmony.dll',
    'BepInEx\core\Il2CppInterop.Runtime.dll',
    'BepInEx\core\Il2CppInterop.Common.dll',
    'BepInEx\core\Il2CppInterop.HarmonySupport.dll',
    'BepInEx\core\Mono.Cecil.dll'
)

$unity = @(
    'BepInEx\unity-libs\UnityEngine.CoreModule.dll',
    'BepInEx\unity-libs\UnityEngine.IMGUIModule.dll',
    'BepInEx\unity-libs\UnityEngine.InputLegacyModule.dll'
)

$interop = @(
    'BepInEx\interop\Hex.dll',
    'BepInEx\interop\Hex.Shared.dll',
    'BepInEx\interop\Il2Cppmscorlib.dll',
    'BepInEx\interop\Il2CppSystem.dll',
    'BepInEx\interop\Il2CppSystem.Core.dll',
    'BepInEx\interop\UnityEngine.UI.dll',
    'BepInEx\interop\Unity.TextMeshPro.dll'
)

$all = $bepinex + $unity + $interop
foreach ($rel in $all) {
    $src = Join-Path $GameRoot $rel
    if (-not (Test-Path $src)) {
        Write-Warning "Missing: $rel"
        continue
    }
    $dst = Join-Path $refsDir (Split-Path $rel -Leaf)
    Copy-Item -Path $src -Destination $dst -Force
    Unblock-File -Path $dst
    Write-Host "  copied $rel"
}

Write-Host "`nrefs/ populated:"
Get-ChildItem $refsDir -Filter *.dll | ForEach-Object { Write-Host ("  " + $_.Name) }
