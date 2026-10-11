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

## Key commits

`dbb7d674`/`2eb4388e` system + data classes · `c41cf7a0` gated by flag · `f9fef67a`/`6f8b5707`/`da641097`/`9d2f1897`/
`842d2b19` NPC restore · `fefd2f0d` alias rebind · `82677fcb` protection and equipment · `081e0c1b` INIT_ trigger crash ·
`8d8f5aba` G2 menus

2026-10-10/11: `868cc590` mover/trigger state · `d8666bdb` registry after an in-game reload

## Main files

`Services/World/SaveGameService.cs` (+754) · `Services/World/UnityCustomSave.cs` (new) ·
`Domain/Npc/NpcInitializerDomain.cs` (+332)

## Config flags

`EnableSaveLoadSystem` (**off** in Production), `EnableSaveFeature`, `EnableLoadFeature`.

2026-10-10/11: `EnableLogicVobSaveState`

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
