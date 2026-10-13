# 8. Rendering: lights, day/night, particles, Bink video

[← Overview](README.md)

## What changed

**Lights**
- Static lights in light mapped areas (caves, huts) light the walls — Gothic used lightmaps there, which we don't
  render. Light mapped polygons are marked while building the world mesh.
- **Light pool like OpenGothic:** all world lights (fires and static) share the shader's light array, the ones
  closest to the camera get a slot (re-sorted every 2 s, immediately freed when unloaded). Array size 1023 on PC,
  512 on mobile (`SHADER_API_MOBILE`). G2 had ~700 fire lights, more than the old 512 array.

**Day and night**
- Gothic's sky `polyColor` scales the outdoor (vertex lit, no lightmap) world light and the sun/ambient of VOB and
  NPC shaders. Rooms, caves and fires keep their light.

**Particles**
- Spell effects around the caster, hit effects, animation PFX, `Wld_PlayEffect`, blood; world particle
  controllers and particle VOBs show up (Saturas' pentagram); loops emit at Gothic's rate (full torch flames).
- Smoke and other `BLEND` particles use plain alpha blending: the premultiplied blend relied on the URP
  `_ALPHAPREMULTIPLY_ON` variant, which builds strip for runtime-made materials (square smoke in builds only).

**Bink video**
- Original `.bik` videos play in a dark VR cinema, decoded by a C# port of OpenGothic's decoder on a background
  thread, synced to the audio sample position. World paused during videos, skippable ("(Trigger) skip" under
  the screen, localized: en/pl/de, English otherwise).

**Added 2026-10-10 – 2026-10-11**

- The Bink video cinema and the lightning strips render in both eyes (stereo instancing macros were missing -
  single-pass instanced drew them only for the left eye).
- The video cinema shows on the monitor/stream (spectator camera) too, its smoothing runs on unscaled time.

**Added 2026-10-11 – 2026-10-13**

- **Particle controllers** show their particle visual like the engine (`zCPFXControler`, OpenGothic `PfxController`) -
  `pfxName` is mostly an empty Spacer leftover `.PFX`: G2's active teleport platforms (`RESURRECTION.pfx`, visible once
  the mover raises them), cauldrons and the bandit camp's fire smoke showed nothing.
- **Texture array materials** follow the sub mesh grouping (opaque, alpha cutout, water in first-seen order) - sub mesh
  `i` got the material of Gothic's sub mesh `i`: palm leaves sampled the opaque array with alpha indices (stone
  textures without alpha on G2 addon palms, fishing nets and Mroczne Tajemnice meshes).

## Key commits

`4982a263` lights + pool · `d07e1c4d` day/night · `8c4b4f51`/`d4e451c5` particles · `1fb8180a` PFX loops · `bc831b78` square smoke in builds ·
`d688f290`/`9504e2c9`/`9f984e2c` Bink · `2de7f2dc` Bink attribution

2026-10-10/11: `9775ab32` stereo cinema · `443afd62` spectator cinema

2026-10-11/13: `87bb86c8` particle controller visuals · `4d39c699` texture array materials

## Main files

`Services/World/StationaryLightsService.cs` (+271) · `Adapters/Vob/StationaryLight.cs` ·
`Domain/Meshes/Builder/WorldMeshBuilder.cs` · `Services/World/SkyService.cs` · Shaders `Lit-World.shader`,
`GothicIncludes.hlsl`, `StationaryLighting.hlsl`, `GothicBinkYUV.shader` · `Domain/Meshes/Builder/VobPfxMeshBuilder.cs`,
`Services/Meshes/ParticleService.cs` · `Bink/*` (new), `Adapters/Video/BinkPlayer.cs`, VR `UI/VRCinema.cs`

## Config flags

`EnableStaticLightsInLightMappedAreas`, `StaticLightPoolSize`, `EnablePooledStationaryLights`,
`StationaryLightMaxDistance`, `StationaryLightPoolRefreshSeconds`, `EnableDayNightWorldLight`,
`DayNightLightStrength`, `EnableAnimationPfx`, `EnablePfxMinimumEmission`, `EnablePfxFullRateLoops`,
`EnableWldPlayEffect`, `EnableScriptVideos`, `EnableCinemaSkipHint`.

2026-10-10/11: —

2026-10-11/13: `EnablePfxControllerVisuals` (needs `EnableParticleEffects`), `EnableTextureArraySubmeshMaterials`

## Review notes

- ✅ Shader loops are unchanged (still max 32 lights per renderer); the pool only decides *which* lights get slots.
- ✅ Bink decoder is self-contained, no Unity dependency, licensed and attributed (see its `README.md`).
- ⚠️ **Mobile untested.** 512 slots, the decoder thread and the extra light work need a Quest/Pico run.
- ⚠️ Lights are still assigned to renderers once at creation (existing limitation): lazily loaded VOB meshes are
  not lit by lights that were already active.
- ⚠️ The world vertex `NormalW` is now used as an "outdoor" flag — anything else reading it must know.

## How to test

- G1 Saturas' cave and G2 caves: walls lit by torches. Walk through Khorinis at night: fires light the ground,
  the world turns dark blue, huts stay lit.
- Start a new game: the intro video plays in the cinema, in sync, skippable.
- Watch a video in the headset (both eyes) and on the monitor/OBS at the same time.

- G2 Jharkendar: activate the canyon teleport - sparks over the platform. Cauldrons bubble in the bandit camp.
- G2 addon beach and Mroczne Tajemnice: palms have leaves, fishing nets look like nets.
