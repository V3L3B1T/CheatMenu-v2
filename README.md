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
