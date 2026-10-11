# 03 Dev tools: Marvin cheat buttons

Branch: `split/03-devtools` (worktree `GU-split-03-devtools`), based on `origin/main` (`e91bc09c`).
Commits: `34e3985c` (runtime NPC spawn), `4686d624` (Marvin buttons), review fixes `31defc8c` (prefab noise
reverted) and `b5b34194` (G1/G2 guilds, level cheat writes instance + vob, nits).

## What
- The Marvin tab (left-hand Marvin menu, `ActivateMarvinMode`) gets a column of cheat buttons. Each one is shown
  only when its `DeveloperConfig` entry is set:
  - **Level +5**: +5 levels, +50 LP, +60 HP max, +100 mana max, HP and mana refilled.
  - **Guild -> Novice** / **Guild -> Scavenger**: sets the hero's guild and true guild to the scripts' `GIL_NOV` /
    `GIL_SCAVENGER` (read on click, so G1 and G2 get their own ids). Scavenger is for NPC combat tests.
  - **Skip Time +30min**.
  - **TP -> <waypoint>**: teleports to `MarvinTeleportWaypoint`.
  - **Spawn <symbol>**: spawns the NPC instance `MarvinSpawnNpcSymbol` 2 m in front of the hero.
  - **Give items**: puts `MarvinGiveItems` (`ITRU_FIREBOLT, ITPO_MANA_03:5`) into the inventory.
- Core helpers: `NpcInitializerDomain.SpawnNpcRuntime`, `NpcService.SpawnNpcByName` (only accepts Daedalus
  instance symbols).

## Why
Every later slice needs repeatable test setups: a level/mana for spells, a guild for attitudes, a waypoint next to
the feature, a monster to fight, the test items. This lands first so testers can use it for all following PRs.

## Config
Kept (all default off / empty in `Production.asset`, under `[Separator("Marvin cheats (dev only)")]` next to
`ActivateMarvinMode`): `EnableLevel5Cheat`, `EnableGuildCheat`, `EnableScavengerGuildCheat`, `EnableTimeSkip`,
`MarvinTeleportWaypoint`, `MarvinSpawnNpcSymbol`, `MarvinGiveItems`.
Not in this PR: `EnableOneHitKill` / `EnableOneHitKnockout` (slice 08, they need the combat code).

## How to test (VR, ~10 min)
1. In your local DeveloperConfig: `ActivateMarvinMode` on, all five cheat bools on,
   `MarvinTeleportWaypoint = GRYD_072` (any waypoint of the world), `MarvinSpawnNpcSymbol = SH` (the example from
   the source branch) or `WOLF`, `MarvinGiveItems = ITFO_APPLE:3, ITMW_1H_SWORD_01`.
2. Open the Marvin menu, Marvin tab: the buttons are listed vertically centered.
3. Drink a strength potion / eat a plant first, then Level +5 → status menu shows +5 levels, +50 LP, full HP
   with +60 max, mana max +100, and the potion/plant bonus is still there.
4. Guild buttons → status menu guild changes, in G1 **and** G2 (G2: Novice = Novize). A game without the
   constant only logs a warning on click.
5. Skip time → the sky jumps 30 min; at 23:45 it goes to 00:15 of the next day. NPC routines follow within
   one game minute.
6. TP → you stand on the waypoint. Spawn → the NPC appears in front of you and behaves (monster attacks/wanders).
7. Give items → items are in the backpack. An unknown name only logs a warning.
8. All cheat entries off / empty → the tab shows no buttons.

## Review notes
- Source commits: `ef3d5fe4` `53a41532` `eb13495e` `bb6e7c4d` `5959c1ad` `517c49f5` `cb3d5b54` `d9a05349`
  `4378ade1` `0a0227f3` `8ea94468`, the Marvin spawn part of `aebea3c9`, the Marvin part of `5e898826`
  (give items, level cheat with mana).
- **Left out on purpose:** `VRMenuCheatAdapter` (an empty `MonoBehaviour` left over after the keyboard/controller
  shortcuts were removed in `517c49f5`) and the `HVR - Player.prefab` change that only added that empty component
  (the rest of that prefab diff was Unity re-serialization noise).
- **Deviations from the source** (maintainability):
  - Time skip no longer calls `NpcRoutineService.RecalculateAllNpcRoutines()` (slice 11 code). `main` already
    re-evaluates all routines every game minute (`NpcRoutineService.OnGameTimeMinuteChanged`, incl. culled NPCs),
    so the extra call is redundant. Skip now passes total hours, so it crosses midnight into the next day
    (`SetTime` supports hours > 23); the source wrapped back to the same day.
  - `SpawnNpcRuntime` relies on `InitLazyLoadNpc` → `InitZkInstance` → `Vm.InitInstance` like all other NPCs,
    instead of calling `Vm.InitInstance` itself plus an `IsZkInstanceInitialized` flag (that flag comes with slice
    01's `Wld_InsertNpc` fix). Works on `main` without slice 01. The `summonedBy` parameter is added by slice 20.
  - `MarvinTabHandler`: no fully qualified types, constants as private consts, lines ≤ 120, no
    `#if GOTHIC_HVR_INSTALLED` (nothing HVR-specific in it, like its sibling `MarvinLogsTabHandler`), unused
    `NpcRoutineService` injection removed, tooltips shortened. The file on `main` held an empty class named
    `MarvinMode`; the class is now `MarvinTabHandler` (matches the file, same script GUID the prefab uses).
  - **Guilds:** the source hardcoded G1's `GIL_NOV` (12) / `GIL_SCAVENGER` (24), wrong in G2. The value is now
    read from the scripts' constant on click (not at menu start: the VM isn't loaded then). It sets guild and
    true guild, like the scripts do when the hero joins (`Npc_SetTrueGuild` + `hero.guild`).
  - **Level +5** writes instance **and** vob for exactly the values it changes (level, LP, HP/mana max and
    current). The source used a blanket `SyncHeroInstanceToVob()` (instance → vob for all attributes), which
    reverted vob-only changes made by `Npc_ChangeAttribute` / fights (potions, plants). That method is no longer
    in this slice; slice 04 keeps its own copy for the status menu.
  - Labels use ASCII `->` (TMP default font has no `→`), magic numbers are named constants, logs are prefixed
    `[MarvinTabHandler]`, no null checks on components the button prefab always has.
- `SpawnNpcRuntime` duplicates the placement steps of slice 01's mid-game `Wld_InsertNpc` spawn
  (`SpawnInsertedNpc`). Kept, because 03 must work without 01, with a comment; **consolidate into one method once
  both are on `main`.**
- `Menu-MarvinMode.prefab`: only the `MarvinTabHandler` component on the Marvin tab (13 lines). The scroll view
  anchors, TMP margins and font size (59.35 → 72) that Unity re-serialized in the source were reverted: the
  buttons are placed by code relative to the tab, not by those values. If the logs/inspector tabs look off in VR,
  that's the place to look. The canvas sorting order change from `ae923da4` was reverted by `d109a6aa` (net none).
- Risks: dev-only, everything is off in Production. The spawned NPC isn't in a save game (no save/load yet).
