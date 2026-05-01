# CheatMenu-v2 — Design Spec

**Date:** 2026-04-30
**Target:** Heroes of Might and Magic: Olden Era (day-one build, `HeroesOldenEra.exe` 2026-04-30)
**Runtime:** BepInEx 6.0.0-be.755 IL2CPP, Unity 6000.0.66f1, .NET 6.0.7
**Status:** Approved for implementation

---

## 1. Context

### 1.1 Why v1 (`CheatMenu.dll`) does not load

The shipped `BepInEx/plugins/CheatMenu.dll` (4608 bytes) is rejected by BepInEx during the chainloader's metadata scan. `BepInEx/LogOutput.log` line 49 records `0 plugins to load` despite the file's presence. Inspection via `Mono.Cecil` reveals two facts:

1. The DLL references `BepInEx.Unity.Mono, Version=6.0.0.0` — but Olden Era runs **IL2CPP** (`GameAssembly.dll` + `Il2CppInterop` are present, Mono runtime is not). The author built v1 against the wrong runtime variant of BepInEx 6.
2. The DLL contains no `[BepInPlugin]`-attributed `BasePlugin` subclass and no human-readable strings. Whether that's a build accident or a side effect of the broken Mono reference preventing type enumeration is moot — it would never have loaded.

The original keyboard shortcut is **not recoverable** from the binary. v2 chooses its own.

### 1.2 What is already in the game

The game's interop assemblies (`BepInEx/interop/Hex.dll`, ~30 MB) ship with a complete, **clean-named** developer cheat panel under the `Hex.Cheat.*` and `Hex.Cheat.UI.*` namespaces. Notable existing classes:

- `Hex.Cheat.BhControllerCheat` — top-level cheat screen, holds slots for `worldCheatPanel`, `cityCheatPanel`, `battleCheatPanel`. Extends `Hex.UI.BhScreen`. Has `hrf()` (show) and `gik()` (hide) methods.
- `Hex.Cheat.UI.BhWindowCheatPanel`, `BhWorldCheatPanel`, `BhBattleCheatPanel`, `BhCityCheatPanel`, `BhResourcePanel`
- `Hex.Cheat.UI.ManagerGlobalTabs` with `OpenWorld()`, `OpenHeroes()`, `OpenMagic()`, `OpenGlobalBattle()`, `OpenLocalBattle()`
- State machines: `GlobalStateMashine`, `StateMachineAdditionWindow`, plus state classes for every cheat (`StateAddResource`, `StateAddSkill`, `StateCreateHero`, etc.)
- Pickers: `BhWindowHeroPick`, `BhWindowUnitPick`, `BhWindowArtefactPick`, `BhWindowSkillPick`, `BhWindowMagicPick`, `BhWindowSquadPick`, `BhWindowArtefactsSetPick`

Cheat *commands* are clean enums:

| Enum | Values |
|---|---|
| `Hex.Session.TypeCmdResourceCheat` | `Add`, `Substract`, `AddAll` |
| `Hex.Session.TypeCmdWorldCheats` | `AddMagicInGuild`, `AddAllMagicInGuild`, `AddAllMagicHeroNoRules`, `AddAllMagicHeroWithRules`, `EndlessCast`, `InfinityMana`, `AddSkill`, `AddAllArtefacts`, `AddSetArtefacts`, `AddArtefact`, `LevelUp`, `InfinityMovePoints`, `BattleAutoWin`, `Fly`, `RemoveBlockHireHero`, `DispelFogOfWar`, `CreateSquad`, `CreateRandomSquad`, `CreateHero`, `UpgradeFractionsLaws`, `UpgradeAstroPoints`, `WinGame`, `LoseGame` |
| `Hex.Session.TypeCmdBattleCheat` | `WinFight`, `LoseFight`, `AllSkipTurn`, `AddEnergyCurrentUnit`, `AddEnergyAllUnits`, `FreeUnitSpell`, `RefreshMana`, `UnlimitMaxMana`, `SetMana999`, `MagicBookResetCD`, `EndlessMagicCast`, `FreeMagicCast`, `EndlessSpellUnit`, `GodMode`, `KillUnitOnCursor`, `SpawnUnitOnCursor`, `TakeDamage`, `SetLowHp` |
| `Hex.Session.TypeCmdCityCheat` | `ConstructAllBuilds`, `UpdateGainUnits`, `ActivateCheatConstructionLimit` |

The plugin's job is to *re-expose* this existing dev tooling, not to reimplement it.

---

## 2. Goals & non-goals

### 2.1 Goals

- Ship a single `CheatMenu-v2.dll` that loads cleanly under BepInEx 6 IL2CPP on the day-one Olden Era build.
- Bind `F1` to toggle a cheat menu.
- Phase A: prove the load + toggle + one-cheat-action pipeline works end-to-end.
- Phase B (after Phase A passes): expose every command in the four `TypeCmd*Cheat` enums via either the native panel or an IMGUI fallback.
- Degrade gracefully: if the native panel can't render (prefabs stripped from shipped asset bundles), fall back to a code-only IMGUI button strip without restart.

### 2.2 Non-goals (explicit, do not regress into these)

- **No `Time.timeScale` modification.** The game uses Epic Online Services and supports multiplayer; pausing the game clock would risk desyncs in co-op or hotseat. The cheat menu is non-modal and the game keeps running underneath.
- **No multiplayer-safe testing.** Phase A and Phase B testing must be performed in **offline skirmish or single-player campaign only.** Olden Era ships with EOS and an analytics collector (`LogOutput.log:276`); cheat-triggered state changes may be reported. `CommandDispatcher.Dispatch()` logs a `[Warning]` if the active `Hex.Processing.Player` role is not offline.
- **No unit tests.** Every method in the plugin calls into Unity / IL2CPP runtime; meaningful unit testing is impossible. End-to-end testing is the four Phase A criteria, executed manually inside the game.
- **No input fields, sliders, or numeric pickers in Phase A or Phase B.** Fixed amounts only (10000 for resources). Configurable amounts are explicitly Phase C, not committed.
- **No new functionality in v1's namespace.** v2 ships under a fresh GUID (`com.velebit.cheatmenuv2`) and assembly name (`CheatMenu-v2`); both can coexist with the broken v1 file.

---

## 3. Architecture

### 3.1 Module split (five units, each with one responsibility)

| Unit | Type | Responsibility | Depends on |
|---|---|---|---|
| `Plugin` | `BasePlugin` | BepInEx entry point. Registers IL2CPP types, reads config, instantiates host GameObject, attaches behaviours. | BepInEx, Unity |
| `InputGate` | `MonoBehaviour` | Per-frame `Update()` polling for the toggle hotkey. Handles suppression contexts (input field focus, pause menu). Calls `MenuCoordinator.Toggle()`. | Unity Input, EventSystem |
| `NativePanelDriver` | POCO | Detects game context (World/City/Battle), instantiates `Hex.Cheat.BhControllerCheat`, shows the right sub-panel, reports success/failure. | `Hex.dll` interop |
| `CommandDispatcher` | POCO | Located the dispatch site for `TypeCmd*Cheat` enums. Exposes typed methods (`AddAllResources`, `LevelUpCurrentHero`, …). All public methods funnel through one `Dispatch()` chokepoint. | `Hex.dll` interop, optionally `0Harmony.dll` + `Il2CppInterop.HarmonySupport.dll` |
| `OverlayUI` | `MonoBehaviour` | IMGUI-only. Always-on status strip (top-right). When `Mode == Fallback`, renders the button strip. Records drawn `Rect`s for click-through prevention. | BepInEx IMGUI |
| `MenuCoordinator` | POCO | Orchestrator. On first toggle: tries `NativePanelDriver`, falls back to `CommandDispatcher` + `OverlayUI`. Caches the decision per-session. Owned by `InputGate`. | all above |

### 3.2 File layout

```
CheatMenu-v2/
├── CheatMenuV2.csproj
├── README.md                     # end-user
├── CHANGELOG.md
├── LICENSE                       # MIT
├── .gitignore
├── refs/                         # local DLL refs, gitignored
│   └── .gitkeep
├── src/
│   ├── Plugin.cs
│   ├── MenuCoordinator.cs
│   ├── Input/InputGate.cs
│   ├── Native/NativePanelDriver.cs
│   ├── Commands/CommandDispatcher.cs
│   ├── UI/
│   │   ├── OverlayUI.cs
│   │   └── MenuHostBehaviour.cs
│   └── Util/ContextDetection.cs  # extract iff it grows past one method
├── tools/
│   ├── sync-refs.ps1
│   └── deploy.ps1
└── docs/recon-notes.md
```

---

## 4. Hotkey, lifecycle, toggle behavior

### 4.1 Hotkey

- **Primary:** `F1` (unmodified). `Shift/Ctrl/Alt+F1` ignored.
- **Why F1:** No `F1`–`F12` reference appears in the cheat namespace; HoMM-series convention puts dev menus on F1.
- **Configurable:** persisted as `BepInEx.Configuration.ConfigEntry<KeyCode>` to `BepInEx/config/CheatMenuV2.cfg` (auto-created on first run). Change once, no recompile.

### 4.2 Suppression contexts

`InputGate` ignores the hotkey when:
- A `TMP_InputField` or `InputField` has keyboard focus (detected via `EventSystem.current.currentSelectedGameObject`).
- The game's main pause menu is open.

### 4.3 Plugin lifecycle (BepInEx 6 IL2CPP order of operations)

| Phase | Where | Actions |
|---|---|---|
| **Cold load** | `Plugin.Load()` | (1) Logger init. (2) `ClassInjector.RegisterTypeInIl2Cpp<InputGate>()`, `<OverlayUI>()`, `<MenuHostBehaviour>()`. (3) Read `KeyCode` config. (4) Create host `GameObject`, `host.AddComponent<MenuHostBehaviour>()`, `DontDestroyOnLoad(host)`. (5) `host.AddComponent<InputGate>()` then `<OverlayUI>()`. (6) Construct `MenuCoordinator` (POCO, held by `InputGate`). (7) Log `Loaded — …`. **Total: well under 50 ms. No game systems touched.** |
| **Idle (no save)** | `InputGate.Update()` | F1 still works; `MenuCoordinator.Toggle()` detects no `Hex.Session` and `OverlayUI.Flash("no game loaded", 2f)`. No crash. |
| **First in-game toggle** | `MenuCoordinator.Toggle()` | (1) `NativePanelDriver.Open()` → context detection + 3-tier instantiation. (2) If `Rendered` → cache `Mode = Native`. (3) If `Failed` → `CommandDispatcher.EnsureLocated()`, cache `Mode = Fallback` (the overlay reads `Mode` and renders the button strip on its own — see §6.3). (4) Both fail → cache `Mode = Failed`, overlay says `Cheat backend unavailable — see BepInEx log`. |
| **Subsequent toggles** | `MenuCoordinator.Toggle()` | Use cached `Mode`. |
| **Scene change** | subscriber on `SceneManager.activeSceneChanged` | Close any open menu, invalidate `NativePanelDriver`'s panel-instance cache. **Mode cache survives** (Native vs Fallback is per-build, not per-scene). |

### 4.4 Edge cases (decided up-front)

- **Open during cutscene/loading:** treated as no-game-state — flash, no panel.
- **Open over another panel:** native panel was designed to overlay; works naturally. Fallback's IMGUI strip is screen-space anchored and always works.
- **Open mid-battle:** `BhBattleCheatPanel` (Native) or battle-context buttons (Fallback). Different button set than world/city.
- **Multiple presses per frame:** `Input.GetKeyDown` is edge-triggered. No chatter.
- **`Time.timeScale`:** never touched. See §2.2.

### 4.5 IL2CPP type-registration rule (load-bearing)

**Every `MonoBehaviour` subclass shipped by this plugin must be registered via `ClassInjector.RegisterTypeInIl2Cpp<T>()` before any `AddComponent<T>()` call.** Skipping this crashes Unity instantly. Enforced in `Plugin.Load()` step (2). Any future `MonoBehaviour` added to the plugin must extend the registration block in step (2) before its first instantiation.

---

## 5. Phase A scope & promotion criterion

### 5.1 Phase A deliverables (exactly four)

1. **Plugin loads.** `LogOutput.log` shows `1 plugin to load` and `[Info :CheatMenuV2] Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra <buildId>`.
2. **Status overlay renders.** Top-right corner of main menu shows `CheatMenu-v2 | Mode: Probing | F1: toggle`.
3. **F1 toggles into a real game.** Inside a loaded save, F1 either renders `BhControllerCheat`'s appropriate sub-panel (status flips to `Mode: Native`) **or** enables the IMGUI fallback panel with one button (status flips to `Mode: Fallback`). Decision is logged.
4. **One cheat action works.** The single button (Native panel's `+Gold` or Fallback's `+10000 Gold (all resources)`) calls `CommandDispatcher.Dispatch(TypeCmdResourceCheat.AddAll, 10000)` and the resource HUD ticks up.

### 5.2 The single Phase A cheat: `TypeCmdResourceCheat.AddAll` (amount 10000)

Chosen because: simplest enum value (no target picker, no hero selection, applies to current player), result immediately visible in resource HUD, works in World context (most common test context), and exercises the full command pipeline including resource broadcasting / network sync — if it works, almost every other resource cheat works for free.

### 5.3 Explicit Phase A non-inclusions (deferred to B)

- Any cheat besides `+10000 all resources`
- Battle/city-context cheats
- Any picker UI
- Substract / negative cheats
- Multiple amount tiers
- Any styling beyond IMGUI defaults in Fallback

### 5.4 Phase A → Phase B promotion criteria (binary)

Phase B work begins **only** when all four below verify on a fresh game launch:

| # | Criterion | Verified by |
|---|---|---|
| 1 | `LogOutput.log` shows `1 plugin to load` and the `CheatMenuV2 Loaded` line | Read log |
| 2 | Status overlay visible on main menu before loading a save | Visual |
| 3 | F1 toggle works inside a loaded game; Mode logged | Press F1, check log |
| 4 | Resource HUD increases by +10000 across all resources, no errors | Visual + log scan |

**If any criterion fails:** Phase A is not done. Fix and re-verify. Do not start Phase B work. The failure indicates an architectural issue that adding more code will only obscure.

**If 1–3 pass but 4 fails in Native:** force-flip `MenuCoordinator` to Fallback for the remainder of testing and re-verify 4. If Fallback also fails 4, bug is in `CommandDispatcher` — fix before B.

**If all 4 pass in Native:** Phase B uses Native path; mostly QA / docs since the native panel already exposes most of B's cheats.

**If all 4 pass in Fallback:** Phase B builds out the IMGUI button strip — one button per cheat enum value, grouped by current context. Strict scope: buttons only, no pickers, no input fields. Pickers are Phase C.

### 5.5 Testing constraints

**Offline only.** Skirmish or single-player campaign. Do not test in matchmade, ranked, or leaderboard-tracked modes — EOS and analytics may report cheat-triggered state changes. `CommandDispatcher.Dispatch()` logs a `[Warning]` if the active player role is not offline at call time.

### 5.6 Estimated effort to Phase A done

- Project scaffolding + build setup: 30–60 min
- `Plugin` + `InputGate` + `OverlayUI` (probing only): 1–2 hr
- `NativePanelDriver` first attempt + logging: 1–2 hr
- `CommandDispatcher` recon + first call: 1–3 hr (uncertainty on dispatcher recon)
- End-to-end smoke + log inspection + fixes: 1–2 hr
- **Total: half a working day to one full day.**

---

## 6. Module internals

### 6.1 `NativePanelDriver`

**Context detection** (called only on toggle, never per-frame; uses Unity-level signals not the game's `BhScreen` stack):

```
DetectContext():
  if SceneManager.GetActiveScene().name matches /battle|arena/i → Battle
  else if no Hex.Session loaded → None
  else if any Hex.UI.City.BhCityHeroesGroupView is activeInHierarchy → City
  else → World
```

`FindObjectsOfType` is acceptable here because it runs only on toggle.

**Three-tier panel instantiation:**

1. `Resources.FindObjectsOfTypeAll<BhControllerCheat>()` — devs frequently leave dev tools disabled in shipped scenes. If found, `gameObject.SetActive(true)` and call `hrf()` (Show; `gik()` is Hide — both visible in Cecil dump).
2. `AddressableAssets.LoadAssetAsync<GameObject>("BhControllerCheat")` — Olden Era uses Addressables. Try a few candidate keys (`BhControllerCheat`, `CheatPanel`, etc.).
3. `Resources.Load<GameObject>("CheatPanel")` — last-ditch legacy path.
4. All fail → return `Failed("no instance, no prefab")`. Triggers Fallback mode.

**Public API (locked — Phase B does not modify):**

```csharp
class NativePanelDriver
{
    enum Result { Rendered, Failed }
    Result Open();
    void   Close();
    bool   IsOpen { get; }
    Context CurrentContext { get; }
}
```

### 6.2 `CommandDispatcher`

**Recon protocol (executed at start of implementation):**

1. Cecil-grep `Hex.dll` for any method whose parameters include `TypeCmdResourceCheat`. The dispatcher is the simplest signature (likely `enum + int amount + optional Hex.Processing.Player target`).
2. Found and clean → `CommandDispatcher` is a thin wrapper.
3. Only consumer is a UI button handler → inline its internal call (skip the UI).
4. Nothing public → Harmony reverse-patch. **IL2CPP rules:** use `HarmonyLib.Harmony` from `BepInEx/core/0Harmony.dll` together with `Il2CppInterop.HarmonySupport.dll` for delegate marshaling. Never call captured `MethodInfo.Invoke()` directly — route through `IL2CPP.il2cpp_runtime_invoke` or an `Il2CppInterop`-generated delegate. Log resolved native pointer + game build ID at `[Warning]` so patch-day breakage is immediately visible.

**Public API (locked shape; Phase B grows the typed methods):**

```csharp
class CommandDispatcher
{
    bool EnsureLocated();                        // one-shot, cached
    void AddAllResources(int amount);            // Phase A: only this
    // Phase B additions, one per enum value, mechanical:
    void LevelUpCurrentHero();
    void DispelFog();
    void SetInfiniteMovement();
    void GodModeCurrentBattle();
    void WinCurrentBattle();
    void ConstructAllBuildsCurrentCity();
    // …
    void Dispatch(Enum cmdType, int? intArg = null, IL2CPP target = null);  // chokepoint: logs, telemetry-warning, offline check
}
```

Adding cheats in Phase B is mechanical: each public method is one line delegating to `Dispatch`.

### 6.3 `OverlayUI`

**Always-on strip (every frame, every mode):**

- Top-right corner, ~280×24 px `GUI.Box`.
- Format: `CheatMenu-v2 | Mode: <Native|Fallback|Probing|Failed> | F1`
- 2 s flash messages append after `F1` then fade: `… | +10000 res`, `… | no game loaded`.

**Fallback button strip** (Phase A: 1 button. Phase B: ~15 grouped by context):

- Drawn only when `MenuCoordinator.Mode == Fallback && IsOpen`.
- Top-left, width ~240 px, vertical button stack grouped by `CurrentContext`.
- Each button calls one `CommandDispatcher` method. Fixed amounts only.

**Click-through prevention:**

- `OverlayUI.OnGUI()` records every drawn box's `Rect` into a per-frame `List<Rect>`.
- `InputGate.Update()` checks: if any mouse button down AND (`EventSystem.current.IsPointerOverGameObject()` OR cursor inside any recorded `Rect`) → `Input.ResetInputAxes()` and skip.
- IMGUI does not participate in EventSystem; the `Rect` check is the actual mechanism. EventSystem check catches uGUI elements layered above us.
- Single-plugin assumption: input-swallow is global. Acceptable.

**Public API:**

```csharp
class OverlayUI : MonoBehaviour
{
    void Flash(string message, float seconds = 2f);
    IReadOnlyList<Rect> ConsumedRectsThisFrame { get; }
}
```

---

## 7. Build, package, install

### 7.1 Build

- **SDK:** .NET 8 on dev machine; cross-targets `net6.0` cleanly.
- **Editor:** any C# IDE.
- **`CheatMenuV2.csproj`:**
  - `TargetFramework=net6.0`, `LangVersion=latest`, `Nullable=enable`, `AllowUnsafeBlocks=false`.
  - `AssemblyName=CheatMenu-v2`, `RootNamespace=CheatMenuV2`.
  - References (HintPath into `./refs/`): `BepInEx.Core`, `BepInEx.Unity.IL2CPP`, `BepInEx.Unity.Common`, `0Harmony`, `Il2CppInterop.{Runtime,Common,HarmonySupport}`, `UnityEngine.{CoreModule,IMGUIModule,InputLegacyModule,UI}`, `Hex`, `Hex.Shared`, `Il2Cppmscorlib`, `Il2CppSystem{,.Core}`.
- **Reference acquisition:** `tools/sync-refs.ps1` copies the needed DLLs from a user-provided game install path into `./refs/`. DLLs not committed.
- **Build command:** `dotnet build CheatMenuV2.csproj -c Release` → `bin/Release/net6.0/CheatMenu-v2.dll`.
- **Local deploy (dev):** `tools/deploy.ps1` copies output into `<game>/BepInEx/plugins/`. Optional `-Restart` flag.
- **Versioning:** SemVer. `0.1.0` Phase A, `0.2.0` Phase B, `1.0.0` after one game patch survived intact.
- **GUID:** `com.velebit.cheatmenuv2`.

### 7.2 Packaging

```
CheatMenu-v2-0.1.0.zip
└── BepInEx/plugins/CheatMenu-v2.dll
```

Plus a top-level `README.md` with: requirements (BepInEx 6 IL2CPP — exact build), install steps, F1 hotkey, troubleshooting checklist (`grep CheatMenuV2 BepInEx/LogOutput.log`), known limitations, license.

### 7.3 Install / uninstall

**Install:** verify BepInEx 6 IL2CPP installed → extract zip into game root → launch → status overlay visible top-right within 2 s of main menu → grep log for `CheatMenuV2 Loaded`.

**Uninstall:** delete `BepInEx/plugins/CheatMenu-v2.dll`. Optionally delete `BepInEx/config/CheatMenuV2.cfg`.

**Coexistence with v1:** both can sit in `BepInEx/plugins/`. v1 still fails silently. README recommends deletion but doesn't require it.

---

## 8. Risks & open questions

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Native cheat prefabs stripped from shipped Addressables | Medium | Native path unusable, falls back to IMGUI | Hybrid approach is exactly the mitigation; Fallback covers all of B's cheats |
| `CommandDispatcher` dispatch site not publicly callable | Medium | Forced into Harmony reverse-patch path | Recon protocol §6.2 step 4; documented IL2CPP-Harmony rules |
| Game patch renames internal types (currently un-obfuscated) | Medium | Breakage on every patch | Recon notes (`docs/recon-notes.md`) document name + native pointer for fast re-RE; Cecil-grep scripts reusable |
| Addressables key naming for the cheat panel unknown | High (uncertainty, not severity) | Tier 2 of instantiation fails | Tier 1 (FindObjectsOfTypeAll) + Tier 3 (Resources) cover the alternative paths |
| EOS reports cheat usage to backend | Low (in offline) | Account flagging in online modes | §2.2 non-goal + §5.5 testing constraint + Dispatch-time `[Warning]` |
| Multi-plugin IMGUI input swallowing collision | Low | Other plugins lose clicks while our menu is open | §6.3 documents single-plugin assumption |

---

## 9. Phase B preview (informational, not committed scope)

Once Phase A passes, Phase B exposes (in priority order):

**World context** (highest value): `LevelUp`, `DispelFogOfWar`, `InfinityMovePoints`, `Fly`, `RemoveBlockHireHero`, `BattleAutoWin`, `AddAllArtefacts`, `WinGame`.
**Battle context**: `GodMode`, `WinFight`, `RefreshMana`, `SetMana999`, `EndlessMagicCast`, `FreeMagicCast`, `KillUnitOnCursor` (cursor-targeted — needs cursor query helper).
**City context**: `ConstructAllBuilds`, `UpdateGainUnits`.
**Resource context** (always available): `Add`, `Substract`, `AddAll` (Phase A's cheat, plus `-` direction).

Anything requiring a picker (`AddSkill`, `AddArtefact`, `AddSetArtefacts`, `CreateHero`, `CreateSquad`, `AddMagicInGuild`) is **Phase C**, not committed.
