Part **1 of the split** of `experimental/game-completion-candidate-vibecoded-borobongo` into small, reviewable and
VR-testable PRs. Based on `main` (`e91bc09c`). Later PRs of the split are referred to by number below
(e.g. "PR 08 – NPC death & knockout"); they are not opened yet.

15 commits (13 fixes + 2 from code review), 22 files, +331 / −124. No new features, no new config flags.

## What
- **Audio:** WAV parser walks RIFF chunks by their declared size (extra chunks / extended `fmt ` no longer give
  garbage bit depths); a negative / too big chunk size (streamed WAVs write `0xFFFFFFFF` into `data`) makes `data`
  run to the end of the file and ends the walk for other chunks; 8-bit samples decoded as unsigned (silence = 128)
  without overflow.
- **Head morphs:** missing morph animations and morph meshes without cached vertex data are skipped (warned once per
  mesh/animation) instead of throwing `InvalidOperationException` / `KeyNotFoundException` on every random face
  animation; `Hurt` and `Viseme` morph names map to their types.
- **Attack animation events:** `DEF_OPT_FRAME`, `DEF_HIT_END`, `DEF_WINDOW` frames are read from ZenKit's
  `EventTag.Frames` (they were parsed from `Slots`, which holds strings like bone names).
- **Daedalus externals:**
  - `Npc_ChangeAttribute` adds the delta instead of setting the value; only HP/mana are clamped to their max.
  - `Npc_ExchangeRoutine`, `AI_ContinueRoutine`, NPC helper externals: no NRE for null / not spawned NPCs.
  - `Npc_IsPlayer`, `Wld_DetectNpc*`: no NRE while the hero instance runs before `GlobalHero` is assigned.
  - `AnimationService.GetPreparedKey`: a null animation name returns an empty key instead of throwing.
  - `Wld_InsertNpc` called mid-game (mod quest/dialog scripts) spawns the NPC right away instead of queueing it in
    a list that is never drained again.
- **Dialogs:** a missing `OU.BIN` block skips the subtitle instead of throwing; an exception in a dialog info
  function is logged with its full stack trace and the dialog continues (no more soft-lock with a missing choice box).
- **Menus:** `items[]` gaps are skipped like the engine (Mroczne Tajemnice's status menu lost all rows after a gap);
  the loop is bounded by the scripts' `MAX_ITEMS` (150). ZenKit's `GetItem()` never throws (returns `""`).
- **Pre-caching:** worlds ZenKit can't parse and PFX missing in the VM are skipped instead of aborting.
- **VR:** a lock pick lying next to a lock no longer throws every frame (only a held pick picks); a weapon destroyed
  while held no longer stays glued to the hand; `GetHeroContainer()` returns null before the hero exists (menus).
- **Build:** `VRBuilderActions` switches the platform first and asks to start the build again (Unity 6 error
  "Unable to build with the current configuration"). CI is unaffected (`-buildTarget` already matches).

## Why
These are the bug fixes from the game-completion branch that apply to code already on `main`. Most showed up with
mods (Mroczne Tajemnice, New Balance) or in G2, where they threw every frame, aborted pre-caching or soft-locked
dialogs. Landing them first gives every following feature PR a quieter log and fewer false alarms.

## Config
Kept flags: none · Dropped flags: none · Production.asset: unchanged.

## How to test (VR, ~10-15 min)
1. G1 vanilla, new game: pre-caching finishes, world loads; log has no new exceptions.
2. Talk to Diego and any NPC: subtitles and voice play as before; the dialog choice box comes back after each line.
3. Look at talking NPCs closely: face (viseme) animations still play; no `KeyNotFoundException` spam in the log.
4. Fight a scavenger with a sword: combo / hit timing behaves as before or better (frames now read correctly).
5. Drink a potion / equip nothing special: HP changes add up (e.g. +50 heals, does not set HP to 50).
6. Put a lock pick on the floor right next to a chest lock: no exception spam; pick it up and pick the lock.
7. Open the status menu: all rows show (MT mod: talent rows after the gap are visible).
8. Optional (mods): Mroczne Tajemnice and G2 New Balance boot and talk without new exceptions.
9. Optional (build): run "Build Windows64" with another platform active - it switches and asks to start again.

## Review notes
- Source commits: `98a928a1` `669f194d` (WAV) · `1e38022f` `2171b062` `f4e273c8` + morph part of `85344e0a` ·
  `dcb2f21d` · `3c6f6df0` `7b480ab8` · `6334e567` (hero container part) `69bf99f6` · `7d67054e` `60488ed6` `08d95db5`
  + NpcHelperService part of `fe5ebc39` · `54f97e9b` · `1513a5b7` · `c5c23786` · `92f4345d` · `e26326bb` · `8080a6c9`.
- Code deliberately left for later PRs / judgement calls:
  - `523c277d` (Vm.InitInstance at Wld_InsertNpc time) is **not** ported. It only has an effect together with
    `IsZkInstanceInitialized` (attributed to save/load) and the NpcInstance attribute sync of `cd88a122` (PR 08).
    Its purpose is dead-on-spawn NPCs (Nek with HP 0), which PR 08 handles. Mid-game `Wld_InsertNpc` (and the
    Marvin spawn button of PR 03 – dev tools) works without it. → moved to PR 08 (NPC death & knockout).
  - `02bf2761` (deferred on_equip for script-equipped items) is **not** ported: `main` never calls `on_equip` for
    script equips; the code it fixes (`EquipItemWithEffects`) comes with PR 18 (equipment).
  - `ce61ebb9` has no net change against `main` (it removes diagnostics added by `28191858`) → nothing
    to port; belongs to PR 12 (NPC movement).
  - `3b81510c` ("logger") only adds an unused `using` → dropped.
  - `6334e567`'s `TryUnlockWithKey` hunk (key unlock) belongs to PR 05 (world VOBs: keys/locks).
  - `60488ed6`'s extra warnings in `ExtNpcGetDistToWp`: `main` already returns `int.MaxValue` for a missing NPC or
    WP; only the null-safe `GetNpc()` is ported (it was the actual NRE). No extra log lines.
  - `dcb2f21d`'s `HasComboWindowOpened` property is only used by NPC combos → PR 09 (NPC melee). Its per-attack info log of the
    combo frames is dropped (log spam); the warning for a window without frames stays.
  - `8080a6c9`: `main` already had the null-audio guard; only the missing-block guard and the info-call try/catch
    are ported. The text-length fallback in the source's `Output.cs` came from a merge resolution on the source branch and isn't needed here.
- Deviations from the source (maintainability): long lines wrapped, em dashes in comments/logs replaced, unused
  `using`s removed (`VRLockPickInteraction`), XML doc comment no longer separated from its method by a field
  (`MorphMeshCacheService`), `_initialNpcLoadComplete` renamed to `_isInitialNpcLoadComplete` (bool naming),
  `SpawnQueuedNpc` renamed to `SpawnInsertedNpc`, `[CanBeNull]` on `TryGetMorphData`.
- `VRBuilderActions` (editor-only build script) uses `Debug.LogWarning` on purpose, like the rest of that file:
  it runs in the editor/CI outside the game's logger setup.
- AI pre-review round 1: fixed in `11798fe9` (corrupt WAV chunk sizes, morph warning once per
  mesh/animation, full dialog exception) and `d694951e` (dead try/catch removed after checking ZenKitCS
  `MenuInstance.GetItem`, `${...}` log typo, lines over 120 chars).
- The missing-animation warning set is per morph component (per NPC head), so each NPC warns at most once per
  animation - no static state in the adapter.
- Known limitations / risks: `Npc_ChangeAttribute` now adds deltas. That is engine behavior, but any place on
  `main` that relied on the old "set" semantics would change; none found in Core/VR.
- Compile check (dotnet build of all Gothic assemblies incl. Flat, outside Unity): OK. **Not yet tested in Unity/VR.**

🤖 Generated with [Claude Code](https://claude.com/claude-code)
