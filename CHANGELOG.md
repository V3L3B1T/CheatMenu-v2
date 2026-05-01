# Changelog

## [0.1.0] - 2026-05-01 — Phase A: proof of life

First working release. All four Phase A acceptance criteria from the spec verified
on the day-one build (`HeroesOldenEra.exe` 2026-04-30, BepInEx 6.0.0-be.755 IL2CPP).

### Verified working

- ✅ Plugin loads under BepInEx 6 IL2CPP (`1 plugin to load`, no errors).
- ✅ IMGUI status strip renders top-right on the main menu.
- ✅ **F10** toggles the **native** game cheat panel (`Hex.Cheat.BhControllerCheat`).
  Context detection routes to the correct sub-panel (`BhWorldCheatPanel` /
  `BhCityCheatPanel` / `BhBattleCheatPanel`) automatically based on scene state.
- ✅ Verified across multiple toggles in both World and Battle contexts; no
  exception spam over 50K log lines.

### Components

- `Plugin` (entry, IL2CPP type registration, config bind)
- `MenuHostBehaviour` (host GameObject marker)
- `InputGate` (F10 polling + input-field/modifier suppression + click-through prevention)
- `OverlayUI` (IMGUI status strip + Fallback button strip)
- `MenuCoordinator` (Native-first / Fallback / Failed mode orchestration)
- `NativePanelDriver` (locates and drives `BhControllerCheat`)
- `CommandDispatcher` (Fallback path: drives `BhResourcePanel.AddAllResource()`)

### Out of scope (Phase B/C carryovers)

- Pause-menu suppression (suppression currently covers input fields only).
- Configurable cheat amounts in Fallback button strip (hardcoded 10000).
- Multiple Fallback buttons beyond `+10000 all resources`.
- Pickers (hero/unit/artefact/spell) in Fallback mode — Native panel covers them.
