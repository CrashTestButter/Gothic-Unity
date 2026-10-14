# 06 Lighting & day/night

Branch: `split/06-lighting` (worktree `GU-split-06-lighting`), based on `origin/main` (`e91bc09c`).

## What

- **Caves and houses lit by their torches.** Gothic bakes static lights into the vertex light outdoors (we render
  that already) and into lightmaps indoors (we don't), so caves and houses were dark. Static lights next to light
  mapped polygons now become runtime stationary lights.
- **Shared light pool.** All world lights (fires and the static lights above) share the shader light slots: 1023 on
  PC (Unity's cap for global arrays), 512 on mobile. The lights closest to the camera get a slot, the same way
  OpenGothic lights everything near you. Unloaded lights free their slot right away, and the pool is re-sorted every
  2 s. Before this, G2's ~700 cached fire lights didn't fit into 512 slots, so the ones above 512 never lit
  anything.
- **Night falls on the world.** Gothic's sky `polyColor` (bright by day, dark blue at night) now scales the outdoor
  world's baked light and the sun/ambient color of VOBs and NPCs. Before, only the hands (URP ambient) followed the
  time of day. Light mapped rooms and caves, fires and torches keep their light.

## Why

Engine parity. In the original game, caves and huts are lit by their torches and the world gets dark at night.
OpenGothic is the reference for lighting every light near the camera.

## Config

Kept (in `DeveloperConfig` → Lighting → "Day/night and world lights"):
- `DayNightLightStrength` (tunable; **0 = day/night off**, 1 = Gothic's sky colors).
- `EnableStaticLightsInLightMappedAreas`, kept as a **performance escape hatch**. Static lights in caves and houses
  add up to 16 lights per world renderer, evaluated per vertex in `Lit-World`. On mobile this is the setting to turn
  off if caves cost too much; caves then stay dark like on `main`. The tooltip says so.
- `EnablePooledStationaryLights` (switches between the pool and the old fixed slots, needed on mobile where G2's
  ~700 fire lights don't fit into 512), and the tunables `StaticLightPoolSize`, `StationaryLightMaxDistance`,
  `StationaryLightPoolRefreshSeconds`.

Dropped: `EnableDayNightWorldLight`. It duplicated `DayNightLightStrength = 0`.
`Production.asset`: same values as the source branch (strength 1, 1023, 80 m, 2 s, both switches on).

## How to test (VR, ~10-15 min)

1. **G1: Saturas' cave / Old Mine entrance, or any G2 cave.** The walls around torches and braziers are lit and the
   light falls off with distance. Before this PR they were flat dark.
2. **G1 Old Camp / G2 Khorinis at night.** Use the Marvin time skip, or set `StartTimeHour` to 23. The ground and
   buildings outdoors turn dark blue. Huts and houses stay lit inside, and campfires/torches still light the ground
   around them.
3. **Day again.** Skip to noon: the colors are back to normal, and the light changes smoothly between sky states.
4. **Pool.** Walk through the G2 city (many fires). Lights keep appearing ahead of you with no gaps around you.
   The `[StationaryLights] pool: …` log line (LogCat `Vob`) shows lit vs. loaded lights.
5. **Switches.** Set `DayNightLightStrength` to 0 → always day. Turn off `EnablePooledStationaryLights` → the old
   behavior (fire lights keep a fixed slot), with the warning when they don't fit.
6. **Mobile (Quest/Pico), needs a device run.** `StationaryLighting.hlsl` uses 512 slots under
   `SHADER_API_MOBILE` and `StationaryLightsService` uses 512 when `Application.isMobilePlatform`. **Verify that the
   `SHADER_API_MOBILE` variant (512 slots) is the one used on Quest/Pico Vulkan.** If the shader compiles 1023 while
   C# uploads 512 (or the other way round), the lights are wrong or missing. Check that caves
   are lit, that G2 Khorinis shows its fires near you (the pool hands 512 slots out by distance), and that the frame
   rate is unchanged compared to `main` (the arrays are re-uploaded only when the pool changes).

## Review notes

- Source commits: `4982a263` (torch-lit caves, light pool), `d07e1c4d` (day/night).
- Branch commits: `b3fdf207` (light pool), `3db13967` (day/night), `3ca511ca` (review fixes).
- **Vertex `NormalW` is now an "outdoor" flag.** The world mesh's normal is `SNorm16 x4`. `WorldMeshBuilder` writes
  `w = short.MaxValue` (1.0) on vertex lit outdoor polygons and `0` on light mapped ones, and `Lit-World.shader` reads
  `normal.w` to scale only outdoor light by `_DayLightColor`. Both places carry a comment. Anything else that reads
  the world normal's `w` has to know this.
- `MAX_TOTAL_STATIONARY_LIGHTS` (shader) and `StationaryLightsService._maxTotalLights` must stay in sync (both
  1023/512). Both places have a comment.
- The global light arrays now always have the full size (1023/512) instead of the cached light count, because Unity
  fixes a global array's size the first time it's set.
- **Left out for later slices:**
  - The reserved "dynamic light" slot (`DynamicLightIndex`, `UpdateDynamicLight()`, `ClearDynamicLight()`) and
    `StationaryLight.Refresh()` are used by the torch (slice 14) and the Light spell (slice 20). Whichever of those
    lands first re-adds them. Reserve one slot right after the cached lights and start the pool one slot later
    (`_firstPoolSlot = cachedCount + 1`); in the source this was `DynamicLightIndex = cachedCount`.
  - The `RoomService` sector cells in `WorldMeshBuilder` belong to slice 11 (it will add its own parameters to
    `ExtractChunks`).
- **Deviations from the source:**
  - Without the dynamic slot, the pool starts at `_firstPoolSlot` (= number of cached lights) instead of
    `DynamicLightIndex + 1`. With `EnablePooledStationaryLights` off, the cached lights may use all slots.
  - The "cached lights don't fit" warning only logs when pooling is off. With pooling on it fired on every world load
    even though nothing was lost.
  - Removed the "V1:" prefixes from tooltips, moved `SetDayLight`'s summary from a field onto the method, and
    wrapped lines to 120 characters.
  - Review fixes (`3ca511ca`): `EnableDayNightWorldLight` dropped (strength 0 = off); the `_polyColor` field moved
    to the top of `SkyService`; the repeated `[Sky] Day light` log removed; the pool computes each light's reach
    once per refresh instead of twice per sort comparison. The reach subtracts the range, so it stays a plain
    distance; squared distances wouldn't sort the same.
- **Known limitations:**
  - Lights are still assigned to renderers when they're created (an existing limitation), so lazily loaded VOB
    meshes aren't lit by lights that were already active.
  - `RebalancePool` sorts the pooled lights in reach every 2 s (reach computed once per light, reused lists).
    That's cheap for ~1000 lights on PC. Measure it on Quest.
