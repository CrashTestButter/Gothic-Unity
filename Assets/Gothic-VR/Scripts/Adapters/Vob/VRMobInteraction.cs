#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Adapters.Vob;
using Gothic.Core.Logging;
using Gothic.Core.Services.Vobs;
using Gothic.VR.Services;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using Reflex.Attributes;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Vob
{
    /// <summary>
    /// Uses a mob (wheel, lever, shrine, bed, ...) when the hero grabs it. Sits next to the mob's HVRGrabbable.
    /// The logic itself is platform neutral (MobInteractionService).
    /// </summary>
    public class VRMobInteraction : MonoBehaviour
    {
        [Inject] private readonly VRPlayerService _vrPlayerService;
        [Inject] private readonly MobInteractionService _mobInteractionService;

        private bool _mobActivated;

        private void Start()
        {
            if (TryGetComponent(out HVRGrabbable grabbable))
            {
                grabbable.Grabbed.AddListener(OnGrabbed);
                grabbable.Released.AddListener(OnReleased);
            }
        }

        private void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            if (_mobActivated)
                return;

            var vobLoader = GetComponentInParent<VobLoader>();
            var vob = vobLoader?.Container?.Vob;
            if (!_mobInteractionService.ShouldHandleGrab(vob))
                return;

            _mobActivated = true;
            Logger.Log($"[VRMobInteraction.OnGrabbed] mob={vobLoader.gameObject.name}", LogCat.Vob);
            _vrPlayerService.HandleMobGrab(vobLoader);
        }

        private void OnReleased(HVRGrabberBase _, HVRGrabbable __)
        {
            _mobActivated = false;
        }
    }
}
#endif
