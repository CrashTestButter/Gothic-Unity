# 07 Original Bink videos in a VR cinema

Branch: `split/07-bink` (worktree `GU-split-07-bink`), based on `origin/main` (`e91bc09c`).
Commits: `c3cc5d93` (decoder), `89138451` (player, cinema, externals), `49e28d66` (review fixes: cleanup,
threading).

## What
- Gothic's original `.bik` videos (intro, chapter videos, G1 ending) play directly, without converting them to MP4.
  - `Gothic.Core.Bink`: a decoder that turns `.bik` into raw YUV420 frames and float PCM audio. It has no Unity
    dependency.
  - `BinkPlayer` (Core adapter): uploads the 3 planes into R8 textures; the new `Gothic/Bink YUV` shader converts
    them to RGB on the GPU. The audio is streamed into an `AudioClip`. Decoding runs on its own thread. The picture
    follows the audio clock, minus the audio output latency, so picture and sound stay in sync.
- The Daedalus externals `PlayVideo` and `PlayVideoEx` are implemented. They are registered only if the scripts
  declare them, with or without a return value, to match the DAT symbol.
- `VRCinema` (VR adapter, `Player-Elements/Cinema.prefab`, nested inactive in `HVR - Player` and referenced by
  `VRPlayerController.Cinema`): the video plays in a dark room in front of the head. Screen distance and width are
  set on the prefab.
  - The screen keeps a fixed direction and follows teleports.
  - The world is paused while the video runs (the engine blocks the game too).
  - Several videos in a row are queued (G1 ending: Extro + Credits).
  - Skip with a trigger or A/X (keyboard: Space/Escape).
  - The player is locked meanwhile: no movement, turning, crouching, jumping or menu button. The previous values
    are restored afterwards.
- `VRCinemaService` (new VR scene singleton) owns the open cinema and listens to the new
  `GlobalEventDispatcher.PlayVideo` event. `GlobalEventDispatcher.VideoPlaybackFinished` fires when the cinema closes.
- The Logo scene plays the logo videos through the same cinema (same screen size and height, player locked,
  skippable) and loads the main menu on `VideoPlaybackFinished`. Its own screen, audio source and black box are gone.
- VR main menu: moved from 5m to 2.5m and scaled to 0.35, the same size as the in-game menu.
- `VideoService` also lists the `.bik` files.
- `GothicBinkYUV.shader` is added to *Always Included Shaders* because it's loaded with `Shader.Find`.

## Why
On `main`, `PlayVideo` isn't implemented, so chapter videos and the G1 ending don't play. The usual alternative,
converting the videos to MP4, is an extra manual step for every player.

## Config
- Kept flags: none. Videos are always on, so there is no `EnableScriptVideos` switch.
- Dropped flags: `EnableScriptVideos` (added and removed in this branch). `PlayVideo` is always registered.

## How to test (VR, ~10 min)
1. G1, new game: when the intro video is scripted, it plays in the dark cinema with sound, in sync.
   Alternatively, any `PlayVideo` call from a script.
2. Press a trigger or A/X after 0.5 s: the video is skipped and the world continues.
3. G1 chapter change (e.g. via Marvin/console to chapter 2): the chapter video plays, the world is paused, and the
   hero is teleported while the screen follows.
4. Logo videos (`playLogoVideos=1` in Gothic.ini): they play in the black cinema at the same size as in-game
   videos. Crouch, jump, moving and the menu button do nothing. A trigger or A/X skips to the next logo, then the
   main menu loads.
5. Look at a video for a while: no stutter, and sound and picture stay in sync.
6. Log: `[BinkPlayer] <file>: 640x480 @ ... fps` once per video. No per-frame or per-second spam.
7. Change the world during a video (e.g. via a teleport or level change while one plays): afterwards the world is
   rendered normally (not black with only UI), time and sound run.
8. Play several videos in a row (G1 ending or repeated chapter videos): memory doesn't grow per video (Profiler:
   no leftover `Bink` AudioClips, `Gothic/Bink YUV` materials or 1280x720 RenderTextures).
9. Main menu: same size as the in-game menu. Opening the tutorial via the Gothic-Unity box needs no big head turn.

## Licensing (maintainers decide)
- The decoder (`Assets/Gothic-Core/Scripts/Bink/`, 6 files) is a C# port of OpenGothic `common/bink`
  (Copyright (c) 2019 Try, **MIT**). OpenGothic's code is based on FFmpeg's `bink.c`, `binkaudio.c`, `binkdsp.c`,
  `fft`, `rdft` and `dct`, which are **LGPL-2.1-or-later**. Every copyright holder is listed in `Bink/README.md`
  together with the MIT notice, and each file keeps its origin header.
- The project is GPLv3. LGPL 2.1 §3 allows LGPL code to be relicensed as GPL, so including it is permitted.
- **No RAD Game Tools code or SDK, and no videos are shipped.** The `.bik` files are read at runtime from the
  player's own installation.
- `BinkPlayer`, the shader, `VRCinema` and `VRCinemaService` are our own code under the project license.

## Review notes
- Source commits: `d688f290`, `9f984e2c`, `2de7f2dc`, and the Bink part of `9504e2c9` (decoder thread and smooth
  clock).
- Deviations from the source (maintainability):
  - `VRCinema._instance` and the static `VRCinema.Play(...)` are replaced by the DI service `VRCinemaService`.
    `VRCinema` has no static state anymore and only exposes `Enqueue(path)`.
  - `BinkException` moved out of `BinkBitStream.cs` into its own file. The README file list was updated.
  - `BinkPlayer`'s diagnostics are removed: the stats log every second, the first-3-audio-packets log and the
    latency log. Playback logic is unchanged.
  - Lines over 120 characters are wrapped, except the generated tables in `BinkTables.cs` (128 lines, kept 1:1
    with the OpenGothic source on purpose).
- Fixes vs. the source, from review (commit `49e28d66`); these are bugs the source branch has too:
  - When a scene changes during a video, `VRCinema.OnDestroy` now also restores the camera's `cullingMask`,
    `clearFlags` and `backgroundColor`. The VR camera lives in the persistent Player scene, so before this it kept
    rendering only UI on black.
  - Per-video leaks are fixed. `VRCinema` destroys its screen material and RenderTexture (before, the texture was
    only `Release()`d). `BinkPlayer` destroys its YUV material and its streamed `AudioClip`.
  - Threading: `BinkPlayer.Close()` is idempotent (it runs from `Stop` and again from `OnDestroy`) and nulls the
    destroyed textures. The decoder thread works on a local video reference. If `Join(500)` times out, the stream
    isn't disposed under the running decoder: the decoder disposes it itself when it ends (`Interlocked.Exchange`).
  - The `[PlayVideo]` log uses `LogCat.Misc` instead of `LogCat.Dialog`.
- Left for other slices:
  - The source's `VRCinema.QuitAfterVideos` and `ScriptExitGame` (ExitGame/ExitSession end the game after the
    credits) belong to slice 14 (`adc2067a`). Slice 14 has to add them to `VRCinemaService`/`VRCinema` as instance
    members, not as statics.
  - `VRContextInteractionService` in the source also registers `VRScreenMessages` listeners. Those belong to slice
    04.
- Placement: `RegisterPlayVideo` and `PlayVideo` sit in `VmExternalDomain`'s existing `#region Print` (as in the
  source) to avoid adding a new region.
- Risks:
  - **Quest/Pico untested**: the decoder thread, R8 textures and the shader on Android/Vulkan.
  - The cinema pauses the world with `Time.timeScale = 0` and `AudioListener.pause`. `OnDestroy` restores both if
    a scene changes mid-video.
- Compile check: `RESULT: OK` (Core, VR, Flat, G1, G2, Lab, PlayMode tests, VR editor).
