# CheatMenu-v2

A BepInEx 6 IL2CPP plugin for **Heroes of Might and Magic: Olden Era** that re-exposes the game's built-in developer cheat panel via a single hotkey.

The shipped game's `Hex.dll` already contains a complete, clean-named `Hex.Cheat.UI.*` namespace — `BhControllerCheat`, `BhWorldCheatPanel`, `BhBattleCheatPanel`, `BhCityCheatPanel`, `BhResourcePanel`, plus state machines and pickers for resources, heroes, units, artefacts, and spells. This plugin's only job is to find that panel in the loaded scene, activate it, and call its native `Show` method.

**Status:** **v0.1.5 update complete.** The plugin has been updated with a patch-resilient binding architecture and is **confirmed working again** with the latest game update patch. Native dev panel renders successfully in both World and Battle contexts. Full cheat surface (gold, units, heroes, spells, artefacts) accessible via the panel's own UI.

## Features

- **F10** opens the native dev cheat panel — the same UI the developers used during development. Press again to close.
- Three-way mode resolution: `Native` (dev panel rendered), `Fallback` (IMGUI button strip if the prefab can't be located), `Failed` (neither — see log).
- IMGUI status strip top-right shows current mode, hotkey, and recent action.
- Suppressed when typing in any uGUI `InputField` or TMP_InputField, and when any modifier key (Shift/Ctrl/Alt) is held.
- Click-through prevention: clicking the IMGUI overlay doesn't bleed through to the world map / hero / etc.
- Hotkey persisted to `BepInEx/config/CheatMenuV2.cfg`, live-rebindable.

## Requirements

- Heroes of Might and Magic: Olden Era — verified against the latest Early Access build updates.
- BepInEx **6.0.0-be.755** IL2CPP edition. Verify by checking that `BepInEx/LogOutput.log` starts with `BepInEx 6.0.0-be.755 - HeroesOldenEra`.
- A single-player skirmish or campaign save. **Do not use this in matchmade, ranked, or leaderboard-tracked modes** — Olden Era ships with Epic Online Services and an analytics collector; cheat-triggered state changes may be reported.

## Install

1. Download `CheatMenu-v2.dll` from the [Releases](../../releases) page (or build from source — see below).
2. Copy it to `<game>/BepInEx/plugins/CheatMenu-v2.dll`.
3. Launch the game. Within ~2 seconds of the main menu appearing, a small status strip appears top-right reading `CheatMenu-v2 | Mode: Probing | F10: toggle`.
4. Verify load: open `<game>/BepInEx/LogOutput.log` and search for `CheatMenu-v2`. You should see the `Loaded —` line and the `GUID=…` line.

## Use

1. Load any single-player save or start a skirmish.
2. Press **F10**. The native dev cheat panel appears (full UI: world cheats, city cheats, battle cheats, hero/unit/artefact/spell pickers, resource panel).
3. Pick what you want. Resources, level-ups, infinite movement, fog reveal, god mode, win-fight, spawn unit on cursor, build all, etc.
4. Press **F10** again to close.

## Hotkey

Default: **F10**. F1 is reserved by the game's bug reporter — avoid it.

To change: edit `<game>/BepInEx/config/CheatMenuV2.cfg`:

```ini
[Hotkeys]
Toggle = F10
```

Save the file; the new key takes effect on the next frame (no game restart required).

## Uninstall

Delete `<game>/BepInEx/plugins/CheatMenu-v2.dll`. Optionally also delete `<game>/BepInEx/config/CheatMenuV2.cfg`.

## Build from source

```sh
git clone https://github.com/<your-username>/CheatMenu-v2.git
cd CheatMenu-v2

# Copy game/BepInEx DLLs into ./refs/ (gitignored, copyrighted material)
pwsh tools/sync-refs.ps1 -GameRoot "C:\Path\To\Heroes of Might and Magic Olden Era"

# Build
dotnet build CheatMenuV2.csproj -c Release

# Deploy to the game (optional helper)
pwsh tools/deploy.ps1 -GameRoot "C:\Path\To\Heroes of Might and Magic Olden Era"
```

Output DLL is at `bin/Release/CheatMenu-v2.dll`.

## Architecture

The plugin is six small units, each with one job. See [docs/superpowers/specs/](docs/superpowers/specs/) for the full design spec and [docs/superpowers/plans/](docs/superpowers/plans/) for the step-by-step implementation plan that produced this codebase.

| Unit | Job |
|---|---|
| `Plugin` | BepInEx entry. Registers IL2CPP types, reads config, builds the host GameObject. |
| `MenuHostBehaviour` | Marker `MonoBehaviour` on the host GameObject for `DontDestroyOnLoad`-survival. |
| `InputGate` | Polls the configured `KeyCode` per frame; suppresses on input-field focus / modifier; eats clicks over the IMGUI rects. |
| `OverlayUI` | IMGUI status strip (always on) and IMGUI fallback button strip (when in Fallback mode). |
| `MenuCoordinator` | Orchestrates: tries Native first, falls to Fallback, records `Mode`. |
| `NativePanelDriver` | Locates `Hex.Cheat.BhControllerCheat`, detects context (World/City/Battle), calls native `hrf()` / `gik()` (Show / Hide). |
| `CommandDispatcher` | Fallback path. Locates `Hex.Cheat.UI.BhResourcePanel` and drives its `AddAllResource()` after populating its input fields. |

## Reverse-engineering notes

See [docs/recon-notes.md](docs/recon-notes.md). Highlights:

- The shipped game ships the dev cheat tooling intact — no decompilation or Harmony reverse-patching needed.
- Names in `Hex.dll` are NOT obfuscated for the cheat namespace; types and method names are clean.
- Three IL2CPP/BepInEx interop pitfalls are documented (UnityEngine.UI sourcing, nullable attribute shadow, property-vs-field marshaling).

## License

[MIT](LICENSE). © 2026 Velebit.

## Disclaimer

Single-player use only. The author is not affiliated with Unfrozen Studios or Hypothetical Games. Use at your own risk; the game is in Early Access and updates may break this plugin without notice.
