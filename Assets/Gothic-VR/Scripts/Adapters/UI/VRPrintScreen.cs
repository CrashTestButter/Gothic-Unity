#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core;
using Gothic.Core.Adapters.UI;
using Gothic.Core.Models.UI;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.UI;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Gothic.VR.Adapters.UI
{
    /// <summary>
    /// Shows PrintScreen() messages in front of the VR camera. Gothic's screen positions (percent) are mapped onto a
    /// virtual screen inside the comfortable field of view. The screen follows the head smoothly (damped) and is only
    /// active while messages are visible. Message texts are pooled copies of the inactive template.
    /// The same text again only extends its line - like the engine, where a repeated PrintScreen draws over itself.
    /// Scripts can print every AI tick (G2 with PC_Rockefeller) - a new line per call made the game lag.
    /// Drawn on top of the world (VRUiOnTop), so walls and NPCs don't cut through it.
    /// </summary>
    public class VRPrintScreen : MonoBehaviour
    {
        [SerializeField] private RectTransform _screen;
        [SerializeField] private TMP_Text _messageTemplate;
        [Tooltip("Distance of the virtual screen in front of the camera in meters.")]
        [SerializeField] private float _distance = 1.5f;
        [Tooltip("Seconds the screen needs to catch up with head movement. Higher values feel more floaty.")]
        [SerializeField] private float _moveSmoothTime = 0.25f;
        [Tooltip("Speed of the rotation catching up with the head. Higher values follow quicker.")]
        [SerializeField] private float _rotationSpeed = 6f;
        [SerializeField] private float _minSeconds = 1f;
        [SerializeField] private VRUiOnTop _onTop;

        [Inject] private readonly FontService _fontService;
        [Inject] private readonly ConfigService _configService;

        private readonly List<(TMP_Text Text, float HideAt)> _activeMessages = new();
        private readonly Stack<TMP_Text> _pool = new();

        private Transform _cameraTransform;
        private Vector3 _moveVelocity;

        public void Init(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
            _screen.gameObject.SetActive(false);
            // New lines are clones of the template - VRUiOnTop wraps their fonts when they're generated.
            if (_configService.Dev.EnableDialogAlwaysOnTop)
                _onTop.Apply();

            GlobalEventDispatcher.PrintScreen.AddListener(OnPrintScreen);
        }

        private void OnDestroy()
        {
            GlobalEventDispatcher.PrintScreen.RemoveListener(OnPrintScreen);
        }

        private void Update()
        {
            if (_activeMessages.Count == 0)
                return;

            HideExpiredMessages();
            FollowCamera(false);
        }

        private void OnPrintScreen(PrintScreenMessage message)
        {
            var hideAt = Time.time + Mathf.Max(message.Seconds, _minSeconds);
            for (var i = 0; i < _activeMessages.Count; i++)
            {
                var (activeText, activeHideAt) = _activeMessages[i];
                if (activeText.text != message.Text)
                    continue;
                _activeMessages[i] = (activeText, Mathf.Max(activeHideAt, hideAt));
                return;
            }

            if (_activeMessages.Count == 0)
            {
                _screen.gameObject.SetActive(true);
                // Start directly in front of us. Otherwise, the screen flies in from its last position.
                FollowCamera(true);
            }

            var text = _pool.Count > 0 ? _pool.Pop() : Instantiate(_messageTemplate, _screen, false);
            SetupText(text, message);
            text.gameObject.SetActive(true);

            _activeMessages.Add((text, hideAt));
        }

        private void SetupText(TMP_Text text, PrintScreenMessage message)
        {
            var isCenteredX = message.PosX == PrintScreenMessage.Centered;
            var isCenteredY = message.PosY == PrintScreenMessage.Centered;

            GothicText.Apply(text);
            text.spriteAsset = _fontService.TryGetFont(message.Font) ?? _fontService.DefaultSpriteAsset;
            text.fontSize = message.Font.ToLowerInvariant().Contains("font_old_10_white") ? 16 : 36;
            text.horizontalAlignment = isCenteredX ? HorizontalAlignmentOptions.Center : HorizontalAlignmentOptions.Left;
            text.verticalAlignment = isCenteredY ? VerticalAlignmentOptions.Middle : VerticalAlignmentOptions.Top;
            text.text = message.Text;

            // Gothic places the top left corner of the text at the position. Centered axes use the text's center.
            var rect = text.rectTransform;
            var anchor = new Vector2(isCenteredX ? 0.5f : message.PosX / 100f,
                isCenteredY ? 0.5f : 1f - message.PosY / 100f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(isCenteredX ? 0.5f : 0f, isCenteredY ? 0.5f : 1f);
            rect.sizeDelta = new Vector2(_screen.rect.width, text.fontSize * 2);
            rect.anchoredPosition = Vector2.zero;
        }

        private void HideExpiredMessages()
        {
            for (var i = _activeMessages.Count - 1; i >= 0; i--)
            {
                var (text, hideAt) = _activeMessages[i];
                if (Time.time < hideAt)
                    continue;

                text.gameObject.SetActive(false);
                _pool.Push(text);
                _activeMessages.RemoveAt(i);
            }

            if (_activeMessages.Count == 0)
                _screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// Damped movement towards the spot in front of the camera. Feels less stiff than parenting it to the head.
        /// </summary>
        private void FollowCamera(bool isImmediate)
        {
            var targetPosition = _cameraTransform.position + _cameraTransform.forward * _distance;
            var targetRotation = Quaternion.LookRotation(_cameraTransform.forward, _cameraTransform.up);

            if (isImmediate)
            {
                _moveVelocity = Vector3.zero;
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            var pos = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, _moveSmoothTime);
            var rotationLerp = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);
            var rot = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp);
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
#endif
