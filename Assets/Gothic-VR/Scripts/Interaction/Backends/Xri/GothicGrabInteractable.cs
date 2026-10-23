using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// XRGrabInteractable for Gothic items. Adds what XRI lacks compared to HVR:
    /// - BeforeGrabbed: raised (via XriInteractableBridge) before XRI captures the Rigidbody state. Gothic items lie
    ///   kinematic, the game turns physics on there, so that VelocityTracking moves them and they fall when dropped.
    /// - Interpolation while held, against jitter of velocity tracked items while walking.
    ///
    /// Disabled in the prefab. XriInteractableBridge enables it on the XRI backend once the runtime colliders exist.
    /// </summary>
    public class GothicGrabInteractable : XRGrabInteractable
    {
        [Tooltip("Rigidbody interpolation while held. Restored on drop.")]
        [SerializeField] private RigidbodyInterpolation _heldInterpolation = RigidbodyInterpolation.Interpolate;

        private XriInteractableBridge _bridge;
        private RigidbodyInterpolation _interpolationBeforeGrab;
        private bool _isInterpolationOverridden;

        protected override void Awake()
        {
            base.Awake();
            _bridge = GetComponent<XriInteractableBridge>();
        }

        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            // Before base: XRGrabInteractable.Grab() stores isKinematic, which it restores on drop.
            if (_bridge != null)
                _bridge.OnSelectEntering(args);

            base.OnSelectEntering(args);
        }

        protected override void SetupRigidbodyGrab(Rigidbody rigidbody)
        {
            // XRI calls it again when the movement type changes while held.
            if (!_isInterpolationOverridden)
            {
                _interpolationBeforeGrab = rigidbody.interpolation;
                _isInterpolationOverridden = true;
            }

            base.SetupRigidbodyGrab(rigidbody);
            rigidbody.interpolation = _heldInterpolation;
        }

        protected override void SetupRigidbodyDrop(Rigidbody rigidbody)
        {
            base.SetupRigidbodyDrop(rigidbody);
            if (!_isInterpolationOverridden)
                return;

            rigidbody.interpolation = _interpolationBeforeGrab;
            _isInterpolationOverridden = false;
        }
    }
}
