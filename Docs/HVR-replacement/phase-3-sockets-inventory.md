# Phase 3 - Sockets and inventory (backpack, holsters, chests, NPC loot)

**Goal:** every place that holds items, implemented on the Xri backend behind `IVrSocket` / `IVrSocketContainer`
(Phase 0), with the same Gothic logic as today: inventory counts, save-game vob list, culling, pages and categories.

**Size:** L (2-3 weeks) · **Depends on:** Phases 0-2 · **Parallel with:** Phase 4

## Exit criteria

- [ ] Backpack: grab from the shoulder, browse categories/pages, take items out, put items in; the inventory and the
      save-game vob list stay consistent.
- [ ] Hip/back holsters keep weapons; drawing and holstering updates the inventory as today.
- [ ] Chests: the content spawns into slots (scaled down), items can be taken and put back; dropping an item into the
      open chest stores it.
- [ ] NPC loot: equipped and inventory items appear in loot slots, taking one moves it to the player.
- [ ] Hover feedback on slots (scale and valid/invalid material) is equivalent to HVR.
- [ ] Socket sounds: Gothic `InvOpen`/`InvClose` for the backpack, socket fallback sounds for slots, silent bulk
      spawning.

## What HVR does for us today (behaviour spec, write new code from this, not from HVR source)

| Behaviour | Where | Settings |
|-----------|-------|----------|
| Socket holds a grabbable, **re-parents the `VobLoader` root** (not the grabbable child), resets local pose, rotates by item category | `VRSocket` (ours, subclass) | rotation table in `VRSocket.RotateItemForCategorySlot` |
| On release, restores `Grabbable → VobLoader → previous parent` and skips destroyed loaders | `VRSocket.OnReleased` | |
| **Scales the item down to fit** the slot (by model bounds) and restores the scale on removal | `HVRSocket.ScaleGrabbable` | backpack slot `Size 0.05`, chest slot `Size 0.18` |
| Only items are accepted | `HVRTagSocketFilter` + `GUZ-VR-Socketables.asset` | only one tag exists: "Items General" |
| Hover feedback | `HVRSocketHoverScale`, `HVRSocketHoverMaterial` (Valid/InvalidSocketRing) | |
| Container = list of slots, find a free one, add programmatically | `HVRSocketContainer.Sockets`, `TryFindAvailableSocket`, `TryAddGrabbable` | |
| Drop into the chest volume → auto-place into a free slot | `HVRSocketContainerGrabber` + `HVRTriggerGrabbableBag` on `oCMobContainer` | |
| Backpack itself is socketable onto the shoulder; sounds on (un)socket | `HVRShoulderSocket`/`VRShoulderSocket`, `HVRSocketable` | clips set in `VRBackpack.Init` |
| Backpack slots follow the backpack | `HVRSocketLink`, `DemoBackpack` (TechDemo) | |
| Detection mode | `GrabDetectionType: 1` (trigger-based), `ParentDisablesGrab: 1` for backpack slots, `CanRemoveGrabbable: 1` | |
| Programmatic place | `socket.TryGrab(grabbable, true, true)` (`VRNpcLoot`, chests) | |

## Design

### `VrSocketSlot` (component, backend-neutral game logic)

Owns the Gothic behaviour: re-parent the `VobLoader` root, category rotation, scale-to-fit (bounds of the item's
renderers, computed once and cached per item), restore on removal, sounds, and `Placed/Removed` events with
`VrGrabArgs`. It does **not** depend on XRI.

### `XriSocketBridge : XRSocketInteractor` (backend)

- Uses XRI's socket for detection, hover and select.
- Overrides the attach/re-parent step so `VrSocketSlot` does the Gothic re-parenting instead of XRI moving the
  grabbable child. XRI 3.x has a socket scale mode (fixed / fit to bounds). Check it against our `VobLoader`
  hierarchy; if it scales the wrong transform, scale in `VrSocketSlot` and leave XRI's at "none".
- Select filter: accept only `VrInteractable`s whose `VobLoader` is an item (`oCItem`). That replaces the tag
  socketables, since there is only one tag anyway. If we later want per-slot categories (only weapons on the hip),
  filter by `ItemInstance.MainFlag`.
- Hover feedback: XRI's `showInteractableHoverMeshes` gives a ghost preview. Additionally, port the scale/material
  ring feedback as a small `VrSocketHoverFeedback` component (own ring mesh + valid/invalid URP materials).

### `VrSocketContainer`

Holds `VrSocketSlot`s and implements `IVrSocketContainer`: `TryFindFree`, `TryPlace`, `SetSilent(bool)` (replaces
the audio-null juggling in `VRVobContainerPhysicsChest.PreSpawnContent/PostSpawnContent`).

### Drop into chest

`VrContainerDropZone`: trigger collider inside the open chest. When a held item is released inside it (listen to
`VrInteractable.Released` + overlap check), call `container.TryPlace`. Simpler than HVR's bag + container grabber.

### Backpack on the shoulder

- The backpack is a `GothicGrabInteractable` (Phase 2) and the shoulder is an `XriSocketBridge` near the head-relative
  shoulder anchor. While it's on the shoulder, its slots are disabled (`ParentDisablesGrab` behaviour).
- Replace `DemoBackpack` + `HVRSocketLink` (TechDemo): slots are children of the backpack, so they follow
  automatically; enable/disable them on shoulder (un)socket.
- `VRBackpack.OnBackpackReleasedFromShoulder` (refresh the inventory view) hooks into the shoulder socket's
  `Removed` event.
- Authored hand pose on the backpack strap: Phase 5. Until then, dynamic attach.

### Holsters

Today: two `VRSocket`s on the player rig (hip), plus HVR TechDemo `DemoHolster` / `HeadRelativeInventory`. New: two
`VrSocketSlot` + `XriSocketBridge` on body-relative anchors. They follow the head yaw, not the head pitch (same idea
as HVR's head-relative inventory). `VRBackpack.OnItemPutIntoHolster/OutOfHolster` subscribe to their events.

## Work items

- [ ] `VrSocketSlot`, `VrSocketContainer`, `VrSocketHoverFeedback`, `VrContainerDropZone` (backend-neutral).
- [ ] `XriSocketBridge`, filter, attach override.
- [ ] Move the logic of `VRSocket` / `VRShoulderSocket` into `VrSocketSlot`. The HVR versions stay in
      `Backends/Hvr` until Phase 6.
- [ ] `VRBackpack`: uses `IVrSocketContainer` (already after Phase 0), handles page refresh with
      `TryPlace(…, raiseEvents: false)` instead of `_tempIgnoreSocketing` if possible.
- [ ] `VRVobContainerPhysicsChest`: split into `VrChestLid` (Phase 4) + `VrChestSlots` (this phase: resize slots to the
      chest mesh, as today's `AlignSockets` logic, bulk spawn silently, pagination still TODO as logged today).
- [ ] `VRNpcLoot`: create `VrSocketSlot`s at runtime instead of `HVRSocket`s; `Removed` → `OnItemTakenFromLoot`.
- [ ] `VRVobContainerSocketInventory`: subscribe to container events.
- [ ] **Assets (UnityMCP):** new `VR Socket Slot.prefab` (replaces `VRBackpack(Large)InventorySlot` + HVR's
      `LargeInventorySlot` / `SocketOval`), ring mesh (simple torus, our own) + `SocketRing/Valid/Invalid` URP materials,
      add slots to `oCMobContainer.prefab` and the backpack prefab, shoulder + hip anchors on the XRI rig.
- [ ] Remove `GUZ-VR-Socketables.asset` usage on the Xri path (keep the file until Phase 6).

## Test plan

1. New game: backpack on the shoulder; grab it off → slots show the current category; flip pages/categories; put it
   back on the shoulder → `InvClose` sound.
2. Pick up an apple, put it into the backpack → inventory +1, the vob disappears from the world (save → load: not in
   the world, in the inventory).
3. Take it out again → world vob again (save → load: in the world).
4. Holster a sword at the hip, walk, teleport, swim (grab disabled), draw it again.
5. Open a chest with 20 items: slots scaled to the chest, items mini-sized, no sound burst while spawning.
6. Take an item, put it back by dropping it into the open chest.
7. Kill a bandit, loot: equipped weapon shows `[E]`, taking it removes the weapon mesh from the corpse.
8. Hover an item over a full slot → invalid feedback; over a free slot → valid.

## Risks

| Risk | Mitigation |
|------|-----------|
| XRI socket moves the grabbable child, breaking the `VobLoader` hierarchy (culling and save rely on it) | Attach override in `XriSocketBridge`; unit-check the hierarchy after place/remove in a PlayMode test (`Gothic.Tests.PlayMode`). |
| Scale-to-fit needs mesh bounds one frame after spawn (today's `yield return null`) | Keep the frame wait in bulk spawn; compute bounds from renderers, not colliders. |
| Kinematic vs. dynamic state of items in slots | A socketed item is kinematic, with collisions off against the player (`IgnorePlayerCollision`, Phase 0). On removal, Phase 2's grab flow takes over. |
