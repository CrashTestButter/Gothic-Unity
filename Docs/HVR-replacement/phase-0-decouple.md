# Phase 0 - Decouple game code from HVR

**Goal:** game code in `Gothic.VR` talks only to Gothic-owned interfaces (ports). HVR moves behind one backend
folder. Nothing changes for the player: HVR still runs everything.

**Size:** M (1-2 weeks) · **Depends on:** nothing · **Unblocks:** all later phases, and any backend
(XRI, UltimateXR fork, own)

## Exit criteria

- [ ] No file outside `Gothic-VR/Scripts/Interaction/Backends/Hvr/` has `using HurricaneVR…` or an `HVR*` type.
- [ ] `#if GOTHIC_HVR_INSTALLED` exists only in the HVR backend folder and in `VRContextBootstrap` (backend selection).
- [ ] `Gothic.VR` compiles with `GOTHIC_HVR_INSTALLED` removed. Without a backend, VR mode fails at boot with a clear
      log error, not a compile error.
- [ ] Our prefabs no longer wire HVR UnityEvents to our methods in the inspector; adapters subscribe in code.
- [ ] Playtest on Quest + PCVR shows no behaviour change (see test plan).

## Proposed structure

```
Assets/Gothic-VR/Scripts/Interaction/            namespace Gothic.VR.Interaction…
├─ Models/      VrHandSide, VrButton, VrButtonState, VrGrabberKind, VrGrabArgs, VrHapticPreset, VrSitStand,
│               VrMoveDirection, VrTurnType
├─ Ports/       IVrRig, IVrInput, IVrHaptics, IVrUiPointers, IVrSocket, IVrSocketContainer
├─ Components/  VrInteractable (game-facing grab/hover events), VrSocketSlot (game-facing socket events)
└─ Backends/
   └─ Hvr/      #if GOTHIC_HVR_INSTALLED - HvrRig, HvrInput, HvrHaptics, HvrUiPointers,
                HvrInteractableBridge, HvrSocketBridge, VRSocket/VRShoulderSocket/VRSimulatorControlsGUI (moved)
```

It stays inside the `Gothic.VR` assembly, so no new asmdef and the star topology is unaffected. Folder names and the
`Ports` term are a proposal; align with the team before PR 0a.

## Design

### Models (replace HVR enums)

| New type | Replaces | Note |
|----------|----------|------|
| `VrHandSide { Left = 0, Right = 1 }` | `HVRHandSide` (34 uses) | Same int values. The lock-pick events pass `int` and `VrHapticsService` casts it to `HVRHandSide`, so keeping 0/1 keeps them working. |
| – | `GlobalEventDispatcher.HandSide { None=0, Left=1, Right=2, Both=3 }` | Keep it (fight events need `None/Both`), but add a `ToVrHandSide()` mapping in one place. Today the two enums use different numbers for Left/Right; document that in both enums. |
| `VrButton` (Grip, Trigger, Primary, Secondary, Menu, JoystickButton) and `VrButtonState { Active, JustActivated, JustDeactivated }` | `HVRButtons`, `HVRButtonState` | |
| `VrSitStand { Sitting=0, Standing=1, PlayerHeight=2 }`, `VrMoveDirection { Camera=0, LeftController=1, RightController=2 }`, `VrTurnType { Smooth=0, Snap=1 }` | `HVRSitStand`, `PlayerDirectionMode`, `RotationType` | **Same int values**: they're stored in the player's Gothic.ini (`VRConstants.IniNames`), so existing settings keep working. |
| `VrGrabberKind { Hand, Distance, Socket }` | `is HVRHandGrabber / HVRForceGrabber / HVRSocket` checks | |
| `VrGrabArgs { Kind, HandSide, Interactable, GrabberGo }` | `(HVRGrabberBase, HVRGrabbable)` event signature | |
| `VrHapticPreset { Info, Warning, Error, Success }` | `VrHapticsService.VibrationType` + `HapticData` | |

### Ports

```csharp
public interface IVrRig
{
    Transform Root { get; }
    Transform Head { get; }
    Transform LeftHand { get; }
    Transform RightHand { get; }
    Camera Camera { get; }
    CharacterController CharacterController { get; }
    float CameraHeight { get; }
    bool IsGrounded { get; }
    bool IsClimbing { get; }

    bool MovementEnabled { get; set; }
    bool TurnEnabled { get; set; }
    bool TeleportEnabled { get; set; }
    bool CanJump { get; set; }
    bool CanCrouch { get; set; }
    bool CanSprint { get; set; }
    float Gravity { get; set; }
    float MaxFallSpeed { get; set; }
    float MoveSpeed { get; set; }
    float RunSpeed { get; set; }
    float StepOffset { get; set; }

    void ApplyComfortSettings(VrSitStand sitStand, VrMoveDirection direction, VrTurnType turn, float snapDegrees,
        float smoothSpeed);
    void Teleport(Vector3 position, Quaternion rotation);
    event Action<Vector3> Teleported;
    void SetGrabbingEnabled(bool enabled);
    void Fade(float alpha, float speed);
}

public interface IVrInput
{
    VrButtonState GetButton(VrHandSide side, VrButton button);
    bool IsSimulator { get; }
}

public interface IVrHaptics
{
    void Vibrate(VrHandSide side, VrHapticPreset preset);
}

public interface IVrUiPointers
{
    void RegisterCanvas(Canvas canvas);
    void UnregisterCanvas(Canvas canvas);
    void RegisterAllCanvases();
    bool TryGetHandSide(PointerEventData eventData, out VrHandSide side);
}
```

`VrInteractable` is a plain MonoBehaviour on every grabbable prefab (items, mobs, NPCs, backpack). It exposes C#
events (`HoverEntered`, `HoverExited`, `BeforeGrabbed`, `Grabbed`, `Released`) plus `IsHeld`, `Colliders`,
`ForceRelease()`, `SetupColliders()`. The backend bridge (`HvrInteractableBridge`, later `XriInteractableBridge`)
sits next to it, translates framework events into `VrGrabArgs` and forwards them.

`IVrSocket` / `IVrSocketContainer`: `IsOccupied`, `Held`, `TryPlace(VrInteractable, bool raiseEvents)`,
`ForceRelease()`, `TryFindFree(VrInteractable, out IVrSocket)`, `Placed` / `Removed` events, plus audio fallback
clips. That covers everything `VRBackpack`, `VRNpcLoot` and `VRVobContainerPhysicsChest` call today.

Registration: `IVrInput`, `IVrHaptics` and `IVrUiPointers` as Reflex singletons in `ReflexVRSceneInstaller`.
`IVrRig` is resolved from the scene by `VRContextInteractionService.SetupPlayerController` and exposed through it
(as `VRPlayerController` is today).

## Work items

Split into four PRs so each one stays reviewable.

### PR 0a - Models and hand side

- [x] Add `Interaction/Models/*` (`VrGrabArgs` waits for PR 0c, it needs `VrInteractable`). HVR mapping lives in
      `Backends/Hvr/HvrEnumExtensions.cs` (`ToHvr()` / `ToVr()`).
- [x] Replace `HVRHandSide` in `VrWeaponAttackDomain`, `VRWeaponService`, `VRPlayerWeaponInteraction`,
      `VRContainerDoorPickingInteraction`, `VrHapticsService` (`VibrationType` → `VrHapticPreset`).
      `VRCinema` and `VRPlayerInputs` only pass it to `HVRController.GetButtonState`, so they move with `IVrInput`
      in PR 0b.
- [x] `VRContextMenuService.cs:115` and `VRPlayerController.SetNormalControls`: replace `HVRSitStand`,
      `PlayerDirectionMode` and `RotationType` with the new enums, converting at the HVR boundary
      (`VRPlayerController.ApplyComfortSettings`, the later `IVrRig` method).

### PR 0b - Rig, input, haptics, UI ports

- [ ] `IVrRig` ← implemented by `VRPlayerController` (moved to `Backends/Hvr/HvrRig.cs`, still subclassing
      `HVRPlayerController`).
- [ ] Callers switch from `VRPlayerController` to `IVrRig`: `VRContextInteractionService`, `VRSwimDive` (Gravity,
      speeds, `CharacterController.Move`, hand transforms, `CameraRig` → `Root`, `ScreenFader` → `Fade`),
      `VRSpectatorCamera` (`Teleporter.PositionUpdate` → `Teleported`), `VRCinema`, `VRCinemaService`,
      `VRFollowCamera`, `VRPlaceInFrontOfCamera`, `SpeechToTextHandler`, `VRContextDialogService`,
      `VRHandVelocityDebugger`.
- [ ] `IVrInput` ← `HvrInput` (wraps `HVRController.GetButtonState`). `VRPlayerInputs` keeps its Gothic-specific
      derived inputs (menu, both grips), but reads them through `IVrInput`. The simulator check moves to
      `IVrInput.IsSimulator`.
- [ ] `IVrHaptics` ← `HvrHaptics`. `VrHapticsService` keeps the event→preset mapping and calls the port.
      `VRPlayerService.GetHand` goes away.
- [ ] `IVrUiPointers` ← `HvrUiPointers` (`HVRInputModule.UICanvases`, `AddCanvas`, `RemoveCanvas`, `Pointers`).
      Callers: `VRContextInteractionService.InitUIInteraction`, `OnMarvinLogoPoked`, `VirtualKeyboard`.
- [ ] `VRSeat`: replace the `HVRCanvasFade` lookup with `IVrRig.Fade`.
- [ ] `SFXPlayer` (HVR's one-shot audio pool, not covered above): `VRWeaponService`, `VrWeaponAttackDomain`,
      `VRSwimDive` play positional SFX through it. Needs an own replacement (e.g. via `AudioService`) - it's the
      only reason these files still have `using HurricaneVR.Framework.Core.Utils`.

### PR 0c - Grab and socket events

- [ ] Add `VrInteractable`, `HvrInteractableBridge`, `IVrSocket(Container)`, `HvrSocketBridge`.
- [ ] Change adapter signatures from `(HVRGrabberBase, HVRGrabbable)` to `(VrGrabArgs)` and subscribe in
      `Awake/OnEnable` instead of inspector bindings: `VRVobItem`, `VRPlayerItemAdapter`, `VRPlayerWeaponInteraction`,
      `VRFocus`, `VRNpc`, `VRNpcMarvinAdapter`, `VRMobInteraction`, `VRSeat`, `VRBackpack`,
      `VRVobContainerSocketInventory`, `VRNpcLoot`.
- [ ] `VRPlayerService.SetGrab/UnsetGrab/IgnorePlayerCollision`: branch on `VrGrabArgs.Kind`, use
      `VrInteractable.Colliders`.
- [ ] `VRDigSpot`: replace `FindObjectsByType<HVRHandGrabber>` + `GrabbedTarget` with a query on
      `VRPlayerService` (`GrabbedItemLeft/Right` already exist).
- [ ] `VRVobContainerPhysicsChest` subclasses `HVRPhysicsDoor`. For now move it under `Backends/Hvr`; Phase 4
      replaces it. The socket part goes through `IVrSocketContainer`.
- [ ] `VRVobDoor`, `VRVobContainer` (door SFX), `VRContainerDoorPickingInteraction` (`Lock/Unlock`): add a small
      `IVrHinge` port (`Lock()`, `Unlock()`, `SetSounds(open, close)`) implemented by an HVR bridge.
- [ ] **Prefabs (UnityMCP only):** in all 11 VR prefabs, add `VrInteractable` + bridge next to `HVRGrabbable` and
      remove the inspector UnityEvent bindings to our methods (17× `OnBeforeGrabbed`, 18× `OnReleased`, 10×
      `OnHoverEnter/Exit`, holster/backpack callbacks, …). If UnityMCP isn't connected, stop and report (CLAUDE.md).

### PR 0d - Gating and leftovers

- [ ] Remove `#if GOTHIC_HVR_INSTALLED` from the 22 files that don't use HVR (list in 02 §6) and from the 8 that
      only used our wrappers.
- [ ] `VRContextBootstrap`: select the backend (`Hvr` for now). If none is available, log
      `Logger.LogError(…, LogCat.VR)` with a hint to set `GameControls = Flat` or install a backend, then throw as
      today. (Switching to Flat automatically would mean loading the Flat player scene; not worth it for a
      transitional state.)
- [ ] `ReflexVRSceneInstaller`: always compiled; registers port implementations from the active backend.
- [ ] `Gothic-Lab/…/LabFightHandler.cs`: replace the `HurricaneVR.Framework.Core.Utils` helper with our own extension.
- [ ] `Gothic-Core/Scripts/Const/Constants.cs:113-117`: change the comment "HVR Layers" to "VR layers". Layer names
      stay (`TagManager.asset` unchanged).
- [ ] Remove the HVR wording from comments in `MobInteractionService`, `VobService` and `NpcHelperService`.

## Test plan (unchanged behaviour)

Run on Quest (OpenXR) and PCVR (Index if available), G1 and G2:

1. Boot → main menu laser works, Marvin logo poke vibrates the correct hand.
2. Move/snap/smooth turn; change sit-stand, direction and turn settings in the accessibility menu → applied live and
   kept after a restart (Gothic.ini compatibility).
3. Pick up an item by hand and by distance grab → inventory count +1; drop → -1; two-hand grab doesn't double-count.
4. Holster a sword on the hip; backpack on the shoulder; put an item in and out of the backpack, check pages.
5. Open a chest, take/put items; lock-pick a chest and a door → haptics on the correct hand per combo result.
6. Talk to an NPC by grabbing; loot a dead NPC.
7. Use a mob (wheel/bed), a ladder, a dig spot.
8. Swim and dive (stroke movement, fade with air), walk out of water.
9. Melee hit an NPC; fight-window haptics.
10. Desktop simulator (`EnableVRDeviceSimulator`): WASD, Esc menu, hand keys.
11. Build with the define removed → compiles; VR boot logs the "no backend" error. Flat mode boots normally.

## Risks

| Risk | Mitigation |
|------|-----------|
| Event ordering differs from the old inspector bindings (e.g. `VRPlayerItemAdapter.OnBeforeGrabbed` must enable physics before HVR moves the item) | The bridge raises `BeforeGrabbed` synchronously from HVR's `BeforeGrabbed`. Keep the order of subscription identical to the old inspector order. |
| Remote grab raises `SetGrab` twice (force grabber + hand grabber) | Keep today's guard in `VRPlayerService`. The bridge passes `Kind = Distance` for the first call. |
| Prefab rewiring misses a binding | Grep `m_MethodName` in `Gothic-VR/Resources/VR/Prefabs` before/after: the count of our method bindings must reach 0. |
