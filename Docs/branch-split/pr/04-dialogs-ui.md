# 04 Dialogs, subtitles, HUD

Branch: `split/04-dialogs-ui` (worktree `GU-split-04-dialogs-ui`), based on `origin/main` (`e91bc09c`).
Commits: `86f0b3ca` (dialogs/HUD), `b12cfd07` (status menu, health bars), `89bdbd56` (seated NPCs talk),
`ad2bf213` (review fixes), `018a7a2f` (`Print` external), `ddecc3b0` (attribute consistency).
Compile check: `RESULT: OK` (Core, VR, Flat, G1, G2, Lab, PlayMode tests, VR editor).

## What

**Dialogs**
- The hero isn't frozen during dialogs. A dialog only starts within `DialogMaxDistance` (5 m) of the NPC. While
  somebody talks, the hero is held inside that bubble (skip the line first). With the main choices shown, walking
  away chooses their END entry (`nr` 999 / `DIALOG_ENDE`, its script runs). Sub-choices (`Info_AddChoice`) and choices
  without END hold the hero like in Gothic, where they can't be left.
- An NPC that draws its weapon during the dialog closes it.
- Important dialogs are checked in their `nr` order like the engine (Baal Cadar's sleep spell before NoTalk).
- G2/mod SVM lines that ZenKit's `C_SVM` doesn't know (e.g. the hero's `$MISSINGITEM`) play via `SVM_<voice>_<key>`.

**Never cut by walls**
- Dialog box and NPC subtitles are drawn on top of walls, benches and the NPC (`VRUiOnTop` + new shader
  `Gothic/UI OnTop`). VR hands render above the box while a dialog is open, so you can see them pointing.
- On-top subtitles farther than 12 m are hidden (ambient smalltalk through camp walls).
- Subtitles disappear when the speaker is killed/knocked out mid-line, plus a 10 s+ fallback.

**HUD**
- `PrintScreen`/`AI_PrintScreen` ("New log entry", "1 item received") are shown as a small HUD in front of the head,
  `Snd_Play` plays 2D sounds ("LogEntry"). Repeated texts extend their line instead of adding one (G2 scripts print
  every AI tick with some heroes).
- `Print(msg)` (engine messages like PLAYER_MOB_MISSING_ITEM's "missing item" text, from `85344e0a`) shows in the
  same HUD (default position, 3 s). Always registered; `EnableScreenMessages` only hides the HUD
  (`Snd_Play` always plays).

**Status menu, health bars**
- Health bars follow damage/healing live, NPC health bars are visible at all, the player's health bar is visible and
  follows the current hero after a save is loaded.
- Status menu: correct protection rows (edge/point/fire/magic), talent rows match the talent index, only the NPC's
  talents, G2 weapon percentages from `hitchance[]`. Opening it syncs values Daedalus wrote directly on the hero.
- `Npc_SetTalentValue/Skill` keep the other half of the talent, `Npc_GetTalentSkill` is registered and returns the
  stored skill (was a stub returning 0 - Mroczne Tajemnice teachers reset ranks to "Nowy").
- Chapter screen waits for its jingle.
- Hero attributes stay consistent between the ZenKit instance (what scripts read/write directly) and the vob (what
  C# reads): `Npc_ChangeAttribute` and fight damage now write both, so the status menu's instance -> vob sync can't
  undo damage or potion effects.

**Project settings**
- `TagManager`: tags `Subtitle` and `Title` registered (UI prefabs on `main` already use them).
- `GraphicsSettings`: unchanged. `Gothic/UI OnTop` (in `Gothic-VR/Shaders`) reaches builds via the `UiOnTop-*` material assets.

**Seated NPCs**
- `AI_StartState(ZS_Talk, 0)` keeps a seated NPC seated (engine behavior), `AI_StandUp` plays the bench's back
  transitions (`AI_StandUpQuick` still pops), a seated NPC (`BS_SIT` mobs only) sees in the direction its body faces.
- Grabbing an NPC to talk releases it immediately - HVR's grab pushed seated NPCs out of the bench.
- `AnimationSystem.IsPlaying` matches alias names (stand-up transitions were skipped).
- Highlighted NPCs no longer glow (focus brightness 2.5 for NPCs).

## Why
On `main` the hero is locked in place during dialogs, dialog boxes and subtitles disappear inside walls and NPC
bodies, script HUD messages are dropped, health bars never move, and the status menu shows shifted talents and wrong
protection values. Seated NPCs pop up inside their bench when talked to. All engine-parity parts follow the original
scripts / OpenGothic.

## Config
- Kept flags (VR design / escape hatch), new separator "Dialogs & HUD": `EnableDialogFreeMovement`,
  `DialogMaxDistance`, `EnableDialogAlwaysOnTop`, `EnableScreenMessages`. `Production.asset`: all on, 5 m (as source).
- Dropped flags (fix always on): `EnableImportantInfoOrder`, `EnableMobSeatFix` (only the seated-dialog parts in this
  slice; slice 05 drops it for its own parts too).
- Not ported: `HideHealthBar` (attributed to slice 20). It's a property with an `internal set` nobody calls - always
  false. Recommend slice 20 drops it.

## How to test (VR, ~10-15 min)
1. G1/G2: talk to an NPC, walk around while he answers (you're held inside ~5 m), skip the line, walk away while the
   main choices are shown - END is chosen (farewell, if any) and the dialog ends. In a sub-choice (e.g. a quest
   decision with `Info_AddChoice`) you're held at the edge until you choose. Start a dialog from 6 m+ - nothing
   happens until you come closer.
2. Talk to an NPC standing next to a wall/bench: the dialog box and subtitles stay visible, your hands are visible
   over the box. Listen to ambient chatter behind a hut wall from far away - no floating subtitles.
3. Kill/knock out an NPC (Marvin cheats) mid-line - his subtitle disappears.
4. Get a quest log entry / receive an item in a dialog: "New log entry" HUD text + sound.
5. Talk to a seated NPC (G1 Old Camp bench): he talks seated or stands up with an animation, never pops up. Also walk
   up to an NPC on a chair/throne from the front - he notices you (sit transitions are assumed to turn the body
   around like on benches). An NPC at an anvil/cauldron notices you from the front too.
6. Hit an NPC/monster: its health bar shrinks; heal yourself: your bar grows. Open the status menu: talents start at
   1H in row 1, protection values match the armor. G2: weapon percentages.
7. Chapter change: the chapter screen stays until the jingle ends.
8. Take damage, drink a health potion (and in G1 a strength elixir, in G2 learn strength at a teacher), then open the
   status menu: HP and attributes must not jump (no healed damage, no lost potion/teacher points). Close and reopen it:
   same values.

## Review notes
- Source commits (primary 04): `ee8210fd` (only whitespace/HideSubtitles parts), `67dd2739`, `d5d7abb2`, `f3968086`,
  `de16ce21`, `59661daf`, `3bc6ca69`, `ae923da4`, `b1d68e70` (print/sound part), `d109a6aa`, `8e6b8f44`, `cd10002c`,
  `be5105d6`, `2d308ea2`, `bf3703ca`, `7d95812a`, `ee4dbc2a`. Secondary: `377057fd` (CurrentDialogNpc, close on draw),
  `c1a4c1ad` (dialog free movement part), `bac73b4d` (PrintScreen lag part), `c257ae7b` (Npc_GetTalentSkill
  registration), `7c3afb78` (UI tags; GothicUI was already always-included on main).
- **Shared with other slices (expect trivial integration conflicts):**
  - `NpcService.SyncHeroInstanceToVob()` (null-safe) is used by the status menu. Slice 03 no longer adds a blanket
    sync, so 04's copy is the one to keep.
  - `NpcService.GetHeroContainer()` null-safety is also slice 01 (`08d95db5`) - identical line. Needed here because the
    player's health bar polls it in menus.
  - `DialogService`: slice 05 later replaces `npcContainer.Go` with `GetDialogGo(npcContainer)` (MOBSI anchor) in
    `ShowDialog`, `IsHeroInDialogRange` and `KeepPlayerWithin`. Slice 16 adds the trade hooks.
- **Left for other slices:**
  - `AI_OutputSVM_Overlay` (external, `DialogService.ExtAiOutputSvmOverlay`, `Output`/`OutputSvm` `Bool0` handling,
    `_audioPlaySeconds` protected) -> 09; it's combat chatter and needs 09's GoToNpc.
  - `Output.cs` block/text-length fallback and null block guard (merge-resolution lines of `8080a6c9`) -> 01.
  - `NpcDialogStopDistance` (GoToNpc stop distance) -> 09/11, GoToNpc was reworked there.
  - Dialog invulnerability in `FightService` (`e5dfc6ce`) -> 09.
  - `ScriptRemovedInvItems` event, `VRSocket.IsPlayerSocket` public, `VRWeaponService` held-item removal,
    `EnableScriptRemovesHeldItems` -> 14.
  - Seated-NPC block in `ExtNpcClearAiQueue` (`8e6b8f44`): on `main` clearing the queue doesn't stop animations, so it's
    not needed yet. Slice 09/10 must re-add it when they port the expanded `ExtNpcClearAiQueue`.
  - `UseMob` "set BS_SIT again when re-used while seated" (`8e6b8f44`) only works with slice 05's
    `IsAlreadySittingOnSameMob()` - 05 should add it with its flag dropped.
- **Deviations from the source (maintainability):**
  - `VRScreenMessages`: no static `_instance` any more. Its text part is `VRPrintScreen`; `Snd_Play` is Core
    (`AudioService.PlaySound2D` + `ScriptSound` adapter in `Bootstrap.unity`, mixer group `SoundEffects`).
  - Dropped debug logs: `StatusMenu` talent count per open, `StatusBarAdapter` "bound after N frames" per NPC,
    `VRUiOnTop` hand material count. Removed unused usings, wrapped lines >120, removed "previously..." history comments.
  - `DrawWeapon` dialog close logs with `LogCat.Dialog` / `Log` (was `LogCat.Fight` / `LogWarning`).
  - `StatusMenu` uses `ContextGameVersionService.IsGothic2()` directly (exists on `main`).
  - Review fixes (`ad2bf213`): player health bar no longer disabled at start (in the source it was behind
    `HideHealthBar`, always false); `SyncHeroInstanceToVob` and the status menu skip without a hero; `VRUiOnTop`
    shares on-top materials per (texture, color, queue) instead of one per graphic per dialog, checks sprite asset
    copies in a `HashSet`, and resets the hands' render queue on `LoadingSceneLoaded` (scene change mid-dialog);
    PrintScreen log throttle has a named interval and a capped dictionary.
- **Attribute consistency (`ddecc3b0`), why this way:** the engine keeps attributes in one place (oCNpc); we have the
  ZenKit instance and the vob. `SyncHeroInstanceToVob()` copies instance -> vob for values scripts write directly
  (`B_RaiseAttribute` at teachers, G2 permanent potions, level-up HP, `hero.lp`). Syncing only a subset of attributes
  isn't correct: G1 elixirs change STR/HP_MAX via `Npc_ChangeAttribute` (vob), G2 permanent potions change the same
  attributes directly (instance). So every C# write goes through `NpcService.SetAttribute`/`SetLevel`/`SetLp`, which
  write both stores (`ExtNpcChangeAttribute`, `FightService.OnHitUpdateHealth`, Marvin cheats). Slices 08/09 add their
  own dual writes (`cd88a122`, FightService) - they must use these setters when integrated. Note:
  `ExtNpcChangeAttribute` still sets an absolute value on `main` (slice 01 fixes it to a delta, `3c6f6df0`); instance and
  vob stay consistent either way.
- Known limitations: `VRUiOnTop` keeps static caches (on-top material and sprite asset copies) -
  deliberate, shared across all dialog boxes. `GothicUIOnTop.shader` contains a long line copied from Unity's UI shader.

## Design review follow-up (2026-10-16)
A design review of the branch (Core/VR slice, UI in prefabs vs C#, AI slop) found the items below. Each one is
changed and committed on its own; the status shows what's done.

| # | Item | Planned change | Status |
|---|---|---|---|
| 1 | On-top UI built in C# | `VRUiOnTop` on the `Menu-Dialog`/`Menu-Subtitles` prefabs with serialized settings, material assets instead of `Shader.Find` + runtime materials (drop the `GraphicsSettings` entry), no per-frame sprite asset re-wrap, no runtime `CanvasGroup` | done: `VRUiOnTop` on `Menu-Dialog/DialogUI`, `Menu-Subtitles` (12 m, `CanvasGroup` on `Items`) and `PrintScreen`; `UiOnTop-Background`/`-Text` material assets, runtime copies only carry the runtime Gothic texture (cached per source material); hover re-wrap via TMP `TEXT_CHANGED_EVENT`; PrintScreen respects `EnableDialogAlwaysOnTop`; shader moved to `Gothic-VR/Shaders` |
| 2 | Static state in `VRUiOnTop` | Hands-on-top + caches move to a VR-scope service; no static ctor subscription, no `FindObjectsByType` per dialog | done: `VRUiOnTopService` (VR scene scope) + `VRHandsOnTop` on `HVR - Player`/`PlayerController` (serialized hand renderers) swapping `sharedMaterial` to the `VRHands-OnTop` asset (queue 3950); no static hand state, no `renderer.materials` copies. Material/font caches stay static in `VRUiOnTop` (bounded, decided) |
| 3 | `Snd_Play` VR-only | `AudioService.PlaySound2D` in Core with one persistent 2D source, delete `VRScriptSound` | done: `ScriptSound` adapter (root GO in `Bootstrap.unity`, 2D `AudioSource` -> `SoundEffects` mixer group) hands its source to `AudioService`; `Snd_Play` calls `AudioService.PlaySound2D`; `ScriptSoundPlay` event and `VRScriptSound` removed, Flat mode plays it too |
| 4 | Instance/vob dual writes | One owner `NpcService.SetAttribute` (+ level/LP) used by `ExtNpcChangeAttribute`, `FightService`, Marvin | done: `NpcService.SetAttribute`/`SetLevel`/`SetLp` are the only C# writers (`ExtNpcChangeAttribute`, `FightService`, `MarvinCheatDomain`); `SyncHeroInstanceToVob` doc shortened. Follow-up (not here): one store as single source of truth, needs save/load work |
| 5 | Dialog watcher coroutine | Keep the coroutine handle, stop it in `StopDialog`; `IsChoosing` into `DialogModel` | done: watcher handle stopped in `StopDialog` and before a restart; `DialogModel.IsChoosing` + `ExitInfo`. Behavior fix: walking away only chooses END (main choices with `nr` 999 or `DIALOG_ENDE`, its info function runs) - sub-choices and choices without END hold the hero (before: any shown choices ended the dialog, skipping forced decisions) |
| 6 | Seated "sees backwards" for all mobs | Limit to sitting mobs (or face by body) | done: `NpcHelperService.CanSeeNpc` flips only for `BS_SIT` + seated (state id >= 0), anvils/cauldrons/beds keep the root direction; no extra parameter, `ExtNpcCanSeeNpc` is back to the `main` one-liner. To verify in playtest: chairs/thrones turn the body like benches |
| 7 | `AI_StandUp` comment contradiction | Fix stale `StandUp.cs` comment, guard `UseMob(-1)` also against `CurrentAction` | done: regression fixed - `AI_StandUpQuick` shared `ExtAiStandUp` and played the back transitions too; now `ExtAiStandUpQuick` pops. Back transition skipped when a stop `UseMob` runs or is queued (`UseMob.IsStopUsingMob` public). `StandUp.cs` comment fixed |
| 8 | Flags | `Snd_Play`/`Print`/`PrintScreen` always registered, `EnableScreenMessages` gates only the HUD | done: externals always registered (no `DefaultExternal` spam when off), `EnableScreenMessages` checked per message (live toggle, Marvin messages unaffected), `Snd_Play` unflagged; tooltips one sentence each. All four flags kept (earlier decision) |
| 9 | Dialog range vs. clamp | Context service measures and clamps with the same transform, null guard | done: same transform already (`hero.Go` is the tagged `PlayerController` with the `CharacterController`, checked in `HVR - Player`). Range check now horizontal like the clamp, `_playerController` null guard, interface doc without "dialog bubble" |
| 10 | Logic in UI adapters | Talent percent into a service, `StatusBarAdapter` event-driven instead of per-frame polling | open |
| 11 | Small cleanups | `NpcContainer.IsUsingMob`, `VRFocus` cached NPC check + constant, history/anecdote comments, regions, shader location/header | open |
