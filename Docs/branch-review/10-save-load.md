# 10. Save / load (WIP, off by default)

[← Overview](README.md)

## What changed

- Two layers: ZenKit's native save plus a `UNITYSAVE.json` with hero and NPC state the native save doesn't carry
  (HP, positions, dead/unconscious, routines, protection, equipped items, model scale).
- NPC restore from merged snapshots with stable keys (GO name, spawn waypoint for monsters), FP routing restored,
  dead NPC animations, session carry-over, chests tracked; multi-world init and cold-start load.
- Daedalus NPC alias bindings (e.g. G2 `BAU_4300_ADDON_BRAGO = BDT_1014_BANDIT_L`) rebound after load by re-running
  `INIT_` with `Wld_InsertNpc` suppressed.
- G2 menus: New Game, save slots 16–20, foreign saves.

**Added 2026-10-10 – 2026-10-11**

- **Movers and triggers are saved:** keyframe, open and disabled state of every changed mover plus trigger activation
  counts, per world in `UNITYSAVE.json`; restored on load and when coming back to a world (level change). Before, every
  mover was closed again after loading - softlocks in Irdorath, the Adanos temple, the Jharkendar portal.
  Movers/triggers take their entry when they're created, lazy loaded ones too.

**Added 2026-10-11 – 2026-10-13**

- **Movers keep their pose:** a mover restored from a save takes its saved pose only in `Start`, after its child VOBs
  exist - G2's canyon teleport platform, its trigger and the focus stone were 9 m underground after loading.
- **Code masters keep their progress** (pressed slots, count) like the engine's CodeMaster keys; older saves count
  used-up slots and free them when all were used - G2's Irdorath bridge stayed up after loading.
- **Spawned NPCs are saved** per world (Marvin, a mid-game `Wld_InsertNpc`) with position, HP and name; summons aren't.
- **Every visited world keeps its NPCs** (position, HP, dead, spawned ones) in the save and when leaving it - also in a
  game started new.

## Key commits

`dbb7d674`/`2eb4388e` system + data classes · `c41cf7a0` gated by flag · `f9fef67a`/`6f8b5707`/`da641097`/`9d2f1897`/
`842d2b19` NPC restore · `fefd2f0d` alias rebind · `82677fcb` protection and equipment · `081e0c1b` INIT_ trigger crash ·
`8d8f5aba` G2 menus

2026-10-10/11: `868cc590` mover/trigger state · `d8666bdb` registry after an in-game reload

2026-10-11/13: `45c99bab` mover pose after children · `dbba8c19` code master state · `8ed9824a` spawned NPCs · `6e70e3fa` world revisit state

## Main files

`Services/World/SaveGameService.cs` (+754) · `Services/World/UnityCustomSave.cs` (new) ·
`Domain/Npc/NpcInitializerDomain.cs` (+332)

## Config flags

`EnableSaveLoadSystem` (**off** in Production), `EnableSaveFeature`, `EnableLoadFeature`.

2026-10-10/11: `EnableLogicVobSaveState`

2026-10-11/13: `EnableSpawnedNpcSaves`, `EnableWorldRevisitState`

## Review notes

- ⚠️ **Off by default and still WIP.** Large surface (`SaveGameService` +754) with no tests. Decide whether it
  ships behind the flag or stays out of a merge.
- ⚠️ `UnityCustomSave.cs` holds five DTO classes in one file (conflicts with "one class per file" — acceptable for
  plain data, but worth a decision).
- ✅ Newer features were written with save/load in mind (model scale, equipment, protection are saved).

## How to test

- Enable `EnableSaveLoadSystem`, save in G1 Old Camp, quit, load: NPCs at posts, dead stay dead, inventory and
  equipment kept.
- Open a door/gate/portal, save, load: it stays open and its one-time switch stays used.
- Change the world and come back: opened movers stay open. New game after a loaded save: everything closed.

- G2: activate the canyon teleport, save, load - it still works.
- G2 Irdorath: hit one bridge target, save, load, hit the other - the bridge comes down.
- Spawn NPCs with Marvin, save, load - they're back where they were.
- G2: kill NPCs in Khorinis, go to the Valley of Mines and back - they stay dead.
