#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Adapters.Vob;
using Gothic.Core.Logging;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Player;
using Gothic.Core.Services.Vobs;
using Gothic.VR.Adapters.HVROverrides;
using Gothic.VR.Services.Context;
using Gothic.Core;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Manager;
using Gothic.Core.Models.Container;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Npc;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Shared;
using Reflex.Attributes;
using UnityEngine;
using ZenKit.Vobs;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Services
{
    /// <summary>
    /// Contains global states about Hurricane VR player.
    /// </summary>
    public class VRPlayerService
    {
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly MobInteractionService _mobInteractionService;

        public VRContextInteractionService VRContextInteractionService =>
            _contextInteractionService.GetImpl<VRContextInteractionService>();

        public VRPlayerInputs VRPlayerInputs => VRContextInteractionService.GetVRPlayerInputs();

        public GameObject GrabbedItemLeft;
        public GameObject GrabbedItemRight;

        public enum HandType
        {
            Left,
            Right
        }

        public bool IsDualGrabbed => GrabbedItemLeft != null && GrabbedItemLeft == GrabbedItemRight;

        public void SetGrab(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            HVRHandGrabber handGrabber;
            if (grabber is HVRForceGrabber forceGrabber)
                handGrabber = forceGrabber.HandGrabber;
            else
                handGrabber = grabber as HVRHandGrabber;

            if (handGrabber == null)
                return;

            if (handGrabber.IsLeftHand)
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
            var vobItem = grabbable.GetComponentInParent<VobLoader>().Container.VobAs<IItem>();
            var instanceName = !string.IsNullOrEmpty(vobItem.Instance) ? vobItem.Instance : vobItem.Name;
            _playerService.AddItem(instanceName, vobItem.Amount);
        }

        public void UnsetGrab(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            var dualGrabPrev = IsDualGrabbed;

            HVRHandGrabber handGrabber;
            if (grabber is HVRForceGrabber forceGrabber)
                handGrabber = forceGrabber.HandGrabber;
            else
                handGrabber = grabber as HVRHandGrabber;

            if (handGrabber == null)
                return;

            if (handGrabber.IsLeftHand)
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
            var vobItem = grabbable.GetComponentInParent<VobLoader>().Container.VobAs<IItem>();
            var instanceName = !string.IsNullOrEmpty(vobItem.Instance) ? vobItem.Instance : vobItem.Name;
            _playerService.RemoveItem(instanceName, vobItem.Amount);
        }

        /// <summary>
        /// The player collides with VobItems (stuck at a sword in the ground like in Gothic). Items in our hands or
        /// sockets (hips, back) would push our own CharacterController, so they ignore it while being held.
        /// </summary>
        public void IgnorePlayerCollision(HVRGrabbable grabbable, bool ignore)
        {
            var playerController = _contextInteractionService.GetCurrentPlayerController();
            if (playerController == null)
                return;

            var characterController = playerController.GetComponent<VRPlayerController>().CharacterController;
            foreach (var itemCollider in grabbable.Colliders)
            {
                if (itemCollider != null)
                    Physics.IgnoreCollision(characterController, itemCollider, ignore);
            }
        }

        public HVRController GetHand(HVRHandSide side)
        {
            if (side == HVRHandSide.Left)
                return _contextInteractionService.GetCurrentPlayerController().GetComponent<VRPlayerController>()
                    .LeftHand.Controller;
            else
                return _contextInteractionService.GetCurrentPlayerController().GetComponent<VRPlayerController>()
                    .RightHand.Controller;
        }

        /// <summary>
        /// The hero grabbed a mob (wheel, lever, ...). The logic is platform neutral (MobInteractionService),
        /// only the dialog anchor next to the mob is VR specific.
        /// </summary>
        public void HandleMobGrab(VobLoader loader)
        {
            _mobInteractionService.HandleMobGrab(loader, () => CreateMobDialogAnchor(loader));
        }

        /// <summary>
        /// Next to the mob on the player's left (= the mob's right, where NPC dialogs are), at eye height.
        /// </summary>
        private static GameObject CreateMobDialogAnchor(VobLoader loader)
        {
            var cam = Camera.main != null ? Camera.main.transform : loader.transform;

            var bounds = new Bounds(loader.transform.position, Vector3.one * 0.5f);
            var hasBounds = false;
            foreach (var mobRenderer in loader.GetComponentsInChildren<Renderer>())
            {
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
    }
}
#endif
