# 2. Magic: VR casting, NPC casters, summons, transformations

[← Overview](README.md)

## What changed

**Hero casting in VR**
- Spell casting overhaul: the rune is held in one hand; the spell effect glows in the hand while mana is invested
  (at the spell's own `time_per_mana`), minimum cast time configurable for "instant" G2 spells.
- Projectile spells (fireball, fire arrow, lightning, ice block…) are charged in the free hand and thrown; throw
  strength from the swing; Gothic invest levels change effect and sound with a burst.
- G2 spells pay their mana in `Spell_Cast_*` like the engine (they fizzled and still ate mana). Refused spells refund.
- Telekinesis (capsule range, bag targeting), Light spell, teleports put the held rune into the backpack.
- Spells without `MFX_<name>_INIT` show their own `VISUALFX` in the hand (ice wave shows ice, not a fireball).

**NPC casters**
- Mages and archers complete their casts (protected from `Npc_ClearAIQueue` resets), get real spell effects,
  fire `PERC_ASSESSMAGIC` so Daedalus drives the reaction (sleep, freeze, fear, berserk…).

**Summons**
- Spawned beside the caster, never fight their master (`NpcContainer.SummonedBy` ↔ Friendly), run, defend the
  hero, and are a training dummy for their own summoner (hits don't turn them hostile).
- NPCs treat the hero's summons the way they treat the hero: enemies fight them, everyone else ignores them.
  **Deviation:** vanilla G2 makes humans hostile to the `GIL_SUMMONED_*` guilds (`B_InitMonsterAttitudes`), so guards
  attack summons there.

**Transformation scrolls**
- The hero becomes the monster: a puppet without AI, camera at its eyes, walks/strafes/swims/sneaks like Gothic.

**Other spells**
- Shrink scales monsters (`Mdl_SetModelScale`, kept on load). Fear: NPCs flee (`AI_Flee`). Berserk.

**Added 2026-10-10 – 2026-10-11**

- **Burning:** fire spells (`DAM_FIRE`) set NPCs on fire for 4 s like the engine - `VOB_BURN` flames over the whole
  skinned body, fire light, burn sound and the `s_Fire_Victim` dance; another hit extends it.
- **Fire storm splash:** the burning area damages everyone within 2 m of the impact, also when it lands on the ground;
  knocked out NPCs aren't finished off.
- **NPC mages** cast one spell per attack (wind-up `t_MagRun_2_FBTShoot`, then `t_FBTShoot_2_Stand`) instead of bursts.
- Spamming a charged spell no longer stacks its effects in the hand; a throw clears them.

**Added 2026-10-11 – 2026-10-13**

- **Charged spells can't be spammed:** firing before the spell is cast asks the game's `Spell_ProcessMana_Release`
  like the engine - fireball, fire storm, wind fist, ... go off at their level, fire rain, summons, transformations and
  the rest fizzle out (a summon army in 3 seconds, fire rain on repeat). Invested mana isn't refunded.
- **Summons have no target** like the engine (`targetCollectAlgo = TARGET_COLLECT_NONE`) - aimed at an NPC, the summon
  hit him for `damage_per_level` × mana and his magic perception turned him against the hero.
- **Only harmful spells hurt:** like the engine, only `SPELL_BAD` spells deal damage - sleep, light, charm, ...
  (`SPELL_NEUTRAL`/`SPELL_GOOD`) inherit `damage_per_level = 1` from `C_Spell_Proto` and hurt their target.
- **Thrown spells fly like a ball:** direction and speed come from the throwing hand's swing (sampled over 0.1 s), not
  the camera's view. A weak throw arcs with gravity, letting go without a swing drops the spell, and only a real throw
  homes on targets (homing disables gravity). In the simulator (WASD) the old camera aim stays.

## Key commits

`eaba6cf6` casting overhaul · `d8e51ea3` NPC casters · `65a4ed33` casts complete · `942032bc` summons ·
`cdc628c2` throwable spells · `5e898826` G2 mana in Spell_Cast · `934b0305` time_per_mana · `a063883c` shrink ·
`f2ebe1f2`/`040ecaf8`/`fc784fa9` transformations · `64d0bd47` VISUALFX in hand · `04d53a8d` teleport keeps items ·
`79896b75` AI_Flee · `a6206509` summons as training dummy · `bc831b78` NPCs ignore the hero's summons

2026-10-10/11: `9b7bf640` burning · `ca86ccb8` fire storm splash · `74066e65` NPC mage one spell per attack · `f819bab9`
charge effects cleared

2026-10-11/13: `ff788ac1` spell release check · `d940825d` summons without target · `0d6977ad` only harmful spells hurt ·
`aaab7cdf` throw physics

## Main files

`Gothic-VR/.../VobItem/VRRuneCaster.cs` (+1356) · `VRTransformPuppet.cs` (+748) · `VRTransformService.cs` ·
`Const/SpellConst.cs` · `Services/Npc/FightService.cs` (SpellHit) · `Domain/Npc/Actions/AnimationActions/Flee.cs`

## Config flags

`EnableThrowableSpells`, `EnableSpellTimePerMana`, `MinSpellCastSeconds`, `EnableVrTransformations`,
`EnableSummonIgnoresMasterHits`, `EnableNpcsIgnoreHeroSummons`, `EnableModelScale`, `EnableAiFlee`, `EnableTeleportKeepsHeldItems`,
`EnableSpellBodyFx`, `SummonSpawnRangeMultiplier`, `RangedCombatRangeMultiplier`, `EnableCasterPerception`.

2026-10-10/11: `EnableBurning`, `EnableSpellSpreadDamage`

2026-10-11/13: `EnableSpellReleaseCheck`, `EnableSummonSpellWithoutTarget`, `EnableOnlyBadSpellsDamage`,
`EnableSpellThrowPhysics`

## Review notes

- ✅ Mana and spell logic call the Daedalus functions (`Spell_ProcessMana`, `Spell_Cast_*`) instead of hardcoding.
- ⚠️ **`VRRuneCaster.cs` is a 1,356-line adapter.** `CLAUDE.md` asks adapters to stay thin. Charging, invest levels,
  projectile throwing and spell lookup should move into a service/domain (`VRSpellCastService`?).
- ⚠️ `VRTransformPuppet.cs` (748 lines) is the same story.
- ⚠️ G2's AI uses the G1 fight move table (known issue).

## How to test

- G1 MT and G2: fireball charged in the free hand and thrown; charge levels show bursts.
- G2: summon a golem; it runs, fights enemies, ignores your hits.
- Transformation scroll: walk, swim, sneak as the wolf.
- Shrink the G1 troll; save, load — still small.
- Fireball an NPC: flames on the whole body for 4 s. Fire storm next to a group: everyone within 2 m burns.
- An NPC mage casts single spells with a wind-up, no bursts.

- Charge a fireball and release early: weaker fireball. Same with fire rain / a summon: it fizzles.
- Aim a summon rune at an NPC: only the summons appear, the NPC takes no damage and stays calm.
- G1: cast sleep on an NPC - he falls asleep without losing HP; a fireball still hurts.
- Throw a fireball hard at an NPC: it flies fast and homes. Throw softly: it arcs into the ground. Open the hand without
  a swing: the fireball drops in front of you.
