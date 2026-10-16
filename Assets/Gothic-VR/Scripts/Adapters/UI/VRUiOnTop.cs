#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core;
using HurricaneVR.Framework.Core.Grabbers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gothic.VR.Adapters.UI
{
    /// <summary>
    /// Draws a world space UI (dialog box, NPC subtitles, PrintScreen) on top of walls, furniture and NPC bodies.
    ///
    /// - The Canvas overrides unity_GUIZTestMode per draw, so graphics get copies of the on-top material assets
    ///   ("Gothic/UI OnTop", ZTest Always). Copies only carry the Gothic texture, which is loaded at runtime.
    /// - Gothic's bitmap fonts are runtime TMP sprite assets. Texts get an on-top copy of their sprite asset; texts
    ///   whose sprite asset is swapped later (hover highlight) are wrapped again when TMP regenerates them.
    /// </summary>
    public class VRUiOnTop : MonoBehaviour
    {
        private const int _handsRenderQueue = 3950;

        [Tooltip("On-top copy for images. Its render queue has to be after all transparents.")]
        [SerializeField] private Material _backgroundMaterial;
        [Tooltip("On-top copy for Gothic font sprites. Render queue after _backgroundMaterial.")]
        [SerializeField] private Material _textMaterial;
        [Tooltip("0 = always visible. Otherwise hidden when farther away from the camera (meters).")]
        [SerializeField] private float _maxVisibleDistance;
        [Tooltip("Hidden by _maxVisibleDistance. Only needed if _maxVisibleDistance > 0.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        // Shared by all on-top UIs: dialog options and PrintScreen lines are cloned at runtime.
        private static readonly Dictionary<Material, Material> _onTopMaterials = new();
        private static readonly HashSet<Material> _onTopMaterialCopies = new();
        private static readonly Dictionary<TMP_SpriteAsset, TMP_SpriteAsset> _onTopSpriteAssets = new();
        private static readonly HashSet<TMP_SpriteAsset> _onTopSpriteAssetCopies = new();
        private static readonly List<(Material material, int queue)> _handMaterials = new();
        private static bool _areHandsOnTop;

        static VRUiOnTop()
        {
            // A scene change in the middle of a dialog never calls EndDialog - put the hands back.
            GlobalEventDispatcher.LoadingSceneLoaded.AddListener(() => SetHandsOnTop(false));
        }

        private readonly HashSet<Graphic> _patchedGraphics = new();
        private bool _isApplied;
        private Transform _cameraTransform;

        /// <summary>
        /// Patches all not yet patched graphics below this GameObject. Call it again after cloning new elements.
        /// </summary>
        public void Apply()
        {
            _isApplied = true;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (!_patchedGraphics.Add(graphic))
                    continue;

                if (graphic is TMP_Text text)
                    WrapSpriteAsset(text);
                // Its material comes from the (wrapped) sprite asset.
                else if (graphic is not TMP_SubMeshUI)
                    graphic.material = GetOnTopMaterial(graphic.material);
            }
        }

        /// <summary>
        /// VR hands above on-top UI, so we see them when pointing at a dialog option. Only while a dialog is open -
        /// otherwise they covered letters/documents held in front of them.
        /// </summary>
        public static void SetHandsOnTop(bool onTop)
        {
            if (_areHandsOnTop == onTop)
                return;
            _areHandsOnTop = onTop;

            if (onTop)
            {
                _handMaterials.Clear();
                var hands = FindObjectsByType<HVRHandGrabber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var hand in hands)
                {
                    var handModel = hand.HandModel != null ? hand.HandModel : hand.transform;
                    foreach (var handRenderer in handModel.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var material in handRenderer.materials)
                        {
                            _handMaterials.Add((material, material.renderQueue));
                            material.renderQueue = _handsRenderQueue;
                        }
                    }
                }
                return;
            }

            foreach (var (material, queue) in _handMaterials)
            {
                if (material != null)
                    material.renderQueue = queue;
            }
            _handMaterials.Clear();
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        private void Update()
        {
            if (_maxVisibleDistance <= 0f || _canvasGroup == null)
                return;

            if (_cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera == null)
                    return;
                _cameraTransform = mainCamera.transform;
            }

            var sqrDistance = (_cameraTransform.position - transform.position).sqrMagnitude;
            var isNear = sqrDistance <= _maxVisibleDistance * _maxVisibleDistance;
            _canvasGroup.alpha = isNear ? 1f : 0f;
            _canvasGroup.blocksRaycasts = isNear;
        }

        /// <summary>
        /// UIEvents swaps the sprite asset on hover (highlight font) - wrap the new one too.
        /// </summary>
        private void OnTextChanged(Object changed)
        {
            if (_isApplied && changed is TMP_Text text && text.transform.IsChildOf(transform))
                WrapSpriteAsset(text);
        }

        private void WrapSpriteAsset(TMP_Text text)
        {
            // No own sprite asset (e.g. NPC subtitle lines): TMP falls back to the default one - wrap that.
            var source = text.spriteAsset != null ? text.spriteAsset : TMP_Settings.defaultSpriteAsset;
            if (source == null || _onTopSpriteAssetCopies.Contains(source))
                return;

            if (!_onTopSpriteAssets.TryGetValue(source, out var onTop) || onTop == null)
            {
                onTop = Instantiate(source);
                onTop.name = source.name; // UIEventsService finds the highlight font by name (..._hi.fnt).
                onTop.material = CreateCopy(_textMaterial, source.material);
                onTop.UpdateLookupTables();
                _onTopSpriteAssets[source] = onTop;
                _onTopSpriteAssetCopies.Add(onTop);
            }

            text.spriteAsset = onTop;
        }

        private Material GetOnTopMaterial(Material source)
        {
            if (_onTopMaterialCopies.Contains(source))
                return source;

            if (!_onTopMaterials.TryGetValue(source, out var onTop) || onTop == null)
            {
                onTop = CreateCopy(_backgroundMaterial, source);
                _onTopMaterials[source] = onTop;
                _onTopMaterialCopies.Add(onTop);
            }
            return onTop;
        }

        private static Material CreateCopy(Material onTopAsset, Material source)
        {
            return new Material(onTopAsset)
            {
                mainTexture = source.mainTexture,
                color = source.color
            };
        }
    }
}
#endif
