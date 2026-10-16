#if GOTHIC_HVR_INSTALLED
using Gothic.VR.Services;
using Reflex.Attributes;
using UnityEngine;

namespace Gothic.VR.Adapters.Player
{
    /// <summary>
    /// Swaps the hand materials to a variant rendered after on-top UI. Controlled by VRUiOnTopService.
    /// </summary>
    public class VRHandsOnTop : MonoBehaviour
    {
        [Inject] private readonly VRUiOnTopService _uiOnTopService;

        [SerializeField] private Renderer[] _handRenderers;
        [Tooltip("Hand material with a render queue after the on-top UI materials (UiOnTop-Text).")]
        [SerializeField] private Material _onTopMaterial;

        private Material[] _originalMaterials;

        private void Start()
        {
            _originalMaterials = new Material[_handRenderers.Length];
            for (var i = 0; i < _handRenderers.Length; i++)
                _originalMaterials[i] = _handRenderers[i].sharedMaterial;

            _uiOnTopService.RegisterHands(this);
        }

        public void SetOnTop(bool onTop)
        {
            for (var i = 0; i < _handRenderers.Length; i++)
                _handRenderers[i].sharedMaterial = onTop ? _onTopMaterial : _originalMaterials[i];
        }
    }
}
#endif
