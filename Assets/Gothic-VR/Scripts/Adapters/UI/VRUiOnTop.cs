#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core.Logging;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.UI
{
    /// <summary>
    /// Draws a world space UI (dialog box, NPC subtitles, HUD, menus) on top of walls, furniture and NPC bodies.
    ///
    /// - Material values can't do it: the Canvas overrides unity_GUIZTestMode per draw, and the dialog background
    ///   (URP Unlit) has no depth test parameter. All graphics get the "Gothic/UI OnTop" shader (ZTest Always).
    /// - Menu backgrounds/frames are MeshRenderers (Gothic textures on quads) - they get the same shader.
    /// - Gothic's bitmap fonts are TMP sprite assets (TMP_SubMeshUI with the asset's material, re-assigned on every text
    ///   change). Labels get an on-top copy of their sprite asset. UIEvents swaps sprite assets on hover (highlight font)
    ///   - LateUpdate wraps those again. The copy keeps the original name, as the highlight font is looked up by name.
    /// - Queues: after all transparents (Gothic alpha materials are sorted back to front and one closer to the camera
    ///   would cover the UI). Without depth the order decides what is in front: deeper in the hierarchy = later (a
    ///   status bar's fill is a nested canvas below its background), text after all images, VR hands last.
    /// </summary>
    public class VRUiOnTop : MonoBehaviour
    {
        private const int _backgroundRenderQueue = 3800;
        private const int _maxHierarchyDepth = 99;
        private const int _textRenderQueue = 3901;
        private const int _handsRenderQueue = 3950;

        // Resolved on first use - Shader.Find isn't allowed in a MonoBehaviour's (static) field initializers.
        private static Shader _onTopShader;
        private static readonly int _ignoreVertexColorId = Shader.PropertyToID("_IgnoreVertexColor");
        private static readonly int _baseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _useBaseMapId = Shader.PropertyToID("_UseBaseMap");
        private static readonly Dictionary<TMP_SpriteAsset, TMP_SpriteAsset> _onTopSpriteAssets = new();
        private static readonly List<(Material material, int queue)> _handMaterials = new();
        private static readonly HashSet<object> _handsOnTopRequests = new();
        private static readonly object _dialogHandsRequest = new();
        // One material per look/queue - re-applying (IsAutoApply) mustn't create new ones.
        private static readonly Dictionary<(Texture, Color, Vector4, int, bool, bool), Material> _onTopMaterials = new();

        private const float _autoApplySeconds = 0.1f;

        private readonly HashSet<TMP_Text> _texts = new();
        private CanvasGroup _canvasGroup;
        private float _nextAutoApplyTime;

        /// <summary>
        /// Patches new graphics (hit numbers, menu entries, ...) and graphics whose material code replaced later (e.g.
        /// a status bar's material set after boot, a menu's background picture) a few times per second.
        /// </summary>
        public bool IsAutoApply;

        /// <summary>
        /// The VR hands (and the UI laser) draw above this UI while it is active - pointing at menu entries.
        /// </summary>
        public bool IsHandsOnTopWhileActive;

        /// <summary>
        /// 0 = always visible. Otherwise hidden when farther away from the camera - on-top NPC subtitles of ambient
        /// smalltalk showed up through every wall of a camp as tiny pixels.
        /// </summary>
        public float MaxVisibleDistance;


        /// <summary>
        /// Adds (once) and applies the on-top drawing to a UI root.
        /// </summary>
        public static VRUiOnTop Ensure(GameObject root, bool isAutoApply, bool isHandsOnTopWhileActive = false)
        {
            if (!root.TryGetComponent<VRUiOnTop>(out var onTop))
                onTop = root.AddComponent<VRUiOnTop>();
            onTop.IsAutoApply |= isAutoApply;
            if (isHandsOnTopWhileActive && !onTop.IsHandsOnTopWhileActive)
            {
                onTop.IsHandsOnTopWhileActive = true;
                if (onTop.isActiveAndEnabled)
                    RequestHandsOnTop(onTop, true);
            }
            onTop.Apply();
            return onTop;
        }

        /// <summary>
        /// Patches all not yet patched graphics and meshes below this GameObject (new dialog options are cloned later on).
        /// </summary>
        public void Apply()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text text)
                {
                    if (_texts.Add(text))
                        WrapSpriteAsset(text);
                    continue;
                }

                // Its material comes from the (patched) sprite asset.
                if (graphic is TMP_SubMeshUI)
                    continue;

                var original = graphic.material;
                if (original != null && original.shader == GetOnTopShader())
                    continue;

                // URP materials (status bars: BAR_HEALTH.TGA in _BaseMap) on Images with a white sprite - the bar's
                // texture is in the material, not in graphic.mainTexture (the bars came out white).
                var baseMap = original != null && original.HasProperty(_baseMapId) ? original.GetTexture(_baseMapId) : null;
                var color = original == null ? Color.white :
                    original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") :
                    original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                graphic.material = CreateOnTopMaterial(baseMap != null ? baseMap : graphic.mainTexture, color,
                    new Vector4(1, 1, 0, 0), GetRenderQueue(graphic.transform), false, baseMap != null);
            }

            foreach (var meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
                PatchMeshRenderer(meshRenderer);
        }

        private void PatchMeshRenderer(MeshRenderer meshRenderer)
        {
            var materials = meshRenderer.sharedMaterials;
            var isChanged = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                if (original == null || original.shader == GetOnTopShader())
                    continue;

                var textureName = original.HasProperty("_BaseMap") ? "_BaseMap" :
                    original.HasProperty("_MainTex") ? "_MainTex" : null;
                var texture = textureName != null ? original.GetTexture(textureName) : null;
                var scale = textureName != null ? original.GetTextureScale(textureName) : Vector2.one;
                var offset = textureName != null ? original.GetTextureOffset(textureName) : Vector2.zero;
                var color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") :
                    original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;

                materials[i] = CreateOnTopMaterial(texture, color, new Vector4(scale.x, scale.y, offset.x, offset.y),
                    GetRenderQueue(meshRenderer.transform), true, false);
                isChanged = true;
            }

            if (isChanged)
                meshRenderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Deeper in the hierarchy = drawn later (in front).
        /// </summary>
        private int GetRenderQueue(Transform target)
        {
            var depth = 0;
            for (var current = target; current != null && current != transform; current = current.parent)
                depth++;
            return _backgroundRenderQueue + Mathf.Min(depth, _maxHierarchyDepth);
        }

        /// <summary>
        /// VR hands above on-top UI, so we see them when pointing at a dialog option. Only while a dialog is open -
        /// otherwise they covered letters/documents held in front of them.
        /// </summary>
        public static void SetHandsOnTop(bool onTop)
        {
            RequestHandsOnTop(_dialogHandsRequest, onTop);
        }

        /// <summary>
        /// Hands (and the UI laser) stay on top while any dialog/menu asks for it.
        /// </summary>
        private static void RequestHandsOnTop(object requester, bool onTop)
        {
            var wasOnTop = _handsOnTopRequests.Count > 0;
            if (onTop)
                _handsOnTopRequests.Add(requester);
            else
                _handsOnTopRequests.Remove(requester);

            var isOnTop = _handsOnTopRequests.Count > 0;
            if (wasOnTop == isOnTop)
                return;

            if (isOnTop)
            {
                _handMaterials.Clear();
                foreach (var hand in FindObjectsByType<HVRHandGrabber>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var handModel = hand.HandModel != null ? hand.HandModel : hand.transform;
                    foreach (var handRenderer in handModel.GetComponentsInChildren<Renderer>(true))
                        MoveToHandsQueue(handRenderer);
                }
                // The laser pointing at menu entries.
                foreach (var pointer in FindObjectsByType<HVRUIPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (pointer.TryGetComponent<LineRenderer>(out var line))
                        MoveToHandsQueue(line);
                }
                Logger.Log($"[VRUiOnTop] {_handMaterials.Count} hand/laser material(s) moved to queue {_handsRenderQueue}.", LogCat.VR);
                return;
            }

            foreach (var (material, queue) in _handMaterials)
            {
                if (material != null)
                    material.renderQueue = queue;
            }
            _handMaterials.Clear();
        }

        private static void MoveToHandsQueue(Renderer handRenderer)
        {
            foreach (var material in handRenderer.materials)
            {
                _handMaterials.Add((material, material.renderQueue));
                material.renderQueue = _handsRenderQueue;
            }
        }

        private void OnEnable()
        {
            if (IsHandsOnTopWhileActive)
                RequestHandsOnTop(this, true);
        }

        private void OnDisable()
        {
            RequestHandsOnTop(this, false);
        }

        private void LateUpdate()
        {
            UpdateDistanceVisibility();

            if (IsAutoApply && Time.unscaledTime >= _nextAutoApplyTime)
            {
                _nextAutoApplyTime = Time.unscaledTime + _autoApplySeconds;
                _texts.RemoveWhere(text => text == null);
                Apply();
            }

            foreach (var text in _texts)
            {
                if (text != null)
                    WrapSpriteAsset(text);
            }
        }

        private void UpdateDistanceVisibility()
        {
            if (MaxVisibleDistance <= 0f || Camera.main == null)
                return;

            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            var isNear = Vector3.Distance(Camera.main.transform.position, transform.position) <= MaxVisibleDistance;
            _canvasGroup.alpha = isNear ? 1f : 0f;
        }

        private static void WrapSpriteAsset(TMP_Text text)
        {
            // No own sprite asset (e.g. NPC subtitle lines): TMP falls back to the default one - wrap that.
            var source = text.spriteAsset != null ? text.spriteAsset : TMP_Settings.defaultSpriteAsset;
            if (source == null || _onTopSpriteAssets.ContainsValue(source))
                return;

            if (!_onTopSpriteAssets.TryGetValue(source, out var onTop) || onTop == null)
            {
                onTop = Instantiate(source);
                onTop.name = source.name; // UIEventsService finds the highlight font by name (..._hi.fnt).
                onTop.material = CreateOnTopMaterial(source.material.mainTexture, Color.white, new Vector4(1, 1, 0, 0),
                    _textRenderQueue, false, false);
                onTop.UpdateLookupTables();
                _onTopSpriteAssets[source] = onTop;
            }

            text.spriteAsset = onTop;
        }

        private static Shader GetOnTopShader()
        {
            if (_onTopShader == null)
                _onTopShader = Shader.Find("Gothic/UI OnTop");
            return _onTopShader;
        }

        /// <param name="isMesh">Meshes have no vertex colors (UI vertices carry the graphic's color).</param>
        /// <param name="isBaseMap">The texture goes into _BaseMap - the Canvas overwrites _MainTex with the sprite's.</param>
        private static Material CreateOnTopMaterial(Texture texture, Color color, Vector4 scaleOffset, int renderQueue,
            bool isMesh, bool isBaseMap)
        {
            var key = (texture, color, scaleOffset, renderQueue, isMesh, isBaseMap);
            if (_onTopMaterials.TryGetValue(key, out var material) && material != null)
                return material;

            material = new Material(GetOnTopShader())
            {
                mainTexture = texture,
                color = color,
                mainTextureScale = new Vector2(scaleOffset.x, scaleOffset.y),
                mainTextureOffset = new Vector2(scaleOffset.z, scaleOffset.w),
                renderQueue = renderQueue
            };
            material.SetFloat(_ignoreVertexColorId, isMesh ? 1f : 0f);
            if (isBaseMap)
            {
                material.SetTexture(_baseMapId, texture);
                material.SetFloat(_useBaseMapId, 1f);
            }
            _onTopMaterials[key] = material;
            return material;
        }
    }
}
#endif
