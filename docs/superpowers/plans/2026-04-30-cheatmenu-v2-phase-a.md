# CheatMenu-v2 Phase A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `CheatMenu-v2.dll` v0.1.0 — a BepInEx 6 IL2CPP plugin that loads in Heroes of Might and Magic: Olden Era (day-one build), binds F1 to a cheat menu, and demonstrates one working cheat (`+10000 all resources`) via either a re-awakened native dev panel or an IMGUI fallback.

**Architecture:** Single .NET 6 assembly. Five units (`Plugin`, `InputGate`, `OverlayUI`, `MenuCoordinator`, `NativePanelDriver`, `CommandDispatcher`) — see [the spec §3.1](../specs/2026-04-30-cheatmenu-v2-design.md). Hybrid strategy: try native panel first, fall back to IMGUI button strip if the dev prefab isn't shipped or fails to render.

**Tech Stack:** C# / .NET 6 / BepInEx 6.0.0-be.755 IL2CPP / Il2CppInterop / Unity 6000.0.66f1 / Mono.Cecil (recon only) / PowerShell (build/deploy scripts).

**Verification model:** No unit tests (spec §2.2). Each task is verified by launching the game and observing log lines, screen state, or in-game effects. Manual smoke verification is the test.

**Source repo location:** `C:\dev\CheatMenu-v2` (default; user may relocate at Task 0). Game install: `c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era`.

**Phase A success criteria** (from spec §5.4 — all four must pass on a fresh game launch):
1. `LogOutput.log` shows `1 plugin to load` and `[Info :CheatMenuV2] Loaded — …`
2. Status overlay visible top-right on main menu
3. F1 in a loaded game flips Mode to `Native` or `Fallback`, logged
4. Resource HUD increases by +10000 across all resources, no errors

**Rollback model:** every task ends with a commit. To roll back a task: `git revert <hash>` (preferred) or `git reset --hard HEAD~1` if uncommitted. To uninstall the deployed DLL: delete `<game>/BepInEx/plugins/CheatMenu-v2.dll`. Each task's "Rollback notes" section calls out anything beyond these defaults.

---

## File Structure (locked at Task 0; matches spec §3.2)

```
C:\dev\CheatMenu-v2\
├── CheatMenuV2.csproj            # SDK-style, net6.0, references in ./refs/
├── README.md                     # end-user install + troubleshooting
├── CHANGELOG.md
├── LICENSE                       # MIT
├── .gitignore                    # excludes refs/, bin/, obj/, *.user
├── refs/                         # game/BepInEx DLLs, gitignored
│   └── .gitkeep
├── src/
│   ├── Plugin.cs                 # BasePlugin, [BepInPlugin], Load()
│   ├── MenuCoordinator.cs        # POCO orchestrator, Mode state machine
│   ├── Input/
│   │   └── InputGate.cs          # MonoBehaviour, F1 polling, suppression
│   ├── Native/
│   │   └── NativePanelDriver.cs  # POCO, context detection + 3-tier instantiation
│   ├── Commands/
│   │   └── CommandDispatcher.cs  # POCO, EnsureLocated() + AddAllResources()
│   └── UI/
│       ├── OverlayUI.cs          # MonoBehaviour, IMGUI strip + fallback button
│       └── MenuHostBehaviour.cs  # empty MonoBehaviour, host-GameObject marker
├── tools/
│   ├── sync-refs.ps1             # copies needed DLLs into ./refs/
│   └── deploy.ps1                # build + copy to <game>/BepInEx/plugins/
└── docs/
    └── recon-notes.md            # RE findings (dispatch site, prefab keys, build IDs)
```

---

## Task 0: Bootstrap repo, references, and project scaffolding

**Files:**
- Create: `C:\dev\CheatMenu-v2\.gitignore`
- Create: `C:\dev\CheatMenu-v2\LICENSE`
- Create: `C:\dev\CheatMenu-v2\README.md` (skeleton)
- Create: `C:\dev\CheatMenu-v2\CHANGELOG.md` (skeleton)
- Create: `C:\dev\CheatMenu-v2\CheatMenuV2.csproj`
- Create: `C:\dev\CheatMenu-v2\refs\.gitkeep`
- Create: `C:\dev\CheatMenu-v2\tools\sync-refs.ps1`
- Create: `C:\dev\CheatMenu-v2\src\Plugin.cs` (minimal placeholder so build succeeds)
- Create: `C:\dev\CheatMenu-v2\docs\recon-notes.md` (empty header)

- [ ] **Step 0.1: Create the source repo directory and init git**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2" | Out-Null
Set-Location "C:\dev\CheatMenu-v2"
git init -b main
```

Expected output: `Initialized empty Git repository in C:/dev/CheatMenu-v2/.git/`.

- [ ] **Step 0.2: Write `.gitignore`**

Create `C:\dev\CheatMenu-v2\.gitignore`:

```gitignore
# Build output
bin/
obj/

# Local DLL refs (game/BepInEx — copyright)
refs/*
!refs/.gitkeep

# IDE
.vs/
.vscode/
.idea/
*.user
*.suo

# OS
Thumbs.db
.DS_Store
```

- [ ] **Step 0.3: Write `LICENSE` (MIT)**

Create `C:\dev\CheatMenu-v2\LICENSE` with the standard MIT text. Author: Velebit. Year: 2026.

- [ ] **Step 0.4: Write `README.md` skeleton**

Create `C:\dev\CheatMenu-v2\README.md`:

```markdown
# CheatMenu-v2

A BepInEx 6 IL2CPP plugin for Heroes of Might and Magic: Olden Era that re-exposes the game's built-in developer cheat panel.

**Status:** Phase A — proof-of-life. See [docs/superpowers/plans](../../docs/superpowers/plans/) (game-side) for current scope.

## Requirements

- Heroes of Might and Magic: Olden Era (day-one build, 2026-04-30)
- BepInEx 6.0.0-be.755 IL2CPP (verify `BepInEx/LogOutput.log` header)

## Install

1. Build or download `CheatMenu-v2.dll`.
2. Copy to `<game>/BepInEx/plugins/CheatMenu-v2.dll`.
3. Launch game. Within 2 seconds of the main menu, a status overlay appears top-right: `CheatMenu-v2 | Mode: Probing | F1: toggle`.
4. Verify load: open `<game>/BepInEx/LogOutput.log` and search for `CheatMenuV2 Loaded`.

## Hotkey

F1 — toggle the cheat menu. Suppressed when typing in any input field. Configurable in `<game>/BepInEx/config/CheatMenuV2.cfg`.

## Uninstall

Delete `<game>/BepInEx/plugins/CheatMenu-v2.dll`. Optionally delete `<game>/BepInEx/config/CheatMenuV2.cfg`.

## Testing constraints

**Offline only.** Skirmish or single-player campaign. Do not test in matchmade, ranked, or leaderboard-tracked modes — Olden Era ships with EOS and an analytics collector; cheat-triggered state changes may be reported.

## License

MIT. See `LICENSE`.
```

- [ ] **Step 0.5: Write `CHANGELOG.md` skeleton**

Create `C:\dev\CheatMenu-v2\CHANGELOG.md`:

```markdown
# Changelog

## [Unreleased]

## [0.1.0] - 2026-04-30

Phase A: proof of life.
- Plugin loads under BepInEx 6 IL2CPP
- F1 toggles cheat menu
- One working cheat: +10000 all resources via either native dev panel or IMGUI fallback
```

- [ ] **Step 0.6: Create `refs/.gitkeep` and `docs/recon-notes.md` placeholder**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\refs" | Out-Null
New-Item -ItemType File -Force -Path "C:\dev\CheatMenu-v2\refs\.gitkeep" | Out-Null
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\docs" | Out-Null
```

Create `C:\dev\CheatMenu-v2\docs\recon-notes.md`:

```markdown
# Recon Notes

Reverse-engineering findings, populated as Task 7 executes.

## Game build

Recorded at first recon run.

## Dispatch site for `Hex.Session.TypeCmdResourceCheat`

To be populated by Task 7.

## Native cheat panel asset paths

To be populated by Task 9 / 10 if Addressables / Resources tiers fire.
```

- [ ] **Step 0.7: Write `tools/sync-refs.ps1`**

Create `C:\dev\CheatMenu-v2\tools\sync-refs.ps1`:

```powershell
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
    'BepInEx\unity-libs\UnityEngine.InputLegacyModule.dll',
    'BepInEx\unity-libs\UnityEngine.UI.dll',
    'BepInEx\unity-libs\UnityEngine.UIModule.dll'
)

$interop = @(
    'BepInEx\interop\Hex.dll',
    'BepInEx\interop\Hex.Shared.dll',
    'BepInEx\interop\Il2Cppmscorlib.dll',
    'BepInEx\interop\Il2CppSystem.dll',
    'BepInEx\interop\Il2CppSystem.Core.dll'
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
```

- [ ] **Step 0.8: Run sync-refs.ps1 to populate `refs/`**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
.\tools\sync-refs.ps1 -GameRoot "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era"
```

Expected: ~16 DLL files copied. No "Missing:" warnings. If `UnityEngine.UIModule.dll` is missing, that's tolerable — `UI.dll` is the only one we hard-require. Any other "Missing" is a blocker.

- [ ] **Step 0.9: Write `CheatMenuV2.csproj`**

Create `C:\dev\CheatMenu-v2\CheatMenuV2.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <AssemblyName>CheatMenu-v2</AssemblyName>
    <RootNamespace>CheatMenuV2</RootNamespace>
    <Version>0.1.0</Version>
    <Description>BepInEx 6 IL2CPP cheat menu for Heroes of Might and Magic: Olden Era</Description>
    <Authors>Velebit</Authors>
    <Copyright>Copyright (c) 2026 Velebit. MIT License.</Copyright>
    <NoWarn>CS0436;CS1701;CS1702</NoWarn>
    <DebugType>portable</DebugType>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="BepInEx.Core">           <HintPath>refs\BepInEx.Core.dll</HintPath>           <Private>false</Private> </Reference>
    <Reference Include="BepInEx.Unity.IL2CPP">   <HintPath>refs\BepInEx.Unity.IL2CPP.dll</HintPath>   <Private>false</Private> </Reference>
    <Reference Include="BepInEx.Unity.Common">   <HintPath>refs\BepInEx.Unity.Common.dll</HintPath>   <Private>false</Private> </Reference>
    <Reference Include="0Harmony">               <HintPath>refs\0Harmony.dll</HintPath>               <Private>false</Private> </Reference>
    <Reference Include="Il2CppInterop.Runtime">  <HintPath>refs\Il2CppInterop.Runtime.dll</HintPath>  <Private>false</Private> </Reference>
    <Reference Include="Il2CppInterop.Common">   <HintPath>refs\Il2CppInterop.Common.dll</HintPath>   <Private>false</Private> </Reference>
    <Reference Include="Il2CppInterop.HarmonySupport"> <HintPath>refs\Il2CppInterop.HarmonySupport.dll</HintPath> <Private>false</Private> </Reference>
    <Reference Include="UnityEngine.CoreModule"> <HintPath>refs\UnityEngine.CoreModule.dll</HintPath> <Private>false</Private> </Reference>
    <Reference Include="UnityEngine.IMGUIModule"> <HintPath>refs\UnityEngine.IMGUIModule.dll</HintPath> <Private>false</Private> </Reference>
    <Reference Include="UnityEngine.InputLegacyModule"> <HintPath>refs\UnityEngine.InputLegacyModule.dll</HintPath> <Private>false</Private> </Reference>
    <Reference Include="UnityEngine.UI">         <HintPath>refs\UnityEngine.UI.dll</HintPath>         <Private>false</Private> </Reference>
    <Reference Include="Hex">                    <HintPath>refs\Hex.dll</HintPath>                    <Private>false</Private> </Reference>
    <Reference Include="Hex.Shared">             <HintPath>refs\Hex.Shared.dll</HintPath>             <Private>false</Private> </Reference>
    <Reference Include="Il2Cppmscorlib">         <HintPath>refs\Il2Cppmscorlib.dll</HintPath>         <Private>false</Private> </Reference>
    <Reference Include="Il2CppSystem">           <HintPath>refs\Il2CppSystem.dll</HintPath>           <Private>false</Private> </Reference>
    <Reference Include="Il2CppSystem.Core">      <HintPath>refs\Il2CppSystem.Core.dll</HintPath>      <Private>false</Private> </Reference>
  </ItemGroup>

</Project>
```

- [ ] **Step 0.10: Write `src/Plugin.cs` placeholder so build succeeds**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\src" | Out-Null
```

Create `C:\dev\CheatMenu-v2\src\Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    public override void Load()
    {
        Log.LogInfo($"{Name} v{Version} placeholder — Task 0 scaffold");
    }
}
```

- [ ] **Step 0.11: Build to verify references resolve**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
dotnet build CheatMenuV2.csproj -c Release
```

Expected output: `Build succeeded.` with `0 Warning(s)`, `0 Error(s)`. Output DLL at `bin\Release\CheatMenu-v2.dll`.

If you see `error CS0246: The type or namespace name 'BasePlugin' could not be found` → `refs/` is incomplete; re-run sync-refs.ps1 and verify all expected DLLs are present.

- [ ] **Step 0.12: Initial commit**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
git add .
git commit -m "Task 0: bootstrap repo, refs sync script, project scaffolding"
```

**Rollback notes:** No deployed artifacts yet. To roll back: `Remove-Item -Recurse -Force C:\dev\CheatMenu-v2`.

---

## Task 1: Plugin entrypoint with real Load() logging

**Files:**
- Modify: `src/Plugin.cs`

- [ ] **Step 1.1: Replace `src/Plugin.cs` with the real entrypoint**

```csharp
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    internal static new BepInEx.Logging.ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");
    }
}
```

- [ ] **Step 1.2: Build**

```powershell
dotnet build CheatMenuV2.csproj -c Release
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 1.3: Deploy DLL into game's BepInEx/plugins**

```powershell
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 1.4: Smoke test — launch game, observe log**

Launch `HeroesOldenEra.exe` through Steam (or directly). Wait until the main menu appears. Quit the game. Open `c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\LogOutput.log`.

Search for these lines (use Grep or Ctrl-F):
- `1 plugin to load` (was `0 plugins to load` in v1)
- `[Info   :   BepInEx] Loading [CheatMenu-v2 0.1.0]`
- `[Info   :CheatMenuV2] Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build 2026-04-30`
- `[Info   :CheatMenuV2] GUID=com.velebit.cheatmenuv2 Version=0.1.0`

If all four lines present: **Phase A criterion #1 SATISFIED**. Proceed.

If you see `[Error :   BepInEx] Could not load [CheatMenu-v2 0.1.0]` followed by a stack trace — capture the trace, paste into `docs/recon-notes.md` under a new "Task 1 load failure" section, and stop. Most likely cause: a reference DLL version mismatch; re-run `sync-refs.ps1`.

- [ ] **Step 1.5: Commit**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
git add src/Plugin.cs
git commit -m "Task 1: real Plugin entrypoint with version + build logging"
```

**Rollback notes:** to remove from game, delete `<game>/BepInEx/plugins/CheatMenu-v2.dll`. v1's broken `CheatMenu.dll` remains untouched.

---

## Task 2: MenuHostBehaviour + DontDestroyOnLoad host GameObject

**Files:**
- Create: `src/UI/MenuHostBehaviour.cs`
- Modify: `src/Plugin.cs`

- [ ] **Step 2.1: Create `src/UI/MenuHostBehaviour.cs`**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\src\UI" | Out-Null
```

Create `C:\dev\CheatMenu-v2\src\UI\MenuHostBehaviour.cs`:

```csharp
using UnityEngine;

namespace CheatMenuV2.UI;

/// Marker component on the plugin's host GameObject. Lets us find the host across scene loads.
public class MenuHostBehaviour : MonoBehaviour
{
    public MenuHostBehaviour(System.IntPtr ptr) : base(ptr) { }
}
```

(The `IntPtr` constructor is the Il2CppInterop idiom for IL2CPP-injected `MonoBehaviour`s.)

- [ ] **Step 2.2: Modify `src/Plugin.cs` to register, instantiate, and DontDestroyOnLoad the host**

Replace `src/Plugin.cs` with:

```csharp
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CheatMenuV2.UI;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    internal static new BepInEx.Logging.ManualLogSource Log = null!;
    internal static GameObject Host = null!;

    public override void Load()
    {
        Log = base.Log;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");

        // §4.5 load-bearing rule: register every MonoBehaviour subclass before AddComponent.
        ClassInjector.RegisterTypeInIl2Cpp<MenuHostBehaviour>();

        Host = new GameObject($"{Name}-Host");
        Host.AddComponent<MenuHostBehaviour>();
        UnityEngine.Object.DontDestroyOnLoad(Host);
        Log.LogInfo("Host GameObject created and marked DontDestroyOnLoad");
    }
}
```

- [ ] **Step 2.3: Build + deploy**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 2.4: Smoke test — host creation, no crash**

Launch game. Wait for main menu. Quit. Open `LogOutput.log`. Search for:
- `[Info   :CheatMenuV2] Host GameObject created and marked DontDestroyOnLoad`

No exception traces. Game reaches main menu normally.

If Unity crashes immediately on launch with a stack trace mentioning `IL2CPP_RUNTIME_CLASS_INIT` or `il2cpp_object_new` for `MenuHostBehaviour` → registration didn't fire before instantiation. Verify the order in `Load()`: `RegisterTypeInIl2Cpp` MUST come before `AddComponent`.

- [ ] **Step 2.5: Commit**

```powershell
git add src/UI/MenuHostBehaviour.cs src/Plugin.cs
git commit -m "Task 2: MenuHostBehaviour + DontDestroyOnLoad host GameObject"
```

**Rollback notes:** standard.

---

## Task 3: OverlayUI status strip (always-on, Probing mode)

**Files:**
- Create: `src/UI/OverlayUI.cs`
- Modify: `src/Plugin.cs` (register + attach)

- [ ] **Step 3.1: Create `src/UI/OverlayUI.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheatMenuV2.UI;

public class OverlayUI : MonoBehaviour
{
    public OverlayUI(IntPtr ptr) : base(ptr) { }

    // Public state — set by MenuCoordinator in later tasks.
    public string ModeText { get; set; } = "Probing";

    // Flash messages: short-lived overlay text.
    private string? _flashText;
    private float   _flashUntilTime;

    // Per-frame record of drawn rects, used for click-through prevention (Task 12).
    private readonly List<Rect> _rectsThisFrame = new(8);
    public IReadOnlyList<Rect> ConsumedRectsThisFrame => _rectsThisFrame;

    public void Flash(string message, float seconds = 2f)
    {
        _flashText      = message;
        _flashUntilTime = Time.unscaledTime + seconds;
    }

    private void OnGUI()
    {
        _rectsThisFrame.Clear();

        var label = $"CheatMenu-v2 | Mode: {ModeText} | F1: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float width  = 360f;
        const float height = 24f;
        const float margin = 8f;
        var rect = new Rect(Screen.width - width - margin, margin, width, height);
        GUI.Box(rect, label);
        _rectsThisFrame.Add(rect);
    }
}
```

- [ ] **Step 3.2: Modify `src/Plugin.cs` to register OverlayUI and attach to host**

Replace the body of `Plugin.Load()` so it now registers and attaches OverlayUI. Full file:

```csharp
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CheatMenuV2.UI;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    internal static new BepInEx.Logging.ManualLogSource Log = null!;
    internal static GameObject  Host    = null!;
    internal static OverlayUI   Overlay = null!;

    public override void Load()
    {
        Log = base.Log;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");

        ClassInjector.RegisterTypeInIl2Cpp<MenuHostBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<OverlayUI>();

        Host    = new GameObject($"{Name}-Host");
        Host.AddComponent<MenuHostBehaviour>();
        Overlay = Host.AddComponent<OverlayUI>();
        UnityEngine.Object.DontDestroyOnLoad(Host);

        Log.LogInfo("Host GameObject created; OverlayUI attached");
    }
}
```

- [ ] **Step 3.3: Build + deploy**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 3.4: Smoke test — overlay visible on main menu**

Launch game. On the main menu screen, look at the **top-right corner**. You must see a box reading:

```
CheatMenu-v2 | Mode: Probing | F1: toggle
```

If visible: **Phase A criterion #2 SATISFIED**. Quit and proceed.

If not visible:
- Open `LogOutput.log`, search for `OverlayUI`. If you see exceptions during OnGUI → typo in IMGUI code; fix.
- If no log entries about Overlay attachment → registration didn't happen; check `Plugin.Load()` step ordering.

- [ ] **Step 3.5: Commit**

```powershell
git add src/UI/OverlayUI.cs src/Plugin.cs
git commit -m "Task 3: OverlayUI status strip (Probing mode)"
```

**Rollback notes:** standard.

---

## Task 4: InputGate with F1 polling, suppression, and flash

**Files:**
- Create: `src/Input/InputGate.cs`
- Modify: `src/Plugin.cs` (register + attach)

- [ ] **Step 4.1: Create `src/Input/InputGate.cs`**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\src\Input" | Out-Null
```

Create `C:\dev\CheatMenu-v2\src\Input\InputGate.cs`:

```csharp
using System;
using CheatMenuV2.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMP_InputField = TMPro.TMP_InputField;  // available via Hex.dll's reference graph

namespace CheatMenuV2.Input;

public class InputGate : MonoBehaviour
{
    public InputGate(IntPtr ptr) : base(ptr) { }

    public KeyCode Toggle = KeyCode.F1;

    // Wired in Task 6 — for now, just flash via the overlay.
    public Action? OnTogglePressed;

    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(Toggle) && !IsModifierHeld() && !IsTextInputFocused())
        {
            if (OnTogglePressed != null) OnTogglePressed();
            else Plugin.Overlay.Flash($"{Toggle} pressed (no handler yet)");
        }
    }

    private static bool IsModifierHeld()
    {
        return UnityEngine.Input.GetKey(KeyCode.LeftShift)   || UnityEngine.Input.GetKey(KeyCode.RightShift)
            || UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)
            || UnityEngine.Input.GetKey(KeyCode.LeftAlt)     || UnityEngine.Input.GetKey(KeyCode.RightAlt);
    }

    private static bool IsTextInputFocused()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var sel = es.currentSelectedGameObject;
        if (sel == null) return false;
        if (sel.GetComponent<InputField>() != null) return true;
        if (sel.GetComponent<TMP_InputField>() != null) return true;
        return false;
    }
}
```

If TMPro reference resolution fails at build time, **drop the TMPro check**: change the `using` line to `// TMPro not referenced — only legacy InputField suppression` and remove the `TMP_InputField` line in `IsTextInputFocused`. Legacy InputField coverage is sufficient for Phase A; we revisit in Phase B if needed.

**Known Phase A gap (deferred to Phase B):** spec §4.2 also calls for suppressing F1 while the game's pause menu is open. We don't implement this here because the pause-menu type name isn't yet identified in `Hex.dll` and recon for it is out of scope for proof-of-life. Practical impact: opening the cheat menu from the pause screen works (F1 fires the toggle anyway). Documented in `docs/recon-notes.md` for Phase B.

- [ ] **Step 4.2: Modify `src/Plugin.cs` to register InputGate**

Add `ClassInjector.RegisterTypeInIl2Cpp<InputGate>();` after the OverlayUI registration line, and attach after host creation. Full updated `Load()` body — leave the file's class declaration / using lines unchanged from Task 3, add `using CheatMenuV2.Input;` at top, replace the post-log block with:

```csharp
        ClassInjector.RegisterTypeInIl2Cpp<MenuHostBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<OverlayUI>();
        ClassInjector.RegisterTypeInIl2Cpp<InputGate>();

        Host    = new GameObject($"{Name}-Host");
        Host.AddComponent<MenuHostBehaviour>();
        Overlay = Host.AddComponent<OverlayUI>();
        Input   = Host.AddComponent<InputGate>();
        UnityEngine.Object.DontDestroyOnLoad(Host);

        Log.LogInfo("Host GameObject created; OverlayUI + InputGate attached");
```

And add the static field at top of class alongside the others:

```csharp
    internal static InputGate Input = null!;
```

- [ ] **Step 4.3: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 4.4: Smoke test — F1 produces flash, no flash when typing**

Launch game. On main menu, press F1. The status strip should append `| F1 pressed (no handler yet)` for ~2 seconds.

Then load any save (or start skirmish). Open any in-game text input (rename hero, chat box, save filename). Press F1 — flash should NOT appear (suppression working).

If F1 has no effect at all: open `LogOutput.log` to see if InputGate.Update is being called. If TMPro reference broke the build, you should have already dropped that line per step 4.1's note.

- [ ] **Step 4.5: Commit**

```powershell
git add src/Input/InputGate.cs src/Plugin.cs
git commit -m "Task 4: InputGate with F1 polling, modifier + input-field suppression"
```

**Rollback notes:** standard.

---

## Task 5: BepInEx config for hotkey persistence

**Files:**
- Modify: `src/Plugin.cs`

- [ ] **Step 5.1: Add config entry, pass to InputGate**

Modify `src/Plugin.cs` — at top of class add:

```csharp
    internal static BepInEx.Configuration.ConfigEntry<KeyCode> ToggleKey = null!;
```

In `Load()`, *before* registering types, read the config:

```csharp
        ToggleKey = Config.Bind(
            section:  "Hotkeys",
            key:      "Toggle",
            defaultValue: KeyCode.F1,
            description: "Key that toggles the cheat menu. Modifier keys (Shift/Ctrl/Alt) are ignored.");
        Log.LogInfo($"Toggle hotkey: {ToggleKey.Value}");
```

In `Load()`, *after* `Input = Host.AddComponent<InputGate>();`, add:

```csharp
        Input.Toggle = ToggleKey.Value;
        ToggleKey.SettingChanged += (_, _) => { Input.Toggle = ToggleKey.Value; Log.LogInfo($"Toggle hotkey changed to {ToggleKey.Value}"); };
```

- [ ] **Step 5.2: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 5.3: Smoke test — config file created, F1 still works**

Launch game. Quit. Verify file exists: `c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\config\CheatMenuV2.cfg`. Open it; should contain a `[Hotkeys]` section with `Toggle = F1`.

Edit the config to `Toggle = F2`, save, launch game. F2 should now produce the flash. F1 should not.

Reset to F1 before next task.

- [ ] **Step 5.4: Commit**

```powershell
git add src/Plugin.cs
git commit -m "Task 5: persistent hotkey config (BepInEx ConfigEntry<KeyCode>)"
```

**Rollback notes:** delete `<game>/BepInEx/config/CheatMenuV2.cfg` to clear user configuration.

---

## Task 6: MenuCoordinator skeleton (Mode state, no panels yet)

**Files:**
- Create: `src/MenuCoordinator.cs`
- Modify: `src/Plugin.cs` (instantiate, wire into InputGate.OnTogglePressed)
- Modify: `src/UI/OverlayUI.cs` (read `MenuCoordinator.Mode` for ModeText)

- [ ] **Step 6.1: Create `src/MenuCoordinator.cs`**

```csharp
using CheatMenuV2.UI;

namespace CheatMenuV2;

public enum CheatMode { Probing, Native, Fallback, Failed }

public class MenuCoordinator
{
    public CheatMode Mode { get; private set; } = CheatMode.Probing;
    public bool      IsOpen { get; private set; }

    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        Open();
    }

    private void Open()
    {
        // Task 9 will replace this with: try NativePanelDriver.Open(); falls back to CommandDispatcher.
        // For now: flip to Failed so the overlay reflects we don't have backends wired.
        Mode = CheatMode.Failed;
        IsOpen = true;
        Plugin.Overlay.Flash("Menu open (no backend yet)");
        Plugin.Log.LogInfo($"Toggle ON — Mode={Mode}");
    }

    private void Close()
    {
        IsOpen = false;
        Plugin.Overlay.Flash("Menu closed");
        Plugin.Log.LogInfo("Toggle OFF");
    }
}
```

- [ ] **Step 6.2: Modify `src/UI/OverlayUI.cs` to render Mode from MenuCoordinator**

Change the `ModeText` property to a method that pulls from the coordinator. Replace `OnGUI()` with:

```csharp
    private void OnGUI()
    {
        _rectsThisFrame.Clear();

        var mode = Plugin.Coordinator?.Mode.ToString() ?? "Probing";
        var label = $"CheatMenu-v2 | Mode: {mode} | F1: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float width  = 360f;
        const float height = 24f;
        const float margin = 8f;
        var rect = new Rect(Screen.width - width - margin, margin, width, height);
        GUI.Box(rect, label);
        _rectsThisFrame.Add(rect);
    }
```

Delete the `public string ModeText { get; set; } = "Probing";` line — it's now dead.

- [ ] **Step 6.3: Modify `src/Plugin.cs` to instantiate MenuCoordinator and wire input**

Add static field:

```csharp
    internal static MenuCoordinator Coordinator = null!;
```

In `Load()`, after attaching `Input`:

```csharp
        Coordinator = new MenuCoordinator();
        Input.OnTogglePressed = () => Coordinator.Toggle();
```

- [ ] **Step 6.4: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 6.5: Smoke test — F1 flips Mode to Failed, log records toggle**

Launch game. Status strip starts with `Mode: Probing`. Press F1: strip should change to `Mode: Failed` and append `| Menu open (no backend yet)`. Press F1 again: still `Failed`, appends `| Menu closed`.

`LogOutput.log` should contain `Toggle ON — Mode=Failed` and `Toggle OFF`.

- [ ] **Step 6.6: Commit**

```powershell
git add src/MenuCoordinator.cs src/UI/OverlayUI.cs src/Plugin.cs
git commit -m "Task 6: MenuCoordinator skeleton with Mode state machine"
```

**Rollback notes:** standard.

---

## Task 7: Recon — locate dispatch site for `TypeCmdResourceCheat`

**Files:**
- Modify: `docs/recon-notes.md`

This is research. No production code changes. Output is a documentation artifact that informs Task 8.

- [ ] **Step 7.1: Run Cecil-grep to enumerate all methods consuming `TypeCmdResourceCheat`**

```powershell
$cecilDll = "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\core\Mono.Cecil.dll"
$tmp = "$env:TEMP\cheat_recon"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
Copy-Item -Force $cecilDll "$tmp\Mono.Cecil.dll"
Unblock-File "$tmp\Mono.Cecil.dll"
Copy-Item -Force "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\interop\Hex.dll" "$tmp\Hex.dll"
Unblock-File "$tmp\Hex.dll"
Add-Type -Path "$tmp\Mono.Cecil.dll"

$m = [Mono.Cecil.ModuleDefinition]::ReadModule("$tmp\Hex.dll")
foreach ($t in $m.Types) {
    foreach ($mm in $t.Methods) {
        $hasIt = $false
        foreach ($p in $mm.Parameters) { if ($p.ParameterType.Name -eq 'TypeCmdResourceCheat') { $hasIt = $true; break } }
        if ($hasIt) {
            $params = ($mm.Parameters | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', '
            $vis = if ($mm.IsPublic) { 'public' } elseif ($mm.IsAssembly) { 'internal' } elseif ($mm.IsFamily) { 'protected' } else { 'private' }
            Write-Output ("{0,-10} {1} {2}.{3}({4})" -f $vis, $mm.ReturnType.Name, $t.FullName, $mm.Name, $params)
        }
    }
}
$m.Dispose()
```

Capture the full output. There will likely be 1–5 hits.

- [ ] **Step 7.2: Identify the dispatcher**

The dispatcher is the simplest signature, ideally `public` with parameters like `(TypeCmdResourceCheat, int)` or `(TypeCmdResourceCheat, int, Player)`. It is **not**:
- A method whose name starts with `On*` (event handler — caller is the dispatcher)
- A method on a UI class like `BhResourcePanel` (caller is the dispatcher)
- A constructor (`.ctor`)

If the only hit is on a UI class: that UI class's `Click` or similar handler calls something internal — re-grep to find that internal target. Repeat until you find the lowest-level handler.

If nothing public exists at any level: record the most-private candidate and note that Task 8 will use the Harmony reverse-patch path (spec §6.2 step 4).

- [ ] **Step 7.3: Record findings in `docs/recon-notes.md`**

Append to `C:\dev\CheatMenu-v2\docs\recon-notes.md`:

```markdown
## Task 7 — Resource cheat dispatch site

**Date:** <YYYY-MM-DD>
**Game build:** <last-write date of HeroesOldenEra.exe>
**Hex.dll size:** <bytes>

### Cecil-grep hits

```
<paste the full PowerShell output from step 7.1>
```

### Selected dispatcher

- **Type.Method:** `<full type name>.<method name>`
- **Signature:** `<return type> Method(<param list>)`
- **Visibility:** `<public|internal|private>`
- **Strategy for Task 8:** `<direct call | inline through wrapper | Harmony reverse-patch>`
- **Notes:** <anything weird — e.g., method also takes a network-replicate flag, or is generic, or is on a singleton accessed via `Hex.Session.Instance`>

### Resource HUD verification target

When we test Task 11, we expect these resources to tick up: gold, wood, ore, mercury, sulfur, crystal, gems. Confirm by inspection of `Hex.Session.Data.Resource` enum/struct (run a follow-up Cecil dump if needed).
```

- [ ] **Step 7.4: Commit**

```powershell
Set-Location "C:\dev\CheatMenu-v2"
git add docs/recon-notes.md
git commit -m "Task 7: recon — dispatch site for TypeCmdResourceCheat documented"
```

**Rollback notes:** purely documentation. No deployed artifacts.

---

## Task 8: CommandDispatcher with `EnsureLocated()` + `AddAllResources()`

**Files:**
- Create: `src/Commands/CommandDispatcher.cs`
- Modify: `src/Plugin.cs`

The exact body of `Dispatch()` depends on Task 7's findings. Pick **Branch A** (public direct call) or **Branch B** (Harmony reverse-patch) based on `docs/recon-notes.md`.

- [ ] **Step 8.1: Create `src/Commands/CommandDispatcher.cs` (skeleton common to both branches)**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\src\Commands" | Out-Null
```

Create `C:\dev\CheatMenu-v2\src\Commands\CommandDispatcher.cs`:

```csharp
using System;
using Il2CppHex.Session;  // namespace from interop wrapper
// Branch B only:
// using HarmonyLib;
// using Il2CppInterop.HarmonySupport;

namespace CheatMenuV2.Commands;

public class CommandDispatcher
{
    private bool _located;
    private string? _failureReason;

    public bool EnsureLocated()
    {
        if (_located) return true;
        if (_failureReason != null) return false;

        try
        {
            LocateImpl();
            _located = true;
            Plugin.Log.LogInfo("CommandDispatcher located dispatch site");
            return true;
        }
        catch (Exception e)
        {
            _failureReason = e.Message;
            Plugin.Log.LogError($"CommandDispatcher failed to locate dispatch site: {e}");
            return false;
        }
    }

    public void AddAllResources(int amount)
    {
        if (!EnsureLocated()) { Plugin.Overlay.Flash("Dispatcher not located"); return; }
        Dispatch(TypeCmdResourceCheat.AddAll, amount);
    }

    // ── per-Branch members below ──
    private void LocateImpl()
    {
        // Branch A: nothing to locate at startup; method is publicly callable.
        // Branch B: capture native MethodInfo here.
        throw new NotImplementedException("Replace per Branch A or B in step 8.2");
    }

    private void Dispatch(TypeCmdResourceCheat cmd, int amount, object? target = null)
    {
        WarnIfMultiplayerRoleActive();

        // Branch A: direct call — see step 8.2 Branch A.
        // Branch B: invoke captured delegate — see step 8.2 Branch B.
        throw new NotImplementedException("Replace per Branch A or B in step 8.2");
    }

    private static void WarnIfMultiplayerRoleActive()
    {
        // Spec §2.2 + §5.5 — log a warning if the active player role is online/ranked.
        // Best-effort: locate Hex.Processing.Player.Role on the active session and check it.
        // For Phase A: emit a generic warning every time (better safe than sorry).
        Plugin.Log.LogWarning("Issuing cheat command — confirm you are NOT in multiplayer/ranked/leaderboard mode (spec §5.5)");
    }
}
```

- [ ] **Step 8.2: Implement `LocateImpl()` and `Dispatch()` per the branch chosen in Task 7**

**Branch A — public direct call** (use this if Task 7 found a public method like `Hex.Session.Logic.ResourceCheats.Apply(TypeCmdResourceCheat, int)`):

Replace the two method bodies:

```csharp
    private void LocateImpl()
    {
        // Direct call — nothing to locate. Validate the type/method exists by referencing it once.
        // (If the method is a static, this is trivial. If instance, we need to resolve the active session.)
        // Example assumes a static method on Hex.Session.Logic.ResourceCheats — replace with the actual symbol from recon-notes.md.
        var t = typeof(Il2CppHex.Session.Logic.ResourceCheats);
        if (t == null) throw new InvalidOperationException("ResourceCheats type not present in interop");
    }

    private void Dispatch(TypeCmdResourceCheat cmd, int amount, object? target = null)
    {
        WarnIfMultiplayerRoleActive();
        // REPLACE this call with the exact signature from Task 7.
        Il2CppHex.Session.Logic.ResourceCheats.Apply(cmd, amount);
        Plugin.Log.LogInfo($"Dispatched {cmd} amount={amount}");
    }
```

**Branch B — Harmony reverse-patch** (use this if no public path exists):

Add Harmony usings at top:

```csharp
using HarmonyLib;
using Il2CppInterop.HarmonySupport;
using System.Reflection;
```

Replace:

```csharp
    private static readonly Harmony _harmony = new("com.velebit.cheatmenuv2.dispatcher");
    private static MethodInfo? _capturedMethod;

    private void LocateImpl()
    {
        // Reverse-patch the private dispatch method's prefix to capture its MethodInfo.
        // Replace TYPE_FQN and METHOD_NAME with the values from recon-notes.md.
        var asm = typeof(TypeCmdResourceCheat).Assembly;
        var t = asm.GetType("Hex.Session.Logic.ResourceCheats")
            ?? throw new InvalidOperationException("Target type not found in interop");
        var m = t.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Target method not found");
        _capturedMethod = m;
        // Capture native pointer + game build ID for patch-day debugging (spec §6.2):
        var exePath  = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var buildId  = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        var ptrField = m.DeclaringType?.GetField($"NativeMethodInfoPtr_{m.Name}_Public_Static_Void_TypeCmdResourceCheat_Int32_0",
                            BindingFlags.Static | BindingFlags.NonPublic);
        var nativePtr = ptrField?.GetValue(null) ?? IntPtr.Zero;
        Plugin.Log.LogWarning(
            $"Harmony reverse-patch path active for {t.FullName}.{m.Name} on game build {buildId}, " +
            $"native pointer={nativePtr} — fragile across game patches; re-RE if this method's signature changes");
    }

    private void Dispatch(TypeCmdResourceCheat cmd, int amount, object? target = null)
    {
        WarnIfMultiplayerRoleActive();
        if (_capturedMethod == null) throw new InvalidOperationException("Dispatcher not located");
        // Use Il2CppInterop's invoke path — never call MethodInfo.Invoke directly.
        _capturedMethod.Invoke(null, new object[] { cmd, amount });
        Plugin.Log.LogInfo($"Dispatched {cmd} amount={amount} (Harmony path)");
    }
```

(In Branch B's `Invoke` line: this works ONLY because Il2CppInterop's `HarmonySupport` rewrites `MethodInfo.Invoke` to route through `il2cpp_runtime_invoke` for IL2CPP-resolved methods. Verify by checking that the captured `MethodInfo`'s declaring assembly is in `BepInEx/interop/`. If you see a `NullReferenceException` in native code on invoke, you have to switch to a generated delegate — see [Il2CppInterop docs](https://github.com/BepInEx/Il2CppInterop) `DelegateSupport.ConvertDelegate`.)

- [ ] **Step 8.3: Wire CommandDispatcher into MenuCoordinator and Plugin**

Modify `src/Plugin.cs` — add static field:

```csharp
    internal static Commands.CommandDispatcher Dispatcher = null!;
```

In `Load()`, after `Coordinator = new MenuCoordinator();`:

```csharp
        Dispatcher = new Commands.CommandDispatcher();
```

Modify `src/MenuCoordinator.cs` `Open()` so that **for now** it tries the dispatcher and flashes the result (the proper Native-then-Fallback flow lands in Task 9):

```csharp
    private void Open()
    {
        IsOpen = true;
        if (Plugin.Dispatcher.EnsureLocated())
        {
            Mode = CheatMode.Fallback;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode} (dispatcher only, no native panel yet)");
        }
        else
        {
            Mode = CheatMode.Failed;
            Plugin.Log.LogError("Toggle ON — both backends unavailable");
        }
        Plugin.Overlay.Flash($"Mode={Mode}");
    }
```

- [ ] **Step 8.4: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

If build fails on `Il2CppHex.Session` or `Il2CppHex.Session.Logic.ResourceCheats` not found: the namespace prefix Il2CppInterop generates may differ. Open `refs\Hex.dll` with a metadata viewer (or rerun a Cecil dump) to confirm the **actual** namespace assigned to types from `Hex.Session.*` after Il2CppInterop processing. Adjust `using` and fully-qualified names accordingly.

- [ ] **Step 8.5: Smoke test — F1 in a loaded game logs successful dispatch (no resource change yet — UI button comes in Task 11)**

Launch game. Load a save (skirmish/campaign). Press F1. Status strip should flip to `Mode: Fallback`. Log should contain `CommandDispatcher located dispatch site`.

(No resource change expected yet — `AddAllResources` is wired but no UI calls it. We're confirming `EnsureLocated()` works.)

If `LocateImpl` throws: Task 7 selected the wrong symbol. Re-do recon and adjust step 8.2 accordingly.

- [ ] **Step 8.6: Commit**

```powershell
git add src/Commands/CommandDispatcher.cs src/Plugin.cs src/MenuCoordinator.cs
git commit -m "Task 8: CommandDispatcher with located dispatch site, no UI yet"
```

**Rollback notes:** if Branch B (Harmony) is in use and patches survive across game restarts in unexpected ways, `Harmony.UnpatchAll("com.velebit.cheatmenuv2.dispatcher")` from any Plugin shutdown hook would clear them — but Phase A doesn't add a shutdown hook and BepInEx 6 doesn't fire one anyway (process tear-down clears it).

---

## Task 9: NativePanelDriver — context detection + tier 1 instantiation

**Files:**
- Create: `src/Native/NativePanelDriver.cs`
- Modify: `src/MenuCoordinator.cs` (try Native first)

- [ ] **Step 9.1: Create `src/Native/NativePanelDriver.cs`**

```powershell
New-Item -ItemType Directory -Force -Path "C:\dev\CheatMenu-v2\src\Native" | Out-Null
```

Create `C:\dev\CheatMenu-v2\src\Native\NativePanelDriver.cs`:

```csharp
using System;
using System.Linq;
using Il2CppHex.Cheat;            // namespace from interop wrapper for BhControllerCheat
using Il2CppHex.UI.City;          // for BhCityHeroesGroupView
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatMenuV2.Native;

public enum CheatContext { None, World, City, Battle }

public class NativePanelDriver
{
    public enum Result { Rendered, Failed }

    public bool         IsOpen          { get; private set; }
    public CheatContext CurrentContext  { get; private set; } = CheatContext.None;

    private BhControllerCheat? _cachedInstance;
    private string?            _failureReason;

    public Result Open()
    {
        CurrentContext = DetectContext();
        if (CurrentContext == CheatContext.None)
        {
            Plugin.Log.LogInfo("NativePanelDriver: no game session — skipping");
            return Result.Failed;
        }

        if (_cachedInstance == null && _failureReason == null)
            _cachedInstance = TryInstantiate();

        if (_cachedInstance == null)
            return Result.Failed;

        try
        {
            _cachedInstance.gameObject.SetActive(true);
            // hrf() is the native Show method on BhScreen subclasses (Cecil dump §1.2 of spec).
            _cachedInstance.hrf();
            IsOpen = true;
            Plugin.Log.LogInfo($"NativePanelDriver: rendered ({CurrentContext})");
            return Result.Rendered;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"NativePanelDriver.Open failed: {e}");
            return Result.Failed;
        }
    }

    public void Close()
    {
        if (_cachedInstance == null || !IsOpen) return;
        try
        {
            _cachedInstance.gik();   // native Hide
            _cachedInstance.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"NativePanelDriver.Close failed: {e}");
        }
        IsOpen = false;
    }

    public void InvalidateOnSceneChange()
    {
        _cachedInstance = null;
        _failureReason  = null;
        IsOpen = false;
        CurrentContext  = CheatContext.None;
    }

    private static CheatContext DetectContext()
    {
        var sceneName = SceneManager.GetActiveScene().name?.ToLowerInvariant() ?? "";
        if (sceneName.Contains("battle") || sceneName.Contains("arena"))
            return CheatContext.Battle;

        // Heuristic for "in a session": presence of any BhCityHeroesGroupView in the scene.
        // If we can't find any object at all, we're probably on a main-menu scene.
        var cityViews = Resources.FindObjectsOfTypeAll<BhCityHeroesGroupView>();
        if (cityViews.Length == 0)
        {
            // Main menu vs. world: check sceneName against known main-menu names.
            if (sceneName.Contains("menu") || sceneName.Contains("title") || sceneName.Contains("loading"))
                return CheatContext.None;
            // Default to World if we can't tell.
            return CheatContext.World;
        }

        // Any city view active in hierarchy → City context.
        foreach (var cv in cityViews)
            if (cv != null && cv.gameObject.activeInHierarchy)
                return CheatContext.City;

        return CheatContext.World;
    }

    private BhControllerCheat? TryInstantiate()
    {
        // Tier 1: existing-but-disabled instance left in shipped scenes.
        var existing = Resources.FindObjectsOfTypeAll<BhControllerCheat>();
        if (existing != null && existing.Length > 0)
        {
            Plugin.Log.LogInfo($"NativePanelDriver tier 1 hit: found {existing.Length} BhControllerCheat instance(s)");
            return existing[0];
        }
        Plugin.Log.LogInfo("NativePanelDriver tier 1 miss: no BhControllerCheat in loaded scenes");

        // Tiers 2 + 3 land in Task 10.
        _failureReason = "tier 1 miss; tier 2/3 not implemented";
        return null;
    }
}
```

If the build complains about the `Il2CppHex.Cheat` namespace prefix: as in Task 8, the actual prefix Il2CppInterop applies may differ. Open `refs\Hex.dll` with a metadata viewer (or `ildasm /text refs\Hex.dll | head -100`) to confirm.

- [ ] **Step 9.2: Wire NativePanelDriver into MenuCoordinator (try Native first)**

Replace `Open()` in `src/MenuCoordinator.cs`:

```csharp
    public Native.NativePanelDriver Native = new();

    private void Open()
    {
        IsOpen = true;

        var nativeResult = Native.Open();
        if (nativeResult == Native.NativePanelDriver.Result.Rendered)
        {
            Mode = CheatMode.Native;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode}");
            Plugin.Overlay.Flash($"Mode={Mode}");
            return;
        }

        if (Plugin.Dispatcher.EnsureLocated())
        {
            Mode = CheatMode.Fallback;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode} (native unavailable)");
        }
        else
        {
            Mode = CheatMode.Failed;
            Plugin.Log.LogError("Toggle ON — both backends unavailable");
        }
        Plugin.Overlay.Flash($"Mode={Mode}");
    }

    private void Close()
    {
        IsOpen = false;
        if (Mode == CheatMode.Native) Native.Close();
        Plugin.Overlay.Flash("Menu closed");
        Plugin.Log.LogInfo("Toggle OFF");
    }
```

Add scene-change subscriber inside `MenuCoordinator`'s constructor — declare a constructor and subscribe:

```csharp
    public MenuCoordinator()
    {
        SceneManager.add_activeSceneChanged((Il2CppSystem.Action<Scene, Scene>)((_, _) =>
        {
            Native.InvalidateOnSceneChange();
            if (IsOpen) Close();
        }));
    }
```

(The Il2CppInterop pattern uses `add_activeSceneChanged` with an `Il2CppSystem.Action<,>` cast. If your Il2CppInterop version uses a different idiom, see `Il2CppInterop.Runtime.Injection.DelegateSupport`.)

- [ ] **Step 9.3: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 9.4: Smoke test — F1 either renders Native or falls to Fallback, decision logged**

Launch game, load a save, press F1. One of these must happen:

- **Native success**: native cheat panel appears on screen (looks like the rest of the game's UI). Status strip says `Mode: Native`. Log says `NativePanelDriver tier 1 hit ... rendered (World|City|Battle)`.
- **Fallback**: log says `NativePanelDriver tier 1 miss`. Status strip says `Mode: Fallback`. (No visible button strip yet — that's Task 11.)

Either outcome **satisfies Phase A criterion #3**.

If neither happens (game crashes, or Mode goes to `Failed`): inspect the log for the exception. Most likely `Il2CppHex.Cheat` namespace mismatch (see step 9.1 note).

- [ ] **Step 9.5: Commit**

```powershell
git add src/Native/NativePanelDriver.cs src/MenuCoordinator.cs
git commit -m "Task 9: NativePanelDriver tier 1 + context detection + scene-change invalidation"
```

**Rollback notes:** standard.

---

## Task 10: NativePanelDriver tiers 2 + 3 (Addressables, Resources.Load fallback)

**Files:**
- Modify: `src/Native/NativePanelDriver.cs`

If Task 9 already returned `Native` mode on the test save, you can SKIP this task — tier 1 is sufficient. Mark all steps complete and commit a no-op message OR move directly to Task 11. Document the skip in `docs/recon-notes.md`.

- [ ] **Step 10.1: Add tiers 2 and 3 to `TryInstantiate`**

In `src/Native/NativePanelDriver.cs`, replace `TryInstantiate()`:

```csharp
    private BhControllerCheat? TryInstantiate()
    {
        // Tier 1
        var existing = Resources.FindObjectsOfTypeAll<BhControllerCheat>();
        if (existing != null && existing.Length > 0)
        {
            Plugin.Log.LogInfo($"NativePanelDriver tier 1 hit: found {existing.Length} BhControllerCheat instance(s)");
            return existing[0];
        }
        Plugin.Log.LogInfo("NativePanelDriver tier 1 miss");

        // Tier 2: Addressables (Olden Era uses them — confirmed by StreamingAssets layout)
        foreach (var key in new[] { "BhControllerCheat", "CheatPanel", "Cheat/BhControllerCheat", "UI/CheatPanel" })
        {
            try
            {
                var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(key);
                op.WaitForCompletion();
                var prefab = op.Result;
                if (prefab != null)
                {
                    Plugin.Log.LogInfo($"NativePanelDriver tier 2 hit: Addressable key '{key}'");
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    var comp = instance.GetComponent<BhControllerCheat>();
                    if (comp != null) return comp;
                    Plugin.Log.LogWarning($"NativePanelDriver tier 2: prefab '{key}' has no BhControllerCheat component");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"NativePanelDriver tier 2 key '{key}' miss: {e.Message}");
            }
        }
        Plugin.Log.LogInfo("NativePanelDriver tier 2 miss");

        // Tier 3: legacy Resources.Load
        foreach (var path in new[] { "CheatPanel", "UI/CheatPanel", "Prefabs/CheatPanel" })
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                Plugin.Log.LogInfo($"NativePanelDriver tier 3 hit: Resources.Load '{path}'");
                var instance = UnityEngine.Object.Instantiate(prefab);
                var comp = instance.GetComponent<BhControllerCheat>();
                if (comp != null) return comp;
            }
        }
        Plugin.Log.LogInfo("NativePanelDriver tier 3 miss");

        _failureReason = "all tiers miss";
        return null;
    }
```

If `UnityEngine.AddressableAssets` namespace isn't available with the current refs, add the corresponding DLL to `tools/sync-refs.ps1` (`Unity.Addressables.dll` from `BepInEx/interop/`) and re-run sync. Then add `<Reference Include="Unity.Addressables">` to the csproj.

If Addressables aren't available at all in this build, **delete the tier 2 block entirely** and rely on tiers 1 + 3 only.

- [ ] **Step 10.2: Build + deploy + smoke test**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

Launch, load save, press F1. Inspect log for which tier hit (or all missed). Document the result in `docs/recon-notes.md` under a "Task 10 — instantiation tiers" section so future debugging is faster.

- [ ] **Step 10.3: Commit**

```powershell
git add src/Native/NativePanelDriver.cs docs/recon-notes.md
git commit -m "Task 10: NativePanelDriver tiers 2 + 3 (Addressables + Resources fallback)"
```

If `tools/sync-refs.ps1` was updated in step 10.1: also add it to the commit.

**Rollback notes:** standard.

---

## Task 11: OverlayUI fallback button strip — single +10000 button

**Files:**
- Modify: `src/UI/OverlayUI.cs`

This is the task that delivers Phase A criterion #4 in Fallback mode. If Task 9/10 already gave you `Mode: Native`, the criterion is satisfied through the native panel — but build this anyway as a safety net for testing.

- [ ] **Step 11.1: Add fallback panel rendering to `OverlayUI`**

In `src/UI/OverlayUI.cs`, replace the entire `OnGUI()` with:

```csharp
    private void OnGUI()
    {
        _rectsThisFrame.Clear();

        // ── Status strip (always on) ──
        var mode = Plugin.Coordinator?.Mode.ToString() ?? "Probing";
        var label = $"CheatMenu-v2 | Mode: {mode} | F1: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float stripW = 360f, stripH = 24f, margin = 8f;
        var stripRect = new Rect(Screen.width - stripW - margin, margin, stripW, stripH);
        GUI.Box(stripRect, label);
        _rectsThisFrame.Add(stripRect);

        // ── Fallback button strip (only when Mode == Fallback && IsOpen) ──
        var coord = Plugin.Coordinator;
        if (coord == null || coord.Mode != CheatMode.Fallback || !coord.IsOpen) return;

        const float panelW = 240f, panelH = 80f;
        var panelRect = new Rect(margin, margin, panelW, panelH);
        GUI.Box(panelRect, "Cheats (Fallback)");
        _rectsThisFrame.Add(panelRect);

        var btnRect = new Rect(margin + 8, margin + 28, panelW - 16, 32);
        if (GUI.Button(btnRect, "+10000 Gold (all resources)"))
        {
            Plugin.Dispatcher.AddAllResources(10000);
            Flash("+10000 res", 1.5f);
        }
        _rectsThisFrame.Add(btnRect);
    }
```

Add a `using CheatMenuV2;` at the top of `OverlayUI.cs` if it's not there already (so `CheatMode` resolves).

- [ ] **Step 11.2: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 11.3: Smoke test — Phase A criterion #4**

Launch game. **Single-player skirmish only** (per spec §5.5).

If you ended up in Native mode in Task 9: open the native panel via F1, find the resource panel's `+Gold` button (or AddAll button), click it. Confirm resource HUD increments.

If you're in Fallback mode: press F1. The button "+10000 Gold (all resources)" appears top-left. Click it. The resource HUD at the top of the screen should tick up by ~10000 across all resource types (gold, wood, ore, mercury, sulfur, crystal, gems).

If resources increase: **Phase A criterion #4 SATISFIED**. All four criteria are now satisfied — Phase A is functionally complete.

If button visible but resources don't change: check log for `Dispatched AddAll amount=10000`. If logged but no change → Task 7 selected the wrong dispatch site (likely a no-op or a UI-internal event). Re-do recon and re-implement Task 8.

- [ ] **Step 11.4: Commit**

```powershell
git add src/UI/OverlayUI.cs
git commit -m "Task 11: OverlayUI fallback button strip with +10000 resources action"
```

**Rollback notes:** standard.

---

## Task 12: Click-through prevention

**Files:**
- Modify: `src/Input/InputGate.cs`

Without this, clicking the cheat button on the world map sends a move command to the hero underneath. This is the spec §6.3 "click-through prevention" requirement.

- [ ] **Step 12.1: Add click-eating to `InputGate.Update`**

In `src/Input/InputGate.cs`, replace `Update()`:

```csharp
    private void Update()
    {
        // Hotkey toggle
        if (UnityEngine.Input.GetKeyDown(Toggle) && !IsModifierHeld() && !IsTextInputFocused())
        {
            if (OnTogglePressed != null) OnTogglePressed();
            else Plugin.Overlay.Flash($"{Toggle} pressed (no handler yet)");
        }

        // Click-through prevention: if any mouse button is down and the cursor sits over our IMGUI,
        // swallow the input so the game's raycasters don't see it.
        if (UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2))
        {
            if (CursorOverOurUI())
                UnityEngine.Input.ResetInputAxes();
        }
    }

    private static bool CursorOverOurUI()
    {
        // Check 1: EventSystem (catches uGUI elements layered above us)
        var es = EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return true;

        // Check 2: any of OverlayUI's drawn rects this frame
        var rects = Plugin.Overlay?.ConsumedRectsThisFrame;
        if (rects == null || rects.Count == 0) return false;

        // IMGUI uses top-left origin; Input.mousePosition uses bottom-left. Convert.
        var p = UnityEngine.Input.mousePosition;
        var imguiP = new Vector2(p.x, Screen.height - p.y);
        for (int i = 0; i < rects.Count; i++)
            if (rects[i].Contains(imguiP)) return true;

        return false;
    }
```

- [ ] **Step 12.2: Build + deploy**

```powershell
dotnet build CheatMenuV2.csproj -c Release
Copy-Item -Force "C:\dev\CheatMenu-v2\bin\Release\CheatMenu-v2.dll" "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\BepInEx\plugins\CheatMenu-v2.dll"
```

- [ ] **Step 12.3: Smoke test — clicking the button doesn't move the hero**

Launch game, load save, position your hero on the world map. Press F1. Click the +10000 button. Verify:
- Resources increase
- Hero does NOT path-move toward where the button was

Try clicking on the status strip (top-right) too — should not move the hero.

If hero still moves: the rect coordinate conversion may be wrong. Add `Plugin.Log.LogDebug($"cursorAt={imguiP} firstRect={rects[0]} contains={rects[0].Contains(imguiP)}");` inside `CursorOverOurUI` and re-test to see what's happening.

- [ ] **Step 12.4: Commit**

```powershell
git add src/Input/InputGate.cs
git commit -m "Task 12: click-through prevention (EventSystem + IMGUI rect checks)"
```

**Rollback notes:** standard.

---

## Task 13: deploy.ps1 + full Phase A verification + version tag

**Files:**
- Create: `C:\dev\CheatMenu-v2\tools\deploy.ps1`
- Modify: `CHANGELOG.md`

- [ ] **Step 13.1: Write `tools/deploy.ps1`**

Create `C:\dev\CheatMenu-v2\tools\deploy.ps1`:

```powershell
# Builds CheatMenu-v2 in Release config and copies the DLL into the game's BepInEx/plugins folder.
# Optional -Restart flag kills any running HeroesOldenEra process and relaunches via Steam.

param(
    [string] $GameRoot = "c:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era",
    [switch] $Restart
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

dotnet build CheatMenuV2.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$src = Join-Path $repoRoot 'bin\Release\CheatMenu-v2.dll'
$dst = Join-Path $GameRoot 'BepInEx\plugins\CheatMenu-v2.dll'
Copy-Item -Force $src $dst
Write-Host "Deployed: $dst"

if ($Restart)
{
    Get-Process -Name 'HeroesOldenEra' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    Start-Process 'steam://rungameid/3105440'  # AppId from LogOutput.log line 1
    Write-Host "Game restart requested via Steam"
}
```

- [ ] **Step 13.2: Run a full clean Phase A verification**

This is the binary acceptance test for Phase A.

1. Close the game if running.
2. From a fresh repo state, run: `.\tools\deploy.ps1`
3. Launch the game via Steam.
4. **Criterion #1** — In `<game>/BepInEx/LogOutput.log`, find:
   - `1 plugin to load`
   - `[Info   :CheatMenuV2] Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build 2026-04-30`

   ☐ Pass / ☐ Fail
5. **Criterion #2** — On the main menu, status strip visible top-right reading `CheatMenu-v2 | Mode: Probing | F1: toggle`.

   ☐ Pass / ☐ Fail
6. Load a single-player skirmish (offline only — spec §5.5).
7. **Criterion #3** — Press F1. Status flips to `Native` or `Fallback`. Log records the decision.

   ☐ Pass / ☐ Fail
8. **Criterion #4** — Either click the native panel's `+Gold/AddAll` button (Native mode) or the IMGUI `+10000 Gold (all resources)` button (Fallback mode). Resource HUD ticks up by ~10000 across all resources. No errors in log.

   ☐ Pass / ☐ Fail

If all four pass: **Phase A is COMPLETE.**

If any fail: do NOT proceed to tagging. Diagnose, fix, redeploy, re-verify.

- [ ] **Step 13.3: Update `CHANGELOG.md` and commit**

Edit `C:\dev\CheatMenu-v2\CHANGELOG.md` — change the `[0.1.0] - 2026-04-30` heading's date to today's date if different. Add a `Verified:` line listing the four criteria you confirmed.

```powershell
git add tools/deploy.ps1 CHANGELOG.md
git commit -m "Task 13: deploy script + Phase A verification recorded"
```

- [ ] **Step 13.4: Tag the release**

```powershell
git tag -a v0.1.0 -m "Phase A: proof of life — load + F1 toggle + +10000 all resources"
git log --oneline -1
git tag --list
```

Phase A shipped.

**Rollback notes:** to delete tag locally if it was created prematurely: `git tag -d v0.1.0`.

---

## Phase A done — Phase B promotion gate

Per spec §5.4 — Phase B work begins **only** after Task 13's four criteria all show ☐ Pass on a fresh game launch. The promotion path then forks based on which mode succeeded:

- **Mode: Native** → Phase B is mostly QA + docs. Native panel already exposes most of B's cheats.
- **Mode: Fallback** → Phase B builds out the IMGUI button strip — one button per cheat enum value, grouped by current context. New tasks 14+ would extend `CommandDispatcher` and `OverlayUI` only.

Phase B planning is out of scope for this document; create a fresh plan `2026-XX-XX-cheatmenu-v2-phase-b.md` once Phase A acceptance is confirmed.
