# Phase 1 - XRI rig, input, locomotion, UI, simulator

**Goal:** a second player rig built on XR Interaction Toolkit that implements the Phase 0 ports `IVrRig`,
`IVrInput`, `IVrHaptics` and `IVrUiPointers`. A contributor without HVR can boot into the world, walk, swim, use
menus and dialogs, and watch videos, on a headset or in the simulator. Grabbing is not part of this phase.

**Size:** L (2-3 weeks) · **Depends on:** Phase 0 · **Unblocks:** Phases 2-4 (parallel)

## Exit criteria

- [ ] `DeveloperConfig.VrInteractionBackend = Xri` boots Quest, Pico and PCVR builds into the world without HVR
      installed.
- [ ] Every comfort setting from the accessibility menu works on the XRI rig, and existing Gothic.ini values load.
- [ ] Menus, dialogs, virtual keyboard, cinema, subtitles, print-screen messages and the spectator camera work.
- [ ] Swimming/diving behaves as on HVR (strokes, gravity, fade with air, walking out of water).
- [ ] The XR Interaction Simulator replaces the HVR simulator for the Xri backend.
- [ ] CI builds the Xri backend without the private HVR repo (new job or label).

## Packages

- Add `com.unity.xr.interaction.toolkit` 3.x. Pin an exact version that is at least 2 weeks old and compatible with
  Unity 6000.3 (check the package manager "compatible" list). `com.unity.inputsystem` and `com.unity.xr.core-utils`
  come in as dependencies.
- Import the samples **Starter Assets** (input actions, reference rig) and **XR Interaction Simulator**. Copy what we
  need into `Gothic-VR/` and don't reference the `Samples/` folder from our prefabs, so a sample re-import can't break
  us.
- Keep OpenXR 1.16, the Meta Quest feature, `pico-openxr-sdk` and XR Hands 1.7 as they are. XRI uses exactly these.

## Backend switch

- Add `DeveloperConfig.VrInteractionBackend { Hvr, Xri }`: the C# field **and** the value in the DeveloperConfig
  asset (see the `feature-checklist` skill). Default `Hvr` while HVR is installed, `Xri` otherwise.
- `Player.unity` (and `Lab.unity`) contain both rigs, disabled. `VRContextInteractionService.SetupPlayerController`
  enables the one matching the backend (today it already enables `transform.parent.parent`).
- The Xri backend files carry no define, since XRI is free and always installed. The Hvr backend stays behind
  `GOTHIC_HVR_INSTALLED`.

## Rig (`Gothic-VR/Resources/VR/Prefabs/VR - Player XRI.prefab`, built via UnityMCP)

```
VR - Player XRI
└─ XR Origin (XROrigin, CharacterController, XRBodyTransformer, LocomotionMediator)
   ├─ Camera Offset
   │  ├─ Main Camera (TrackedPoseDriver, Camera, AudioListener, WallFade, ScreenFade)
   │  ├─ Left Controller  (TrackedPoseDriver / XRI controller input, NearFarInteractor [Phase 2], hand visual)
   │  └─ Right Controller (same)
   ├─ Locomotion
   │  ├─ ContinuousMoveProvider   (forward source = head / left / right controller)
   │  ├─ SnapTurnProvider + ContinuousTurnProvider (one enabled at a time)
   │  ├─ TeleportationProvider
   │  ├─ GravityProvider, JumpProvider
   │  └─ ClimbProvider [Phase 4]
   └─ Gothic components moved from the HVR rig: MenuHandler, VRCinema, VRSwimDive, VRSpectatorCamera,
      VRUiOnTop, VRPlaceInFrontOfCamera, VRPrintScreen anchor, VRMouth, VRHandsOnTop, …
```

`XriRig : MonoBehaviour, IVrRig` maps the port onto these components.

| `IVrRig` member | XRI implementation |
|-----------------|--------------------|
| `MovementEnabled` / `TurnEnabled` / `TeleportEnabled` | enable/disable the providers (or their input actions) |
| `MoveSpeed` / `RunSpeed` / `CanSprint` | `ContinuousMoveProvider.moveSpeed`; sprint is **ours**: a sprint action switches between the two speeds |
| `Gravity` / `MaxFallSpeed` | `GravityProvider` (gravity, terminal velocity); swimming sets 0 |
| `CanJump` | `JumpProvider.enabled` |
| `CanCrouch` | **ours**: a crouch action toggles an extra `Camera Offset` y-offset and shrinks the `CharacterController` height |
| `StepOffset` | `CharacterController.stepOffset` |
| `IsGrounded` | `CharacterController.isGrounded` (or `GravityProvider.isGrounded`) |
| `IsClimbing` | `ClimbProvider` state (Phase 4; `false` until then) |
| `Teleport(pos, rot)` | `TeleportationProvider.QueueTeleportRequest` with `MatchOrientation.TargetUpForward`; raise `Teleported` after the provider applied it. Today's workaround of setting rotation afterwards (`VRContextInteractionService.cs:202`) may no longer be needed. Verify. |
| `ApplyComfortSettings` | `VrSitStand.Sitting` → `XROrigin.RequestedTrackingOriginMode = Device` + `CameraYOffset` = eye height. `Standing`/`PlayerHeight` → `Floor`. HVR's `Standing` *scales* the camera (`HVRCameraRig.cs:244`); if we want that, scale `Camera Offset` by `EyeHeight / HMD height` ourselves. `VrMoveDirection` → `ContinuousMoveProvider.forwardSource`. `VrTurnType` → enable snap or continuous turn. `snapDegrees` → `SnapTurnProvider.turnAmount`. `smoothSpeed` → `ContinuousTurnProvider.turnSpeed`. |
| `SetGrabbingEnabled` | no-op in Phase 1; Phase 2 toggles the interactors |
| `Fade(alpha, speed)` | own `ScreenFade` (see below) |

## Input (`Gothic-VR/Resources/VR/Input/GothicVR.inputactions`)

Start from the Starter Assets actions and add Gothic actions per hand:

| Action | Binding (Quest/Pico/Touch-style) | Index | Simulator key |
|--------|----------------------------------|-------|---------------|
| Menu | left secondary (Y) | left B | Esc |
| Jump | right primary (A) | right A | Space |
| Crouch | right secondary (B), as today (`HVRPlayerInputs.GetCrouch`; Vive: trackpad up) | right B | Z / X |
| Sprint | left thumbstick click, as today (`HVRPlayerInputs.GetSprinting`) | same | Left Shift |
| BothGrips | derived in `VRPlayerInputs` from both grip actions | | T |
| SkipVideo | trigger or primary, either hand (`VRCinema`) | | |

`XriInput : IVrInput` reads the actions and keeps a `VrButtonState` per hand/button (pressed this frame / held /
released this frame). `IsSimulator` is true when the simulator is active.

## Haptics

`XriHaptics : IVrHaptics` uses one `HapticImpulsePlayer` per controller. Map the presets from `VrHapticsService`
(amplitude/duration/frequency as today: Info 0.15/0.25/30, Warning 0.25/0.5/100, Error 0.4/0.8/175,
Success 0.5/0.6/80). Note: HVR's `HapticData(duration, amplitude, frequency)` argument order. Check it and keep the
same perceived strength.

## UI

- `EventSystem` gets `XRUIInputModule` instead of `HVRInputModule` (rig variant only).
- `XriUiPointers.RegisterCanvas` adds a `TrackedDeviceGraphicRaycaster` to the canvas (if missing).
  `RegisterAllCanvases` keeps today's one-shot scan in `InitUIInteraction`, but it can be removed later once all VR
  canvases carry the raycaster in their prefabs.
- `TryGetHandSide(eventData)`: `TrackedDeviceEventData.interactor` → which controller → `VrHandSide` (Marvin logo
  haptics).
- Ray interactor per hand for UI only in this phase (Phase 2 replaces it with the Near-Far interactor).

## Fades

- `ScreenFade`: a camera-attached inverted sphere or quad with an unlit URP material whose alpha is driven by
  `Fade(alpha, speed)`. Used by `VRSwimDive` (dive darkness by air), `VRSeat`, and scene transitions where
  `HVRCanvasFade` is used today.
- `WallFade` (replaces `HVRHeadCollision`): `Physics.OverlapSphere` on the camera with the world layers. Fade towards
  black by penetration depth. UltimateXR's `UxrCameraWallFade` (MIT) is a reference.

## Swim/dive (`VRSwimDive.cs`)

Already on `IVrRig` after Phase 0. Check:
- Stroke movement reads `IVrRig.LeftHand/RightHand` positions and moves `CharacterController` directly. The XRI
  providers must not fight it, so `MovementEnabled = false` while swimming, as today.
- Rotation math (`VRSwimDive.cs:350-415`) assumed HVR's rig hierarchy (`CameraRig.TransformDirection`). Re-check
  with `XROrigin.Origin` and the camera's yaw.

## Simulator

- Use the XR Interaction Simulator prefab in `Player.unity`. Enable it via `EnableVRDeviceSimulator`, as today with
  `HVRBodySimulator`.
- Port the on-screen help from `VRSimulatorControlsGUI` (keys list) as a small `OnGUI`/UGUI overlay.

## CI

- Add a build job that doesn't inject HVR, removes `GOTHIC_HVR_INSTALLED` and sets the backend to `Xri`. Methods
  stay `VRBuilderActions.Perform*Build`.

## Test plan

On Quest 3, Pico 4 and PCVR (Index), backend `Xri`:
1. Main menu: laser click on every menu page, virtual keyboard (save name), settings sliders.
2. All accessibility settings: sit-stand (sitting in a chair → eye height correct), move direction head/left/right,
   snap 5°…, smooth speed min/max. Restart → kept.
3. Walk, sprint, jump, crouch, fall from a ledge, walk up stairs, Xardas' tower water exit (step height).
4. Teleport from code: new game spawn, `TeleportPlayerTo` (Marvin goto, chapter change), spectator camera follows.
5. Dialog with an NPC (start with the menu fallback until Phase 2 grab-to-talk), subtitles, cinema video, skip.
6. Swim and dive; air bar fade; climb out.
7. Head into a wall → fades.
8. Simulator on a desktop without a headset: all of the above that applies.

## Risks

| Risk | Mitigation |
|------|-----------|
| Sitting/Standing semantics differ from HVR | Implement `PlayerHeight` first (most players), then `Sitting`; check `Standing` scaling with a playtester. |
| Pico button names differ in OpenXR profiles | Test the Pico 4 profile early; bind via the generic `<XRController>{LeftHand}/secondaryButton` paths where possible. |
| Gravity/jump providers vs. our `KeepPlayerWithin` (`controller.Move`) | Both move the `CharacterController`. Keep `KeepPlayerWithin` after the providers (late update order) and verify dialogs. |
