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
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly VobService _vobService;
        
        public VRContextInteractionService VRContextInteractionService => _contextInteractionService.GetImpl<VRContextInteractionService>();
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

        public HVRController GetHand(HVRHandSide side)
        {
            if (side == HVRHandSide.Left)
                return _contextInteractionService.GetCurrentPlayerController().GetComponent<VRPlayerController>().LeftHand.Controller;
            else
                return _contextInteractionService.GetCurrentPlayerController().GetComponent<VRPlayerController>().RightHand.Controller;
        }

        /// <summary>
        /// The hero grabbed a mob (wheel, lever, ...): like AI_UseMob, its ConditionFunction decides whether it may be
        /// used, then its Target (e.g. a gate mover) is moved.
        /// </summary>
        public void HandleMobGrab(VobLoader loader)
        {
            var mob = loader.Container.VobAs<IInteractiveObject>();
            var vm = _gameStateService.GothicVm;
            if (mob == null || vm == null)
                return;

            var condFunc = mob.ConditionFunction ?? "";
            Logger.Log($"[VRPlayerService] HandleMobGrab mob={loader.gameObject.name} condFunc={condFunc}", LogCat.Vob);

            // ConditionFunction is a Daedalus check: returns 0 = interaction denied.
            if (condFunc.Length > 0)
            {
                var symbol = vm.GetSymbolByName(condFunc);
                if (symbol == null)
                {
                    Logger.LogWarning($"[VRPlayerService] ConditionFunction '{condFunc}' not found in VM", LogCat.Vob);
                    return;
                }
                if (vm.Call<int>(symbol.Index) == 0)
                {
                    Logger.Log($"[VRPlayerService] HandleMobGrab: '{condFunc}' denied (returned 0)", LogCat.Vob);
                    return;
                }
            }

            var target = mob.Target;
            if (string.IsNullOrEmpty(target))
            {
                Logger.LogWarning($"[VRPlayerService] HandleMobGrab: no Target on {loader.gameObject.name}", LogCat.Vob);
                return;
            }

            // Direct mover: Toggle (the player physically interacts - can open and close).
            if (!_vobService.TryGetMovers(target, out var movers))
            {
                Logger.LogWarning($"[VRPlayerService] HandleMobGrab: mover '{target}' not found", LogCat.Vob);
                return;
            }

            Logger.Log($"[VRPlayerService] HandleMobGrab: toggling mover '{target}' ({movers.Count} instance(s))",
                LogCat.Vob);
            foreach (var mover in movers)
            {
                if (mover?.Go == null || !mover.Go)
                    continue;

                mover.Go.GetComponentInChildren<MoverAdapter>()?.Toggle();
            }
        }
    }
}
#endif
