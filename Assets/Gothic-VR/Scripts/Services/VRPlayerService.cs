using System;
using System.Collections.Generic;
using System.Linq;
using Gothic.Core.Adapters.Vob;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Player;
using Gothic.Core.Services.Vobs;
using Gothic.VR.Services.Context;
using Gothic.Core;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Manager;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Npc;
using Gothic.VR.Adapters.Vob;
using Gothic.VR.Adapters.Vob.Container;
using Gothic.Core.Models.Container;
using Gothic.VR.Interaction.Components;
using Gothic.VR.Interaction.Models;
using Gothic.VR.Interaction.Ports;
#if GOTHIC_HVR_INSTALLED
using Gothic.VR.Interaction.Backends.Hvr;
using HurricaneVR.Framework.Core.Bags;
using HurricaneVR.Framework.Core.Grabbers;
#endif
using Reflex.Attributes;
using UnityEngine;
using ZenKit;
using ZenKit.Daedalus;
using ZenKit.Vobs;
using EventType = ZenKit.EventType;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Services
{
    /// <summary>
    /// Contains global states about the VR player (items in hands).
    /// </summary>
    public class VRPlayerService
    {
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly VobService _vobService;
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly DialogService _dialogService;
        [Inject] private readonly VmCacheService _vmCacheService;
        [Inject] private readonly NpcInventoryService _npcInventoryService;
        [Inject] private readonly ResourceCacheService _resourceCacheService;
        [Inject] private readonly DocService _docService;
        [Inject] private readonly MobInteractionService _mobInteractionService;

        public VRContextInteractionService VRContextInteractionService =>
            _contextInteractionService.GetImpl<VRContextInteractionService>();

        public GameObject GrabbedItemLeft;
        public GameObject GrabbedItemRight;

        private IVrInput _vrInput;

        /// <summary>
        /// Keyboard and mouse instead of controllers (IVrInput.IsSimulator).
        /// </summary>
        public bool IsSimulator => (_vrInput ??= ReflexProjectInstaller.DIContainer.Resolve<IVrInput>()).IsSimulator;

        public enum HandType
        {
            Left,
            Right
        }

        // Who currently holds an item: hands (per side) and sockets. Decides which collisions the item ignores.
        private readonly Dictionary<VrInteractable, HashSet<(VrGrabberKind, VrHandSide)>> _grabbers = new();

        public bool IsDualGrabbed => GrabbedItemLeft != null && GrabbedItemLeft == GrabbedItemRight;

        /// <summary>
        /// An item was grabbed by a hand (directly or by distance). Sockets and other grabbers are ignored.
        /// </summary>
        public void SetGrab(VrGrabArgs args)
        {
            if (!args.IsHandOrDistance)
                return;

            var grabbable = args.Interactable;
            if (args.HandSide == VrHandSide.Left)
            {
                // If we did remote grabbing, this function is called twice (remote grabber+hand grabber).
                // As we already count remote grabbing as "in inventory", we skip it the second time.
                if (GrabbedItemLeft == grabbable.gameObject)
                    return;

                GrabbedItemLeft = grabbable.gameObject;
            }
            else
            {
                // If we did remote grabbing, this function is called twice (remote grabber+hand grabber).
                // As we already count remote grabbing as "in inventory", we skip it the second time.
                if (GrabbedItemRight == grabbable.gameObject)
                    return;

                GrabbedItemRight = grabbable.gameObject;
            }

            // If we grabbed the element with second hand, return.
            if (IsDualGrabbed)
                return;

            // Otherwise alter inventory count
            var vobItem = grabbable.GetComponentInParent<VobLoader>()?.Container.VobAs<IItem>();
            if (vobItem == null) return;
            var instanceName = !string.IsNullOrEmpty(vobItem.Instance) ? vobItem.Instance : vobItem.Name;
            _playerService.AddItem(instanceName, vobItem.Amount);
        }

        /// <summary>
        /// An item was released by a hand (directly or by distance). Sockets and other grabbers are ignored.
        /// </summary>
        public void UnsetGrab(VrGrabArgs args)
        {
            if (!args.IsHandOrDistance)
                return;

            var dualGrabPrev = IsDualGrabbed;
            var grabbable = args.Interactable;
            if (args.HandSide == VrHandSide.Left)
            {
                // If we did remote grabbing, this function is called twice (remote grabber+hand grabber).
                // As we already count remote grabbing as "in inventory", we skip it the second time.
                if (GrabbedItemLeft == null)
                    return;

                GrabbedItemLeft = null;
            }
            else
            {
                // If we did remote grabbing, this function is called twice (remote grabber+hand grabber).
                // As we already count remote grabbing as "in inventory", we skip it the second time.
                if (GrabbedItemRight == null)
                    return;

                GrabbedItemRight = null;
            }

            // If we removed one hand from our item.
            if (dualGrabPrev)
                return;

            // Otherwise alter inventory count
            var vobItem = grabbable.GetComponentInParent<VobLoader>()?.Container.VobAs<IItem>();
            if (vobItem == null) return;
            var instanceName = !string.IsNullOrEmpty(vobItem.Instance) ? vobItem.Instance : vobItem.Name;
            _playerService.RemoveItem(instanceName, vobItem.Amount);
        }

        /// <summary>
        /// The player collides with VobItems (stuck at a sword in the ground like in Gothic). Items in our hands or
        /// sockets (hips, back) would push our own CharacterController, so they ignore it while being held.
        ///
        /// Physical hands (like HVR): only the hand holding an item ignores it (a held sword would push the hand
        /// holding it). The other hand still collides with it and can push it. Socketed items ignore both hands,
        /// otherwise reaching for the hip or backpack pushes them around.
        /// </summary>
        public void UpdatePlayerCollision(VrGrabArgs args, bool isGrabbed)
        {
            var grabbable = args.Interactable;
            if (!_grabbers.TryGetValue(grabbable, out var grabbers))
            {
                grabbers = new HashSet<(VrGrabberKind, VrHandSide)>();
                _grabbers[grabbable] = grabbers;
            }

            // Sockets and other grabbers count as one (their HandSide is meaningless).
            var key = args.IsHandOrDistance ? (args.Kind, args.HandSide) : (VrGrabberKind.Socket, VrHandSide.Left);
            if (isGrabbed)
                grabbers.Add(key);
            else
                grabbers.Remove(key);

            if (grabbers.Count == 0)
                _grabbers.Remove(grabbable);

            var isSocketed = grabbers.Contains((VrGrabberKind.Socket, VrHandSide.Left));
            var context = VRContextInteractionService;
            foreach (var hand in context.PhysicsHands)
            {
                var isHolding = grabbers.Contains((VrGrabberKind.Hand, hand.Side))
                                || grabbers.Contains((VrGrabberKind.Distance, hand.Side));
                hand.IgnoreCollisions(grabbable.Colliders, isSocketed || isHolding);
            }

            var characterController = context.Rig?.CharacterController;
            if (characterController == null)
                return;

            var ignoreCharacterController = grabbers.Count > 0;
            foreach (var itemCollider in grabbable.Colliders)
            {
                if (itemCollider != null)
                    Physics.IgnoreCollision(characterController, itemCollider, ignoreCharacterController);
            }
        }

        /// <summary>
        /// The item is destroyed (e.g. put into a chest or eaten) while it might still be held.
        /// </summary>
        public void ForgetPlayerCollision(VrInteractable grabbable)
        {
            _grabbers.Remove(grabbable);
        }

        /// <summary>
        /// The hero grabbed a mob (wheel, lever, ...). The logic is platform neutral (MobInteractionService),
        /// only the dialog anchor next to the mob (and a bookstand's page) is VR specific.
        /// </summary>
        public void HandleMobGrab(VobLoader loader)
        {
            if (TryCloseMobDocument(loader))
                return;

            _mobInteractionService.HandleMobGrab(loader,
                () => CreateMobDialogAnchor(loader, _configService.Dev.EnableMobDialogOwnBounds),
                () => BeginMobDocument(loader));
        }

        /// <summary>
        /// A container without a lid (mummy) is a plain mob: the click shows/hides a loot backpack like a corpse.
        /// Chests with a lid (VRVobContainer) open physically.
        /// </summary>
        public void TryToggleStaticContainerLoot(VobLoader loader, IContainer container)
        {
            if (loader.GetComponentInChildren<VRVobContainer>(true) != null)
                return;

            if (!loader.TryGetComponent<VRStaticContainerLoot>(out var loot))
                loot = loader.gameObject.AddComponent<VRStaticContainerLoot>();
            loot.Toggle(container);
        }

        /// <summary>
        /// DeveloperConfig.EnableMobDocuments: an open page (bookstand) closes on the next touch.
        /// </summary>
        private bool TryCloseMobDocument(VobLoader loader)
        {
            if (!_configService.Dev.EnableMobDocuments)
                return false;

            var openDocument = loader.transform.Find(VRMobDocument.AnchorName);
            if (openDocument == null)
                return false;

            UnityEngine.Object.Destroy(openDocument.gameObject);
            Logger.Log($"[MobDialog] Closed the page of '{loader.name}'.", LogCat.Dialog);
            return true;
        }

        /// <summary>
        /// DeveloperConfig.EnableMobDocuments: a page next to the mob for a Doc_Show of its "[onStateFunc]_S1"
        /// (bookstands). Removed again if the script showed nothing.
        /// </summary>
        private Action BeginMobDocument(VobLoader loader)
        {
            if (!_configService.Dev.EnableMobDocuments)
                return null;

            var document = VRMobDocument.Create(loader.gameObject);
            _docService.PendingItemGo = document.gameObject;
            return () =>
            {
                _docService.PendingItemGo = null;
                if (document.HasPages)
                    Logger.Log($"[MobDialog] '{loader.name}' shows a page (Doc_Show).", LogCat.Dialog);
                else
                    UnityEngine.Object.Destroy(document.gameObject);
            };
        }

        /// <summary>
        /// Next to the mob on the player's left (= the mob's right, where NPC dialogs are), at eye height.
        /// </summary>
        private static GameObject CreateMobDialogAnchor(VobLoader loader, bool isOwnBoundsOnly)
        {
            var cam = Camera.main != null ? Camera.main.transform : loader.transform;

            var bounds = new Bounds(loader.transform.position, Vector3.one * 0.5f);
            var hasBounds = false;
            foreach (var mobRenderer in loader.GetComponentsInChildren<Renderer>())
            {
                // DeveloperConfig.EnableMobDialogOwnBounds: VOBs nested below the mob have their own loader. One of
                // Mroczne Tajemnice's anvils has three more below it - the anchor landed hundreds of meters away
                // ("hero farther than 5 m").
                if (isOwnBoundsOnly && mobRenderer.GetComponentInParent<VobLoader>(true) != loader)
                    continue;
                if (!hasBounds)
                {
                    bounds = mobRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mobRenderer.bounds);
                }
            }

            var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            var position = bounds.center - right * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.4f);
            position.y = cam.position.y - 0.2f;

            var anchor = new GameObject("MobDialogAnchor");
            anchor.transform.position = position;
            new GameObject("DialogMenuRootPos").transform.SetParent(anchor.transform, false);
            return anchor;
        }

        /// <summary>
        /// The hand as the player sees it (IVrRig.GetHandVisual).
        /// </summary>
        public GameObject GetHandModelGo(VrHandSide side)
        {
            var hand = VRContextInteractionService?.Rig?.GetHandVisual(side);
            return hand != null ? hand.gameObject : null;
        }

        // True during any active spell (telekinesis or combat targeting) — blocks NPC dialog.
        public bool IsSpellActive => IsTelekinesisActive || IsSpellTargetingActive;
        public bool IsSpellTargetingActive { get; private set; }

        // Telekinesis state — lives here so it survives VRRuneCaster destruction when one hand releases.
        public bool IsTelekinesisActive { get; private set; }
        public event System.Action TelekinesisDeactivated;

#if GOTHIC_HVR_INSTALLED
        // HVR backend only (force grabber bags) - not ported to XRI yet.
        public HVRForceGrabber GetForceGrabber(VrHandSide side)
        {
            var ctrl = _contextInteractionService.GetCurrentPlayerController()?.GetComponent<HvrRig>();
            if (ctrl == null) return null;
            return side == VrHandSide.Left ? ctrl.LeftHand.ForceGrabber : ctrl.RightHand.ForceGrabber;
        }

        private float _savedForceGrabRange;
        private float _savedBagMaxDistance;
        private Vector3 _savedCapsuleCenter;
        private float _savedCapsuleHeight;
        private float _savedCapsuleRadius;

        // Spell targeting state (combat spells — extends rune hand bags only).
        private VrHandSide _spellTargetingSide;
        private Vector3 _savedSpellCapsuleCenter;
        private float _savedSpellCapsuleHeight;
        private float _savedSpellCapsuleRadius;
        private float _savedSpellBagMaxDistance;

        public void ActivateSpellTargeting(VrHandSide runeHandSide, float range)
        {
            if (IsSpellTargetingActive) return;
            _spellTargetingSide = runeHandSide;
            var fg = GetForceGrabber(runeHandSide);
            if (fg == null) return;
            var savedOnce = false;
            foreach (var bag in fg.GrabBags)
            {
                if (!(bag is HVRForceGrabberBag fgBag)) continue;
                var capsule = bag.GetComponent<CapsuleCollider>();
                if (capsule == null) continue;
                if (!savedOnce)
                {
                    _savedSpellCapsuleCenter = capsule.center;
                    _savedSpellCapsuleHeight = capsule.height;
                    _savedSpellCapsuleRadius = capsule.radius;
                    _savedSpellBagMaxDistance = fgBag.MaxDistanceAllowed;
                    savedOnce = true;
                }
                var newHeight = _savedSpellCapsuleHeight * 2.5f;
                capsule.height = newHeight;
                capsule.center = new Vector3(0f, 0f, newHeight * 0.5f);
                fgBag.MaxDistanceAllowed = range;
                Logger.Log($"[SpellTarget] {fg.gameObject.name}/{bag.gameObject.name} capsule extended", LogCat.VR);
            }
            IsSpellTargetingActive = true;
        }

        public void DeactivateSpellTargeting()
        {
            if (!IsSpellTargetingActive) return;
            var fg = GetForceGrabber(_spellTargetingSide);
            if (fg != null)
            {
                foreach (var bag in fg.GrabBags)
                {
                    if (!(bag is HVRForceGrabberBag fgBag)) continue;
                    var capsule = bag.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        capsule.center = _savedSpellCapsuleCenter;
                        capsule.height = _savedSpellCapsuleHeight;
                        capsule.radius = _savedSpellCapsuleRadius;
                    }
                    fgBag.MaxDistanceAllowed = _savedSpellBagMaxDistance;
                }
            }
            IsSpellTargetingActive = false;
        }

        public void ActivateTelekinesis(float range)
        {
            if (IsTelekinesisActive) return;

            var left  = GetForceGrabber(VrHandSide.Left);
            var right = GetForceGrabber(VrHandSide.Right);
            if (right != null) _savedForceGrabRange = right.MaxRayCastDistance;
            else if (left != null) _savedForceGrabRange = left.MaxRayCastDistance;
            else _savedForceGrabRange = 10f;

            var savedOnce = false;
            foreach (var fg in new[] { left, right })
            {
                if (fg == null) continue;
                fg.MaxRayCastDistance = range;

                foreach (var bag in fg.GrabBags)
                {
                    if (!(bag is HVRForceGrabberBag fgBag)) continue;
                    var capsule = bag.GetComponent<CapsuleCollider>();
                    if (capsule == null) continue;
                    if (!savedOnce)
                    {
                        _savedCapsuleCenter = capsule.center;
                        _savedCapsuleHeight = capsule.height;
                        _savedCapsuleRadius = capsule.radius;
                        _savedBagMaxDistance = fgBag.MaxDistanceAllowed;
                        savedOnce = true;
                    }
                    var newHeight = _savedCapsuleHeight * 2.5f;
                    capsule.height = newHeight;
                    capsule.center = new Vector3(0f, 0f, newHeight * 0.5f);
                    fgBag.MaxDistanceAllowed = range;
                    Logger.Log($"[Telekinesis] {fg.gameObject.name}/{bag.gameObject.name} capsule extended to {range}", LogCat.VR);
                }

                fg.Grabbed.AddListener(OnTelekinesisItemGrabbed);
            }

            IsTelekinesisActive = true;
        }

        public void DeactivateTelekinesis()
        {
            if (!IsTelekinesisActive) return;

            foreach (var side in new[] { VrHandSide.Left, VrHandSide.Right })
            {
                var fg = GetForceGrabber(side);
                if (fg == null) continue;
                fg.MaxRayCastDistance = _savedForceGrabRange;

                foreach (var bag in fg.GrabBags)
                {
                    if (!(bag is HVRForceGrabberBag fgBag)) continue;
                    var capsule = bag.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        capsule.center = _savedCapsuleCenter;
                        capsule.height = _savedCapsuleHeight;
                        capsule.radius = _savedCapsuleRadius;
                    }
                    fgBag.MaxDistanceAllowed = _savedBagMaxDistance;
                }

                fg.Grabbed.RemoveListener(OnTelekinesisItemGrabbed);
            }

            IsTelekinesisActive = false;
            TelekinesisDeactivated?.Invoke();
        }

        public bool TryTelekinesisGrab()
        {
            foreach (var side in new[] { VrHandSide.Right, VrHandSide.Left })
            {
                var fg = GetForceGrabber(side);
                if (fg == null || fg.HoverTarget == null) continue;
                if (fg.TryGrab(fg.HoverTarget, force: true))
                    return true;
            }
            return false;
        }

        private void OnTelekinesisItemGrabbed(HVRGrabberBase grabber, HurricaneVR.Framework.Core.HVRGrabbable grabbable)
        {
            DeactivateTelekinesis();
        }
#endif
    }
}
