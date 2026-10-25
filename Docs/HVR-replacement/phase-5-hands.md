# Phase 5 - Hands (physical hand presence, block hands, finger curls, procedural posing)

**Goal:** hands that have a physical presence in the world, as HVR's do: they stop at walls, chests, doors and
tables, push loose objects, carry held items with them (a sword stops at the wall together with the hand), and give
contact haptics. This physical feedback is a core part of how the game feels in VR.

**Size:** L (2-4 weeks for 5a + 5b) · **Depends on:** Phases 1-4 · **Riskiest phase** (see 04, "What we accept")

> **Decision 2026-10-25 (supersedes the 2026-10-22 note):**
> - **Physical hand presence (5b) is in scope and the core of Phase 5.** The 2026-10-22 "skip physics hands" note was
>   a misunderstanding: what was rejected was UltimateXR's hand meshes, not physics hands.
> - **No imported hand meshes.** Nothing from UltimateXR (meshes, pose editor, code). HVR's hand meshes are licensed
>   and go away in Phase 6, so they can't stay either.
> - **Hand visual: Minecraft-style block hands** built from Unity primitives (5a). They can later be swapped for
>   Gothic's own fist meshes, loaded at runtime via ZenKit (separate future task).
> - Finger curls (5e) and procedural finger posing (5f) are designed for articulated hands. They stay **on hold**
>   until we have an articulated hand model; block hands get a simple two-part "grip" animation instead (5a).
> - Phase 6 has no technical dependency on Phase 5 (5a-5d don't use HVR) and can land before or after it.

Physical hand presence is split into three PRs (5b-5d), stacked on 5a.

| Part | Content | Size | Status |
|------|---------|------|--------|
| 5a | Block hand visual (palm + finger block, grip hinge), `VRHandsOnTop` | S | done |
| 5b | `PhysicsHand` core: rigidbody follow, strength limit, ghost hand, max-distance snap, collision layers, setting | M | done |
| 5c | Held items follow the physics hand, contact haptics | M | planned |
| 5d | Teleport, Marvin "goto", ladder, swim/dive handling + PlayMode tests | S-M | planned |
| 5e | `FingerCurlProvider` (buttons, touches, XR Hands) | S-M | on hold (articulated hands) |
| 5f | `ProceduralHandPoser` + few authored poses | M-L | on hold (articulated hands) |

## Exit criteria

- [ ] No HVR hand/controller meshes, poses or settings are referenced by the Xri rig.
- [x] Block hands replace the `Hand Placeholder` spheres; the finger block closes with the grip value. (5a)
- [ ] With physical hands on (default), hands stop at walls, chests, doors, tables and mobs; held items stop too;
      a ghost hand shows the real controller pose when separated.
- [ ] Touching or hitting a surface plays a contact haptic, scaled by impact speed.
- [ ] Teleport, Marvin "goto", ladders, swimming, dialogs and `KeepPlayerWithin` never leave a hand or held item stuck.
- [ ] With physical hands off (comfort setting), hands follow the controllers directly (Phase 2 state).

## 5a - Block hands (visual)

- Per hand, built from primitives (UnityMCP, no imported art): a **palm block** (~8 × 3 × 9 cm) and a **finger block**
  (~8 × 2.5 × 5 cm) on a hinge at the knuckles, plus a small **thumb block**. URP Lit, Gothic-ish skin/leather colour.
- "Animation": the finger block rotates around the knuckle hinge by `grip` (0 = open, 1 = ~100° closed); the thumb
  block by `max(primary/secondary/thumbstick touch)`. Index-specific trigger curl is optional (split the finger block
  in two: index + rest). That's a cheap stand-in for 5c and reads well with the blocky style.
- While holding an item: the finger block closes to a fixed "holding" angle so it doesn't clip deep into the item.
- Swimming: `IVrRig.SetGrabbingEnabled(false)` also forces open hands.
- Materials: plus the on-top variant for `VRHandsOnTop` (render queue 3950, as today).
- Hand layer stays `Hand` (`Constants.HandLayer`). `VRFistAttackAdapter` resets the attack collider layer; keep it.
- Controller models: not needed. The simulator shows the block hands as well.
- The visual sits under the **physics hand** (5b) when physical hands are on, under the controller when off.

**Where reality deviated (5a, `engine/hvr-replacement-5a`):**
- Hierarchy per hand: `Left/Right Controller/Block Hand` (`VrBlockHand`, the visual root to reparent in 5b) →
  `Palm`, `Finger Hinge/Fingers`, `Thumb Hinge/Thumb`. Unity cubes without colliders, layer `Hand`, no shadows.
  The root sits directly under the controller (sibling of the Near-Far interactor), not under the interactor like
  the old `Hand Placeholder`. Left and right are built mirrored (no negative scale, so 5b's box colliders work).
  Hand frame: the controller is the aim pose (XRI binds `pointerPosition` first), +Z forward; palm faces inwards,
  knuckles at z ≈ 0, the fingers wrap around the controller. Offsets are guesses, check them on a headset.
- No index/rest split; one finger block (grip). Thumb: 35° at touch, binary (touch is a button).
- Material: `Gothic/Lit/Misc` like the old hands, not plain URP Lit. Gothic worlds have flat ambient light only, so
  the cube faces had no shading and the blocks merged into one shape. A generated 16 × 16 px texture
  (`VRBlockHands.png`, skin noise with a dark 1 px border per face, point filtered) gives the Minecraft look and
  readable edges. `VRBlockHands-OnTop.mat` is the `VRHandsOnTop` variant (queue set in code, as before).
- Ports: `IVrInput.GetGripValue` / `IsThumbTouching` and `IVrRig.IsGrabbingEnabled(side)` have **default
  implementations** (grip button 1/0, primary/secondary/stick pressed, always true), so the Hvr backend stays
  untouched. Xri reads the new actions `Grip Value` (`{Grip}`) and `Thumb Touch` (`{PrimaryTouch}`,
  `{SecondaryTouch}`, `{Primary2DAxisTouch}`) of the `Gothic Left/Right` maps.
- While grabbing is disabled XRI also drops the held item (Near-Far `allowSelect = false`), so "open hand while
  holding during swimming" can't happen on Xri anyway.

## 5b-5d - Physical hand presence (`PhysicsHand`)

Replaces `HVRJointHand` + `HVRHandPhysics` + `HVRGhostHand` + the hand part of `HVRTeleportCollisonHandler`.

PR split: **5b** = follow, strength limit, ghost hand, max distance, collision layers, setting. **5c** = held items
and contact haptics. **5d** = teleport and scene changes (incl. ladders, swimming) + PlayMode tests.

- Physics hand = `Rigidbody` (interpolated, continuous dynamic collision) with box colliders matching the block hand
  (palm + finger block + thumb). Block hands make the colliders trivial: the visual *is* the collider shape.
- Driven towards the tracked controller pose every `FixedUpdate`:
  - Position: velocity-based (`rb.linearVelocity = (target - pos) / dt`, clamped to max speed) to start with. Fewer
    parameters than a `ConfigurableJoint` and the same approach XRI's VelocityTracking uses for items. Switch to a
    joint with drives only if playtests show it's needed (e.g. for strength limits).
  - Rotation: angular velocity from the quaternion delta, clamped.
  - **Strength limit:** clamp the force the hand can apply (via max velocity change per step or mass), so hands push
    apples and bottles but can't launch heavy chests or shove NPCs.
- **Ghost hand:** a transparent copy of the block hand follows the controller when physics hand ↔ controller > ~5 cm.
- **Max distance:** if physics hand ↔ controller > ~0.5 m for > 0.2 s (stuck behind a wall), snap the physics hand
  back to the controller; release the held item if it's the one stuck (HVR's `MaxDistanceBehaviour`).
- **Held items:** the Near-Far interactor's attach transform follows the **physics hand**, not the controller, so
  VelocityTracking pulls the item to the physics hand and the sword stops at the wall together with the hand. Ignore
  collisions between a hand and the item it holds (Phase 2 already ignores held item ↔ player).
- **Contact haptics:** `OnCollisionEnter` on the physics hand (and on a held item) → `IVrHaptics.Vibrate(side, …)`,
  amplitude from the relative impact speed, with a short cooldown per hand so sliding along a wall doesn't buzz
  constantly. Add a `VrHapticPreset` for contact if the existing presets don't fit; an amplitude parameter may be
  needed on `IVrHaptics`.
- **Teleport and scene changes:** after `IVrRig.Teleported`, move hands and held items with the rig (same offset),
  then sweep; if they overlap geometry, snap to the controller. Most of HVR's fixes were here (see 04 data points).
  Also covers ladders (Phase 4 climb moves the rig) and the swim/dive rig changes.
- **Collision layers:** hands collide with the world, mobs, doors, chests, items and NPCs; not with the player's
  CharacterController, not with UI, not with the item held by the same hand. Doors/chest lids (Phase 4 hinges) can be
  pushed open with the hand.
- **Setting:** "physical hands" in the immersion menu (`VRSettingsImmersionMenu`, Gothic.ini key in
  `VRConstants.IniNames`), **on by default**. Off = hands follow the controllers directly.
- Fist attacks (`VRFistAttackAdapter`, disabled today) become possible with physical hands. Enabling them is a
  separate gameplay task.

**Where reality deviated (5b, `engine/hvr-replacement-5b`):**
- Names: `VrPhysicsHand` and `VrGhostHand` (`Gothic.VR.Interaction.Components`, repo prefix). `VrPhysicsHand` sits on
  each `Block Hand` next to `VrBlockHand` (prefab: only the component + ghost material). Everything else is built at
  runtime: the rigidbody `Physics Hand (Left|Right)` is a **root GameObject in the Player scene** (not under the
  moving rig, so the CharacterController can't drag it through walls), with one `BoxCollider` per block
  (`Palm/Fingers/Thumb Collider`, layer `Hand`) that copies the block poses every physics step (grip curl, rig scale).
- **The visual is not reparented.** `Block Hand` stays under the controller; in `LateUpdate` it's placed at
  *current controller pose + what the last physics step blocked* (`body - lastTarget`). A free hand therefore has no
  lag (fixed step is 50 Hz, interpolation would trail by up to 2 frames while walking) and still gets the
  late-latched controller pose; a blocked hand stays where the physics left it. `VrBlockHand` is unchanged.
  Rigidbody interpolation is therefore off.
- Follow: `AddForce(VelocityChange)` towards `(target - pos) / dt`, clamped to 15 m/s; the velocity change per step
  is limited by `MaxForce / Mass` (200 N / 1 kg) = the strength limit. Rotation the same with 50 rad/s and
  2500 rad/s². Fixed center of mass/inertia (finger colliders move). `maxDepenetrationVelocity` 2 m/s.
- Ghost: unlit transparent copy (`VRBlockHands-Ghost.mat`, URP Unlit) under the controller, shows above 5 cm
  separation, hides below 3 cm. It's occluded by the wall the hand is stuck in (no "see through" yet).
- Max distance: > 0.5 m for > 0.2 s → snap (log `[VrPhysicsHand] … Snapped back.`, editor only). Snap turns
  (> 10° rig rotation per physics step), rig jumps (> 1 m) and `IVrRig.Teleported` snap as well (minimal 5d part).
- While `IVrRig.IsClimbing` and with the setting off, the body is inactive and the visual follows the controller
  directly (5a state). Ladders are left to 5d.
- Collisions: matrix change **Hand ↔ VobItemNoWorldCollision off** (items in the first second of a grab and the XRI
  backpack, which is on that layer). Held and socketed items are ignored by **both** hands, not only the holding one:
  `VRPlayerService.IgnorePlayerCollision` (already used for the CharacterController) also calls
  `VrPhysicsHand.IgnoreCollisions`. On release the pair is restored only once the item's bounds no longer touch the
  hand (no depenetration kick). Pairs are re-applied when a body is re-activated (Unity resets them).
- What the hand meets in Gothic worlds: world items lie **kinematic** until first grabbed (so the hand stops at them
  like at a table); dropped items are dynamic and get pushed. NPC `RootMotionCollider`s are dynamic (80 kg) but
  X/Z-frozen, so NPCs never move.
- Doors and chest lids: on Xri the `VrHinge` leaf is **kinematic** (own angle solver, Phase 4), so rigidbody contact
  alone can't move it - the hand just stopped at it. `VrHinge.OnCollisionStay` now turns the leaf by the hand's blocked
  movement (`VrPhysicsHand.PushOffset`) along the swing direction at the contact point (max `MaxHandSpeed`, swings on
  after contact ends, not while locked or held). Pushing a door open/shut and pushing an open lid shut work; opening a
  closed lid needs the hand under its front edge, otherwise grab it.
- Setting: Gothic.ini `[GOTHIC_VR_IMMERSION] physicalHands` (1 default), menu "Physical Hands" (en/de/pl), applied by
  `VRContextInteractionService.ApplyPhysicalHands()` on ini load and change. `VRContextInteractionService.PhysicsHands`
  lists the hands (empty on Hvr).

## 5e - Finger curls (`FingerCurlProvider`) - ON HOLD (needs articulated hands)

Replaces `HVRHandAnimator` + `HVRFingerSettings` + `HVROpenXRFingerCurls`. Block hands use the simple grip hinge
from 5a instead.

- Output: `float[5]` curls per hand (thumb…pinky, 0 = open, 1 = fist).
- Input source per finger (configurable ScriptableObject `VrFingerSettings`, replacing `HVR_FingerSettings.asset`):

  | Finger | Source |
  |--------|--------|
  | Thumb | max(primary/secondary/thumbstick touch) → 0.6-0.8 |
  | Index | trigger value (+ trigger touch → 0.2) |
  | Middle, ring, pinky | grip value |

- Finger tracking: if `XRHandSubsystem` is running and the hand is tracked, compute curls from joint angles
  (metacarpal → tip) and **override middle/ring/pinky** (as HVR does today for Knuckles, fingers 2-4). Index and
  thumb stay on buttons, because the trigger/joystick need them.
  - Index under SteamVR OpenXR exposes controller-driven hand joints through `XR_EXT_hand_tracking`. That's what HVR
    uses today via `USING_XRHANDS`.
  - Steam Frame: verify on a device that its runtime exposes the same. If yes, it works without extra code.
  - Quest/Pico controller-free hand tracking is out of scope (HVR doesn't support it either).
- The pose blend per finger: `curl` lerps between the open and the fist pose (two `VrHandPose` assets), unless a
  grab pose overrides that finger (5f).

## 5f - Procedural hand posing (`ProceduralHandPoser`) - ON HOLD (needs articulated hands)

Replaces `HVRPhysicsPoser` (`PoseType: PhysicPoser` on every `oCItem`). Needed only once hands have finger bones,
because no one can author poses for thousands of runtime-loaded Gothic items.

Algorithm (one-shot on grab, not every frame):
1. Hand attach: with XRI dynamic attach (Phase 2), the item is held where the palm touched. Align the palm normal
   to the closest point on the item's collider (`Collider.ClosestPoint` from the palm center), and offset the palm by
   a small skin distance.
2. For each finger, iterate the bones from proximal to distal: rotate the bone in steps (e.g. 5°) around its curl
   axis until a `Physics.ComputePenetration`/`CapsuleCast` of the bone capsule against **the item's colliders only**
   hits, then step back one increment. Cap at the fist pose.
3. Thumb: same, but with its opposition axis first.
4. Store the result as a `VrHandPose` and apply it while held (the item doesn't change shape).
5. Cache per item **instance name** + attach-point bucket (e.g. a 2 cm grid in item space).

Budget: ~15 bones × ~20 steps × 1 capsule query = ~300 queries per grab, done once. Fine even on Quest.

**Authored poses** (few): backpack strap (today `backpack_left/right_hand_pose.asset`), ladder, door handle (optional).

## Work items

- [x] 5a: block hand prefabs (UnityMCP), grip hinge driver, `VRHandsOnTop` renderers, replace `Hand Placeholder`.
      (Built inside `VR - Player XRI.prefab`, no separate hand prefab; driver `VrBlockHand`.)
- [x] 5b: `PhysicsHand`, `GhostHand`, strength limit, max-distance snap, collision layer matrix, immersion setting
      (C# + menu + ini). (`VrPhysicsHand`, `VrGhostHand`; see "Where reality deviated (5b)" below.)
- [ ] 5c: attach transform on the physics hand, hand ↔ held item collision ignore, contact haptics.
- [ ] 5d: teleport/goto/ladder/swim handling; PlayMode test: physics hand pushed against a static box stops at its
      surface and reconnects when the target moves back; after a rig teleport the hand isn't left behind
      (`Gothic.Tests.PlayMode`).
- [ ] 5e/5f: re-plan once an articulated hand model exists.

## Test plan

1. Quest 3, Pico 4, Index: grip closes the finger block, thumb touch moves the thumb block.
2. Push a hand into a wall, a table, a chest and a door → the hand stops, ghost hand visible, haptic pulse; pull back
   → reconnects. Slide along a wall → no constant buzzing.
3. Hold a sword against a wall → sword and hand stop together, no visual separation. Swing into an NPC → the hit
   still registers (combat velocity stays rigidbody-based, Phase 2).
4. Push an apple/bottle on a table → it moves; push a chest or an NPC → it doesn't fly away.
5. Teleport, Marvin "goto", climb a ladder, swim/dive while holding an item → nothing stuck, nothing left behind.
6. Dialog + `KeepPlayerWithin` unaffected; menu laser pointer works with physical hands on.
7. Setting off: same as the Phase 2 state.
8. Performance: Quest 3 frame time with physical hands on during a fight in the old camp.

## Risks

| Risk | Mitigation |
|------|-----------|
| Physics hands tuning eats time (HVR history: ~170 release-note lines on this) | Start velocity-based with few parameters; block-hand box colliders keep the contact shapes simple; keep the off switch. |
| Hand jitter at low/uneven frame rates (Quest) | Interpolated rigidbody, fixed timestep matched to display rate, visual hand follows the interpolated pose. |
| Hands get stuck after teleport/climb/swim | Central handling on `IVrRig.Teleported` + max-distance snap; covered by test 5. |
| Contact haptics too noisy | Impact-speed threshold + per-hand cooldown. |
| Block hands look out of place | Accepted as interim; swap for Gothic's fist meshes (ZenKit) later without touching 5b. |
