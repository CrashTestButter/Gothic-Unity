# 02 Mods, game selection & audio

Branch: `split/02-mods` (worktree `GU-split-02-mods`), based on `origin/main` (`e91bc09c`).
Compile check: `RESULT: OK` (Core, VR, Flat, G1, G2, Lab, PlayMode tests, VR editor).

## What

**Mod loading**
- A mod folder is used as the game root (`ModPath` + `ModIni`). In the Editor `DeveloperConfig.EnableMod` overrides
  `GameSettings.json`.
- `.mod` archives are mounted on top of the base VDFs, only when a mod is active. When the mod ini has a
  `[FILES] vdf=` list, only those archives are mounted (multi-language packages like Dolina Zombie).
- Mod subtitles: `OU.BIN`/`OU.CSL` are also looked up in `VDFS/_WORK` (extracted from the mod's speech archive).
- A mod DAT that ZenKit can't parse (e.g. G2 Renovation's extended `C_FightAI`) falls back to the loose `_work`
  copy.
- Language detection accepts mod spellings of `MOBNAME_CRATE`. `DeveloperConfig.FallbackLanguage` boots anyway when
  detection fails.
- The static cache (VOB bounds, texture arrays, world chunks) is kept per game **and** mod (folder name + ini name,
  so mods sharing the default `GothicGame.ini` don't overwrite each other).
- No `OU.BIN`/`CSL` found: dialogs play without subtitles (one warning) instead of an NRE per line.
- A `GameSettings.json` `ModPath` that doesn't exist logs a warning and starts vanilla.
- An unparsable world logs an error instead of breaking the session.

**One build for all games**
- `GameSettings.json` gets `GameVersion`, `ModPath`, `ModIni`, `EnableMusic`, `EnableZSpyLogs` and `EnableOggAudio`.
  In a build, `GameVersion` preselects G1/G2. Placeholders (`---…---`) count as empty, so vanilla behaves as before.
- Runtime checks ("is this G2?") now ask the running game (`ContextGameVersionService`), not
  `DeveloperConfig.GameVersion`. That affects `AiHandler` (item status AI var) and G2-only externals in
  `VmExternalDomain`.

**Audio**
- Ogg Vorbis files (New Balance ships its dubbing as Ogg with a `.wav` extension) are detected by content and
  decoded with NVorbis (MIT, `Assets/Plugins/NVorbis`, committed directly via a `.gitignore` exception; full
  `PluginImporter` meta for any platform, no XML docs).
- A clip that fails to decode returns no audio (the text-length fallback takes over) instead of breaking the dialog.
- **dmusic loading changed for all games, vanilla included:** the loader no longer uses `LoaderOptions.Download`.
  `TryGetSegment` calls `Segment.Download()` itself. Why: dmusic's internal auto-download error path double-frees the
  segment (native heap corruption, process crash) on mod segments with incomplete instruments. Done manually, the
  failure is a catchable managed exception and the segment is skipped from then on.
- If `RenderPcm` faults, music goes silent for the rest of the session.
- `EnableMusic` is an escape hatch for mod segues that crash dmusic natively.

**Menus (G2 and mods)**
- Menus without a prefab are skipped instead of aborting the whole menu init (`ResourceCacheService` still logs the
  missing prefab as an error, as on `main`). Menu init failures are caught per menu.
- New Game confirmation menus without a prefab (G2 `MENU_NEW_GAME`, Dolina Zombie `MENU_WIRKLICH_NEUES`, Świat
  Skazańców `MENU_PLAY_SELECT`, New Balance `MENU_NG`) start the new game directly (vanilla G2 → `NewWorld.zen`).
  Other unavailable menus keep the current menu open.
- The save/load menu skips folders beyond our 15 slots (G2 has 16–20) and saves that fail to parse.
- Mod menus without a backPic, and renamed vanilla items used as templates, no longer crash menu init.

**Mod content robustness**
- VR melee, swimming, eating and dialog gestures work when a mod's `Humans.mds` lacks animations or sound events.
  VR melee then uses default attack and combo windows.
- Animations without a node list skip pose baking and speed detection.
- NPC attacks work without a fight AI move table.
- A Daedalus perception function that throws is logged and contained.
- Ikarus/LeGo Trialoge functions are only overridden when the scripts declare them.
- The VOB bounds/collider cache, fire VOB trees and texture arrays over the hardware slice limit no longer throw.

## Why
Vanilla G1/G2 already worked. These changes make mods bootable and playable (tested by the author with Mroczne
Tajemnice, Dolina Zombie, G2 Renovation and G2 New Balance), and let one build run G1, G2 or a mod by editing
`GameSettings.json`.

## Config
- Kept (user settings / escape hatches): `EnableMod`, `ModPath`, `ModIni` (existed), `FallbackLanguage`,
  `EnableMusic`, `EnableOggAudio`.
- Dropped: none.
- `Production.asset`: `EnableMod: 0`, `ModPath`/`FallbackLanguage` empty, `EnableMusic: 1`, `EnableOggAudio: 1`.
  `Lab.HVR.Simulator.asset`: `EnableMusic: 1`, `EnableOggAudio: 1`.

## How to test (VR, ~15 min)
1. Vanilla G1 and G2 with `ModPath` empty: boot, new game, talk to an NPC, open save/load. Nothing changes.
2. Editor: `DeveloperConfig.EnableMod = true`, `ModPath` = a mod folder (e.g. Mroczne Tajemnice), `ModIni` = its ini.
   Boot, start a new game, talk (subtitles), fight once, swim.
3. G2 (vanilla): main menu → New Game starts the game (no dead confirmation menu). Save/load menu opens even with
   `savegame16`–`20` folders present.
4. A mod with Ogg dubbing (G2 New Balance): voices play. With `EnableOggAudio = false` the lines show text with
   silence.
5. Build: set `GameSettings.json` `"GameVersion": "Gothic2"` (and optionally `ModPath`/`ModIni`). The build starts G2
   (or the mod) directly. Check `%AppData%/../LocalLow/.../Cache/` now has a separate folder per game+mod.

## Review notes
- Source commits: `5eb534aa` `75287b0e` `fb20254b` `fca4712c` `2c4127a4`/`37c1edd6` (net effect) `fe5ebc39`
  `937e141c` `803816ee` `1107c651` `8d8f5aba` `77d74e77` `07cab21b` `4b801c12` `a39b128c` `50c003a4` `3c504c71`
  `0e6359ef` `33c5f200` `0d220118` `bc632361` `47294207` `50c3a989` `55ce4bcc` (code only, not its `Docs/` edits).
- **Left for other slices:**
  - `SoundDomain` WAV chunk parsing and the 8-bit fix (slice 01). Expect an adjacent-hunk conflict in integration.
  - `SaveMenu` `UNITYSAVE.json` marker check: it would hide every save on `main`, where nothing writes that file
    yet → slice 21. The native OOM on Union-format foreign saves (G2 Renovation) therefore still exists here; the
    added try/catch only covers managed parse errors.
  - `SaveMenu` `SavesFolderPath` and the slot-item activation (slice 21).
  - `BootstrapDomain` `VfxVm` (slice 20).
  - `BootstrapService` navmesh/save init lines (slices 12, 21).
  - `StatusMenu` and `VRCrossbow` game-version checks sit inside code that slices 04 and 13 add. Those slices must
    use `ContextGameVersionService.IsGothic2()` there, as the source does.
  - `Attack.cs` debug log line (slice 09).
- **Deviations from the source (maintainability):**
  - Wrapped all added lines over 120 characters.
  - Removed a per-menu diagnostic dump in `AbstractMenu` and the "instantiating N menus: <all names>" log in
    `MenuHandler`.
  - `MenuHandler` uses `SaveGameService.SlotId.NewGame` instead of a literal `0`.
  - `IsOggVorbis` is extracted as a helper in `SoundDomain`.
  - `using System.IO` instead of a fully qualified `Path` in `GothicModIniConfig`.
  - `AiHandler` no longer injects `ConfigService` (its only use became the game-version check).
  - `VrWeaponAttackDomain.GetAttackAnimation` uses `FirstOrDefault`, which the null fallback needs.
- **Mod-specific hardcoding** (deliberate, each commented with mod and reason): language probe values (Renovation
  "Skrzynka", Dolina Zombie "Ящик") and New Game confirmation menu names (4 mods).
- **Review round 1 (2026-10-08):** cache suffix with mod folder, cutscene library null guard in `Output`, prefab error
  log restored, dmusic download change documented, NVorbis meta/XML/LICENSE line endings, Editor-only tooltips and a
  `ModPath` tooltip, missing-`ModPath` warning, `MusicDomain` flag renamed and the off-thread `volume = 0` removed,
  `_fallbackWindowTimes`, comment fixes, `NpcHelperService.GetNpc` hunk left to slice 01.
- **Risks:**
  - dmusic segment download is now manual for vanilla too (see What → Audio). Vanilla music needs a listen test
    (zone changes, fight music transitions).
  - A failed `ChangeWorld` (unparsable world) logs and returns before the Loading scene. When it comes from a level
    change trigger, the old world may already be torn down, so the player can be stuck on a black screen instead of
    a crash. Better than crashing the session, but not a recovery.
  - `MusicDomain` uses `[HandleProcessCorruptedStateExceptions]`, which is best effort only under Mono.
  - Builds only honour `ModPath`/`ModIni`/`GameVersion` from `GameSettings.json`, and the Editor only from
    `DeveloperConfig`. That split is by design (`#if UNITY_EDITOR`).
  - Release/packaging scripts must ship the new `GameSettings.json` keys.
