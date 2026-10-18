# 05 World VOBs: movers, triggers, MOBSI, dig spots, keys, seats

Branch: `split/05-world-vobs` (worktree `GU-split-05-world-vobs`), based on `origin/main` (`e91bc09c`).
12 commits: one per sub-feature, then review fixes. `compile_check.py`: OK (Core, VR, Flat, G1, G2, Lab,
PlayMode tests, VR editor).

## What

1. **Movers** (`0bd02110`, `fd42bab6`): `MoverAdapter` moves zCMovers keyframe by keyframe at Gothic's speed
   (OpenGothic `movetrigger.cpp`: speed = cm/ms; rotation-only segments in rotations/s), with Gothic's speed curves
   and open/close sounds; chained target movers follow, other targets get a trigger when a move ends. Movers react
   per behavior (OpenGothic `MoveTrigger::onTrigger/onUntrigger`: Toggle toggles, TriggerControl opens/closes on
   trigger/untrigger, OpenTime opens, SingleKeys ignores triggers). Grabbing a wheel/lever (`VRFocus` →
   `MobInteractionService.HandleMobGrab`, Core) runs the mob's `ConditionFunction` with self = hero (OpenGothic
   `invokeCond`); like OpenGothic the mob keeps its `State`: used from 0 it triggers its target, used again it
   untriggers it. Movers are registered by name (copy-pasted movers share one; `//` prefixes and missing `.3DS`
   suffixes are tolerated). Doors (HVRPhysicsDoor) and chests (lid physics) don't go through this path; doors are no
   longer in `VobsInteractable`.
2. **Trigger system** (`ffe58329`, `eda43305`): one handler per VOB type – `zCTriggerList`, `zCCodeMaster`,
   `zCTrigger`, `zCTriggerUntouch`, `zCTriggerWorldStart`, `zCMoverController`, `zCMessageFilter`.
   `oCTriggerScript` can be triggered by other VOBs and respects `MaxActivationCount`.
   `Wld_SendTrigger`/`Wld_SendUntrigger` externals. `VobService.DispatchTrigger`/`DispatchUntrigger` route by name
   (untrigger: TriggerControl movers close, code masters reset, message filters run OnUntrigger). Chained triggers
   pass their own VOB name as sender (code master slaves). Chains are capped at 8 levels (synchronous dispatch would recurse forever
   on self-retriggering scripts, G2 Renovation). Mobs whose target isn't a mover run the chain (sender = mob).
   All lookups are cleared on `LoadGameStart` (new game, load) and on level changes (`BootstrapService`). Movers and
   triggers below another VOB get their own `VobLoader` and are registered too (`GameStateService.RegisterNamedVob`).
   Code masters handle duplicate slave names.
3. **MOBSI + beds** (`1cb05946`, `0d1f0dfe`): shrines, alchemy/rune tables, bookstands call `[onStateFunc]_S1` with
   self = hero; the dialog opens next to the mob (anchor released again if the script opens no dialog), the hero's
   AI queue runs in `DialogService` meanwhile. Missing mob item → `PLAYER_MOB_MISSING_ITEM`. G2 beds (`BEDHIGH_*`)
   get the bed prefab and the sleep dialog (`IVirtualObject.IsBed()`). Mods whose hero isn't `PC_HERO` (Mroczne
   Tajemnice) get `PC_HERO`'s C_INFOs; `AI_ProcessInfos(null)` = hero.
4. **Dig spots** (`f429770f`, `0d1f0dfe`): G2 treasure X marks get a placeholder X (their mesh is untextured and was
   destroyed) and are dug up by 3 weapon hits (`VRDigSpot`). One test for both: `VmCacheService.IsDigSpot`.
5. **Keys/locks + collision** (`424440e7`, `eda43305`): a held key unlocks its door/chest (script success sound,
   success vibration); G2's `ItKe_Lockpick` gets the lock pick prefab; chest items keep their amount; the player
   collides with chests/doors.
6. **Child VobLoaders** (`c5c8f2b7`): doors/chests/items below another VOB get their own `VobLoader` (G2 Xardas' tower
   doors/chests threw InvalidCastExceptions).
7. **Seats** (`94381336`): seated NPCs stay on the seat on repeated `AI_UseMob` (`IsAlreadySittingOnSameMob`), don't
   turn while using a mob, the culling respawn ends mob usage; the bench/throne BIP01 yaw-inversion hack is removed.
8. `f965d8f5` + `6b2b725e`: the head-morph warn-once fix was ported here first and reverted again – slice 01 owns it.
   (Squash both when merging, or drop them.)

## Why

On `main` gates, lifts and drawbridges never moved, trigger VOBs were empty placeholders (quest scripts calling
`Wld_SendTrigger` did nothing), MOBSI mobs (shrines, G2 beds, alchemy) couldn't be used and G2 treasures were
invisible. Several G1/G2 quests depend on these (e.g. G1 Old Camp gate, Chapter 3 orc graveyard gate). Behavior
follows the engine/OpenGothic (mob state 1 → `_S1`, `invokeCond`, `isRotationAllowed`, mover timing, mob slots +
root motion).

## Config

- No new flags. Movers always run at original Gothic speed (the source branch's `MoverSpeedMultiplier` 3.48 only
  compensated a unit error – speed was used as m/s instead of cm/ms, factor 10).
- Dropped (fix/feature always on): `EnableMobSeatFix`, `EnableNoTurnWhileUsingMob`, `EnableChildVobLoaders`,
  `MoverSpeedMultiplier`, `EnableMobsiDialogs`, `EnableDigSpots`, `DigSpotsRequireTool` (dig spots now need the
  mob's own tool, like vanilla: the pickaxe for G2 X marks).
  ⚠ Slice 04 also used `EnableMobSeatFix` – it must use the "on" path without the flag too.

## Asset changes

- `oCMobBed.prefab`: the bed slot's bone-name pattern changes from `ZM_PC_BED_STRAW` to `BIP01.*|ZM.*`, so G2 beds
  (other bone names) find their slot too (74302081).
- `ProjectSettings/DynamicsManager.asset`: only `m_LayerCollisionMatrix` changes (player ↔ chests/doors collide,
  locked doors block). The source commit also carried Unity 6 re-serialization noise (`serializedVersion`,
  world bounds, …); it's left out – Unity re-adds it when the project is saved, that's harmless.

## How to test (quick checks first, then longer play)

Quick (~5 min, any save):
1. Boot G1 and G2 to the main menu and into a new game – no new errors in the Uber Console (category Vob).
2. Grab a chest lid and a bench – no "no Target" warnings, chests open as before.
3. Locked chest with its key in the inventory: hold the key into the lock → opens with sound and one vibration.
4. Walk into a closed door → you're blocked; a locked door stays closed.

Short play (~10 min):
5. G1 Old Camp main gate: grab the winch → the gate opens at its Gothic speed (compare with the original: a few
   seconds, not instant, not a minute); grab again → closes (TriggerControl gates too:
   the second use untriggers). Multi-keyframe movers follow their whole path.
6. G2: open a gate via a lever; pray at a shrine (dialog next to the shrine, choices work, closes).
7. G2: alchemy table without a flask → "missing item" text + hero line; with a flask → alchemy dialog.
8. G2: sleep in a bed in Khorinis (highlights, sleep dialog). G1 Mroczne Tajemnice: sleep in a bed.
9. NPCs on benches (Old Camp): they sit on the seat, don't spin around, stay seated when talked to.

Longer play (~15+ min):
10. G2: find an X mark (e.g. near the lighthouse), hit it 3x with a weapon → treasure spawns.
11. G2 Xardas' tower: doors/chests work, no InvalidCastException.
12. Change level (G2 Khorinis ↔ Valley of Mines or G1 ↔ Sleeper temple) and use a lever/gate there – triggers of the
    old world aren't hit (no "trigger chain deeper" or missing-handler warnings for old names).
13. G1 Chapter 3 orc graveyard gate (wheel), G1 Sleeper temple puzzles (code masters / trigger lists).

## Review notes

- Source commits: f200e883 8752e129 75bd89fd cd614258 8e9558b3 bd4c9fc6 c574645a 2f2e4fbd ad0c6d64 0cc8533b
  12eca488 8b4fef07 b3faeaf5 a1a8f2a2 e37c8952 765b48b2 443f2438 a0f4f16e b773c4c0 (movers), 9636179d 05be2e1a
  (triggers), f44ca0ac 74302081 6bd758e0 85344e0a (MOBSI/beds), 3edf3460 (dig), a696f99b key part + 6334e567
  key-trigger guard (from 01) (keys), 7c3afb78 DynamicsManager part, c0299809 child-loader part (attributed to 13 in
  blame), 2fb20421 (seats), e3cd93e3 mover part.
- **Left for other slices**:
  - 21 (save/load): `081e0c1b` (`SuppressVobTriggerDispatch` + `RebindNpcAliases`): 21 adds the flag to
    `GameStateService` and the guard at the top of `VobService.DispatchTrigger`.
  - 09: `UseMob` state propagation + `TriggerLinkedMover` (930b8b76) – use `VobService.TryGetMovers` (the singular
    `TryGetMover` isn't ported); `a696f99b` combat tuning.
  - 04: `Print` external (85344e0a) needs 04's `PrintScreen`; `UseMob` `SetBodyState` for seated talking (8e6b8f44)
    and the other `EnableMobSeatFix` places.
  - 01: morph warn-once fix; lock pick "only a held pick picks" part of 6334e567.
  - 11: `PERC_ASSESSUSEMOB` in `VRFocus.OnGrabbed`, mob owner diagnostics in `VobService` (00f95e72).
  - 13: must not port `EnsureOwnVobLoader` again.
- **Deviations from the source**:
  - `VRWheelInteraction` not ported: nothing in the final source references it (GUID and name searched). Wheels work
    through `VRFocus` → `HandleMobGrab`.
  - Mover timing rewritten after OpenGothic (per-segment, cm/ms, rotation speed) – the source lerped first → last
    keyframe with m/s and a fixed 0.5 s for rotations. Production multiplier 3.48 → 1.
  - Mob grab logic lives in Core `MobInteractionService` (source: `VRPlayerService`); VR only creates the dialog
    anchor. It dispatches directly (trigger/untrigger by mob state); conditions run as hero (`VmService.CallAsHero`);
    chests skipped; no-target is a Log.
  - `Wld_SendUntrigger` uses `DispatchUntrigger` (also code masters/message filters).
  - Registration/dispatch/GO creation de-duplicated (`RegisterNamedVob`, `TryGetTriggerVob`, two generic creation
    helpers), `IsLockPick`, `IsBed`, `VmCacheService.IsDigSpot`.
  - `ClearTriggerVobs` on every world change; `Dispose` also clears `VobsTriggerScript`.
  - Bench/throne yaw-inversion hack deleted (dead with the seat fix always on) and with it the `onBeforeRender`
    moves of `VRFocus` labels/`VRBillboard`.
  - `[Inject]` fields `readonly`, lines ≤ 120, logs in `LogCat.Vob`, less log spam.
- **Known limitations**:
  - Dispatch is synchronous: no event-manager queue, no `fireDelay` (depth cap instead).
  - Movers: no `OpenTimed` auto-close (`stayOpenTime`), no looping movers, no spline lerp (`lerpType`), no
    OpenGothic boat exception (`NW_HARBOUR_BOAT_*` rotation speed), speed 0 = jump to the target keyframe.
    Mover state isn't saved (save/load is slice 21).
  - `zCMessageFilter` Enable/Disable/Toggle only log a warning.
  - Only `TriggerScriptHandler` injects itself on demand; other handlers triggered by `INIT_` before their GameObject
    was activated would have null services (not seen in testing).
  - `oCMobDoor` (incl. G2 beds) is no longer in `VobsInteractable`: NPC `AI_UseMob` on doors/G2 beds finds nothing
    (as in the source).
