# Changelog: experimental build

Branch `experimental/game-completion-candidate-vibecoded-borobongo` (BoroBongo's experimental build). Newest first.
Each entry lists what players notice; the technical details and commits are on the [theme pages](README.md).

Almost everything is behind a `DeveloperConfig` flag and on in `Production.asset`. Tested on PCVR (SteamVR / Steam
Link), not on Quest/Pico standalone.

---

## In testing (not in the build yet)

- **Charged spells can't be spammed:** firing before a spell is cast asks the game's own `Spell_ProcessMana_Release`,
  like the original. Fireball, fire storm, wind fist, … still go off weaker; fire rain, summons, transformations and
  the rest fizzle out. No more demon army in 3 seconds.
- **Hand tracking moves all five fingers** of the VR hands (PCVR with hand tracking, e.g. Steam Link). Touching a
  controller switches back to the controller's finger curls.

## 2026-10-11: G2 can be finished, movers survive saves

**Gothic 2: Irdorath and the ending**
- The whole Irdorath island works: shooting the bridge targets, the gate levers and wheels, the touchplate puzzle rooms,
  the four pillars, the main door's lock and the last door behind the Innos statue.
- Shootable switches that are invisible get a **green X** where to aim; arrows, bolts and spells home in on them.
- The touchplates (stone buttons) can be highlighted and pressed.
- Code puzzles work like in the original: a wrong order is judged after all presses and triggers the "wrong" event.
- Dragons talk: their lines are heard with subtitles, also from far away (they're huge).
- The undead dragon's death plays its video and teleports you on; after the ending's videos and credits the game goes
  back to the main menu instead of closing.
- Treasure X marks are visible in builds again.

**Movers and the world**
- Movers (gates, doors, bridges, wheels) move through all their keyframes at the original speed.
- Nested movers move together (the Jharkendar portal opens fully).
- Levers that need an item (the portal ring) refuse without it ("I'm missing something"); letters you must read first
  work too (Irdorath's last door).
- **Saves keep the world:** opened gates, doors, pillars and portals stay open after loading and when you come back to
  a world; one-time switches stay used. Before, loading closed everything again and could lock you in.
- Loading a save while playing no longer breaks levers and wheels until a restart.

**Video and stream**
- Videos play in both eyes and show on the monitor/stream too.

**Marvin (cheat panel)**
- God mode, "Friends" (spawns e.g. Diego, Gorn, Milten and Lester), the panel scrolls when it has many options.

## 2026-10-10: NPCs sense and chase like in Gothic, fire, hit numbers

**NPCs and monsters**
- Senses like the original: NPCs see only in front of them, hear you only when you walk or run, smell you up close.
  Sneaking behind an orc works.
- Orcs threaten before they chase you; chasing guards and orcs give up and go back to their routine.
- Orcs are friends with each other, monster packs don't fight among themselves; attack one orc and its friends come
  for you. The Ulu-Mulu makes orcs tolerate you, in the hand or at the belt.
- NPCs and monsters strike out of the run; archers and crossbowmen shoot once per attack and give up when you're out of
  range; NPC mages cast single spells.
- Sleeping resets NPCs to their routine for the new time.
- NPC bodies keep 1 m away from your head, attacks no longer push your hands.
- The G2 wisp amulet works (the wisp finds items).

**Combat and magic**
- Fire spells set NPCs on fire (flames on the whole body, burn dance); the fire storm burns everyone around the impact.
- Every hit does at least the game's minimum damage.
- Arrows, bolts and spells trigger switches (Sleeper's temple).

**UI and options**
- Hit numbers (`-X HP` over heads), HP/MAX and MANA/MAX on bars (Immersion settings).
- Health/mana bars, messages and menus draw on top of the world, your hands on top of them.
- Game profiles: pick G1, G2 or a mod when the game starts. "Restart game" in the settings.
- Marvin can be switched on/off in the settings; "TP +4 m" (Gothic's Marvin K) with fall rescue.

**Loot**
- Orc mummies (containers without a lid) can be looted like corpses.

## Up to 2026-10-09: the base

The branch started from `main` with these big blocks (details per theme in the [overview](README.md)):
- **Combat:** VR bows with a real string, crossbows, spells charged in the free hand and thrown; NPC melee with combos,
  ranged and magic attacks; knockouts like the engine.
- **NPCs:** engine-like perceptions (warnings, murder witnesses, owners guarding chests), NavMesh chases, swimming,
  falling, ladders, fleeing, berserk, NPCs drinking potions.
- **VR hero:** full body in armor with IK, transformation scrolls, torches, joints, armor/amulets/rings, books and maps.
- **Inventory:** VR trade counter, stack splitting, NPC loot backpack, item details.
- **World:** movers and triggers, winches, shrines/alchemy/rune tables/beds, treasure digging, original Bink videos in a
  VR cinema.
- **Look:** torch-lit caves, day and night, particles, light pool.
- **Mods:** G1 Mroczne Tajemnice, G2 Union mods (partly), Ogg Vorbis dubbing.
- **Save/load** (off by default, work in progress).
